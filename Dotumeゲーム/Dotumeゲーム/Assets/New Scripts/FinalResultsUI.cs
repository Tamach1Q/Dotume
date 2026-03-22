using UnityEngine;
using TMPro;

public class FinalResultsUI : MonoBehaviour
{
    [SerializeField] TMP_Text timeText;
    [SerializeField] string label = "TIME  ";

    [SerializeField] bool stopTimerOnEnable = true; // 念のためここでも停止可能

    void OnEnable()
    {
        if (stopTimerOnEnable) GameTimer.Stop();
        if (!timeText) timeText = GetComponent<TMP_Text>();
        if (!timeText) return;

        timeText.text = $"{label}{GameTimer.FormatLong(GameTimer.Total)}";
    }
}