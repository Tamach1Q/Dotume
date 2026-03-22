using UnityEngine;
using TMPro;

[RequireComponent(typeof(RectTransform))]
public class PlaytimeHUD : MonoBehaviour
{
    [SerializeField] TMP_Text text;
    [SerializeField] bool showWhenStopped = true;

    string _template;        // 例: "ノコリジカン: <sprite name=clock> {time}"
    string _compiledFormat;  // 例: "ノコリジカン: <sprite name=clock> {0:00}:{1:00}"
    bool   _hasPlaceholder;  // テンプレに {time} が含まれているか
    int    _lastShownTotalSec = -1;

    void Reset()
    {
        var rt = GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 0f);
        rt.pivot = new Vector2(0f, 0f);
        rt.anchoredPosition = new Vector2(12f, 12f);
    }

    void Awake()
    {
        if (!text) text = GetComponentInChildren<TMP_Text>();
        _template = text ? text.text : "{time}";
        _hasPlaceholder = _template.Contains("{time}");

        // {time} があるならテンプレ式を作る。無いなら数字だけを出す。
        _compiledFormat = _hasPlaceholder
            ? _template.Replace("{time}", "{0:00}:{1:00}")
            : "{0:00}:{1:00}";
    }

    void Update()
    {
        if (!text) return;
        if (!showWhenStopped && !GameTimer.IsRunning) return;

        var totalSec = Mathf.FloorToInt((float)GameTimer.TotalSeconds);
        if (totalSec == _lastShownTotalSec) return; // 1秒ごとにだけ更新
        _lastShownTotalSec = totalSec;

        int minutes = (totalSec / 60) % 60;
        int hours   =  totalSec / 3600;
        int seconds =  totalSec % 60;

        if (hours > 0)
        {
            // 1時間超は hh:mm:ss で表示
            var fmt = _compiledFormat.Replace("{0:00}:{1:00}", "{0:00}:{1:00}:{2:00}");
            text.SetText(fmt, hours, minutes, seconds);
        }
        else
        {
            // 通常は mm:ss
            text.SetText(_compiledFormat, minutes, seconds);
        }
    }
}
