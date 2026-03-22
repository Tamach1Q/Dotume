using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections.Generic;

[RequireComponent(typeof(Rigidbody2D))]
public class RideableCar : MonoBehaviour
{
    public static RideableCar I;

    [Header("座席(プレイヤーをぶら下げる位置)")]
    [SerializeField] Transform seatAnchor;
    [Header("走行")]
    [SerializeField] float driveSpeed = 12f;
    [SerializeField] float collisionSkin = 0.02f; // 進行方向の余白（貫通防止）
    [Header("衝突検知(任意)")]
    [SerializeField] LayerMask obstacleMask = ~0; // 水平方向の進行を止める対象レイヤー（Ground/Wall など）
    [Header("UIヒント(任意)")]
    [SerializeField] GameObject ridingHint; // "Nで下車" などのワールド表示
    [SerializeField] string conveyorTag = "Conveyor";
    [SerializeField] Collider2D[] carSolidColliders;
    List<Collider2D> riderColliders = new List<Collider2D>();

    [Header("Key Pickup(乗車中のみ有効)")]
    [Tooltip("このシーン名の時だけKey接触で下車扱いにする。空なら常に有効。")]
    [SerializeField] string keyValidSceneName = "stage_3";
    [Tooltip("Key判定に使うレイヤー名")]
    [SerializeField] string keyLayerName = "Key";
    int keyLayer = -1;
    bool madePlayerPersistent = false; // 二重適用防止

    Rigidbody2D rb;
    Vector3 spawnPos;
    GameObject rider;
    PlayerMove riderMove;  // あなたの移動スクリプト名に合わせて
    Rigidbody2D riderRb;
    Transform riderOriginalParent;
    bool riderWasInDDOL;
    bool rbKinematicBackup;   // 追加
    float riderGravityBackup = 1f;

    const string FLAG_RIDING = "car_riding";
    const string FLAG_ON_CONVEYOR = "on_conveyor";


    void Awake()
    {
        I = this;
        rb = GetComponent<Rigidbody2D>();
        spawnPos = transform.position;
        if (!seatAnchor) seatAnchor = this.transform;
        if (ridingHint) ridingHint.SetActive(false);

        // Keyレイヤーを解決
        keyLayer = LayerMask.NameToLayer(keyLayerName);
    }

    // RideableCar.Update
    void Update()
    { 
        if (IsRiding())
        {
            float input = 0f;
            if (Input.GetKey(KeyCode.D)) input += 1f;
            if (Input.GetKey(KeyCode.A)) input -= 1f;
            // 入力に応じた目標速度
            var targetVx = input * driveSpeed;

            // Rigidbody2D が Kinematic でも BoxCollider2D に“当たって止まる”ように、
            // 次の物理ステップで衝突しそうなら水平速度を 0 にする。
            float dt = Time.deltaTime;
            float moveDist = Mathf.Abs(targetVx) * dt;
            if (moveDist > 0.0001f)
            {
                Vector2 dir = new Vector2(Mathf.Sign(targetVx), 0f);
                var filter = new ContactFilter2D();
                filter.useTriggers = false; // トリガーは無視
                filter.useLayerMask = true;
                filter.SetLayerMask(obstacleMask);

                // 進行方向にキャストして、すぐ先に壁がないか調べる
                var hits = new RaycastHit2D[8];
                int hitCount = rb.Cast(dir, filter, hits, moveDist + collisionSkin);
                bool blocked = false;
                for (int i = 0; i < hitCount; i++)
                {
                    var h = hits[i];
                    if (!h.collider) continue;
                    if (h.collider.isTrigger) continue;
                    // 前方（進行方向側）からの壁だけをブロックとみなす
                    var n = h.normal;
                    bool front = (dir.x > 0f && n.x < -0.5f) || (dir.x < 0f && n.x > 0.5f);
                    if (!front) continue; // 床や天井など横方向でない接触は無視
                    blocked = true;
                    break;
                }

                if (blocked)
                {
                    // 前方に壁があるので停止
                    rb.velocity = new Vector2(0f, rb.velocity.y);
                }
                else
                {
                    rb.velocity = new Vector2(targetVx, rb.velocity.y);
                }
            }
            else
            {
                rb.velocity = new Vector2(0f, rb.velocity.y);
            }

            AlignRiderToSeat();

            bool inConveyor = InConveyorZone();
            if (ridingHint) ridingHint.SetActive(!inConveyor); // コンベア上は非表示

            if (!inConveyor && Input.GetKeyDown(KeyCode.N)) { TryDismount(); }
        }
        else
        {
            rb.velocity = new Vector2(0f, rb.velocity.y);
            if (ridingHint) ridingHint.SetActive(false);
        }
    }

