using UnityEngine;
using UnityEngine.Events;

// 単語切り（WordCut）に成功したら、インベントリではなくFlagで状態を管理したい場合に使うアクション。
// - 既存の OpenWordCutAction と同様にUIを開く
// - 成功後は指定の Flag を Set/Unset する（インベントリは増やさない）
// - ゲームシステムには干渉せず、フラグ駆動の Reveal/EnableOnFlag が使える
public class OpenWordCutFlagAction : ActionBase
{
    [Header("WordCut")]
    [SerializeField] string owner = "WordCut";
    [SerializeField] string word = "ostrich";
    [SerializeField] string expected = "rich";
    [SerializeField] int cutsRequired = 1;
    [TextArea] [SerializeField] string guide = "";

    [Header("Flag")] 
    [SerializeField] string flagId = "met_rich";  // 成功時に立てたいフラグ
    [SerializeField] bool value = true;            // 立てる(true)/下げる(false)

    [Header("Events")] 
    [SerializeField] UnityEvent onSuccess;         // 成功時の追加処理
    [SerializeField] UnityEvent onFailure;         // 失敗（閉じた/不一致）時の処理

    public override void Execute()
    {
        if (!isActiveAndEnabled) return;
        StartCoroutine(Run());
    }

    System.Collections.IEnumerator Run()
    {
        // アイテムは付与しない（インベントリを圧迫しない）
        UIRouter.I?.OpenWordCut(owner, word, expected, itemId: "", cutsRequired: cutsRequired, guide: guide, addToInventory: false);

        // WordCut が閉じるまで待つ
        while (WordCutUI.Instance != null && WordCutUI.Instance.IsActive) yield return null;

        // 成功判定（最後に一致したセグメントが expected と同じか）
        var seg = WordCutUI.Instance ? WordCutUI.Instance.LastMatchedSegment : null;
        bool ok = !string.IsNullOrEmpty(seg) && string.Equals(seg, expected, System.StringComparison.OrdinalIgnoreCase);

        if (ok)
        {
            if (GameState.I != null && !string.IsNullOrEmpty(flagId))
            {
                GameState.I.SetFlag(flagId, value);
                EnableOnFlag.RefreshAll();
                RevealOnFlag.TryRefreshAll();
            }
            yield return null; // 1フレーム後にイベント実行で競合を避ける
            onSuccess?.Invoke();
        }
        else
        {
            onFailure?.Invoke();
        }
    }
}
