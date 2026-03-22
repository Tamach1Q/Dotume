using UnityEngine;
using UnityEngine.SceneManagement;

/// 現在のシーンを“cashの詰み処理と同様”に安全に再ロードするアクション。
/// - UIクローズ/時間再開などは SceneTransitionKit 側に委譲
/// - 既存のStack遷移の残骸は事前にクリア
/// - 既定ではアイテム/フラグを持ち越さない（必要ならInspectorで上書き）
public class ReloadCurrentSceneAction : ActionBase
{
    [Header("Timing")]
    [Min(0f)] public float delayRealtime = 0f;

    [Header("Carry Policy (optional)")]
    [Tooltip("null の場合や useDefaultRestartPolicy=true の場合は、既定で“何も持ち越さない”ポリシーを適用します。")]
    public CarryPolicy policyOverride = null;
    [Tooltip("既定の再スタートポリシー（アイテム/フラグを全てリセット）を使う")]
    public bool useDefaultRestartPolicy = true;

    public override void Execute()
    {
        var cur = SceneManager.GetActiveScene();
        if (!cur.IsValid())
        {
            Debug.LogError("[ReloadCurrentScene] Active scene is invalid.");
            return;
        }

        // 既存のAdditive Stack履歴を掃除（保険）
        try { SceneStackManager.I?.ClearAndUnloadAllKeptScenes(); } catch { /* ignore */ }

        // 直前状態の後引きを避けるため、代表的な一時フラグ/状態を事前にクリア
        try
        {
            var gs = GameState.I;
            if (gs != null)
            {
                // 風・ベルト・分離・操作系のフラグを明示クリア
                string[] flagsToClear = new[]
                {
                    "wind_lvl1","wind_lvl2","wind_lvl3",
                    "wind_dir_left","wind_dir_right",
                    "on_conveyor",
                    "me_leads","me_split_active",
                    "menu_open",
                    // rich系の表示/分岐を初期化（必要に応じて）
                    "met_rich"
                };
                foreach (var f in flagsToClear) gs.RemoveFlag(f);

                // 代表的な一時アイテムを除去（必要に応じて拡張）
                if (gs.Has("rich")) gs.Remove("rich");

                // リスナに反映されるよう、可視更新を促す
                try { EnableOnFlag.RefreshAll(); } catch { /* ignore */ }
                try { RevealOnItem.TryRefreshAll(); } catch { /* ignore */ }
                try { RevealOnFlag.TryRefreshAll(); } catch { /* ignore */ }
            }
            // プレイヤーの慣性も事前にリセット（初期フレームの流れを抑止）
            try
            {
                var player = GameObject.FindWithTag("Player");
                if (player)
                {
                    var rb = player.GetComponent<Rigidbody2D>();
                    if (rb) rb.velocity = Vector2.zero;
                }
            }
            catch { /* ignore */ }
        }
        catch { /* ignore */ }

        // ポリシー決定：明示があれば優先。無ければ“何も持ち越さない”
        var pol = policyOverride;
        if (pol == null && useDefaultRestartPolicy)
            pol = new CarryPolicy { keepAllItems = false, keepAllFlags = false };

        Debug.Log($"[ReloadCurrentScene] Reload '{cur.name}' (delay={delayRealtime:F2})");
        SceneTransitionKit.Load(cur.name, pol, delayRealtime);
    }
}
