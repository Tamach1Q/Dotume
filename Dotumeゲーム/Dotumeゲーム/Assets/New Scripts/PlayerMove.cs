using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerMove : MonoBehaviour
{
    [Header("Move")]
    public float moveSpeed = 5f;
    public float jumpForce = 5f;

    [Header("Ground Check")]
    [SerializeField] LayerMask groundMask;
    [SerializeField] string groundTag = "Ground";

    [Header("Feel")]
    [SerializeField] float coyoteTime = 0.12f;
    [SerializeField] float jumpBufferTime = 0.12f;
    [SerializeField] float stickJumpThreshold = 0.6f;

    Vector2 _move;
    float _prevMoveY;

    Rigidbody2D rb;
    float inputX;
    float coyoteTimer;
    float jumpBufferTimer;
    bool grounded;

    [Header("Crouch (しゃがみ)")]
    public Animator animator;
    [Range(0.2f, 1f)] public float stickCrouchThreshold = 0.6f;
    bool crouchVisual;

    public void OnMove(InputValue v) => _move = v.Get<Vector2>();
    public void OnJump(InputValue v)
    {
        if (v.isPressed && !crouchVisual)
            jumpBufferTimer = jumpBufferTime;
    }

    void Awake()
    {
        DontDestroyOnLoad(gameObject);
        rb = GetComponent<Rigidbody2D>();
        if (!animator) animator = GetComponentInChildren<Animator>();
    }

    void Update()
    {
        if (ShouldBlockMovement())
        {
            _move = Vector2.zero;
            inputX = 0f;
            jumpBufferTimer = 0f;
            if (rb) rb.velocity = new Vector2(0f, rb.velocity.y);
            return;
        }

        // Only allow crouch input while grounded
        bool wantCrouch = grounded && (Input.GetKey(KeyCode.S) || (_move.y < -stickCrouchThreshold));

        // Update crouch visuals when the state changes
        if (wantCrouch != crouchVisual)
        {
            crouchVisual = wantCrouch;
            if (crouchVisual)
                rb.velocity = new Vector2(0f, rb.velocity.y); // Stop horizontal movement when starting to crouch
            if (animator)
                animator.SetBool("Crouch", crouchVisual);
        }

        float gx = _move.x;
        inputX = (Mathf.Abs(gx) > 0.001f)
            ? Mathf.Clamp(gx, -1f, 1f)
            : (Input.GetKey(KeyCode.D) ? 1f : 0f) + (Input.GetKey(KeyCode.A) ? -1f : 0f);

        if (!crouchVisual && (Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.Space)))
            jumpBufferTimer = jumpBufferTime;

        if (!crouchVisual && _prevMoveY <= stickJumpThreshold && _move.y > stickJumpThreshold)
            jumpBufferTimer = jumpBufferTime;

        _prevMoveY = _move.y;
    }

    void FixedUpdate()
    {
        float dt = Time.fixedDeltaTime;

        if (ShouldBlockMovement())
        {
            if (rb) rb.velocity = new Vector2(0f, rb.velocity.y);
            jumpBufferTimer = 0f;
            return;
        }

        grounded = IsGrounded();
        if (grounded)
            coyoteTimer = coyoteTime;
        else
            coyoteTimer = Mathf.Max(0f, coyoteTimer - dt);

        if (crouchVisual)
            jumpBufferTimer = 0f;

        if (!crouchVisual && jumpBufferTimer > 0f && (grounded || coyoteTimer > 0f))
        {
            rb.velocity = new Vector2(rb.velocity.x, 0f);
            rb.AddForce(Vector2.up * jumpForce, ForceMode2D.Impulse);
            jumpBufferTimer = 0f;
            coyoteTimer = 0f;
            grounded = false;
        }

        if (crouchVisual)
            rb.velocity = new Vector2(0f, rb.velocity.y);
        else
            rb.velocity = new Vector2(inputX * moveSpeed, rb.velocity.y);

        if (jumpBufferTimer > 0f)
            jumpBufferTimer -= dt;
    }

    bool ShouldBlockMovement()
    {
        if (GameDirector.I != null && GameDirector.I.IsPaused) return true;
        if (GameState.I != null && GameState.I.HasFlag("menu_open")) return true;
        // スクリプト側が主導（演出中）は完全停止
        // ただし、FollowXOnly2D の分離演出中（me_split_active）はプレイヤーを動かしたいので除外
        if (GameState.I != null && GameState.I.HasFlag("me_leads") && !(GameState.I.HasFlag("me_split_active"))) return true;
        if (InventoryOverlay.IsOpen) return true;
        return false;
    }

    bool IsGrounded()
    {
        var col = GetComponent<Collider2D>();
        if (!col) return false;

        Bounds b = col.bounds;
        Vector2 center = new Vector2(b.center.x, b.min.y - 0.02f);
        Vector2 size = new Vector2(b.size.x * 0.8f, 0.06f);

        if (groundMask.value != 0)
            return Physics2D.OverlapBox(center, size, 0f, groundMask) != null;

        var hits = Physics2D.OverlapBoxAll(center, size, 0f);
        for (int i = 0; i < hits.Length; i++)
        {
            var h = hits[i];
            if (h.attachedRigidbody == rb) continue;
            if (h.isTrigger) continue;
            if (h.CompareTag(groundTag)) return true;
        }
        return false;
    }

    void OnDrawGizmosSelected()
    {
        var col = GetComponent<Collider2D>();
        if (col)
        {
            Bounds b = col.bounds;
            Vector2 center = new Vector2(b.center.x, b.min.y - 0.02f);
            Vector2 size = new Vector2(b.size.x * 0.8f, 0.06f);
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireCube(center, size);
        }
    }
}
