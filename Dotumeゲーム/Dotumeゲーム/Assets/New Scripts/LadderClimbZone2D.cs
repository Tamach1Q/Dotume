using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class LadderClimbZone2D : MonoBehaviour
{
    [Header("Target")]
    [Tooltip("このタグを持つプレイヤーのみを対象にします。空ならすべて対象。")]
    [SerializeField] private string playerTag = "Player";

    [Header("Movement")]
    [Tooltip("WASD(または矢印キー)による移動速度(単位/秒)")]
    [SerializeField] private float moveSpeed = 4f;
    [Tooltip("梯子内での左右移動を許可するか")]
    [SerializeField] private bool allowHorizontal = true;
    [Tooltip("梯子のBoxCollider2Dの範囲外へ出ないように位置を制限します")]
    [SerializeField] private bool confineWithinCollider = true;
    [Tooltip("侵入時に速度をゼロにするか")]
    [SerializeField] private bool zeroVelocityOnEnter = true;

    [Header("Exit / Attach")]
    [Tooltip("Space(Jump)で梯子から離脱できるようにします")] 
    [SerializeField] private bool detachWithJump = false;
    [Tooltip("下入力(S/↓)で梯子から離脱できるようにします")] 
    [SerializeField] private bool detachWithDown = false;
    [Tooltip("ゾーン内に居るまま上入力で再アタッチを許可")] 
    [SerializeField] private bool allowReattachInside = true;
    [Tooltip("再アタッチ判定の縦入力しきい値")] 
    [SerializeField] private float reattachVerticalThreshold = 0.5f;

    [Header("Auto Detach By Distance")]
    [Tooltip("判定ゾーンから一定距離離れたら自動で離脱する")]
    [SerializeField] private bool autoDetachByDistance = true;
    [Tooltip("自動離脱のしきい値(メートル)。ClosestPointとの距離で判定")]
    [SerializeField] private float detachDistance = 0.25f;

    private readonly HashSet<Rigidbody2D> _climbers = new HashSet<Rigidbody2D>();
    private readonly Dictionary<Rigidbody2D, float> _originalGravity = new Dictionary<Rigidbody2D, float>();
    private Collider2D _zoneCollider;
    private bool _exitRequested;
    private bool _reattachRequested;

    private void Awake()
    {
        _zoneCollider = GetComponent<Collider2D>();
        if (_zoneCollider != null && !_zoneCollider.isTrigger)
        {
            // はしごゾーンはトリガー推奨
            _zoneCollider.isTrigger = true;
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        var rb = other.attachedRigidbody;
        if (rb == null) return;
        if (!MatchesTargetTag(other, rb)) return;

        Attach(rb);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        var rb = other.attachedRigidbody;
        if (rb == null) return;

        if (_climbers.Remove(rb))
        {
            if (_originalGravity.TryGetValue(rb, out var g))
            {
                rb.gravityScale = g;
            }
            else
            {
                rb.gravityScale = 1f;
            }
            _originalGravity.Remove(rb);
        }
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        if (!allowReattachInside) return;
        if (!_reattachRequested) return;
        var rb = other.attachedRigidbody;
        if (rb == null) return;
        if (!MatchesTargetTag(other, rb)) return;
        if (_climbers.Contains(rb)) return;

        Attach(rb);
    }

    private void Update()
    {
        // 入力はUpdateで取る（GetButtonDownの取りこぼし対策）
        bool downPressed = detachWithDown && Input.GetAxisRaw("Vertical") < -0.5f;
        bool jumpPressed = detachWithJump && Input.GetButtonDown("Jump");
        _exitRequested = downPressed || jumpPressed;
        _reattachRequested = Input.GetAxisRaw("Vertical") > reattachVerticalThreshold;
    }

    private void FixedUpdate()
    {
        if (_climbers.Count == 0) return;

        // 入力取得: WASD/矢印キー (Unity標準のHorizontal/Vertical)
        float h = allowHorizontal ? Input.GetAxisRaw("Horizontal") : 0f;
        float v = Input.GetAxisRaw("Vertical");
        Vector2 input = new Vector2(h, v);

        // 離脱要求がある場合、現在のクライマーを退避してから一括処理
        List<Rigidbody2D> toDetach = null;

        foreach (var rb in _climbers)
        {
            if (rb == null) continue;

            // 1) 距離による自動離脱（現在位置）
            if (autoDetachByDistance && DistanceFromZone(rb.position) > detachDistance)
            {
                toDetach ??= new List<Rigidbody2D>();
                toDetach.Add(rb);
                continue;
            }

            if (_exitRequested)
            {
                toDetach ??= new List<Rigidbody2D>();
                toDetach.Add(rb);
                continue;
            }

            if (input.sqrMagnitude > 0.0001f)
            {
                Vector2 dir = input.normalized;
                Vector2 delta = dir * moveSpeed * Time.fixedDeltaTime;
                Vector2 nextPos = rb.position + delta;

                // 2) 距離による自動離脱（次位置）: クランプより優先
                if (autoDetachByDistance && DistanceFromZone(nextPos) > detachDistance)
                {
                    toDetach ??= new List<Rigidbody2D>();
                    toDetach.Add(rb);
                    continue;
                }

                if (confineWithinCollider && _zoneCollider != null)
                {
                    nextPos = ClampToColliderBounds(nextPos, rb);
                }

                rb.MovePosition(nextPos);
            }
            else
            {
                // 入力なし: その場で静止
                rb.velocity = Vector2.zero;
            }
        }

        if (toDetach != null)
        {
            foreach (var rb in toDetach)
            {
                Detach(rb);
            }
        }
    }

    private bool MatchesTargetTag(Collider2D other, Rigidbody2D rb)
    {
        if (string.IsNullOrEmpty(playerTag)) return true;
        if (other.CompareTag(playerTag)) return true;
        if (rb.CompareTag(playerTag)) return true;
        if (rb.gameObject.CompareTag(playerTag)) return true;
        return false;
    }

    private Vector2 ClampToColliderBounds(Vector2 desired, Rigidbody2D rb)
    {
        if (_zoneCollider == null) return desired;

        Bounds b = _zoneCollider.bounds;
        // ざっくりRigidbodyの中心位置で制限。必要ならプレイヤーColliderの半径分のマージン追加など拡張可。
        float x = Mathf.Clamp(desired.x, b.min.x, b.max.x);
        float y = Mathf.Clamp(desired.y, b.min.y, b.max.y);
        return new Vector2(x, y);
    }

    private float DistanceFromZone(Vector2 pos)
    {
        if (_zoneCollider == null) return float.PositiveInfinity;
        Vector2 nearest = _zoneCollider.ClosestPoint(pos);
        return Vector2.Distance(nearest, pos);
    }

    private void Attach(Rigidbody2D rb)
    {
        if (!_climbers.Add(rb)) return;
        if (!_originalGravity.ContainsKey(rb))
        {
            _originalGravity[rb] = rb.gravityScale;
        }

        rb.gravityScale = 0f;
        if (zeroVelocityOnEnter)
        {
            rb.velocity = Vector2.zero;
        }
    }

    private void Detach(Rigidbody2D rb)
    {
        if (!_climbers.Remove(rb)) return;
        if (_originalGravity.TryGetValue(rb, out var g))
        {
            rb.gravityScale = g;
        }
        else
        {
            rb.gravityScale = 1f;
        }
        _originalGravity.Remove(rb);
    }
}
