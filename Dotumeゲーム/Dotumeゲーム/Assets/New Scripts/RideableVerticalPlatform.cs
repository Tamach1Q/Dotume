using UnityEngine;
using TMPro;

/// 垂直方向に手動操作できる乗り物ベース（W/Sで上下、Nで下車）。
public class RideableVerticalPlatform : MonoBehaviour
{
    [Header("Move")]
    [SerializeField] float speed = 6f;
    [SerializeField] float collisionSkin = 0.02f;
    [Tooltip("衝突直前にどれだけ余白を残して停止するか（上方向）")]
    [SerializeField] float stopGapUp = 0.01f;
    [Tooltip("衝突直前にどれだけ余白を残して停止するか（下方向）")]
    [SerializeField] float stopGapDown = 0.001f;

    [Header("Seat & UI")]
    [SerializeField] protected Transform seatAnchor;
    [SerializeField] protected GameObject ridingHint;
    [SerializeField] TMP_Text ridingHintLabel;                 // 任意: ヒント文言を動的に差し替え
    [SerializeField] string ridingHintMessage = "Press ↑/↓ to move"; // 例: ↑/↓ を使う表記でもOK

    [Header("Dismount")]
    [Tooltip("自動降車時にseatAnchorから上方向へ加えるオフセット(m)")]
    [SerializeField] float dismountUpOffset = 0.18f;

    [Header("Optional")]
    [SerializeField] protected Behaviour[] disableWhileRiding;
    [SerializeField] protected Collider2D[] platformSolidColliders;

    [Header("Stop Zones (任意)")]
    [Tooltip("上端の停止ポイントに使う Trigger(BoxCollider2D 等)。未設定なら無効。")]
    [SerializeField] Collider2D topStopZone;
    [Tooltip("下端の停止ポイントに使う Trigger(BoxCollider2D 等)。未設定なら無効。")]
    [SerializeField] Collider2D bottomStopZone;
    [Tooltip("停止ゾーンで自動降車させるか（false ならその場で停止するだけ）")] 
    [SerializeField] bool dismountOnStopZone = false;

    [Header("Debug")]
    [SerializeField] bool debugLog = false;
    [SerializeField] float debugThrottleSec = 0.05f;
    float _lastDbgAt = -999f;

    Rigidbody2D rb;
    GameObject rider;
    PlayerMove riderMove;
    Rigidbody2D riderRb;
    RigidbodyType2D riderType;
    float riderGrav;
    bool isRiding;
    Transform riderOriginalParent;
    bool riderWasInDDOL;

    // 乗車中を他所から判定できるようにフラグを共有（Bubbles消灯などに使用）
    const string FLAG_VERTICAL_RIDING = "vertical_riding";

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        if (!rb) rb = gameObject.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;
        if (!seatAnchor) seatAnchor = transform;
        if (ridingHint) ridingHint.SetActive(false);

        // ridingHintLabel が未設定なら、ridingHint配下から自動で拾う
        if (!ridingHintLabel && ridingHint)
        {
            ridingHintLabel = ridingHint.GetComponentInChildren<TMP_Text>(includeInactive: true);
        }

