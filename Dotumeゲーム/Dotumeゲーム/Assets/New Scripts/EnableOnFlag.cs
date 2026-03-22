// EnableOnFlag.cs （復元版）
using UnityEngine;

public class EnableOnFlag : MonoBehaviour
{
    [SerializeField] string flagId = "";
    [Header("Enable when the condition is TRUE")]
    [SerializeField] Behaviour[] enableComponents;   // Collider2D, Renderer, 任意のMonoBehaviour等
    [SerializeField] GameObject[] enableObjects;     // 任意のGameObject
    [SerializeField] bool enableWhenHas = true;      // スクショの「Enable When Has」
    [SerializeField] bool debugLog = false;

    bool lastApplied; 

    bool HasFlag()
    {
        return GameState.I != null && !string.IsNullOrEmpty(flagId) && GameState.I.HasFlag(flagId);
    }

    bool ShouldEnable() => enableWhenHas ? HasFlag() : !HasFlag();

    void Start() { Refresh(); }
    void OnEnable() { Refresh(); }

    void Update()
    {
        // 毎フレームでも十分軽いですが、気になるならFixedUpdate等にしてもOK
        bool now = ShouldEnable();
        if (now != lastApplied) Apply(now);
    }

    void Apply(bool enable)
    {
        if (enableComponents != null)
            foreach (var c in enableComponents) if (c) c.enabled = enable;

        if (enableObjects != null)
            foreach (var go in enableObjects) if (go) go.SetActive(enable);

        lastApplied = enable;
        if (debugLog)
            Debug.Log($"[EnableOnFlag] {(enable ? "Enable" : "Disable")} (flag={flagId}, has={HasFlag()}) on {name}");
    }

    public void Refresh() => Apply(ShouldEnable());

    // 便利: どこからでも全リフレッシュ
    public static void TryRefreshAll()
    {
        var all = FindObjectsOfType<EnableOnFlag>(true);
        foreach (var e in all) e.Refresh();
    }

    public static void RefreshAll() => TryRefreshAll();
}