using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class ProximityBubble : MonoBehaviour
{
    [Header("表示させるオブジェクト(World Space)")]
    [SerializeField] GameObject bubbleRoot;
    [Header("プレイヤー検出")]
    [SerializeField] string playerTag = "Player";

    void Reset()
    {
        var col = GetComponent<Collider2D>();
        col.isTrigger = true;
    }

    void OnEnable() { if (bubbleRoot) bubbleRoot.SetActive(false); }
    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag(playerTag) && bubbleRoot) bubbleRoot.SetActive(true);
    }
    void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag(playerTag) && bubbleRoot) bubbleRoot.SetActive(false);
    }
}