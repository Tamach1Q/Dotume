using UnityEngine;

/// 横ゲー用：Idle/Walk 切替 ＋ 左右反転（flipX）
/// Animator 側：BaseLayerに 1D BlendTree×2（IdleBT / WalkBT）
///  Parameters: Float "Speed", Float "MoveX"
///  IdleBT/WalkBT ともに Parameter=MoveX、右用クリップを +1 と -1 に置く（左絵は不要）
[RequireComponent(typeof(Animator))]
[DisallowMultipleComponent]
public class PlayerSideAnimator : MonoBehaviour
{
    [Header("Optional References")]
    [SerializeField] Rigidbody2D rb;                 // 使っていれば割り当て（同じObjなら自動取得でもOK）
    [SerializeField] SpriteRenderer sprite;          // 見た目のSR（子にあるならここへドラッグ）

    [Header("Tuning")]
    [Tooltip("この速度を超えたら歩きへ（ヒステリシス上限）")]
    [SerializeField] float walkEnterSpeed = 0.08f;
    [Tooltip("この速度未満でIdleへ（ヒステリシス下限）")]
    [SerializeField] float walkExitSpeed = 0.02f;

    Animator anim;
    float lastMoveX = 1f;          // 停止中の向き（-1 or +1）
    Vector3 lastPos;               // Transform差分で速度推定用（RBが無くても動く）
    Vector2 externalVelocity;      // 自前移動の人は SetExternalVelocity で渡す
    bool isWalking;                // ヒステリシス用の現在状態

    void Awake()
    {
        anim = GetComponent<Animator>();
        if (!sprite)
            sprite = GetComponent<SpriteRenderer>() ?? GetComponentInChildren<SpriteRenderer>(true);
        if (!rb) rb = GetComponent<Rigidbody2D>();
        lastPos = transform.position;
    }

    void Update()
    {
        // 1) 速度ベクトルを決定（優先：external → rb → Transform差分）
        Vector2 v = externalVelocity;
        if (v == Vector2.zero && rb) v = rb.velocity;
        if (v == Vector2.zero)
        {
            Vector3 delta = transform.position - lastPos;
            float dt = Mathf.Max(Time.deltaTime, 0.0001f);
            v = new Vector2(delta.x, delta.y) / dt;
        }
        lastPos = transform.position;

        // 横だけ見れば十分
        float absX = Mathf.Abs(v.x);

        // 2) ヒステリシスで Idle/Walk を安定切替
        if (!isWalking && absX > walkEnterSpeed) isWalking = true;
        else if (isWalking && absX <= walkExitSpeed) isWalking = false;

        float speedParam = isWalking ? absX : 0f;   // Animatorには0/正の値だけ渡す

        // 向き更新（動いてる時のみ）
        if (absX > 0.01f) lastMoveX = Mathf.Sign(v.x);

        // 3) Animator へ（★Play/Triggerは使わない）
        anim.SetFloat("Speed", speedParam);
        anim.SetFloat("MoveX", lastMoveX);

        // 4) 左のときだけ反転（右素材を左に見せる）
        if (sprite) sprite.flipX = (lastMoveX < 0f);

        // externalVelocity は外部から毎フレ渡す想定なのでここで勝手に0にしない
    }

    /// 自前移動のとき毎フレ呼ぶ：例) SetExternalVelocity(move * moveSpeed);
    public void SetExternalVelocity(Vector2 v) => externalVelocity = v;
}
