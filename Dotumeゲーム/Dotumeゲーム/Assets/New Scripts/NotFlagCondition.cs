using UnityEngine;

public class NotFlagCondition : ConditionBase
{
    [SerializeField] string flagId;
    public override bool Evaluate()
    {
        return GameState.I == null || string.IsNullOrEmpty(flagId) || !GameState.I.HasFlag(flagId);
    }
}