using UnityEngine;

// トリガや他アクションから呼んでTOM会話を開始するコンポーネント
public class TomEncounterAction : ActionBase
{
    [SerializeField] string owner = "TOM";
    [SerializeField] TomDialogueAsset dialogue;           // 直指定（任意）
    [SerializeField] string dialogueId;                   // ID指定（推奨）
    [SerializeField] bool autoLookup = true;              // 未割当ならIDで自動解決
    [Tooltip("Resources を使う場合の検索ルート。空なら全域")] [SerializeField]
    string resourcesSearchRoot = "";

    [Header("Conditions (optional)")]
    [Tooltip("trueの場合、すでに dlg_done_{id} が立っていたら起動しない")] 
    [SerializeField] bool respectPlayOnce = true;

    [Header("Layout (optional)")]
    [SerializeField] bool overrideNormalizedY = true;    // ONなら下端0〜上端1で位置指定
    [SerializeField, Range(0f, 1f)] float normalizedY = 1f;

    [Header("Fallback UI Lookup")]
    [SerializeField] TomDialogueView viewInScene; // UIRouter未配線時の直接参照用

    public override void Execute()
    {
        // 参照解決：直指定 > ID解決（DB/Resources）
        var asset = dialogue;
        if (!asset && autoLookup && !string.IsNullOrEmpty(dialogueId))
        {
            asset = TomDialogueDatabase.Find(dialogueId, resourcesSearchRoot);
        }
        if (!asset)
        {
            Debug.LogWarning($"[TomEncounterAction] Dialogue not found (dialogue ref empty, id='{dialogueId}')");
            return;
        }

        // 1回限りの抑止（実際に再生するアセットに対して判定することが重要）
        if (respectPlayOnce && asset && asset.playOnce && GameState.I != null && !string.IsNullOrEmpty(asset.id))
        {
            string flagId = $"dlg_done_{asset.id}";
            if (GameState.I.HasFlag(flagId))
            {
                Debug.Log($"[TomEncounterAction] already done: {flagId}");
                return;
            }
        }

        // UIRouter 経由を優先（モーダル管理と競合回避のため）
        if (UIRouter.I != null)
        {
            float yParam = overrideNormalizedY ? normalizedY : -1f;
            bool ok = UIRouter.I.OpenTomDialogue(owner, asset, onComplete: null, yNormalized: yParam);
            if (!ok)
            {
                Debug.Log("[TomEncounterAction] OpenTomDialogue failed (modal locked)");
            }
            return;
        }

        // フォールバック（直接ビューを開く）
        var v = viewInScene ? viewInScene : Object.FindObjectOfType<TomDialogueView>(includeInactive: true);
        if (!v)
        {
            Debug.LogWarning("[TomEncounterAction] TomDialogueView not found in scene");
            return;
        }

        try { if (overrideNormalizedY) v.SetNormalizedY(normalizedY); } catch { }
        v.Open(asset, string.IsNullOrEmpty(owner) ? "TOM" : owner, onClosed: null);
    }
}
