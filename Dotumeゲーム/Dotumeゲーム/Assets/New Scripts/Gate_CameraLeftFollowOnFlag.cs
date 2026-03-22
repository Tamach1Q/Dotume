using UnityEngine;

public class Gate_CameraLeftFollowOnFlag : MonoBehaviour
{
    [SerializeField] CameraFollow2D_RightScroll cameraFollow;
    [SerializeField] string flagId = "need_rope_hint";
    [SerializeField] bool enableWhenPresent = true;

    void Awake()
    {
        if (!cameraFollow) cameraFollow = FindObjectOfType<CameraFollow2D_RightScroll>();
        Apply();
    }

    void OnEnable() { if (GameState.I != null) GameState.I.OnFlagChanged += OnFlag; Apply(); }
    void OnDisable() { if (GameState.I != null) GameState.I.OnFlagChanged -= OnFlag; }

    void OnFlag(string id, bool present) { if (id == flagId) Apply(); }

    void Apply()
    {
        if (!cameraFollow || GameState.I == null) return;
        bool has = GameState.I.HasFlag(flagId);
        cameraFollow.SetFollowLeftOverride(enableWhenPresent ? has : !has);
    }
}