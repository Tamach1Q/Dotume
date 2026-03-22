using UnityEngine;
using UnityEngine.Events;

public class WindStepRouterAction : ActionBase
{
    [Header("Flags")]
    [SerializeField] string lvl1 = "wind_lvl1";
    [SerializeField] string lvl2 = "wind_lvl2";
    [SerializeField] string lvl3 = "wind_lvl3";
    [SerializeField] string dirLeft = "wind_dir_left";
    [SerializeField] string dirRight = "wind_dir_right";
    [SerializeField] string oilItem = "oil";

    [Header("Events")]
    public UnityEvent onStep1; // L1へ進むとき
    public UnityEvent onStep2; // L2へ進むとき
    public UnityEvent onStep3; // L3へ進むとき（最終）

    public override void Execute()
    {
        var gs = GameState.I;
        if (gs == null) return;

        bool isL1 = gs.HasFlag(lvl1);
        bool isL2 = gs.HasFlag(lvl2);
        bool isL3 = gs.HasFlag(lvl3);

        // まだ風なし → Step1
        if (!isL1 && !isL2 && !isL3) { onStep1?.Invoke(); return; }

        // L1中 → Step2
        if (isL1 && !isL2 && !isL3) { onStep2?.Invoke(); return; }

        // L2中 → Step3（ここで方向も決める）
        if (!isL1 && isL2 && !isL3)
        {
            // 方向はここで一意に決める
            bool hasOil = gs.Has(oilItem);
            gs.SetFlag(dirLeft, hasOil);
            gs.SetFlag(dirRight, !hasOil);

            // 風演出の最終段（L3）に移行する間は、
            // プレイヤー操作＆インタラクトを完全停止させる
            gs.SetFlag("me_leads", true);

            onStep3?.Invoke();
            return;
        }

        // 既にL3なら何もしない（冪等）
    }
}
