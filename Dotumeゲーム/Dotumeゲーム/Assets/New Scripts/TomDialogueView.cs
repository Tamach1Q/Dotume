using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class TomDialogueView : MonoBehaviour
{
    [Header("UI Refs")]
    [SerializeField] CanvasGroup root;
    [SerializeField] TMP_Text nameText;
    [SerializeField] TMP_Text bodyText;
    [SerializeField] Image portraitImage;
    [SerializeField] float typeSpeedCharsPerSec = 40f; // 1秒に何文字描画するか

    [Header("Layout")]
    [SerializeField, Range(0f, 1f)] float defaultYNormalized = 0f; // 0=下端, 1=上端
    [Tooltip("Y位置を動かしたいRectTransform（未指定なら自身/CanvasGroupのRectTransform）")]
    [SerializeField] RectTransform moveTarget;

    [Header("Audio")]
    [SerializeField] AudioSource audioSource;  // 任意。無ければ動的に追加

    public bool IsOpen { get; private set; }

    TomDialogueAsset current;
    int index;
    string owner;
    System.Action onClosed;
    Coroutine typeCo;
    bool fastForward;

    void Awake()
    {
        if (!audioSource)
        {
            audioSource = GetComponent<AudioSource>();
            if (!audioSource) audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
        }
        SetVisible(false, immediate: true);

        // 既定のY位置を適用（非表示でもRectTransformは更新可）
        try { SetNormalizedY(defaultYNormalized); } catch { }
    }

    void SetVisible(bool v, bool immediate = false)
    {
        if (!root)
        {
            // CanvasGroup が未指定でも安全に ON/OFF
            gameObject.SetActive(v);
            return;
        }
        gameObject.SetActive(true);
        if (immediate)
        {
            root.alpha = v ? 1f : 0f;
            root.blocksRaycasts = v;
            root.interactable = v;
            if (!v) gameObject.SetActive(false);
        }
        else
        {
            StopAllCoroutines();
            StartCoroutine(FadeRoutine(v));
        }
    }

    IEnumerator FadeRoutine(bool toVisible)
    {
        float start = root ? root.alpha : (toVisible ? 0f : 1f);
        float end = toVisible ? 1f : 0f;
        float t = 0f;
        if (toVisible) gameObject.SetActive(true);
        while (t < 0.12f)
        {
            t += Time.unscaledDeltaTime;
            if (root) root.alpha = Mathf.Lerp(start, end, Mathf.SmoothStep(0, 1, t / 0.12f));
            yield return null;
        }
        if (root) root.alpha = end;
        if (root)
        {
            root.blocksRaycasts = toVisible;
            root.interactable = toVisible;
        }
        if (!toVisible) gameObject.SetActive(false);
    }

    public void Open(TomDialogueAsset asset, string owner, System.Action onClosed)
    {
        if (asset == null || asset.lines == null || asset.lines.Length == 0)
        {
            Debug.LogWarning("[TomDialogueView] Invalid asset");
            return;
        }

        this.current = asset;
        this.index = 0;
        this.owner = string.IsNullOrEmpty(owner) ? "TOM" : owner;
        this.onClosed = onClosed;

        // ポーズ（UI表示中はゲーム停止）
        if (GameDirector.I) GameDirector.I.Pause(this.owner);

        IsOpen = true;
        if (nameText) nameText.text = string.IsNullOrEmpty(asset.displayName) ? "TOM" : asset.displayName;
        SetVisible(true, immediate: true);
        ShowCurrentLine();
    }

    // 画面下端=0, 上端=1 の正規化Yでビューの親内位置を設定
    public void SetNormalizedY(float y01)
    {
        y01 = Mathf.Clamp01(y01);
        var t = moveTarget ? moveTarget : (root ? root.transform as RectTransform : transform as RectTransform);
        if (!t) return;
        var parent = t.parent as RectTransform;
        if (!parent)
        {
            // 親がRectTransformでないケースは何もしない
            return;
        }

        // アンカーは下基準を想定（推奨: anchorMin.y=anchorMax.y=0, pivot.y=0）
        // 下=0, 上=parent高さ へ線形マップ
        float yPixels = parent.rect.height * y01;
        var pos = t.anchoredPosition;
        pos.y = yPixels;
        t.anchoredPosition = pos;
    }

    void Update()
    {
        if (!IsOpen) return;

        // 進行キー: Enter / KeypadEnter / E / Space
        bool confirmDown = Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.Space);
        if (confirmDown)
        {
            if (typeCo != null)
            {
                // タイピング中 → 早送り
                fastForward = true;
            }
            else
            {
                // 次の行へ
                index++;
                if (index >= current.lines.Length)
                {
                    Close();
                }
                else
                {
                    ShowCurrentLine();
                }
            }
        }
    }

    void ShowCurrentLine()
    {
        if (current == null || index < 0 || index >= current.lines.Length) return;
        var line = current.lines[index];
        if (portraitImage) portraitImage.sprite = line != null ? line.portrait : null;
        if (typeCo != null) StopCoroutine(typeCo);
        typeCo = StartCoroutine(TypeRoutine(line != null ? line.text : ""));

        // 任意のボイス
        if (audioSource && line != null && line.voice)
        {
            audioSource.PlayOneShot(line.voice, 1f);
        }
    }

    IEnumerator TypeRoutine(string text)
    {
        if (!bodyText) yield break;
        if (string.IsNullOrEmpty(text)) { bodyText.text = ""; typeCo = null; yield break; }

        fastForward = false;
        bodyText.text = "";
        int shown = 0;
        float cps = Mathf.Max(1f, typeSpeedCharsPerSec);
        while (shown < text.Length && !fastForward)
        {
            float add = cps * Time.unscaledDeltaTime;
            int addCount = Mathf.Max(1, Mathf.FloorToInt(add));
            shown = Mathf.Min(text.Length, shown + addCount);
            bodyText.text = text.Substring(0, shown);
            yield return null;
        }
        // 早送り/タイプ完了 → 全文表示
        bodyText.text = text;
        typeCo = null;
    }

    public void Close()
    {
        if (!IsOpen) return;
        IsOpen = false;
        SetVisible(false, immediate: false);

        // 報酬/フラグ
        try
        {
            if (GameState.I != null && current != null)
            {
                if (current.setFlagsOnComplete != null)
                {
                    foreach (var f in current.setFlagsOnComplete)
                        if (!string.IsNullOrEmpty(f)) GameState.I.AddFlag(f);
                }
                if (current.addItemsOnComplete != null)
                {
                    foreach (var it in current.addItemsOnComplete)
                        if (!string.IsNullOrEmpty(it)) GameState.I.Add(it);
                }
                if (current.playOnce && !string.IsNullOrEmpty(current.id))
                {
                    GameState.I.AddFlag($"dlg_done_{current.id}");
                }
            }
        }
        catch { }

        // ポーズ解除
        if (GameDirector.I) GameDirector.I.Resume(owner);

        var cb = onClosed; onClosed = null; current = null; index = 0; owner = null;
        cb?.Invoke();
    }
}
