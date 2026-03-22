using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

public class InventoryOverlayBridge : MonoBehaviour
{
    public static InventoryOverlayBridge I { get; private set; }
    [SerializeField] int capacity = 100;

    GameState subscribedTarget; // 現在購読している GameState

    void Awake()
    {
        if (I != null && I != this) { Destroy(gameObject); return; }
        I = this;
        DontDestroyOnLoad(gameObject);
    }

    void OnEnable()
    {
        SceneManager.activeSceneChanged += OnSceneChanged;
        TrySubscribe();
        RefreshFromGameState();
    }

    void OnDisable()
    {
        SceneManager.activeSceneChanged -= OnSceneChanged;
        Unsubscribe();
    }

    void Start()
    {
        TrySubscribe();
        RefreshFromGameState();
    }

    void OnSceneChanged(Scene _, Scene __)
    {
        TrySubscribe();          // シーン切替時も再購読
        RefreshFromGameState();
    }

    void TrySubscribe()
    {
        if (subscribedTarget == GameState.I) return;
        Unsubscribe();
        if (GameState.I != null)
        {
            subscribedTarget = GameState.I;
            subscribedTarget.OnItemsChanged += RefreshFromGameState;
            subscribedTarget.OnCashChanged += OnGameStateCashChanged;
            // Debug.Log("[Bridge] Subscribed to GameState");
        }
    }

    void Unsubscribe()
    {
        if (subscribedTarget != null)
        {
            subscribedTarget.OnItemsChanged -= RefreshFromGameState;
            subscribedTarget.OnCashChanged -= OnGameStateCashChanged;
            subscribedTarget = null;
        }
    }

    public static void NotifyInventoryChanged()   // 任意：手動更新用（セーブ/ロード後など）
    {
        if (I == null) return;
        I.TrySubscribe();
        I.RefreshFromGameState();
        RevealOnItem.TryRefreshAll();             // 使わないなら削除OK
    }

    void RefreshFromGameState()
    {
        if (GameState.I == null) { InventoryOverlay.SetItems(null); return; }

        var ids = GameState.I.GetAllItemIds().Take(capacity).ToList();
        if (ids.Count == 0) { InventoryOverlay.SetItems(ids); return; }

        var display = new System.Collections.Generic.List<string>(ids.Count);
        foreach (var id in ids)
        {
            if (id == "cash")
            {
                int yen = GameState.I.CashYen;
                display.Add($"cash : ¥{yen}");
            }
            else
            {
                display.Add(id);
            }
        }
        InventoryOverlay.SetItems(display);
    }

    void OnGameStateCashChanged(int _)
    {
        RefreshFromGameState();
    }
}
