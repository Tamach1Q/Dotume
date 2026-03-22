using UnityEngine;

/// 所持金を加算し、任意で簡単なポップアップを出すアクション。
public class AddCashAction : ActionBase
{
    [SerializeField] int amount = 200;
    [Header("Popup")]
    [SerializeField] bool showPopup = true;
    [SerializeField] string popupOwner = "Popup";
    [SerializeField] string popupFormat = "got ¥{0}!"; // {0}=amount
    [SerializeField] bool requireConfirm = false;
    [SerializeField] float durationSeconds = 1.0f;

    public override void Execute()
    {
        if (GameState.I == null) return;
        GameState.I.AddCash(amount);
        if (amount > 0) GameState.I.Add("cash");
        if (showPopup && UIRouter.I != null)
        {
            string msg = string.Format(popupFormat, amount);
            UIRouter.I.ShowPopup(popupOwner, msg, requireConfirm, durationSeconds, null);
        }
    }
}
