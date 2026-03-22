// Assets/New Scripts/ConditionNotRidingCar.cs
using UnityEngine;

public class ConditionNotRidingCar : ConditionBase
{
    [SerializeField] string ridingFlag = "car_riding";

    public override bool Evaluate()
    {
        // GameState が無いケースでも落ちないように
        return GameState.I != null && !GameState.I.HasFlag(ridingFlag);
    }
}