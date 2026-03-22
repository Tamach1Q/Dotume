using UnityEngine;

public class RideCarIfHasOilAction : ActionBase
{
    [SerializeField] RideableCar car;
    [SerializeField] string requiredItemId = "oil";
    [Header("Popup (when missing)")]
    [SerializeField] string popupOwner = "Popup";
    [SerializeField] string messageIfMissing = "You need oil to ride!";
    [SerializeField] bool confirm = true;
    [SerializeField] float popupSeconds = 1.0f;
    [Header("Flags")]
    [SerializeField] string filledFlag = "car_oil_filled"; // 一度満たせば以後はOKにする場合に使用
    [SerializeField] bool markFilledOnFirstUse = false;
    [SerializeField] bool consumeItemOnFirstUse = false;

    public override void Execute()
    {
        if (!car) car = Object.FindObjectOfType<RideableCar>(includeInactive: true);
        var player = GameObject.FindGameObjectWithTag("Player");
        if (!car || !player) { UIDebug.Log("RideCarIfHasOil", "car or player missing"); return; }
        if (car.IsRiding()) { UIDebug.Log("RideCarIfHasOil", "already riding"); return; }

        bool ok = false;
        if (GameState.I)
        {
            bool filled = !string.IsNullOrEmpty(filledFlag) && GameState.I.HasFlag(filledFlag);
            bool hasItem = !string.IsNullOrEmpty(requiredItemId) && GameState.I.Has(requiredItemId);
            ok = filled || hasItem;
            UIDebug.Log("RideCarIfHasOil.Check", $"filled={filled} hasItem={hasItem} item='{requiredItemId}'");
        }

        if (!ok)
        {
            UIRouter.I?.ShowPopup(popupOwner, messageIfMissing, confirm, popupSeconds, null);
            return;
        }

        UIDebug.DumpState("RideCarIfHasOil.beforeMount");
        if (car.TryMount(player))
        {
            if (GameState.I)
            {
                if (markFilledOnFirstUse && !string.IsNullOrEmpty(filledFlag)) GameState.I.AddFlag(filledFlag);
                if (consumeItemOnFirstUse && !string.IsNullOrEmpty(requiredItemId) && GameState.I.Has(requiredItemId)) GameState.I.Remove(requiredItemId);
            }
        }
    }
}
