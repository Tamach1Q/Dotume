using UnityEngine;

[RequireComponent(typeof(Camera))]
[DefaultExecutionOrder(1000)] // なるべく最後に動く（他より後で上書き）
public class CameraFollow2D_RightScroll : MonoBehaviour
{
    [Header("Target")]
    public Transform target;
    public Rigidbody2D targetRb;  // （任意）補間を尊重

    [Header("Framing")]
    [Range(0f, 1f)] public float anchorX = 0.30f;
    public float offsetY = 1.0f;

    [Header("Dead Zone (world units)")]
    public float deadZoneX = 1.0f;
    public float deadZoneY = 0.5f;

    [Header("Smoothing")]
    public float smoothTime = 0.10f;
    public float maxSpeed = 100f;

    [Header("Bounds")]
    public BoxCollider2D levelBounds;
    public Vector2 boundsPadding = new Vector2(0.2f, 0.2f);

    [Header("Behavior")]
    public bool allowFollowLeft = false;
    public float leftCatchupBuffer = 3f;

    [Header("External control")]
    [SerializeField] bool followLeftOverride = false;
    public void SetFollowLeftOverride(bool on) { followLeftOverride = on; }
    public bool GetFollowLeftOverride() => followLeftOverride;

    Camera cam;
    Vector3 vel;
    float lastDesiredX;
    bool initialized;

    // ★ 他のスクリプトが位置を書き換えたか検知用
    Vector3 lastApplied;
    bool hasLastApplied;

    void Awake()
    {
        cam = GetComponent<Camera>();
        if (!targetRb && target) targetRb = target.GetComponent<Rigidbody2D>();
        DontDestroyOnLoad(gameObject); 
    }

    void LateUpdate()
    {
        if (!target) return;

        // 前フレームに自分が置いた位置と違っていたら、誰かが動かしています
        if (hasLastApplied)
        {
            float moved = (transform.position - lastApplied).sqrMagnitude;
            if (moved > 0.0001f)
            {
                Debug.LogWarning(
                    "[CameraFollow2D_RightScroll] Camera transform was moved by another script. " +
                    "Check other components on the Camera (Cinemachine, other follow scripts, tweens, etc.)."
                );
            }
        }

        // WordCut中は止める
        if (WordCutUI.Instance && WordCutUI.Instance.IsOpen) return;

        float vert = cam.orthographicSize;
        float hor = vert * cam.aspect;

        Vector3 tpos = targetRb ? (Vector3)targetRb.position : target.position;
        Vector3 camPos = transform.position;

        // アンカー位置基準の理想位置
        float wantX = tpos.x - ((anchorX - 0.5f) * 2f * hor);
        float wantY = tpos.y + offsetY;

        // デッドゾーン（超えた分だけ動く＝ヒステリシス）
        float dx = wantX - camPos.x;
        float dy = wantY - camPos.y;
        float desiredX = (Mathf.Abs(dx) <= deadZoneX) ? camPos.x : camPos.x + (dx - Mathf.Sign(dx) * deadZoneX);
        float desiredY = (Mathf.Abs(dy) <= deadZoneY) ? camPos.y : camPos.y + (dy - Mathf.Sign(dy) * deadZoneY);

        if (!initialized) { lastDesiredX = camPos.x; initialized = true; }

        bool allowLeftNow = allowFollowLeft || followLeftOverride;
        if (!allowLeftNow)
        {
            float leftScreenWorldX = camPos.x - hor;
            if (tpos.x < leftScreenWorldX - leftCatchupBuffer)
                lastDesiredX = desiredX;               // 見失い防止
            else
                lastDesiredX = Mathf.Max(lastDesiredX, desiredX); // 右だけ追従
            desiredX = lastDesiredX;
        }
        else
        {
            lastDesiredX = desiredX;
        }

        Vector3 desired = new Vector3(desiredX, desiredY, camPos.z);
        Vector3 next = Vector3.SmoothDamp(camPos, desired, ref vel, smoothTime, maxSpeed, Time.deltaTime);

        // レベル境界でクランプ
        if (levelBounds)
        {
            Vector2 ext = GetCameraExtents();
            Vector3 bmin = levelBounds.bounds.min + (Vector3)ext;
            Vector3 bmax = levelBounds.bounds.max - (Vector3)ext;
            if (bmin.x > bmax.x) { var m = (bmin.x + bmax.x) * 0.5f; bmin.x = bmax.x = m; } // 逆転保険
            if (bmin.y > bmax.y) { var m = (bmin.y + bmax.y) * 0.5f; bmin.y = bmax.y = m; }
            next.x = Mathf.Clamp(next.x, bmin.x, bmax.x);
            next.y = Mathf.Clamp(next.y, bmin.y, bmax.y);
        }

        next.z = -10f;
        transform.position = next;

        // 自分が置いた位置を記録（次フレームで他者書き換え検知）
        lastApplied = next;
        hasLastApplied = true;
    }

    Vector2 GetCameraExtents()
    {
        float vert = cam.orthographicSize;
        float hor = vert * cam.aspect;
        return new Vector2(Mathf.Max(0f, hor - boundsPadding.x),
                           Mathf.Max(0f, vert - boundsPadding.y));
    }

    public void SnapToTarget()
    {
        if (!target) return;
        float vert = cam.orthographicSize;
        float hor = vert * cam.aspect;
        float x = (targetRb ? targetRb.position.x : target.position.x) - ((anchorX - 0.5f) * 2f * hor);
        float y = (targetRb ? targetRb.position.y : target.position.y) + offsetY;
        Vector3 pos = new Vector3(x, y, -10f);
        transform.position = pos;
        vel = Vector3.zero;
        lastDesiredX = x;
        initialized = true;
        lastApplied = pos;
        hasLastApplied = true;
    }
}