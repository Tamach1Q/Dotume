using System.Collections.Generic;
using System.Text;
using UnityEngine;
using TMPro;

[DefaultExecutionOrder(-2000)]
public class InventoryOverlay : MonoBehaviour
{
    public static InventoryOverlay I { get; private set; }

    [Header("UI Refs")]
    [SerializeField] GameObject panel;    // 黒パネル（最初は非表示）
    [SerializeField] TMP_Text titleText;  // "ITEM"
    [SerializeField] TMP_Text listText;   // 本文
    [SerializeField] TMP_Text hintText;   // "Close with I"

    [Header("Settings")]
    [SerializeField] int capacity = 6;
    [SerializeField] KeyCode toggleKey = KeyCode.I;
    [SerializeField] bool pauseWhileOpen = true;         // 開いてる間は停止
    [SerializeField] float reopenCooldown = 0.2f;        // Cutを閉じた直後のクールダウン

    // 外部からもブロックできる汎用フラグ（必要なら他システムから制御可）
    public static bool BlockOpen { get; set; } = false;

    // Cut中 or 直後は開かせない
    bool ShouldBlock =>
        BlockOpen ||
        (WordCutUI.Instance != null && WordCutUI.Instance.IsActive) ||
        (WordCutUI.RecentlyClosed(reopenCooldown));

    private readonly List<string> _currentNames = new();
    public static bool IsOpen { get; private set; }
    static int _togglePressedFrame = -1;
    public static bool TogglePressedThisFrame => Time.frameCount == _togglePressedFrame;

    void Awake()
    {
        if (I != null && I != this) { Destroy(gameObject); return; }
        I = this;
        DontDestroyOnLoad(gameObject);

        if (panel) panel.SetActive(false);
        Refresh();
    }

    void OnDestroy()
    {
        if (I == this) I = null;
        if (pauseWhileOpen && !IsOtherUIPausingGameplay()) Time.timeScale = 1f;
    }

    void Update()
    {
        // ロック中は自動で閉じる＆入力を無視
        bool togglePressed = Input.GetKeyDown(toggleKey);
        if (togglePressed) _togglePressedFrame = Time.frameCount;

        if (ShouldBlock)
        {
            if (IsOpen) SetOpen(false);
            if (togglePressed) Input.ResetInputAxes();
            return;
        }

        if (togglePressed)
        {
            Toggle();
            Input.ResetInputAxes(); // Clear other key inputs so they don't fire alongside the inventory toggle
        }

        if (pauseWhileOpen && IsOpen && Time.timeScale > 0f)
        {
            Time.timeScale = 0f; // Keep gameplay paused while the inventory overlay is visible
        }
    }

    // ===== 外部API（Bridgeから呼ばれる） =====
    public static void SetItems(IEnumerable<string> displayNames)
    {
        if (I == null) return;
        I._currentNames.Clear();
        if (displayNames != null) I._currentNames.AddRange(displayNames);
        I.Refresh();
    }
    public static void Show()   { if (I) I.SetOpen(true);  }
    public static void Hide()   { if (I) I.SetOpen(false); }
    public static void Toggle() { if (I) I.SetOpen(!IsOpen); }
    public static void ForceClose() { if (I != null && IsOpen) I.SetOpen(false); }

    void SetOpen(bool open)
    {
        IsOpen = open;
        if (panel) panel.SetActive(open);
        if (pauseWhileOpen)
        {
            if (open)
            {
                Time.timeScale = 0f;
            }
            else if (!IsOtherUIPausingGameplay())
            {
                Time.timeScale = 1f;
            }
        }
        Refresh();
    }

    static bool IsOtherUIPausingGameplay()
    {
        return WordCutUI.Instance != null && WordCutUI.Instance.IsOpen;
    }

    void Refresh()
    {
        if (!panel) return;

        if (titleText) titleText.text = "ITEM";
        if (hintText)  hintText.text  = IsOpen ? "Close with I" : "";

        if (listText == null) return;

        // 空のときは何も表示しない
        if (_currentNames.Count == 0) { listText.text = ""; return; }

        var sb = new StringBuilder();
        int n = Mathf.Min(_currentNames.Count, capacity);
        for (int i = 0; i < n; i++) sb.AppendLine($"  {_currentNames[i]}");
        listText.text = sb.ToString();
    }
}
