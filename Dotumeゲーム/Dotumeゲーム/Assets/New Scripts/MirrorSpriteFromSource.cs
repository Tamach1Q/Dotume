using UnityEngine;

/// プレイヤーの見た目（SpriteRenderer）をそのまま"鏡写し"にする。
/// - me 側に付けて、source に Player の SpriteRenderer を割り当てるだけ。
/// - Animator は不要。プレイヤーのアニメーション・フリップ・色なども反映される。
public class MirrorSpriteFromSource : MonoBehaviour
{
    [SerializeField] SpriteRenderer source;    // Player側
    [SerializeField] SpriteRenderer target;    // me側（未指定なら自身）
    [SerializeField] bool copyFlipX = true;
    [SerializeField] bool copyColor = true;
    [SerializeField] bool copySorting = true;
    [SerializeField] bool copyMaterial = false;
    [SerializeField] bool autoBindPlayer = true; // PlayerがDDOLでも自動検索
    bool warnedNoSourceOnce = false;

    void Reset()
    {
        target = GetComponent<SpriteRenderer>();
    }

    void Awake()
    {
        if (!target) target = GetComponent<SpriteRenderer>();
        if (autoBindPlayer && !source)
        {
            TryBindPlayerSprite();
        }
    }

    void LateUpdate()
    {
        if (!source && autoBindPlayer) TryBindPlayerSprite();
        if (!source || !target)
        {
            if (!warnedNoSourceOnce)
            {
                Debug.Log($"[MirrorSpriteFromSource] waiting binding: source={(source?source.name:"null")} target={(target?target.name:"null")} autoBind={autoBindPlayer}");
                warnedNoSourceOnce = true;
            }
            return;
        }
        warnedNoSourceOnce = false;
        target.sprite = source.sprite;
        if (copyFlipX) target.flipX = source.flipX;
        if (copyColor) target.color = source.color;
        if (copySorting)
        {
            target.sortingLayerID = source.sortingLayerID;
            target.sortingOrder = source.sortingOrder;
        }
        if (copyMaterial) target.sharedMaterial = source.sharedMaterial;
    }

    void TryBindPlayerSprite()
    {
        var p = GameObject.FindGameObjectWithTag("Player");
        if (!p)
        {
            var pm = FindObjectOfType<PlayerMove>();
            if (pm) p = pm.gameObject;
        }
        if (!p) return;
        // ルートのSpriteRenderer優先、無ければ子から最初のを拾う
        var sr = p.GetComponent<SpriteRenderer>();
        if (!sr) sr = p.GetComponentInChildren<SpriteRenderer>(true);
        if (sr)
        {
            source = sr;
            Debug.Log($"[MirrorSpriteFromSource] bound source='{sr.gameObject.name}' to target='{(target?target.gameObject.name:"(null)")}'");
        }
    }
}
