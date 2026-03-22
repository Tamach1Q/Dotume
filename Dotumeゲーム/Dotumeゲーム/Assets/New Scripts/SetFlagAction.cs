using UnityEngine;

public class SetFlagAction : ActionBase
{
    [SerializeField] string flagId;
    [SerializeField] bool value = true;
    public override void Execute()
    {
        if (value) GameState.I?.AddFlag(flagId);
        else GameState.I?.RemoveFlag(flagId);
    }
} 