using System;
using System.Collections.Generic;
using UnityEngine;

public class GameState : MonoBehaviour
{
    public static GameState I { get; private set; }

    private readonly HashSet<string> items = new();
    private readonly HashSet<string> flags = new();

    // ===== Currency (Yen) =====
    // Stage_Field4での所持金管理用。整数円で扱う。
    [SerializeField] int cashYen = 0;
    public int CashYen => cashYen;
    public event Action<int> OnCashChanged; // 新しい残高を通知

    public event Action OnItemsChanged;                 // 所持変更通知
    public event Action<string, bool> OnFlagChanged;

    void Awake()
    {
        if (I != null && I != this) { Destroy(gameObject); return; }
        I = this;
        DontDestroyOnLoad(gameObject);                  // ルート直下に置いてね
    }

    // --- Items ---
    public void Add(string id)
    {
        if (!string.IsNullOrEmpty(id) && items.Add(id))
        {
            Debug.Log($"[GameState] Item + {id}");
            OnItemsChanged?.Invoke();
        }
    }
    public bool Has(string id) => !string.IsNullOrEmpty(id) && items.Contains(id);
    public void Remove(string id)
    {
        if (!string.IsNullOrEmpty(id) && items.Remove(id))
        {
            Debug.Log($"[GameState] Item - {id}");
            OnItemsChanged?.Invoke();
        }
    }
    public IReadOnlyCollection<string> GetAllItemIds() => items;

    // --- Flags（必要に応じて使用） ---
    public void AddFlag(string id)
    {
        if (string.IsNullOrEmpty(id)) return;
        if (flags.Add(id))
        {
            Debug.Log($"[GameState] Flag + {id}");
            OnFlagChanged?.Invoke(id, true);
            EnableOnFlag.RefreshAll();
            RevealOnFlag.TryRefreshAll();
        }
    }
    public void RemoveFlag(string id)
    {
        if (string.IsNullOrEmpty(id)) return;
        if (flags.Remove(id))
        {
            Debug.Log($"[GameState] Flag - {id}");
            OnFlagChanged?.Invoke(id, false);
            EnableOnFlag.RefreshAll();
            RevealOnFlag.TryRefreshAll();
        }
    }
    public bool HasFlag(string id) => !string.IsNullOrEmpty(id) && flags.Contains(id);
    public void SetFlag(string id, bool v) { if (v) AddFlag(id); else RemoveFlag(id); }

    // --- Cash (Yen) API ---
    public void SetCash(int yen)
    {
        yen = Mathf.Max(0, yen);
        if (cashYen == yen) return;
        cashYen = yen;
        Debug.Log($"[GameState] Cash = {cashYen}¥");
        OnCashChanged?.Invoke(cashYen);
    }

    public void AddCash(int delta)
    {
        if (delta == 0) return;
        SetCash(cashYen + delta);
    }

    public bool TrySpendCash(int amount)
    {
        if (amount <= 0) return true;
        if (cashYen < amount) return false;
        SetCash(cashYen - amount);
        return true;
    }
}
