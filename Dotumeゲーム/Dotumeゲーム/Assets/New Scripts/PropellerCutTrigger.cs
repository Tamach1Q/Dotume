using UnityEngine;

public class PropellerCutTrigger : MonoBehaviour
{
    [Header("Word Cut")]
    [SerializeField] string word = "propeller";
    [SerializeField] string expected = "rope";
    [SerializeField] string itemId = "rope";
    [SerializeField] int cutsRequired = 2;
    [SerializeField] bool oneShot = true;

    [Header("Gate by Hint Flag")]
    [SerializeField] string requiredFlag = "need_rope_hint"; // これがある時だけ切れる

    bool used = false;
      
    void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;

        // rope ヒントがまだ出ていない＝フラグが無い → ブロック
        if (GameState.I == null || !GameState.I.Has(requiredFlag))
        {
            Debug.Log($"[PropellerCutTrigger] blocked. need flag '{requiredFlag}'");
            return;
        }

        if (oneShot && used) return;
        if (WordCutUI.Instance == null) return;

        used = true;
        WordCutUI.Instance.Open(word, expected, itemId, cutsRequired);
    } 
}