// AutoStartSceneLoader.cs (新しく作成するスクリプト)
using UnityEngine;

public class AutoStartSceneLoader : MonoBehaviour
{
    // アタッチされた LoadSceneWithPolicyAction の Execute メソッドを保持
    private LoadSceneWithPolicyAction loader;

    void Start()
    {
        // アタッチされている LoadSceneWithPolicyAction を取得
        loader = GetComponent<LoadSceneWithPolicyAction>();

        // 【重要】ゲーム開始時に1フレーム待ってからロードを実行
        // UIやシングルトンのAwake/Startが完了するのを待つ
        StartCoroutine(ExecuteLater());
    }

    System.Collections.IEnumerator ExecuteLater()
    {
        // 1フレーム待機
        yield return null;

        // LoadSceneWithPolicyAction の安全な Execute() を呼び出す
        loader?.Execute();

        // このローダーは役目を終えたので破棄しても良い
        // Destroy(gameObject); 
    }
}