// MonkeyWanderInBox.cs
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class MonkeyWanderInBox : MonoBehaviour
{
    [Header("Area")]
    public BoxCollider2D area;       // ここに“動かす範囲”の BoxCollider2D をドラッグ
    public float borderPadding = 0.2f;

    [Header("Move")]
    public float speed = 3f;
    public float retargetInterval = 2f;
    public float arriveEps = 0.08f;

    Rigidbody2D rb;
    Vector2 target;
    float timer;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.interpolation = RigidbodyInterpolation2D.Interpolate; // 滑らか
    }

    void OnEnable()
    {
        if (area) SnapInsideIfNeeded();
        PickTarget();
    }

    void FixedUpdate()
    {
        if (!area) return;

        timer += Time.fixedDeltaTime;

        // ターゲット到達 or 時間で取り直し
        if (Vector2.Distance(rb.position, target) <= arriveEps || timer >= retargetInterval)
            PickTarget(); 

        // 目標へ移動
        var next = Vector2.MoveTowards(rb.position, target, speed * Time.fixedDeltaTime);

        // 毎ステップ範囲にクランプ（外へ出ない）
        next = ClampToArea(next);

        rb.MovePosition(next);
    }

    void PickTarget()
    {
        var b = area.bounds;
        float x = Random.Range(b.min.x + borderPadding, b.max.x - borderPadding);
        float y = Random.Range(b.min.y + borderPadding, b.max.y - borderPadding);
        target = new Vector2(x, y);
        timer = 0f;
    }

    Vector2 ClampToArea(Vector2 p)
    {
        var b = area.bounds;
        return new Vector2(
            Mathf.Clamp(p.x, b.min.x + borderPadding, b.max.x - borderPadding),
            Mathf.Clamp(p.y, b.min.y + borderPadding, b.max.y - borderPadding)
        );
    }

    void SnapInsideIfNeeded()
    {
        rb.position = ClampToArea(rb.position);
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        if (!area) return;
        Gizmos.color = new Color(1f, 1f, 0f, 0.25f);
        Gizmos.DrawCube(area.bounds.center, area.bounds.size);
    }
#endif
}