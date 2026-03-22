// PropellerRide.cs
using UnityEngine;
using UnityEngine.SceneManagement;

public class PropellerRide : MonoBehaviour
{
    [Header("Move")]
    [SerializeField] Transform upperPoint;
    [SerializeField] float speed = 6f;
    [SerializeField] float arriveEps = 0.02f;

    [Header("Optional")]
    [SerializeField] Behaviour[] disableWhileMoving; // PlayerMove など

    Rigidbody2D rbPlatform;
    Transform rider;
    Rigidbody2D riderRb;
    RigidbodyType2D riderType;
    float riderGrav;
    bool moving;

    // 追加: 親とDDOL状態の退避
    Transform riderOriginalParent;
    bool riderWasInDDOL;

    void Awake()
    {
        rbPlatform = GetComponent<Rigidbody2D>();
        if (!rbPlatform) rbPlatform = gameObject.AddComponent<Rigidbody2D>();
        rbPlatform.bodyType = RigidbodyType2D.Kinematic;
    }

    public void BeginWith(Transform player)
    {
        if (moving || !player) return;

        rider = player;
        riderRb = player.GetComponent<Rigidbody2D>();

        // 元状態を退避 → 一時停止
        if (riderRb)
        {
            riderType = riderRb.bodyType;
            riderGrav = riderRb.gravityScale;
            riderRb.bodyType = RigidbodyType2D.Kinematic;
            riderRb.gravityScale = 0f;
            riderRb.velocity = Vector2.zero;
        }

        // 退避: 親とDDOL所属
        riderOriginalParent = rider.parent;
        riderWasInDDOL = (rider.gameObject.scene.name == "DontDestroyOnLoad");

        rider.SetParent(transform, true);          // 乗せる
        SetBehaviours(false);                      // 既存の無効化呼び出し（元コード参照）  //  [oai_citation:2‡all_code.txt](sediment://file_0000000078346209b96df7b755f7454c)
        StartCoroutine(RideUp());
    }

    System.Collections.IEnumerator RideUp()
    {
        if (!upperPoint) yield break;
        moving = true;

        // finally で必ず解放
        try
        {
            while (Vector2.Distance(transform.position, upperPoint.position) > arriveEps)
            {
                var next = Vector3.MoveTowards(transform.position, upperPoint.position, speed * Time.deltaTime);
                rbPlatform.MovePosition(next);
                yield return null;
            }
            rbPlatform.MovePosition(upperPoint.position);
        }
        finally
        {
            ForceReleaseRider();
            moving = false;
        }
    }

    // 追加: コンポーネントが無効化/破棄された時も確実に実行
    void OnDisable() { ForceReleaseRider(); }
    void OnDestroy() { ForceReleaseRider(); }

    void ForceReleaseRider()
    {
        // 二重実行ガード
        if (!rider) return;

        // 親を元に戻す（もともとDDOLなら復帰も）
        if (riderOriginalParent)
            rider.SetParent(riderOriginalParent, true);
        else
            rider.SetParent(null, true);

        if (riderWasInDDOL)
            DontDestroyOnLoad(rider.gameObject);

        // 物理を元に戻す
        if (riderRb)
        {
            riderRb.bodyType = riderType;
            riderRb.gravityScale = riderGrav;
            riderRb.velocity = Vector2.zero;
        }

        SetBehaviours(true); // 既存の再有効化呼び出し  //  [oai_citation:3‡all_code.txt](sediment://file_0000000078346209b96df7b755f7454c)

        // 退避クリア
        rider = null; riderRb = null;
        riderOriginalParent = null; riderWasInDDOL = false;
    }

    void SetBehaviours(bool enable)
    {
        if (disableWhileMoving == null) return;
        for (int i = 0; i < disableWhileMoving.Length; i++)
            if (disableWhileMoving[i]) disableWhileMoving[i].enabled = enable;
    }
}