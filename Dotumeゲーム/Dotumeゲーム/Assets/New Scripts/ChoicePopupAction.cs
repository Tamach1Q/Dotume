using UnityEngine;
using UnityEngine.Events;

public class ChoicePopupAction : ActionBase
{
    [SerializeField] string owner = "Popup";
    [TextArea] [SerializeField] string message = "Continue? A/Yes,B/No";
    [SerializeField] KeyCode yesKey = KeyCode.E;
    [SerializeField] KeyCode noKey = KeyCode.N;
    [SerializeField] bool showKeyHints = true;

    [SerializeField] UnityEvent onYes;
    [SerializeField] UnityEvent onNo;

    public override void Execute()
    {
        if (!isActiveAndEnabled) return;
        StartCoroutine(Run());
    }

    System.Collections.IEnumerator Run()
    {
        // 画面にテキストを出す（WordCutUIを流用）
        var ui = WordCutUI.Instance;
        if (ui) { ui.gameObject.SetActive(true); ui.SetInfoOnly(ComposeMessage()); }

        // 一時停止（あれば）
        GameDirector.I?.Pause(owner);

        // 入力待ち（リアルタイム）
        while (true)
        {
            if (Input.GetKeyDown(yesKey)) { break; }
            if (Input.GetKeyDown(noKey)) { break; }
            yield return null;
        }

        bool isYes = Input.GetKeyDown(yesKey);

        // 片付け
        if (ui) { ui.SetInfoOnly(""); ui.gameObject.SetActive(false); }
        GameDirector.I?.Resume(owner);

        // 実行（1フレーム後にすると他UIと競合しにくい）
        yield return null;
        if (isYes) onYes?.Invoke(); else onNo?.Invoke();
    }

    string ComposeMessage()
    {
        if (!showKeyHints) return message;
        return $"{message}\nA/Yes,B/No";
    }
}