    void SyncConveyorFlag()
    {
        if (GameState.I == null) return;
        bool want = (conveyorContacts > 0);
        bool has = GameState.I.HasFlag(FLAG_ON_CONVEYOR);
        if (want && !has) GameState.I.AddFlag(FLAG_ON_CONVEYOR);
        else if (!want && has) GameState.I.RemoveFlag(FLAG_ON_CONVEYOR);
    }

    int conveyorContacts = 0;
    bool InConveyorZone()
    {
        // 既存のフラグ運用があるなら併用
        bool byFlag = GameState.I && GameState.I.HasFlag("on_conveyor");
        return conveyorContacts > 0 || byFlag;
    }

    void OnTriggerEnter2D(Collider2D c)
    {
        if (c.CompareTag(conveyorTag)) conveyorContacts++;
        TryHandleKeyContact(c.gameObject);
    }
    void OnTriggerExit2D(Collider2D c) { if (c.CompareTag(conveyorTag)) conveyorContacts = Mathf.Max(0, conveyorContacts - 1); }

    void OnCollisionEnter2D(Collision2D c)
    {
        TryHandleKeyContact(c.collider ? c.collider.gameObject : null);
    }

    void TryHandleKeyContact(GameObject other)
    {
        if (!other) return;
        if (!IsRiding()) return;
        // シーン制限
        if (!string.IsNullOrEmpty(keyValidSceneName))
        {
            var cur = SceneManager.GetActiveScene().name;
            if (!string.Equals(cur, keyValidSceneName, System.StringComparison.Ordinal))
                return;
        }
        // レイヤー一致
        if (keyLayer >= 0)
        {
            if (other.layer != keyLayer) return;
        }
        else
        {
            // レイヤー名が見つからない場合は安全のため何もしない
            return;
        }

        // 一度だけ実行（多重接触対策）
        if (madePlayerPersistent) return;

        var currentRider = rider; // DoDismount内でnull化されるので退避

        // 強制下車＋車リセット
        ForceDismountAndReset();

        // Player を永続化（DontDestroyOnLoad 直呼び）
        if (currentRider) {
            DontDestroyOnLoad(currentRider);
            madePlayerPersistent = true;
        }
    }


    public bool IsRiding() => GameState.I.HasFlag(FLAG_RIDING);

    void AlignRiderToSeat()
    {
        if (rider && seatAnchor)
            rider.transform.position = seatAnchor.position;
    }

    // RideableCar.TryMount
    public bool TryMount(GameObject player)
    {
        if (!player)
        {
            Debug.LogWarning("[RideableCar] TryMount called with null player reference.", this);
            return false;
        }

        if (IsRiding())
        {
            Debug.Log("[RideableCar] TryMount ignored: already riding.");
            return false;
        }

        UIDebug.DumpState("RideableCar.TryMount.before");

        rider = player;                               // 追加
        riderMove = rider.GetComponent<PlayerMove>(); // 既存
        riderRb = rider.GetComponent<Rigidbody2D>(); // 既存
        riderOriginalParent = rider.transform.parent;
        riderWasInDDOL = (rider.scene.name == "DontDestroyOnLoad");

        if (riderMove) riderMove.enabled = false;
        if (riderRb) { riderGravityBackup = riderRb.gravityScale; rbKinematicBackup = riderRb.isKinematic; riderRb.isKinematic = true; riderRb.gravityScale = 0f; riderRb.velocity = Vector2.zero; }

        rider.transform.SetParent(seatAnchor, true);
        AlignRiderToSeat();

        riderColliders.Clear();
        riderColliders.AddRange(rider.GetComponentsInChildren<Collider2D>(true));
        SetRiderCollisionEnabled(false);  // ←ここが肝

        GameState.I?.AddFlag("car_riding");  // 既存
        return true;

    }

    public void TryDismount()
    {
        if (!IsRiding()) return;
        if (GameState.I.HasFlag(FLAG_ON_CONVEYOR)) return; // ベルト上は下車不可

        // 車の左右を評価して“落下しやすい/安全な”側へ降ろす
        Vector3 drop = GetBestDismountPosition();
        DoDismount(drop);

        // 車は初期位置へ戻す
        ResetCarToSpawn();
        UIDebug.DumpState("RideableCar.TryDismount.after");
    }

