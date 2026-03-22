using UnityEngine;

public class LoadSceneWithPolicyAction : ActionBase
{
    [Header("Destination")]
    public string sceneName;

    [Header("Timing")]
    public bool useTrigger = false;        // 物理トリガーで自動遷移するならON
    public string requiredTag = "Player";  // 触れた相手のタグ条件
    [Min(0f)]
    public float delayRealtime = 0f;       // ポップアップ演出後などに少し待ちたい時（unscaled）

    [Header("Carry Policy (optional)")]
    public CarryPolicy policy = new CarryPolicy(); // 未設定でも「全部持ち越し」挙動

    [Header("Required Items To Proceed (Stage2/3/4 用)")]
    [Tooltip("次シーンへ進むために 'key' と 'letterId' の同時所持を要求するか")]
    public bool requireKeyAndLetter = false;
    [Tooltip("キーの itemId（全シーン必須）")]
    public string keyItemId = "key";
    [Tooltip("レター（名称は自由に変更）の itemId")]
    public string letterItemId = "letter";
    [Tooltip("キーが無い時に実行する代替アクション")]
    public ActionBase onMissingKey;
    [Tooltip("レターが無い時に実行する代替アクション")]
    public ActionBase onMissingLetter;

    // 二重実行防止フラグ
    private static bool isLoading = false;
    private static string currentLoadingScene = "";

    // ActionBase.Execute() を上書きして実装する
    public override void Execute()
    {
        // 必要アイテムチェック（Stage2/3/4 でのみ有効化想定）
        if (requireKeyAndLetter)
        {
            bool hasKey = GameState.I != null && GameState.I.Has(keyItemId);
            bool hasLetter = GameState.I != null && GameState.I.Has(letterItemId);

            if (!hasKey && !hasLetter)
            {
                // 両方無い場合はキー側のみ実行（キーは全シーンでマスト）
                if (onMissingKey != null) { onMissingKey.Execute(); }
                else Debug.Log("[LoadSceneWithPolicy] Both missing; onMissingKey not set.");
                return;
            }
            if (!hasKey)
            {
                if (onMissingKey != null) { onMissingKey.Execute(); }
                else Debug.Log("[LoadSceneWithPolicy] Missing key; onMissingKey not set.");
                return;
            }
            if (!hasLetter)
            {
                if (onMissingLetter != null) { onMissingLetter.Execute(); }
                else Debug.Log("[LoadSceneWithPolicy] Missing letter; onMissingLetter not set.");
                return;
            }
        }

        // 既にロード中なら無視
        if (isLoading)
        {
            Debug.Log($"[LoadScene] Already loading {currentLoadingScene}, ignoring duplicate call from {gameObject.name}");
            return;
        }

        // 同じシーンへの重複ロードを防止
        if (!string.IsNullOrEmpty(currentLoadingScene) && currentLoadingScene == sceneName)
        {
            Debug.Log($"[LoadScene] Scene {sceneName} already loading, ignoring duplicate call from {gameObject.name}");
            return;
        }

        // Execute() は ActionBase から呼ばれるエントリーポイント
        Debug.Log($"[LoadScene] Start loading: {sceneName} from {gameObject.name}");
        
        // ポリシー遷移では、過去のStack遷移の履歴を完全クリアして
        // 以前のシーンが次シーンへ引き継がれないようにする
        try { SceneStackManager.I?.ClearAndUnloadAllKeptScenes(); } catch { /* ignore */ }
        isLoading = true;
        currentLoadingScene = sceneName;
        Time.timeScale = 1f;
        SceneTransitionKit.Load(sceneName, policy, delayRealtime);
    }


    void OnTriggerEnter2D(Collider2D other)
    {
        // useTrigger が ON の場合、TriggerZone2D 経由ではなく直接このコンポーネントが実行を担当する
        if (!useTrigger) return;

        // タグの比較を行う
        if (!string.IsNullOrEmpty(requiredTag) && !other.CompareTag(requiredTag)) return;

        // 遷移実行
        Execute();
    }
}
