using UnityEngine;
using System.Collections;
//using UnityEngine.SceneManagement; // 残っていてもOK

public class LoadSceneAction : ActionBase
{
    [SerializeField] string sceneName = "Stage_Field1";
    [SerializeField] float delayRealtime = 0f; // ポップアップと併用時は 0.8〜1s 推奨

    // ★追加：持ち越し設定（未設定=全部持ち越し）
    [SerializeField] CarryPolicy policy = new CarryPolicy();

    public override void Execute()
    {
        if (delayRealtime <= 0f) StartCoroutine(LoadAdditiveNow(sceneName));
        else StartCoroutine(LoadLater());
    }

    IEnumerator LoadLater()
    {
        float t = 0f; while (t < delayRealtime) { t += Time.unscaledDeltaTime; yield return null; }
        yield return LoadAdditiveNow(sceneName);
    }

    IEnumerator LoadAdditiveNow(string target)
    {
        var prev = UnityEngine.SceneManagement.SceneManager.GetActiveScene();

        // 非同期 + Additive
        var op = UnityEngine.SceneManagement.SceneManager.LoadSceneAsync(target, UnityEngine.SceneManagement.LoadSceneMode.Additive);
        yield return op;

        // 新シーンをアクティブに
        var newScene = UnityEngine.SceneManagement.SceneManager.GetSceneByName(target);
        if (newScene.IsValid()) UnityEngine.SceneManagement.SceneManager.SetActiveScene(newScene);

        // 旧シーンをアンロード
        if (prev.IsValid())
            yield return UnityEngine.SceneManagement.SceneManager.UnloadSceneAsync(prev);
    }
}