// File: ClearSceneController.cs
using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

public class ClearSceneController : MonoBehaviour
{
    [Header("Scene Names")]
    [SerializeField] string titleSceneName = "Title";

    [Header("Credits (Viewport & Content)")]
    [SerializeField] RectTransform creditsRoot;    // マスクされたビューポート
    [SerializeField] RectTransform creditsContent; // 流す中身（縦長）

    [Header("UI")]
    [SerializeField] TMP_Text pressAny;            // 「Press Enter」(最初は非表示推奨)

    [Header("Flow")]
    [SerializeField] float minShowSeconds = 3f;    // 最低表示秒数
    [SerializeField] float scrollSpeed = 80f;      // px/sec（Time.unscaledDeltaTime）
    [SerializeField] bool showPressAnyWhenReady = true;

    float _startUnscaled;
    bool _readyToExit;
    Vector2 _contentStartPos;

    void Awake()
    {
        if (creditsContent) _contentStartPos = creditsContent.anchoredPosition;
    }

    void OnEnable()
    {
        if (creditsContent) creditsContent.anchoredPosition = _contentStartPos;
    }

    void Start()
    {
        _startUnscaled = Time.unscaledTime;
        if (pressAny) pressAny.gameObject.SetActive(false);

        // ▼GameTimer を使っている場合はコメントを外してください（無ければこのままでOK）
        // GameTimer.Stop();
    }

    void Update()
    {
        // スクロール
        if (creditsRoot && creditsContent)
        {
            var pos = creditsContent.anchoredPosition;
            pos.y += scrollSpeed * Time.unscaledDeltaTime;
            creditsContent.anchoredPosition = pos;
        }

        // 終了条件（中身が領域外まで流れ切った ＋ 最低時間）
        bool enoughTime = (Time.unscaledTime - _startUnscaled) >= minShowSeconds;
        bool scrolledOut = false;
        if (creditsRoot && creditsContent)
        {
            float contentTop = creditsContent.anchoredPosition.y + creditsContent.rect.height;
            scrolledOut = contentTop >= creditsRoot.rect.height;
        }
        else scrolledOut = true; // 参照未セットなら即終了扱い

        if (!_readyToExit && enoughTime && scrolledOut)
        {
            _readyToExit = true;
            if (showPressAnyWhenReady && pressAny) pressAny.gameObject.SetActive(true);
        }

        // Enter / Space / パッドA で終了
        if (_readyToExit && (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)
                          || Input.GetKeyDown(KeyCode.Space)  || Input.GetKeyDown(KeyCode.JoystickButton0)))
        {
            ExitToTitle();
        }
    }

    public void ExitToTitle()
    {
        // ▼GameTimer を使っている場合はコメントを外してください（無ければこのままでOK）
        GameTimer.ResetTimer(true);

        SceneManager.LoadScene(titleSceneName, LoadSceneMode.Single);
    }
}