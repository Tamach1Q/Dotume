using UnityEngine;

public class RevealOnItem : MonoBehaviour
{
    [SerializeField] string itemId;              // 例: "rich"
    [SerializeField] GameObject activeWhenHas;   // 例: RichVisual
    [SerializeField] GameObject activeWhenNot;   // 例: OstrichVisual

    [Header("One-shot Lock (optional)")]
    [Tooltip("所持(true)から未所持(false)へ遷移したら、それ以降は復活させない（両方とも非表示）")]
    [SerializeField] bool lockForeverAfterLosingItem = false;
    [Tooltip("上記ロック状態を永続化したい場合のFlag ID（空ならローカルのみ）")]
    [SerializeField] string lockFlagId = "";

    GameState _subscribed;
    bool _lockedLocal = false;   // Flag未使用時のローカルロック
    bool? _prevHas = null;       // 直前の所持状態（遷移検出用）

    void OnEnable()
    {
        Subscribe();
        Refresh();
    }
    void Start()
    {
        Subscribe();
        Refresh();
    }
    void OnDisable() { Unsubscribe(); }

    void Subscribe()
    {
        if (GameState.I == null || _subscribed == GameState.I) return;
        Unsubscribe();
        GameState.I.OnItemsChanged += Refresh; // 所持品が変われば即時反映
        _subscribed = GameState.I;
    }
    void Unsubscribe()
    {
        if (_subscribed != null)
        {
            _subscribed.OnItemsChanged -= Refresh;
            _subscribed = null;
        }
    }

    public void Refresh()
    {
        bool has = GameState.I != null && GameState.I.Has(itemId);

        // One-shot ロック（所持→未所持へ落ちた瞬間にロック）
        if (lockForeverAfterLosingItem)
        {
            bool lockedByFlag = (!string.IsNullOrEmpty(lockFlagId) && GameState.I != null && GameState.I.HasFlag(lockFlagId));
            bool locked = _lockedLocal || lockedByFlag;

            if (!locked && _prevHas.HasValue && _prevHas.Value && !has)
            {
                // 今までは所持していたが、今回未所持に落ちた → ここで永久ロック
                _lockedLocal = true;
                if (GameState.I != null && !string.IsNullOrEmpty(lockFlagId)) GameState.I.AddFlag(lockFlagId);
                locked = true;
            }

            _prevHas = has;

            if (locked)
            {
                // 復活を完全に抑止（両側とも非表示）
                if (activeWhenHas) activeWhenHas.SetActive(false);
                if (activeWhenNot) activeWhenNot.SetActive(false);
                return;
            }
        }

        if (activeWhenHas) activeWhenHas.SetActive(has);
        if (activeWhenNot) activeWhenNot.SetActive(!has);
    }

    public static void TryRefreshAll()
    {
        foreach (var r in Object.FindObjectsOfType<RevealOnItem>(true)) r.Refresh();
    }
}
