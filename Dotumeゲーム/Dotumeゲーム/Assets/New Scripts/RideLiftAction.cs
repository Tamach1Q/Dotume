using UnityEngine;

/// E/Returnで近くのリフト(RideableLift)に乗るアクション。
public class RideLiftAction : ActionBase
{
    [SerializeField] RideableLift lift; // 未指定なら近場から自動取得

    public override void Execute()
    {
        // 1) 自分（UseZone）から親方向に検索
        if (!lift)
        {
            lift = GetComponentInParent<RideableLift>();
        }

        // 2) 近場から検索（プレイヤー→自分基準）
        if (!lift)
        {
            Transform playerT = null;
            try { playerT = PersistentObjectHelper.GetPlayer(); } catch { /* ignore */ }
            var all = Object.FindObjectsOfType<RideableLift>(includeInactive: true);
            float best = float.PositiveInfinity;
            RideableLift bestLift = null;
            if (all != null)
            {
                Vector3 origin = playerT ? playerT.position : transform.position;
                foreach (var e in all)
                {
                    if (!e || !e.isActiveAndEnabled) continue;
                    float d = (e.transform.position - origin).sqrMagnitude;
                    if (d < best) { best = d; bestLift = e; }
                }
            }
            lift = bestLift;
        }

        // プレイヤー自動取得（DDOL対応）
        GameObject player = null;
        try { player = PersistentObjectHelper.GetPlayer()?.gameObject; } catch { /* ignore */ }
        if (!player) player = GameObject.FindGameObjectWithTag("Player");

        if (!lift || !player) return;
        lift.TryMount(player);
    }
}