        // 自動配線: platformSolidColliders が未設定なら、自身と子の非Trigger Collider2Dを収集
        if (platformSolidColliders == null || platformSolidColliders.Length == 0)
        {
            try
            {
                var all = GetComponentsInChildren<Collider2D>(includeInactive: true);
                var list = new System.Collections.Generic.List<Collider2D>();
                foreach (var c in all)
                {
                    if (!c) continue;
                    if (c.isTrigger) continue;
                    list.Add(c);
                }
                platformSolidColliders = list.ToArray();
            }
            catch { /* ignore */ }
        }
    }

    void Update()
    {
        if (!isRiding)
        {
            if (ridingHint) ridingHint.SetActive(false);
            return;
        }

        float input = 0f;
        if (Input.GetKey(KeyCode.W)) input += 1f;
        if (Input.GetKey(KeyCode.S)) input -= 1f;

        float vy = input * speed;
        float dt = Time.deltaTime;
        float moveDist = Mathf.Abs(vy) * dt;

        if (moveDist > 0.0001f)
        {
            Vector2 dir = new Vector2(0f, Mathf.Sign(vy));

            float allowed = moveDist;          // このフレームで許可される移動量
            Collider2D targetZone = dir.y > 0f ? topStopZone : bottomStopZone;
            bool usingStopZone = targetZone != null;
            bool hitStopZone = false;
            float minZoneDist = float.MaxValue;
            if (usingStopZone)
            {
                var zFilter = new ContactFilter2D();
                zFilter.useTriggers = true;
                int zoneMask = 0;
                if (topStopZone) zoneMask |= (1 << topStopZone.gameObject.layer);
                if (bottomStopZone) zoneMask |= (1 << bottomStopZone.gameObject.layer);
                if (zoneMask != 0) { zFilter.useLayerMask = true; zFilter.SetLayerMask(zoneMask); }
                else { zFilter.useLayerMask = false; }

                var zHits = new RaycastHit2D[8];
                int zCount = rb.Cast(dir, zFilter, zHits, moveDist + collisionSkin);
                for (int i = 0; i < zCount; i++)
                {
                    var h = zHits[i];
                    if (!h.collider) continue;
                    bool match = targetZone != null && (h.collider == targetZone || h.collider.transform.IsChildOf(targetZone.transform));
                    if (!match) continue;
                    hitStopZone = true;
                    if (h.distance < minZoneDist) minZoneDist = h.distance;
                    if (debugLog && (Time.unscaledTime - _lastDbgAt) > debugThrottleSec)
                    {
                        Debug.Log($"[LiftDbg] {name} hitZone dir={(dir.y>0?"UP":"DOWN")} target={h.collider.name} dist={h.distance:F4}");
                        _lastDbgAt = Time.unscaledTime;
                    }
                }

                if (hitStopZone)
                {
                    float gap = (dir.y > 0f) ? Mathf.Max(0f, stopGapUp) : Mathf.Max(0f, stopGapDown);
                    float safe = Mathf.Max(0f, minZoneDist - gap);
                    allowed = Mathf.Min(allowed, safe);
                }
            }

            if (allowed > 0.0001f)
            {
                float step = Mathf.Sign(vy) * allowed;
                if (debugLog && (Time.unscaledTime - _lastDbgAt) > debugThrottleSec)
                {
                    Debug.Log($"[LiftDbg] {name} move dir={(dir.y>0?"UP":"DOWN")} vy={vy:F3} dt={dt:F3} moveDist={moveDist:F3} allowed={allowed:F4} hitZone={hitStopZone} gap={(dir.y>0?stopGapUp:stopGapDown):F4}");
                    _lastDbgAt = Time.unscaledTime;
                }
                rb.MovePosition(transform.position + new Vector3(0f, step, 0f));
            }
            else if (hitStopZone)
            {
                // 停止ゾーンでブロック
                if (debugLog)
                {
                    Debug.Log($"[LiftDbg] {name} stop at zone dir={(dir.y>0?"UP":"DOWN")} minZone={minZoneDist:F4} gap={(dir.y>0?stopGapUp:stopGapDown):F4} autoDismount={dismountOnStopZone}");
                }
                if (dismountOnStopZone) AutoDismountAtEdge();
            }
            // ゾーンにも衝突にも該当しない場合はそのまま移動を継続
        }

        if (ridingHint) ridingHint.SetActive(true);
        AlignRiderToSeat();
    }

    void AlignRiderToSeat()
    {
        if (rider && seatAnchor) rider.transform.position = seatAnchor.position;
    }

    public bool TryMount(GameObject player)
    {
        if (isRiding || !player) return false;
        rider = player;
        riderMove = rider.GetComponent<PlayerMove>();
        riderRb = rider.GetComponent<Rigidbody2D>();

        if (riderRb)
        {
            riderType = riderRb.bodyType;
            riderGrav = riderRb.gravityScale;
            riderRb.bodyType = RigidbodyType2D.Kinematic;
            riderRb.gravityScale = 0f;
            riderRb.velocity = Vector2.zero;
        }
        riderOriginalParent = rider.transform.parent;
        riderWasInDDOL = (rider.scene.name == "DontDestroyOnLoad");
    if (riderMove) riderMove.enabled = false;
        rider.transform.SetParent(seatAnchor, true);
        AlignRiderToSeat();
        SetRiderCollisionEnabled(false);
        SetBehaviours(false);
        isRiding = true;
        // 乗車ヒントの文言を差し替え
        if (ridingHintLabel && !string.IsNullOrEmpty(ridingHintMessage)) ridingHintLabel.text = ridingHintMessage;
        if (ridingHint) ridingHint.SetActive(true);
        try { GameState.I?.AddFlag(FLAG_VERTICAL_RIDING); } catch { /* ignore */ }
        return true;
    
    }

    // 手動降車は提供しない（上端/下端に到達したときのみ自動降車）
    void AutoDismountAtEdge()
    {
        if (!isRiding) return;
        // seatAnchorの“少し上”に降車させる（台より下に落ちないように）
        var basePos = seatAnchor ? seatAnchor.position : transform.position;
        var drop = basePos + Vector3.up * dismountUpOffset;
        if (debugLog)
        {
            Debug.Log($"[LiftDbg] {name} AutoDismountAtEdge baseY={basePos.y:F3} dropY={drop.y:F3}");
        }
        DoDismount(drop);
    }

    void DoDismount(Vector3 dropWorldPos)
    {
        if (rider)
        {
            rider.transform.SetParent(null, true);
            rider.transform.position = dropWorldPos;
            if (riderMove) riderMove.enabled = true;
            if (riderRb)
            {
                riderRb.bodyType = riderType;
                riderRb.gravityScale = riderGrav;
            }
            if (riderWasInDDOL) DontDestroyOnLoad(rider);
            SetRiderCollisionEnabled(true);
            SetBehaviours(true);
        }
        if (ridingHint) ridingHint.SetActive(false);
        rider = null; riderRb = null; riderMove = null; riderOriginalParent = null; riderWasInDDOL = false;
        isRiding = false;
        try { GameState.I?.RemoveFlag(FLAG_VERTICAL_RIDING); } catch { /* ignore */ }
    }

    void OnDisable() { if (isRiding) DoDismount(transform.position); }
    void OnDestroy() { if (isRiding) DoDismount(transform.position); }

    void SetBehaviours(bool enable)
    {
        if (disableWhileRiding == null) return;
        foreach (var b in disableWhileRiding) if (b) b.enabled = enable;
    }

    void SetRiderCollisionEnabled(bool enable)
    {
        if (!rider) return;
        var riderColliders = rider.GetComponentsInChildren<Collider2D>(true);
        if (platformSolidColliders == null || riderColliders == null) return;
        foreach (var rc in riderColliders)
        {
            if (!rc) continue;
            foreach (var pc in platformSolidColliders)
                if (pc) Physics2D.IgnoreCollision(rc, pc, !enable);
        }
    }
}
