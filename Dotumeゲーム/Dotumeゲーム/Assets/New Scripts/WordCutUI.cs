using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using TMPro;
using System;
using UnityEngine.Serialization;

public class WordCutUI : MonoBehaviour
{
    public static WordCutUI Instance;
    public static float LastClosedAt { get; private set; } = -999f;
    public static bool RecentlyClosed(float sec = 0.2f)
        => (Time.unscaledTime - LastClosedAt) < sec;

    public enum Mode { Closed, Prompt, Open }
    Mode mode = Mode.Closed;

    [Header("UI References")]
    [SerializeField] TMP_Text displayText;
    [SerializeField] TMP_Text infoText;

    [Header("Gameplay")]
    [SerializeField] float invalidClearDelay = 1f;
    [SerializeField] string invalidMsg = "Not valid. Undoing...";

    // SFX：安全化（以前の Random フィールド名を互換維持）
    [FormerlySerializedAs("Random")]
    [SerializeField] private AudioSource sfx;
    [SerializeField] private AudioClip seSuccess;
    [SerializeField] private AudioClip seMove;
    [SerializeField] private AudioClip seFailure;

    public bool IsOpen => mode == Mode.Open;
    public bool IsActive => mode != Mode.Closed;

    string word, expected, itemId, guide = "";
    bool addToInventory = true; // 成功時にインベントリへ追加するか
    int cutsRequired = 1;
    string[] expectedOptions = null;
    public string LastMatchedSegment { get; private set; } = null;

    int caret = 0;
    readonly List<int> cuts = new();
    float prevTimeScale = 1f;

    int lastPlacedCut = -1;
    int correctCut = -1;
    float flashUntil = 0f;

