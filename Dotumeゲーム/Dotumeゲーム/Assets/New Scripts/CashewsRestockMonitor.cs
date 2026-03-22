using UnityEngine;

/// 所持金が600未満のタイミングで、キー未所持なら cashewnuts を再販扱いにし、
/// 演出後リスタートを促す（Stage_Field4 仕様）。
public class CashewsRestockMonitor : MonoBehaviour
{
    const string FLAG_CASHEWS_SOLD = "cashews_sold_once";      // 一度購入済みか
    const string FLAG_CASHEWS_RESTOCKED = "cashews_restocked"; // 再販済み

    void OnEnable()
    {
        if (GameState.I != null) GameState.I.OnCashChanged += OnCashChanged;
    }
    void OnDisable()
    {
        if (GameState.I != null) GameState.I.OnCashChanged -= OnCashChanged;
    }

    void Start()
    {
        // 起動時にも評価
        if (GameState.I != null) OnCashChanged(GameState.I.CashYen);
    }

    void OnCashChanged(int yen)
    {
        if (GameState.I == null) return;
        if (GameState.I.Has("key")) return; // キー入手後は無効
        if (GameState.I.HasFlag(FLAG_CASHEWS_RESTOCKED)) return; // 既に再販済み
        if (!GameState.I.HasFlag(FLAG_CASHEWS_SOLD)) return; // まだ一度もカシューナッツを買っていない
        if (yen > 600) return; // 600以下になった瞬間のみ

        // 再販フラグのみ立てる（詰み演出は購入操作側で処理）
        GameState.I.SetFlag(FLAG_CASHEWS_RESTOCKED, true);
    }
}
