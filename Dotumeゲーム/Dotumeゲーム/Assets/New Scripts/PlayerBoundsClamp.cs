using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
#if CINEMACHINE
using Cinemachine;
#endif

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerBoundsClamp : MonoBehaviour
{
    [Tooltip("カメラのConfinerと同じCollider2D。空なら自動取得")]
    public Collider2D bounds;
    [Tooltip("境界の内側へ押し戻す厚み(m)。0.06〜0.15くらい")]
    public float inset = 0.1f;

    Rigidbody2D rb;
    Collider2D myCol;

    // 壁ヒット後の“水平速度停止”を数フレーム保持してビタ止まり感を出す
    int wallStickFrames;

    void Awake()
    {
        rb     = GetComponent<Rigidbody2D>();
        myCol  = GetComponent<Collider2D>();

        // 起動時 & シーンロード時に毎回取り直す（Confinerと同じものを掴む）
        StartCoroutine(RefetchBoundsNextFrame());
        SceneManager.sceneLoaded += (_, __) => StartCoroutine(RefetchBoundsNextFrame());
    }

    void OnDestroy()
    {
        SceneManager.sceneLoaded -= (_, __) => StartCoroutine(RefetchBoundsNextFrame());
    }

    IEnumerator RefetchBoundsNextFrame()
    {
        // シーン切替直後はカメラ/Confinerのセットが完了するまで1〜2フレーム待つ
        yield return null;

        if (TryPickFromConfiner(out var col) || TryPickByTag(out col))
            bounds = col;

        // 念のため（Cinemachineのキャッシュ更新タイミングに合わせる）
        yield return null;
    }

    bool TryPickFromConfiner(out Collider2D col)
    {
        col = null;
        #if CINEMACHINE
        var cam   = Camera.main;
        var brain = cam ? cam.GetComponent<CinemachineBrain>() : null;
        var vcam  = brain ? brain.ActiveVirtualCamera as CinemachineVirtualCamera : null;
        if (vcam)
        {
            var conf2d = vcam.GetComponent<CinemachineConfiner2D>();
            if (conf2d && conf2d.m_BoundingShape2D)
            {
                col = conf2d.m_BoundingShape2D;
                return true;
            }
        }
        #endif
        return false;
    }

    bool TryPickByTag(out Collider2D col)
    {
        col = null;
        var go = GameObject.FindGameObjectWithTag("CameraBounds");
        if (!go) return false;
        col = go.GetComponent<Collider2D>();
        return col;
    }

    void FixedUpdate()
    {
        if (!bounds) return;

        float dt = Time.fixedDeltaTime;

        // 次フレームの予測位置で外に出そうなら、内側へ“スナップ”
        Vector2 pos  = rb.position;
        Vector2 next = pos + rb.velocity * dt;

        if (!bounds.OverlapPoint(next))
        {
            // 境界上の最近点を取り、内向きへ少し押し込む
            Vector2 edge = bounds.ClosestPoint(next);
            Vector2 n    = ((Vector2)bounds.bounds.center - edge);
            if (n.sqrMagnitude < 1e-8f) n = Vector2.up;
            n.Normalize();

            float extra = inset;
            if (myCol != null)
            {
                var ext = myCol.bounds.extents;
                extra = Mathf.Max(extra, Mathf.Min(ext.x, ext.y) * 0.8f);
            }

            Vector2 corrected = edge + n * extra;

            // “壁に着いた”感：横壁なら水平速度を止める
            bool sideHit = Mathf.Abs(n.x) > 0.7f; // 法線がほぼ左右
            if (sideHit)
            {
                rb.velocity = new Vector2(0f, rb.velocity.y);
                wallStickFrames = 2; // 数フレームだけ水平加速を抑える
            }
            else
            {
                // 上下の境界：跳ねないように上方向のみ抑える程度
                rb.velocity = new Vector2(rb.velocity.x * 0.9f, Mathf.Min(rb.velocity.y, 0f));
            }

            rb.MovePosition(corrected);
            return;
        }

        // 壁に当たった直後は水平速度を抑えてビリつき防止
        if (wallStickFrames > 0)
        {
            rb.velocity = new Vector2(0f, rb.velocity.y);
            wallStickFrames--;
        }
    }
}