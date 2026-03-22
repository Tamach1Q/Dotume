using UnityEngine;

public static class UIDebug
{
    // 有効/無効を簡単に切り替えたい時はここを false に
    public static bool Enabled = true;

    public static void Log(string tag, string msg)
    {
        if (!Enabled) return;
        Debug.Log($"[UIDebug] {tag} | {msg}");
    }

    public static void DumpState(string where)
    {
        if (!Enabled) return;

        var r = UIRouter.I;
        var w = WordCutUI.Instance;
        var gate = Object.FindObjectOfType<ModalGate>();
        var gs = GameState.I;

        string owner = gate ? (gate.CurrentOwner ?? "-") : "(no gate)";
        bool modal = r != null && r.IsModalOpen();
        bool busy = r != null && r.IsBusyForInteraction();
        bool wOpen = w != null && w.IsOpen;
        bool wActive = w != null && w.IsActive;
        bool wGOActive = w != null && w.gameObject.activeInHierarchy;
        bool riding = gs != null && gs.HasFlag("car_riding");
        float lastCutClose = WordCutUI.LastClosedAt;
        bool invOpen = InventoryOverlay.IsOpen;

        Debug.Log($"[UIDebug] {where} | gateOwner={owner} modal={modal} busyForInteract={busy} wOpen={wOpen} wActive={wActive} wGOActive={wGOActive} invOpen={invOpen} ridingCar={riding} timeScale={Time.timeScale:0.###} lastCutClose={lastCutClose:0.###}");
    }
}

