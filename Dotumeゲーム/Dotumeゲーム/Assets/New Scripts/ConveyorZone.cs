using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class ConveyorZone : MonoBehaviour
{
    [Header("押し戻しに使うフラグ名")]
    [SerializeField] string levelFlag = "wind_lvl2";
    [SerializeField] string leftFlag = "wind_dir_left";
    [SerializeField] string stayFlag = "on_conveyor";
    [SerializeField] string playerTag = "Player";

    void Reset()
    {
        var col = GetComponent<Collider2D>();
        col.isTrigger = true;
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag(playerTag)) return;
        GameState.I.AddFlag(levelFlag);
        GameState.I.AddFlag(leftFlag);
        GameState.I.AddFlag(stayFlag);
    }

    void OnTriggerExit2D(Collider2D other)
    {
        if (!other.CompareTag(playerTag)) return;
        GameState.I.RemoveFlag(levelFlag);
        GameState.I.RemoveFlag(leftFlag);
        GameState.I.RemoveFlag(stayFlag);
    }
}