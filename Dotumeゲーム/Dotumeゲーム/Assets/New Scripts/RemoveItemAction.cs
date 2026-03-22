using UnityEngine;

public class RemoveItemAction : ActionBase
{
    [SerializeField] string itemId;
    public override void Execute() { GameState.I?.Remove(itemId); }
}