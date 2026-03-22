using UnityEngine;

public class MonkeyMover : MonoBehaviour
{
    [SerializeField] float speed = 3f;
    [SerializeField] float borderPadding = 0.5f;   // 画面端から少し内側
    [SerializeField] float arriveDist = 0.1f;      // 到達判定
    [SerializeField] float retargetInterval = 2f;  // 目標の取り直し間隔(秒)

    Camera cam;
    Vector2 target;
    float timer;
    bool activated = false; // ★初めて画面に入るまでfalse

    void Start()
    {
        cam = Camera.main;
        // ここでは目標を決めない：見えるまでは静止
    }

    void Update()
    {
        if (!cam) return;

        // まだ未起動 → 画面内に入ったら起動
        if (!activated)
        {
            if (IsInsideView(transform.position))
            {
                activated = true;
                PickTargetInView();
                timer = 0f;
            }
            return; // 起動するまで何もしない
        }

        // 以降は常に画面内の目標へ移動
        timer += Time.deltaTime;

        // 画面が動くので、目標が画面外に出た/到達/一定時間で取り直す
        if (!IsInsideView(target) || Vector2.Distance(transform.position, target) < arriveDist || timer >= retargetInterval)
        {
            PickTargetInView();
            timer = 0f;
        }

        transform.position = Vector2.MoveTowards(transform.position, target, speed * Time.deltaTime);

        // 画面外に押し出されないよう最終クランプ（保険）
        var v = GetViewBounds();
        float x = Mathf.Clamp(transform.position.x, v.min.x + borderPadding, v.max.x - borderPadding);
        float y = Mathf.Clamp(transform.position.y, v.min.y + borderPadding, v.max.y - borderPadding);
        transform.position = new Vector2(x, y);
    }

    void PickTargetInView()
    {
        var v = GetViewBounds();
        float x = Random.Range(v.min.x + borderPadding, v.max.x - borderPadding);
        float y = Random.Range(v.min.y + borderPadding, v.max.y - borderPadding);
        target = new Vector2(x, y);
    }

    bool IsInsideView(Vector2 p)
    {
        var v = GetViewBounds();
        return (p.x > v.min.x && p.x < v.max.x && p.y > v.min.y && p.y < v.max.y);
    }

    Bounds GetViewBounds()
    {
        float halfH = cam.orthographicSize;
        float halfW = halfH * cam.aspect;
        Vector3 c = cam.transform.position;
        Bounds b = new Bounds();
        b.SetMinMax(new Vector3(c.x - halfW, c.y - halfH, 0f),
                    new Vector3(c.x + halfW, c.y + halfH, 0f));
        return b;
    }
}