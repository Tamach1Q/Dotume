using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

public class Door : MonoBehaviour
{
    [SerializeField] string requiredItemId = "key";     // 必須アイテム
    [SerializeField] string nextSceneName = "Stage_Field1"; // 遷移先シーン名
    [SerializeField] float messageDuration = 1.0f;      // メッセージ表示時間（秒）

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;

        // 鍵チェック
        bool ok = (GameState.I != null && GameState.I.Has(requiredItemId));
        if (ok)
        {
            StartCoroutine(OpenDoorAndChangeScene());
        }
        else
        {
            // 鍵がないとき
            ShowPopup("You need a key.");
        }
    }

    IEnumerator OpenDoorAndChangeScene()
    {
        ShowPopup("Door opened!");
        yield return new WaitForSecondsRealtime(messageDuration);
        SceneManager.LoadScene(nextSceneName);
    }

    void ShowPopup(string message)
    {
        if (WordCutUI.Instance != null)
        {
            WordCutUI.Instance.gameObject.SetActive(true);
            WordCutUI.Instance.SetInfoOnly(message); // ←後でWordCutUIに追加するメソッド
        }
        else
        {
            Debug.Log(message);
        }
    }
}