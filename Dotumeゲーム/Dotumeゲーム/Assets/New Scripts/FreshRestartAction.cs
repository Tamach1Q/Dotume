using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// タイトルへ“完全リスタート”するためのアクション。
/// - すべてのUI/ポーズ解除
/// - SceneStackManager の保持シーンをクリア
/// - DontDestroyOnLoad に居るオブジェクトを原則全破棄（任意のホワイトリスト除外可）
/// - 最後に Single ロードでタイトルへ
public class FreshRestartAction : ActionBase
{
    [Header("Destination")]
    [SerializeField] string titleSceneName = "Title";

    [Header("Options")]
    [Tooltip("DDOL(DontDestroyOnLoad)にいる全オブジェクトを破棄する（推奨）")]
    [SerializeField] bool wipeAllDDOLObjects = true;
    [Tooltip("wipeAllDDOLObjects=true 時、ここに列挙した名前は破棄しない（任意）")]
    [SerializeField] string[] ddolKeepNames;

    [Tooltip("GameState を初期化（DDOLに居る場合は破棄）する")]
    [SerializeField] bool resetGameState = true;

    public override void Execute()
    {
        if (!isActiveAndEnabled) return;
        StartCoroutine(CoFreshRestart());
    }

    IEnumerator CoFreshRestart()
    {
        // 時間系/入力系の安全化
        Time.timeScale = 1f;
        try { UIRouter.I?.ForceCloseAll(); } catch { /* ignore */ }
        try
        {
            var w = WordCutUI.Instance;
            if (w) { w.Close(); w.gameObject.SetActive(false); }
        }
        catch { /* ignore */ }

        // SceneStack の掃除（過去のAdditiveスタックを確実に破棄）
        try { SceneStackManager.I?.ClearAndUnloadAllKeptScenes(); } catch { /* ignore */ }

        // GameState を破棄（必要なら）
        if (resetGameState)
        {
            try
            {
                if (GameState.I != null)
                {
                    var go = GameState.I.gameObject;
                    if (go) Object.Destroy(go);
                }
            }
            catch { /* ignore */ }
        }

        // DDOL 破棄（ホワイトリスト除外）
        if (wipeAllDDOLObjects)
        {
            var keep = new HashSet<string>(ddolKeepNames ?? System.Array.Empty<string>());
            foreach (var g in Resources.FindObjectsOfTypeAll<GameObject>())
            {
                bool isInDDOL = false;
                try { isInDDOL = g && g.scene.name == "DontDestroyOnLoad"; }
                catch { isInDDOL = false; }
                if (!isInDDOL) continue;
                if (keep.Contains(g.name)) continue;
                // 自分自身やエディタ上の隠しオブジェクトも巻き込まない
                if (!g || !g.hideFlags.Equals(HideFlags.None)) continue;
                Object.Destroy(g);
            }
            // 1フレーム待って破棄を反映
            yield return null;
        }

        // 最後にタイトルをSingleロード（追加ロードではなく完全置き換え）
        SceneManager.LoadScene(titleSceneName, LoadSceneMode.Single);
    }
}

