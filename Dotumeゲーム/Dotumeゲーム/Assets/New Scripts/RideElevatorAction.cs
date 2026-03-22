using UnityEngine;

/// E/Returnで近くのエレベーター(RideableElevator)に乗るアクション。
public class RideElevatorAction : ActionBase
{
    [SerializeField] RideableElevator elevator; // 未指定なら近場から自動取得

    public override void Execute()
    {
        // 1) まずは自分（UseZone）から親方向に探す（同じエレベーターツリー配下想定）
        if (!elevator)
        {
            elevator = GetComponentInParent<RideableElevator>();
        }

        // 2) 見つからなければ最寄りを検索（プレイヤー基準→自分基準の順）
        if (!elevator)
        {
            Transform playerT = null;
            try { playerT = PersistentObjectHelper.GetPlayer(); } catch { /* ignore */ }
            var all = Object.FindObjectsOfType<RideableElevator>(includeInactive: true);
            float best = float.PositiveInfinity;
            RideableElevator bestE = null;
            if (all != null)
            {
                Vector3 origin = playerT ? playerT.position : transform.position;
                foreach (var e in all)
                {
                    if (!e || !e.isActiveAndEnabled) continue;
                    float d = (e.transform.position - origin).sqrMagnitude;
                    if (d < best) { best = d; bestE = e; }
                }
            }
            elevator = bestE;
        }

        // プレイヤーはDDOLのためInspector無しで自動取得
        GameObject player = null;
        try { player = PersistentObjectHelper.GetPlayer()?.gameObject; } catch { /* ignore */ }
        if (!player) player = GameObject.FindGameObjectWithTag("Player");

        if (!elevator || !player) return;
        elevator.TryMount(player);
    }
}
