using UnityEngine;

/// Return to the previous scene kept on the SceneStackManager, preserving its last runtime state.
public class ReturnToPreviousSceneAction : ActionBase
{
    [Header("Timing")]
    [Min(0f)] public float delayRealtime = 0f;

    static bool isLoading;

    public override void Execute()
    {
        if (isLoading) return;
        if (!SceneStackManager.I)
        {
            Debug.LogWarning("[ReturnPrev] SceneStackManager not present; nothing to return to.");
            return;
        }
        isLoading = true;
        SceneStackManager.I.ReturnToPrevious(delayRealtime);
        StartCoroutine(ReleaseGuardNextFrame());
    }

    System.Collections.IEnumerator ReleaseGuardNextFrame()
    {
        yield return null; isLoading = false;
    }

    // このアクションは TriggerZone2D からのみ実行させる（直接のOnTrigger経由では発火しない）
}
