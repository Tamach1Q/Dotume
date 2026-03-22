using UnityEngine;
using UnityEngine.Events;

public class ShowPopupAction : ActionBase
{
    [SerializeField] string owner = "Popup";
    [TextArea] [SerializeField] string message = "Message";
    [SerializeField] bool requireConfirm = false;
    [SerializeField] float durationSeconds = 1.0f;
    [SerializeField] UnityEvent onConfirmed; // Confirm後に実行したい処理をInspectorで配線

    bool _pendingRetry; // WordCut終了待ちのリトライ重複防止

    
    public override void Execute()
    {
        var r = UIRouter.I;
    var w = WordCutUI.Instance;

    // UIRouter は busy だと言っているのに WordCut が開いていない＝古いロック
    if (r != null && r.IsModalOpen() && !(w != null && w.IsOpen && w.gameObject.activeInHierarchy))
    {
        Debug.Log("[ShowPopupAction] fix: stale modal -> ForceCloseAll");
        r.ForceCloseAll();                           // なければ w.CloseImmediate() 相当を呼ぶ
    }

        Debug.Log(" アクションベース呼ばれた2");
        UIDebug.DumpState("ShowPopupAction.before");
        // ★ 全角記号→半角に置換（必要な分だけ追加OK）
        var msg = message;
        if (!string.IsNullOrEmpty(msg))
        {
            msg = msg.Replace('？', '?')
                     .Replace('！', '!')
                     .Replace('：', ':')
                     .Replace('；', ';')
                     .Replace('〜', '~');
            // 必要なら他も：'（'→'(', '）'→')' など
        }

        bool ok = UIRouter.I != null && UIRouter.I.ShowPopup(owner, msg, requireConfirm, durationSeconds,
            onConfirm: () => onConfirmed?.Invoke());
        if (!ok)
        {
            Debug.Log("open failed (modal locked)" );
            // WordCutが開いている最中に呼ばれた場合は、閉じた後に1回だけ再試行する
            w = WordCutUI.Instance;
            if (w != null && w.IsOpen && !_pendingRetry)
            {
                StartCoroutine(RetryAfterWordCutClosed());
            }
        }
        else
        {
            Debug.Log("open!");
        }
        UIDebug.DumpState("ShowPopupAction.after");
    }

    System.Collections.IEnumerator RetryAfterWordCutClosed()
    {
        _pendingRetry = true;
        // 完全に閉じるまで待つ（activeInHierarchy もfalseになるのを待つ）
        while (WordCutUI.Instance != null && (WordCutUI.Instance.IsOpen || WordCutUI.Instance.gameObject.activeInHierarchy))
            yield return null;

        // 1フレーム余裕を見て再試行
        yield return null;

        Debug.Log("[ShowPopupAction] retry after WordCut closed");
        Execute();
        _pendingRetry = false;
    }
}
