using UnityEngine;

public class HasItemCondition : ConditionBase
{
    [SerializeField] string itemId;
    public override bool Evaluate()
    {
        return GameState.I != null && !string.IsNullOrEmpty(itemId) && GameState.I.Has(itemId);
    }
}