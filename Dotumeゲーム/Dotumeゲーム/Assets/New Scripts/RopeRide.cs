using System.Collections;
using UnityEngine;

/// ロープ使用（上端の UseZone 上で E を押す → 中央へ整列 → 下端まで下降）
[RequireComponent(typeof(Collider2D))]
public class RopeRide : MonoBehaviour
{
    [Header("Refs")]
    [Tooltip("プレイヤーの Transform（たいてい Player）")]
    [SerializeField] Transform player;
    [Tooltip("プレイヤーのメイン Collider2D（バリアとの衝突を一時的に無効化するため）")]
    [SerializeField] Collider2D playerCollider;
    [Tooltip("上端の位置（吸い付け開始点）")]
    [SerializeField] Transform topPoint;
    [Tooltip("下端の位置（到達すると解放）")]
    [SerializeField] Transform bottomPoint;
    [Tooltip("物理バリア（非 Trigger）— 下降中だけプレイヤーとの衝突を無効化")]
    [SerializeField] Collider2D barrierSolid;

    [Header("Control")]
    [Tooltip("ロープ所持のアイテムID")]
    [SerializeField] string ropeItemId = "rope";
    [Tooltip("使用キー")]
    [SerializeField] KeyCode useKey = KeyCode.Return;
    [Tooltip("吸い付け速度（上端中央に寄せる）")]
    [SerializeField] float alignSpeed = 7f;
    [Tooltip("下降速度")]
    [SerializeField] float descendSpeed = 6f;
    [Tooltip("停止判定の誤差")]
    [SerializeField] float arriveEps = 0.02f;

    [Header("Optional: 無効化するプレイヤー制御")]
    [SerializeField] Behaviour[] disableWhileRiding;  // PlayerMove など

    // 内部
    bool inside;
    bool riding;
    Collider2D zone;

    Rigidbody2D playerRb;
    RigidbodyType2D savedType;
    float savedGrav;
    bool savedIsTrigger;
    // このスクリプトを付けた UseZone（isTrigger=true）

    void Awake()
    {
        zone = GetComponent<Collider2D>();
        zone.isTrigger = true;

        if (!player) player = GameObject.FindGameObjectWithTag("Player")?.transform;
        if (!playerCollider && player) playerCollider = player.GetComponent<Collider2D>();
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;
        inside = true;

        Debug.Log($"[RopeRide] hasRope={HasRope()}, ui={(WordCutUI.Instance != null)}, isOpen={(WordCutUI.Instance?.IsOpen ?? false)}");

        // 入った瞬間に一度ヒントを出す
        if (HasRope() && WordCutUI.Instance && !WordCutUI.Instance.IsOpen)
        {
            WordCutUI.Instance.TryShowPrompt("Press A to use the rope.");
        }
    }

    // ★追加：滞在中もヒントを“出し直す”。UI側で非表示にされても復活する
    void OnTriggerStay2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;
        inside = true;

        if (!riding && HasRope() && WordCutUI.Instance && !WordCutUI.Instance.IsOpen)
        {
            WordCutUI.Instance.TryShowPrompt("Press A to use the rope.");
        }
    }

    void OnTriggerExit2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;
        inside = false;

        if (!riding)
            WordCutUI.Instance?.SetInfoOnly(""); // ヒント消去
    }

    void Update()
    {
        if (!inside || riding) return;
        if (!HasRope()) return;

        if (Input.GetKeyDown(useKey) || Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
        {
            StartCoroutine(RideRoutine());
        }
    }

    bool HasRope()
    {
        return (GameState.I != null && GameState.I.Has(ropeItemId));
    }


    IEnumerator RideRoutine()
    {
        if (!player || !topPoint || !bottomPoint) yield break;
        riding = true;

        // ヒントを「Riding...」に
        WordCutUI.Instance?.TryShowPrompt("Riding the rope...");

        // ★ プレイヤーを“通過モード”に
        playerRb = player.GetComponent<Rigidbody2D>();
        var col = playerCollider ? playerCollider : player.GetComponent<Collider2D>();
        if (playerRb)
        {
            savedType = playerRb.bodyType;
            savedGrav = playerRb.gravityScale;
            playerRb.bodyType = RigidbodyType2D.Kinematic;
            playerRb.gravityScale = 0f;
            playerRb.velocity = Vector2.zero;
        }
        if (col)
        {
            savedIsTrigger = col.isTrigger;
            col.isTrigger = true;               // ← これで床・壁を素通り
        }

        // （既存）バリアとは念のため無視
        bool ignored = false;
        if (playerCollider && barrierSolid)
        {
            Physics2D.IgnoreCollision(playerCollider, barrierSolid, true);
            ignored = true;
        }

        // 整列 → 下降
        yield return MoveTo(player, topPoint.position, alignSpeed);
        yield return MoveTo(player, bottomPoint.position, descendSpeed);

        // 復帰
        if (ignored && playerCollider && barrierSolid)
            Physics2D.IgnoreCollision(playerCollider, barrierSolid, false);

        if (col) col.isTrigger = savedIsTrigger;
        if (playerRb)
        {
            playerRb.bodyType = savedType;
            playerRb.gravityScale = savedGrav;
            playerRb.velocity = Vector2.zero;
        }

        SetBehavioursEnabled(true);
        riding = false;
        WordCutUI.Instance?.HidePrompt();
    }

    IEnumerator MoveTo(Transform t, Vector3 target, float speed)
    {
        while (Vector2.Distance(t.position, target) > arriveEps)
        {
            t.position = Vector3.MoveTowards(t.position, target, speed * Time.deltaTime);
            yield return null;
        }
        t.position = target;
    }

    void SetBehavioursEnabled(bool enabled)
    {
        if (disableWhileRiding == null) return;
        foreach (var b in disableWhileRiding) if (b) b.enabled = enabled;
    }
}
