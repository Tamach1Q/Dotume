using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class KeyPickupTrigger : MonoBehaviour
{
    [SerializeField] string keyItemId = "key";
    [SerializeField] string playerTag = "Player";
    [SerializeField] bool destroyAfterPickup = true;

    void Reset()
    {
        var c = GetComponent<Collider2D>();
        c.isTrigger = true;
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag(playerTag)) return;

        // 乗車中なら強制下車＋車を初期位置へ
        if (GameState.I.HasFlag("car_riding") && RideableCar.I != null)
        {
            RideableCar.I.ForceDismountAndReset();
        }

        if (GameState.I != null && !GameState.I.Has(keyItemId))
        {
            GameState.I.Add(keyItemId);
            UIRouter.I?.ShowPopup("System", "You got a Key!", false, 1.0f, null);
        }

        if (destroyAfterPickup) gameObject.SetActive(false);
    }
}