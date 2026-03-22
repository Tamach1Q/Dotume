using UnityEngine;

/// me（分身）用の自動バインダ。
/// - Player(DDOL)を見つけて FollowXOnly2D.target と MirrorSpriteFromSource.source を自動設定。
/// - meが非アクティブ→有効化で出現する運用でも OnEnable で再バインドする。
public class MeAutoBinder : MonoBehaviour
{
    [SerializeField] bool rebindOnEnable = true;

    void Awake() { TryBindAll(); }
    void OnEnable() { if (rebindOnEnable) TryBindAll(); }

    void TryBindAll()
    {
        Transform playerTr = FindPlayerTransform();
        if (!playerTr) return;

        // FollowXOnly2D の target
        foreach (var f in GetComponentsInChildren<FollowXOnly2D>(true))
        {
            if (f && !f.target) f.target = playerTr;
        }

        // MirrorSpriteFromSource の source（SpriteRenderer）
        var sr = FindPlayerSpriteRenderer(playerTr.gameObject);
        foreach (var m in GetComponentsInChildren<MirrorSpriteFromSource>(true))
        {
            if (m)
            {
                var fSrc = typeof(MirrorSpriteFromSource).GetField("source", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (fSrc != null)
                {
                    var cur = fSrc.GetValue(m) as SpriteRenderer;
                    if (!cur && sr) fSrc.SetValue(m, sr);
                }
            }
        }
    }

    Transform FindPlayerTransform()
    {
        var p = GameObject.FindGameObjectWithTag("Player");
        if (!p)
        {
            var pm = FindObjectOfType<PlayerMove>();
            if (pm) p = pm.gameObject;
        }
        return p ? p.transform : null;
    }

    SpriteRenderer FindPlayerSpriteRenderer(GameObject player)
    {
        if (!player) return null;
        var sr = player.GetComponent<SpriteRenderer>();
        if (!sr) sr = player.GetComponentInChildren<SpriteRenderer>(true);
        return sr;
    }
}

