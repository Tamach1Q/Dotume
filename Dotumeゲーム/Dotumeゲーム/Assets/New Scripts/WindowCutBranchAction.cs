using UnityEngine;
using UnityEngine.Events;

public class WindowCutBranchAction : ActionBase
{
    [Header("WordCut")]
    [SerializeField] string owner = "WordCut";
    [SerializeField] string word = "window";
    [SerializeField] int cutsRequired = 1;
    [TextArea] [SerializeField] string guide = "";

    [Header("Segments")]
    [SerializeField] string winSegment = "win";
    [SerializeField] string windSegment = "wind";

    [Header("Flags")]
    [SerializeField] string winChanceFlag = "win_chance_used";

    [Header("Events")]
    [SerializeField] UnityEvent onWin;         // 初回だけ
    [SerializeField] UnityEvent onWinBlocked;  // 2回目以降のWIN
    [SerializeField] UnityEvent onWind;

    public override void Execute()
    {
        if (!isActiveAndEnabled) return;
        StartCoroutine(Run());
    }

    System.Collections.IEnumerator Run()
    {
        // 1) 「window」を開いて、win / wind どちらでも正解にする
        string[] options = new[] { winSegment, windSegment };
        UIRouter.I?.OpenWordCutMulti(owner, word, options, cutsRequired, guide);

        // 閉じるまで待つ（=成功した）
        while (WordCutUI.Instance != null && WordCutUI.Instance.IsActive)
            yield return null;

        var seg = WordCutUI.Instance ? WordCutUI.Instance.LastMatchedSegment : null;
        if (string.IsNullOrEmpty(seg)) yield break; // ありえないが保険

        // 2) 分岐
        if (string.Equals(seg, winSegment, System.StringComparison.OrdinalIgnoreCase))
        {
            bool used = GameState.I != null && GameState.I.HasFlag(winChanceFlag);
            if (!used) GameState.I?.SetFlag(winChanceFlag, true);
            yield return null;
            if (!used) onWin?.Invoke();
            else onWinBlocked?.Invoke();
        }
        else if (string.Equals(seg, windSegment, System.StringComparison.OrdinalIgnoreCase))
        {
            yield return null;
            onWind?.Invoke();
        }
    }
}