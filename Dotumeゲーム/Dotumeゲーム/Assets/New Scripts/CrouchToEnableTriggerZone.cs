using UnityEngine;

/// 自販機前で「S（十字キー下）」を押すと、インタラクト待ちに切り替える補助スクリプト。
/// - 同一GameObjectに2D Trigger Collider と TriggerZone2D を付与しておく。
/// - バブル（吹き出し）表示はこのスクリプトの bubbleRoot で制御（TriggerZone2D 側には割り当てない）。
/// - Crouch後に E でインタラクト可能（TriggerZone2D 側に HasFlagCondition などでゲートする想定）。
public class CrouchToEnableTriggerZone : MonoBehaviour
{
    [SerializeField] GameObject bubbleRoot;
    [SerializeField] string readyFlagId = "vm_crouch_ready"; // HasFlagConditionで参照
    [SerializeField] KeyCode crouchKey = KeyCode.S;           // コントローラーは十字キー下で対応
    [Header("Hold Crouch Flag (optional)")]
    [SerializeField] bool setCrouchingFlagWhileHeld = true;   // しゃがみ押下中フラグを発火抑止側で参照
    [SerializeField] string crouchingFlagId = "vm_crouching"; // TriggerZone2D 側のブロック条件用
    [Tooltip("ONで、readyFlagもSキーのホールド中のみtrueにする（E解放条件が“押しっぱなし中のみ”に変わる）")]
    [SerializeField] bool readyFlagWhileHeld = true;

    int inside = 0;

    void OnEnable()
    {
        if (bubbleRoot) bubbleRoot.SetActive(false);
        GameState.I?.SetFlag(readyFlagId, false);
        if (setCrouchingFlagWhileHeld) GameState.I?.SetFlag(crouchingFlagId, false);
    }

    void OnDisable()
    {
        if (bubbleRoot) bubbleRoot.SetActive(false);
        GameState.I?.SetFlag(readyFlagId, false);
        if (setCrouchingFlagWhileHeld) GameState.I?.SetFlag(crouchingFlagId, false);
        inside = 0;
    }

    void OnTriggerEnter2D(Collider2D c)
    {
        if (!IsPlayer(c)) return;
        inside++;
    }

    void OnTriggerExit2D(Collider2D c)
    {
        if (!IsPlayer(c)) return;
        inside = Mathf.Max(0, inside - 1);
        if (inside == 0)
        {
            if (bubbleRoot) bubbleRoot.SetActive(false);
            GameState.I?.SetFlag(readyFlagId, false);
            if (setCrouchingFlagWhileHeld) GameState.I?.SetFlag(crouchingFlagId, false);
        }
    }

    void Update()
    {
        if (inside <= 0) return;
        // メニューUI（カスタム）やモーダルが開いている間は無視
        if ((GameState.I != null && GameState.I.HasFlag("menu_open")) ||
            (UIRouter.I != null && UIRouter.I.IsBusyForInteraction())) return;

        if (readyFlagWhileHeld)
        {
            bool holding = Input.GetKey(crouchKey);
            GameState.I?.SetFlag(readyFlagId, holding);
            if (bubbleRoot) bubbleRoot.SetActive(holding);
        }
        else
        {
            if (Input.GetKeyDown(crouchKey))
            {
                GameState.I?.SetFlag(readyFlagId, true);
                if (bubbleRoot) bubbleRoot.SetActive(true);
            }
        }

        // しゃがみホールド中フラグ（Enter抑止用）
        if (setCrouchingFlagWhileHeld)
        {
            bool holding = Input.GetKey(crouchKey);
            GameState.I?.SetFlag(crouchingFlagId, holding);
        }
    }

    bool IsPlayer(Collider2D c)
    {
        var go = c.attachedRigidbody ? c.attachedRigidbody.gameObject : c.gameObject;
        return go.CompareTag("Player");
    }
}