    // ---- Toast (一時メッセージ) 支援 ----
    Coroutine toastCo;
    bool toastActive = false;
    bool toastRequireConfirm = false;
    bool toastConfirmPressed = false;
    string toastPrevInfo = null;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        if (sfx == null) sfx = GetComponent<AudioSource>();
        if (sfx == null)
        {
            sfx = gameObject.AddComponent<AudioSource>();
            sfx.playOnAwake = false;
        }

    }
    // WordCutUI.cs に音量変更用のメソッドを追加する例

    public void SetSoundEffectVolume(float newVolume)
    {
    // newVolume は 0.0f から 1.0f の値
        if (sfx != null)
        {
            sfx.volume = newVolume;
        }
    }

    void EnsureRootVisible()
    {
        if (!gameObject.activeSelf) gameObject.SetActive(true);
        if (infoText) infoText.gameObject.SetActive(true);
        if (displayText) displayText.gameObject.SetActive(true);
    }

    // ===== Prompt =====
    public void ShowPrompt(string message)
    {
        if (mode == Mode.Open) return;
        mode = Mode.Prompt;

        EnsureRootVisible();
        if (displayText) displayText.text = "";
        if (infoText) infoText.text = message ?? "";
        UIDebug.DumpState("WordCutUI.ShowPrompt");
    }

    // ===== Open (単一解) =====
    public void Open(string word, string expected, string itemId, int cutsRequired = 1, string guide = "", bool addToInventory = true)
    {
        this.word = word;
        this.expected = expected;
        this.itemId = itemId;
        this.cutsRequired = Mathf.Max(1, cutsRequired);
        this.guide = guide ?? "";
        this.addToInventory = addToInventory;

        cuts.Clear();
        caret = 0;
        lastPlacedCut = -1;
        correctCut = -1;
        flashUntil = 0f;
        LastMatchedSegment = null; // 次の挑戦に向けてリセット（Closeでは消さない）

        mode = Mode.Open;
        EnsureRootVisible();

        prevTimeScale = Time.timeScale;
        Time.timeScale = 0f;

        UpdateInfo();
        Redraw();
        UIDebug.DumpState($"WordCutUI.Open word={word} expected={expected} item={itemId}");
    }

    // ===== Open (複数選択肢) =====
    public void OpenMulti(string word, string[] expectedOptions, int cutsRequired = 1, string guide = "")
    {
        this.word = word;
        this.expected = null;
        this.expectedOptions = expectedOptions;
        this.itemId = "";
        this.cutsRequired = Mathf.Max(1, cutsRequired);
        this.guide = guide ?? "";

        cuts.Clear();
        caret = 0;
        lastPlacedCut = -1;
        correctCut = -1;
        flashUntil = 0f;
        LastMatchedSegment = null;

        mode = Mode.Open;
        EnsureRootVisible();

        prevTimeScale = Time.timeScale;
        Time.timeScale = 0f;

        UpdateInfo();
        Redraw();
        UIDebug.DumpState($"WordCutUI.OpenMulti word={word}");
    }

    void Update()
    {
        // トースト表示中は WordCut の操作をブロック
        if (mode == Mode.Open && toastActive)
        {
            if (toastRequireConfirm && (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)))
            {
                toastConfirmPressed = true;
            }
            return;
        }
        if (mode != Mode.Open) return;

        if (Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.LeftArrow))
        { SafePlay(seMove); caret = Mathf.Max(0, caret - 1); Redraw(); }

        if (Input.GetKeyDown(KeyCode.D) || Input.GetKeyDown(KeyCode.RightArrow))
        { SafePlay(seMove); caret = Mathf.Min(word.Length, caret + 1); Redraw();}

        bool lockedByInvalid = Time.unscaledTime < flashUntil;

        if (!lockedByInvalid &&
            (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)))
        {
            TryPlaceCut();
        }
    }

    // 既存の Open 画面の上に一時メッセージを重ねる（PuzzleのUIは維持）
    public void ShowToast(string message, bool requireConfirm, float seconds, System.Action onClose = null)
    {
        EnsureRootVisible();
        if (toastCo != null) StopCoroutine(toastCo);
        toastCo = StartCoroutine(ToastRoutine(message ?? "", requireConfirm, seconds, onClose));
        UIDebug.DumpState($"WordCutUI.ShowToast confirm={requireConfirm} sec={seconds}");
    }

    IEnumerator ToastRoutine(string msg, bool requireConfirm, float seconds, System.Action onClose)
    {
        toastActive = true;
        toastRequireConfirm = requireConfirm;
        toastConfirmPressed = false;

        if (infoText)
        {
            toastPrevInfo = infoText.text;
            infoText.text = msg;
            infoText.gameObject.SetActive(true);
        }

        if (requireConfirm)
        {
            while (!toastConfirmPressed) yield return null;
        }
        else
        {
            if (seconds < 0.01f) seconds = 1.0f;
            float t = 0f; while (t < seconds) { t += Time.unscaledDeltaTime; yield return null; }
        }

        if (infoText) infoText.text = toastPrevInfo ?? "";
        toastPrevInfo = null;
        toastActive = false;
        toastRequireConfirm = false;
        toastConfirmPressed = false;
        if (onClose != null) onClose.Invoke();
    }

    void TryPlaceCut()
    {
        if (caret <= 0 || caret >= word.Length)
        {
            if (infoText) infoText.text = $"{guide}\nCan't cut at the edge.";
            SafePlay(seFailure, 0.2f);
            return;
        }

        if (cuts.Contains(caret))
        {
            cuts.Remove(caret);
            lastPlacedCut = -1;
            UpdateInfo();
            Redraw();
            return;
        }

        cuts.Add(caret);
        cuts.Sort();
        lastPlacedCut = caret;
        UpdateInfo();
        Redraw();

        if (cuts.Count >= cutsRequired) EvaluateCuts();
    }

    void EvaluateCuts()
    {
        var idx = new List<int>(cuts.Count + 2);
        idx.Add(0); idx.AddRange(cuts); idx.Add(word.Length);

        bool ok = false;
        correctCut = -1;

        var hasMulti = (expectedOptions != null && expectedOptions.Length > 0);
        HashSet<string> optionSet = null;
        if (hasMulti)
        {
            optionSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var opt in expectedOptions)
                if (!string.IsNullOrEmpty(opt)) optionSet.Add(opt);
            LastMatchedSegment = null;
        }

        for (int i = 0; i < idx.Count - 1; i++)
        {
            int s = idx[i];
            int len = idx[i + 1] - idx[i];
            if (len <= 0) continue;

            string seg = word.Substring(s, len);

            if (!string.IsNullOrEmpty(expected) &&
                string.Equals(seg, expected, StringComparison.OrdinalIgnoreCase))
            {
                ok = true;
                correctCut = s;
                LastMatchedSegment = expected;
                break;
            }

            if (hasMulti && optionSet.Contains(seg))
            {
                ok = true;
                correctCut = s;
                LastMatchedSegment = seg;
                break;
            }
        }

        if (ok)
        {
            SafePlay(seSuccess, 0.2f);   // ← 以前の Random.Play() を安全再生に変更

            string got = !string.IsNullOrEmpty(LastMatchedSegment) ? LastMatchedSegment : expected;
            if (infoText && !string.Equals(itemId, "cash", StringComparison.OrdinalIgnoreCase))
                infoText.text = $"Got {got}!";

            if (GameState.I != null)
            {
                if (!string.IsNullOrEmpty(itemId) && addToInventory)
                    GameState.I.Add(itemId);

                if (!string.IsNullOrEmpty(itemId))
                {
                    if (itemId == "rich")
                    {
                        GameState.I.SetFlag("met_rich", true);
                    }
                    else if (itemId == "rope")
                    {
                        RopeReveal.TryRefreshAll();
                        GameState.I.RemoveFlag("need_rope_hint");
                        SetInfoOnly("The rope has been activated!");
                    }
                }

                RevealOnItem.TryRefreshAll();
            }

            Redraw();
            StartCoroutine(CCloseAfterRealtime(0.8f));
        }
        else
        {
            SafePlay(seFailure, 0.2f);
            flashUntil = Time.unscaledTime + Mathf.Max(0.05f, invalidClearDelay);
            if (infoText) infoText.text = $"{invalidMsg}";
            Redraw();
            StartCoroutine(AutoUndoLastCut(invalidClearDelay));
        }
    }

    void SafePlay(AudioClip clip, float volumeScale = 1.0f) // ★ volumeScale を追加
    {
        if (sfx != null && clip != null) 
        {
            // PlayOneShotのオーバーロードを使って音量を指定
            sfx.PlayOneShot(clip, volumeScale); 
        }
    }

    IEnumerator AutoUndoLastCut(float delay)
    {
        yield return new WaitForSecondsRealtime(delay);
        if (cuts.Count > 0)
        {
            if (lastPlacedCut >= 0) cuts.Remove(lastPlacedCut);
            else cuts.RemoveAt(cuts.Count - 1);
            lastPlacedCut = -1;
        }
        UpdateInfo();
        Redraw();
    }

    void UpdateInfo()
    {
        int left = Mathf.Max(0, cutsRequired - cuts.Count);
        if (infoText)
            infoText.text = $"{guide}Cuts left: {left}. Move with ←/→, A to cut.";
    }

    IEnumerator CCloseAfterRealtime(float sec)
    {
        yield return new WaitForSecondsRealtime(sec);
        Close();
    }

    public void Close()
    {
        mode = Mode.Closed;
        if (displayText)
        {
            displayText.text = "";
            displayText.gameObject.SetActive(false);
        }
        if (infoText)
        {
            infoText.text = "";
        }
        gameObject.SetActive(false);
        word = null;
        expected = null;
        expectedOptions = null;
        itemId = "";
        cuts.Clear();
        Time.timeScale = prevTimeScale;
        LastClosedAt = Time.unscaledTime;               // クールダウン用
        UIDebug.DumpState("WordCutUI.Close");
    }

    void Redraw()
    {
        var placed = new HashSet<int>(cuts);
        var sb = new StringBuilder(word.Length * 2 + 8);

        bool flashing = (Time.unscaledTime < flashUntil);

        for (int i = 0; i <= word.Length; i++)
        {
            if (placed.Contains(i))
            {
                if (i == correctCut)
                    sb.Append("<color=#00FF00>|</color>");
                else if (flashing && i == lastPlacedCut)
                    sb.Append("<color=#FF6666>|</color>");
                else
                    sb.Append("<color=#FFD700>|</color>");
            }

            if (i == caret && !placed.Contains(i))
                sb.Append("<color=#00FFFF>|</color>");

            if (i < word.Length) sb.Append(word[i]);
        }
        if (displayText) displayText.text = sb.ToString();
    }

    public void SetInfoOnly(string message)
    {
        if (!gameObject.activeSelf) gameObject.SetActive(true);
        if (infoText)
        {
            infoText.gameObject.SetActive(true);
            infoText.text = message ?? "";
        }
        if (displayText) displayText.gameObject.SetActive(false);
        UIDebug.Log("WordCutUI.SetInfoOnly", $"msg='{message}'");
    }

    public bool TryShowPrompt(string msg)
    {
        try
        {
            if (!gameObject.activeSelf) gameObject.SetActive(true);

            if (infoText != null)
            {
                infoText.gameObject.SetActive(true);
                infoText.text = msg;
            }

            if (displayText != null) displayText.gameObject.SetActive(false);

            var img = GetComponent<UnityEngine.UI.Image>();
            if (img) img.color = new Color(0, 0, 0, 0);

            return true;
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[WordCutUI] TryShowPrompt failed: {e.Message}");
            return false;
        }
    }

    public void HidePrompt()
    {
        if (infoText) infoText.gameObject.SetActive(false);
        gameObject.SetActive(false);
    }

    public bool IsPromptVisible => infoText && infoText.gameObject.activeInHierarchy && gameObject.activeInHierarchy;

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }
}
