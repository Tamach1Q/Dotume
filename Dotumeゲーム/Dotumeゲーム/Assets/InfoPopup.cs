using System.Collections;
using UnityEngine;
using TMPro;

public class InfoPopup : MonoBehaviour
{
    public static InfoPopup I { get; private set; }

    [Header("UI")]
    [SerializeField] TMP_Text infoText;       // 表示に使う Text（独立）
    [SerializeField] CanvasGroup canvasGroup; // 任意（未指定なら自動付与）

    Coroutine autoHideCo;

    void Awake()
    {
        if (I && I != this) { Destroy(gameObject); return; }
        I = this;

        if (!canvasGroup)
        {
            canvasGroup = gameObject.GetComponent<CanvasGroup>();
            if (!canvasGroup) canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }
        HideImmediate();
    }

    // 強制表示（秒を指定しなければ出しっぱなし）
    public void Show(string message, float seconds = -1f)
    {
        if (!infoText) return;

        infoText.text = message;
        gameObject.SetActive(true);
        canvasGroup.alpha = 1f;

        if (autoHideCo != null) StopCoroutine(autoHideCo);
        if (seconds > 0f) autoHideCo = StartCoroutine(CoAutoHide(seconds));
    }

    // 明示的に隠す
    public void Hide()
    {
        if (autoHideCo != null) { StopCoroutine(autoHideCo); autoHideCo = null; }
        HideImmediate();
    }

    void HideImmediate()
    {
        if (!infoText) return;
        infoText.text = "";
        canvasGroup.alpha = 0f;
        gameObject.SetActive(true); // Canvas内のレイアウトを保つためActiveは維持
    }

    IEnumerator CoAutoHide(float sec)
    {
        yield return new WaitForSeconds(sec);
        Hide();
    }
}