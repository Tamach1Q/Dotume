using UnityEngine;

public class Gate_EnableIfFlags : MonoBehaviour
{
    [SerializeField] GameObject target;            // 無指定なら自身
    [SerializeField] string[] mustHaveFlags;       // すべて必要
    [SerializeField] string[] mustNotHaveFlags;    // どれかあればOUT

    void Awake()
    {
        if (!target) target = gameObject;
        Refresh();
    }

    void OnEnable()
    {
        if (GameState.I != null)
        {
            GameState.I.OnFlagChanged += OnFlagChanged;
            Refresh();
        }
    }

    void OnDisable()
    {
        if (GameState.I != null) GameState.I.OnFlagChanged -= OnFlagChanged;
    }

    void OnFlagChanged(string flagId, bool present) => Refresh();

    void Refresh()
    {
        bool ok = true;
        if (GameState.I != null)
        {
            if (mustHaveFlags != null)
                foreach (var f in mustHaveFlags) if (!string.IsNullOrEmpty(f) && !GameState.I.HasFlag(f)) { ok = false; break; }
            if (ok && mustNotHaveFlags != null)
                foreach (var f in mustNotHaveFlags) if (!string.IsNullOrEmpty(f) && GameState.I.HasFlag(f)) { ok = false; break; }
        }
        if (target) target.SetActive(ok);
    }
}