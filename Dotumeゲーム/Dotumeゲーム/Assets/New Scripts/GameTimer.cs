using System;
using UnityEngine;

/// ゲームの総プレイ時間を計測する常駐タイマー（Time.timeScaleの影響を受けない）
[DisallowMultipleComponent]
public class GameTimer : MonoBehaviour
{
    public static GameTimer I { get; private set; }

    // 公開ステータス
    public static bool IsRunning => I && I._running;
    public static double TotalSeconds => I ? I._totalSeconds : 0.0;         // 現在までの積算（秒）
    public static TimeSpan Total => TimeSpan.FromSeconds(TotalSeconds);     // 同、TimeSpan

    // イベント（止まった/再開/リセットなどをUIが知りたい時用）
    public static event Action OnStopped;
    public static event Action OnResumed;
    public static event Action OnReset;

    double _totalSeconds;     // 積算（秒）
    bool _running = false;

    void Awake()
    {
        if (I && I != this) { Destroy(gameObject); return; }
        I = this;
        DontDestroyOnLoad(gameObject);
        // 起動と同時に計測開始
        Resume();
    }

    void Update()
    {
        if (_running)
            _totalSeconds += Time.unscaledDeltaTime; // ポーズ中（timeScale=0）でも進む
    }

    // ====== 外部API（どこからでも呼べる静的メソッド） ======
    public static void Stop()
    {
        if (!I) return;
        I._running = false;
        OnStopped?.Invoke();
    }

    public static void Resume()
    {
        if (!I) return;
        I._running = true;
        OnResumed?.Invoke();
    }

    public static void ResetTimer(bool autoResume = true)
    {
        if (!I) return;
        I._totalSeconds = 0.0;
        if (autoResume) I._running = true;
        OnReset?.Invoke();
    }

    // 表示用フォーマッタ（mm:ss / hh:mm:ss など）
    public static string FormatShort(TimeSpan t)   // 例: 12:34
        => (t.TotalHours >= 1.0) ? $"{(int)t.TotalHours:00}:{t.Minutes:00}:{t.Seconds:00}"
                                 : $"{t.Minutes:00}:{t.Seconds:00}";

    public static string FormatLong(TimeSpan t)    // 例: 01:23:45.6
        => $"{(int)t.TotalHours:00}:{t.Minutes:00}:{t.Seconds:00}.{t.Milliseconds/100}";
}
