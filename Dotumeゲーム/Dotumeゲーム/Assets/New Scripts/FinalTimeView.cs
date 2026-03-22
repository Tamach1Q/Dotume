using UnityEngine;
using TMPro;

/// 最終画面で総プレイ時間を表示
public class FinalTimeView : MonoBehaviour
{
    [SerializeField] TMP_Text text;
    [SerializeField] string label = "Your time  ";

    void Awake()
    {
        if (!text) text = GetComponentInChildren<TMP_Text>();
    }

    void OnEnable()
    {
        if (!text) return;
        text.text = $"{label}{GameTimer.FormatLong(GameTimer.Total)}";
        // ここで止めたいなら ↓
        // GameTimer.Stop();
    }
}
