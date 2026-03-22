using UnityEngine;

public class PropellerRideStarter : MonoBehaviour
{
    [SerializeField] PropellerRide ride;
    [SerializeField] Transform player;

    private void Awake()
    {
        // Awakeでは自動設定しない（シーン遷移後に手動で設定）
    }
    
    public void Begin()
    {
        // 実行時に再度Playerを確認
        if (!player)
        {
            player = PersistentObjectHelper.GetPlayer();
        }
        
        if (!ride || !player) return;
        ride.BeginWith(player);
    }
} 