using UnityEngine;

public class AddItemAction : ActionBase
{
    [SerializeField] string itemId;
    public override void Execute() { GameState.I?.Add(itemId); }
}