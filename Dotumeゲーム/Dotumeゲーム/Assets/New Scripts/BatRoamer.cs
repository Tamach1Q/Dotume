using UnityEngine;

[RequireComponent(typeof(Transform))]
public class BatRoamer : MonoBehaviour
{
    [Header("Area")]
    [SerializeField] BoxCollider2D roamArea;

    [Header("Move")]
    [SerializeField] float speedMin = 1.8f;
    [SerializeField] float speedMax = 3.2f;
    [SerializeField] float retargetMin = 1.2f;
    [SerializeField] float retargetMax = 2.5f;
    [SerializeField] float arriveDist = 0.2f;

    [Header("Look (either is fine)")]
    [SerializeField] SpriteRenderer sr;                 // 単一レンダラ方式
    [SerializeField] SpriteRendererCycler cycler;       // ← 3レンダラ方式（あるならこちら優先）

    Vector2 target;
    float speed;
    float until;

    void Awake()
    {
        if (!roamArea) Debug.LogWarning("[BatRoamer] roamArea is not set.", this);
        PickNewTarget(true);
    }

    void Update()
    {
        if (!roamArea) return;

        until -= Time.deltaTime;
        if (until <= 0f || Vector2.Distance(transform.position, target) < arriveDist)
            PickNewTarget();

        var pos = (Vector2)transform.position;
        var dir = (target - pos).normalized;

        pos += dir * speed * Time.deltaTime;
        transform.position = pos;

        // 向き：cycler があれば全フレームへ、なければ単一 sr へ
        if (Mathf.Abs(dir.x) > 0.01f)
        {
            bool flip = dir.x < 0;
            if (cycler) cycler.SetFlipX(flip);
            else if (sr) sr.flipX = flip;
        }
    }

    void PickNewTarget(bool first = false)
    {
        var b = roamArea.bounds;
        target = new Vector2(Random.Range(b.min.x, b.max.x), Random.Range(b.max.y, b.min.y));
        speed = Random.Range(speedMin, speedMax);
        until = Random.Range(retargetMin, retargetMax);
        if (first) transform.position = target; // 初期配置をエリア内に
    }
}