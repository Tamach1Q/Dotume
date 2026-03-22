using UnityEngine;

public class RopeReveal : MonoBehaviour
{
    [SerializeField] GameObject ropeVisual;   // ロープの見た目（SpriteRendererをもつオブジェクト）
    [SerializeField] Collider2D barrierSolid; // 落下を防いでいたバリア（IsTrigger=OFF推奨）
    [SerializeField] string requiredItemId = "rope";

    void Start() => Refresh();

    public void Refresh()
    {
        bool has = (GameState.I != null && GameState.I.Has(requiredItemId));
         
        // ロープ見た目
        if (ropeVisual) ropeVisual.SetActive(has);
         
        // バリア：ロープを持っていればOFF、なければON
        if (barrierSolid) barrierSolid.enabled = !has;

        Debug.Log($"[RopeReveal] Refresh: has={has}, ropeVisual={(ropeVisual ? ropeVisual.activeSelf : false)}, barrierEnabled={(barrierSolid ? barrierSolid.enabled : false)}");
    }

    public static void TryRefreshAll()
    {
        var all = Object.FindObjectsOfType<RopeReveal>(includeInactive: true);
        foreach (var r in all) if (r != null) r.Refresh();
    }
}