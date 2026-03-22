using UnityEngine;
using UnityEngine.Events;

public class InventoryPauseFootsteps : MonoBehaviour
{
    [Tooltip("OnEnableで自動停止、OnDisableで自動再開します")]
    public bool autoPauseOnEnable = true;

    public UnityEvent onPaused;
    public UnityEvent onResumed;

    void OnEnable()
    {
        if (autoPauseOnEnable)
        {
            FootstepLooper.GlobalPause(true);
            onPaused?.Invoke();
        }
    }
    void OnDisable()
    {
        if (autoPauseOnEnable)
        {
            FootstepLooper.GlobalPause(false);
            onResumed?.Invoke();
        }
    }

    // 任意のタイミングで使えるAPI（装備ボタン等から呼ぶ）
    public void PauseFootsteps()  { FootstepLooper.GlobalPause(true);  onPaused?.Invoke(); }
    public void ResumeFootsteps() { FootstepLooper.GlobalPause(false); onResumed?.Invoke(); }
}
