using UnityEngine;
using System.Collections;

public class AllowCameraLeftAction : ActionBase
{
    [SerializeField] CameraFollow2D_RightScroll cameraFollow;
    [SerializeField] bool enable = true;
    [SerializeField] float seconds = 0f; // 0=ずっと

    public override void Execute()
    {
        if (!cameraFollow) cameraFollow = GameObject.FindFirstObjectByType<CameraFollow2D_RightScroll>();
        cameraFollow?.SetFollowLeftOverride(enable);
        if (enable && seconds > 0f) StartCoroutine(Revert()); // ←これでOK
    }

    IEnumerator Revert()
    {
        float t = 0f; while (t < seconds) { t += Time.unscaledDeltaTime; yield return null; }
        var cam = GameObject.FindFirstObjectByType<CameraFollow2D_RightScroll>();
        cam?.SetFollowLeftOverride(false);
    }
}