    public void ForceDismountAndReset()
    {
        if (!IsRiding())
        {
            ResetCarToSpawn();
            return;
        }
        // その場で下車。ただし左右を評価してより安全な側を選ぶ
        Vector3 drop = GetBestDismountPosition(preferTighterOffset:true);
        DoDismount(drop);
        ResetCarToSpawn();
    }

    // --- 降車位置選定ヘルパ ---
    [Header("下車位置判定")]
    [SerializeField] float dismountSideOffset = 1.2f; // 右/左にずらす基本オフセット
    [SerializeField] float dismountTightOffset = 0.8f; // 強制下車時などのやや狭いオフセット
    [SerializeField] float dismountProbeRadius = 0.25f; // その地点が空いているかを調べる半径
    [SerializeField] float groundProbeMax = 12f;        // 真下の地面までの最大探索距離

    Vector3 GetBestDismountPosition(bool preferTighterOffset = false)
    {
        float side = dismountSideOffset;
        if (preferTighterOffset) side = dismountTightOffset;

        var basePos = seatAnchor ? seatAnchor.position : transform.position;

        // 候補: 左/右
        var left = basePos + Vector3.left * side;
        var right = basePos + Vector3.right * side;

        // 各候補の“空き具合”と“下方向の落下距離”を評価
        var leftScore = ScoreDismountSpot(left);
        var rightScore = ScoreDismountSpot(right);

        // スコアが高い側（=より空いていて、下に広い）を採用
        // 同点なら左を優先（stage3の左落下(DDDDL)想定にフィット）
        if (leftScore >= rightScore)
            return left;
        return right;
    }

    float ScoreDismountSpot(Vector3 pos)
    {
        // 1) その地点が空いているか
        bool clear = IsClearAt(pos, dismountProbeRadius);
        if (!clear)
        {
            // 詰まっている場所は大きく減点
            return -1000f;
        }

        // 2) 下方向にどれだけ自由落下できるか（遠いほど高評価）
        float fallDist = GetGroundDistanceBelow(pos, groundProbeMax);

        // スコア = 落下距離（十分な空間を優先）
        return fallDist;
    }

    bool IsClearAt(Vector3 pos, float radius)
    {
        // obstacleMask を使用し、プレイヤーが降り立つ空間が空いているかを確認
        var hit = Physics2D.OverlapCircle(pos, radius, obstacleMask);
        return hit == null || hit.isTrigger; // 実体に重ならなければOK
    }

    float GetGroundDistanceBelow(Vector3 pos, float maxDist)
    {
        var hit = Physics2D.Raycast(pos, Vector2.down, maxDist, obstacleMask);
        if (hit.collider == null) return maxDist; // 床が無ければ“落下し放題”とみなす
        return hit.distance;
    }

    void DoDismount(Vector3 dropWorldPos)
    {
        // 置換: DoDismount 内の復帰部分
        if (rider)
        {
            rider.transform.SetParent(null, true);
            rider.transform.position = dropWorldPos;

            if (riderMove) riderMove.enabled = true;
            if (riderRb)
            {
                riderRb.isKinematic = rbKinematicBackup; // 追加
                riderRb.gravityScale = riderGravityBackup;
            }

            // もともとDDDL(DontDestroyOnLoad)配下に居たなら復帰させる
            if (riderWasInDDOL)
            {
                DontDestroyOnLoad(rider);
            }

            SetRiderCollisionEnabled(true);
        }
        GameState.I.RemoveFlag(FLAG_RIDING);
        if (ridingHint) ridingHint.SetActive(false);
        rider = null; riderMove = null; riderRb = null; riderOriginalParent = null; riderWasInDDOL = false;
        UIDebug.DumpState("RideableCar.DoDismount.end");
    }

    void SetRiderCollisionEnabled(bool enable)
    {
        if (carSolidColliders == null || riderColliders == null) return;
        foreach (var rc in riderColliders)
        {
            if (rc == null) continue;
            foreach (var cc in carSolidColliders)
            {
                if (cc == null) continue;
                Physics2D.IgnoreCollision(rc, cc, !enable);
            }
        }
    }

    public void ResetCarToSpawn()
    {
        rb.velocity = Vector2.zero;
        transform.position = spawnPos;
    }

}
