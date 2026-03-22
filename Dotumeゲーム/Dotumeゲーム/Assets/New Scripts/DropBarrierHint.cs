using System.Collections;
using UnityEngine;

/// 落下バリアに触れたら「ロープが必要」のヒントを出し、
/// 切る用プロペラ（CutPropeller）を有効化するゲート。
/// ロープ入手後はヒントフラグを消し、バリアを外して自身は停止します。
public class DropBarrierHint : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] GameObject cutPropeller;   // 2つ目（切る用）プロペラ（最初は Inactive 推奨）
    [SerializeField] Collider2D barrierSolid;   // 落下防止の当たり（IsTrigger=OFF 推奨）

    [Header("Ids")]
    [SerializeField] string needFlagId = "need_rope_hint";
    [SerializeField] string ropeItemId = "rope";

    [Header("Options")]
    [SerializeField] bool forceInactiveAtStart = true; // 実行開始時に確実に非表示にする

    bool hintShown;

    void Awake()
    {
        // 実行開始時は必ずOFF（エディタでActiveでも実行時に消す）
        if (forceInactiveAtStart && cutPropeller) cutPropeller.SetActive(false);
    }

    void Start()
    {
        RefreshBarrier();

        // 念のため初期化（ここで needFlag を消すのは OK）
        GameState.I?.Remove(needFlagId);

        Debug.Log($"[DropBarrierHint] Start: ropeHas={(GameState.I?.Has(ropeItemId) ?? false)}, " +
                  $"cutProp={(cutPropeller && cutPropeller.activeSelf)}, " +
                  $"barrier={(barrierSolid ? barrierSolid.enabled : false)}");
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;

        // まだロープ未所持 → ヒントを1度だけ
        if (GameState.I == null || !GameState.I.Has(ropeItemId))
        {
            if (hintShown) return;
            hintShown = true;

            // まずフラグを立てる
            if (!GameState.I.Has(needFlagId))
            {
                GameState.I.Add(needFlagId);
                Debug.Log($"[DropBarrierHint] flag '{needFlagId}' ADDED");
            }

            // ヒントを“確実に”表示（WordCutUI は強制表示版の SetInfoOnly を利用）
            WordCutUI.Instance?.SetInfoOnly("You need a rope to go down.");

            // 物理／トリガの順序競合を避けるため 1フレーム遅らせて有効化
            StartCoroutine(EnableCutPropellerNextFrame());

            // バリアはONのまま
            RefreshBarrier();
            Debug.Log("[DropBarrierHint] hint shown & will enable cut-propeller next frame.");
        }
    }

    IEnumerator EnableCutPropellerNextFrame()
    {
        yield return null; // 次フレーム

        if (cutPropeller && !cutPropeller.activeSelf)
        {
            cutPropeller.SetActive(true);
            Debug.Log("[DropBarrierHint] cut-propeller ENABLED");
        }
    }

    void Update()
    {
        // ロープ入手後：ヒント解除、バリア解除、Cut プロペラは消してもOK
        if (GameState.I != null && GameState.I.Has(ropeItemId))
        {
            if (GameState.I.Has(needFlagId))
            {
                GameState.I.Remove(needFlagId);
                Debug.Log($"[DropBarrierHint] flag '{needFlagId}' REMOVED (got rope)");
            }

            if (cutPropeller && cutPropeller.activeSelf)
                cutPropeller.SetActive(false);   // もう不要なら消す（お好み）

            RefreshBarrier(); // バリアOFF
            enabled = false;  // 以後このゲートは用済み
        }
    }

    void RefreshBarrier()
    {
        bool hasRope = (GameState.I != null && GameState.I.Has(ropeItemId));
        if (barrierSolid) barrierSolid.enabled = !hasRope;
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        if (!Application.isPlaying && cutPropeller) cutPropeller.SetActive(false);
    }
#endif
}