using UnityEngine;
using UnityEngine.Events;

/// RectTransform を上に流すクレジット。停止位置をいくつかの方式で指定可能。
public class CreditsAutoScroll : MonoBehaviour
{
    public enum StopMode
    {
        FullOut,     // 既存: コンテンツ全体が上に抜け切ったら止まる
        TopMargin,   // ビューポート上端の手前で margin 分だけ余らせて止める
        ContentY,    // content.anchoredPosition.y が指定値に達したら止める（手動指定）
    }

    [Header("Refs")]
    [SerializeField] RectTransform viewport; // 見える枠（Mask ON）
    [SerializeField] RectTransform content;  // 流す中身（縦長）

    [Header("Scroll")]
    [SerializeField] float speed = 60f;      // px/sec（unscaled）
    
    [Header("Stop Condition")]
    [SerializeField] StopMode stopMode = StopMode.FullOut;

    [Tooltip("TopMargin: ビューポート上端の手前に残す余白(px)。例えば 100 なら、少し残して止まる。")]
    [SerializeField] float topMargin = 0f;

    [Tooltip("ContentY: content.anchoredPosition.y がこの値以上で止める。")]
    [SerializeField] float stopAtContentY = 800f;

    [Header("Events")]
    public UnityEvent onFinished;            // 止まった瞬間に呼ばれる

    public bool Finished { get; private set; }

    Vector2 _startPos;

    void Awake()
    {
        if (!content) content = transform as RectTransform;
        _startPos = content ? content.anchoredPosition : Vector2.zero;
    }

    void OnEnable()
    {
        Finished = false;
        if (content) content.anchoredPosition = _startPos;
    }

    void Update()
    {
        if (Finished || !viewport || !content) return;

        // 移動
        var pos = content.anchoredPosition;
        pos.y += speed * Time.unscaledDeltaTime;
        content.anchoredPosition = pos;

        // 停止判定
        if (ShouldStop())
        {
            Finished = true;
            onFinished?.Invoke();
        }
    }

    bool ShouldStop()
    {
        switch (stopMode)
        {
            case StopMode.FullOut:
            {
                float contentTop = content.anchoredPosition.y + content.rect.height;
                float viewHeight = viewport.rect.height;
                return contentTop >= viewHeight;
            }
            case StopMode.TopMargin:
            {
                float contentTop = content.anchoredPosition.y + content.rect.height;
                float viewHeight = viewport.rect.height;
                // 上端までの必要距離から margin を引いて少し手前で止める
                return contentTop >= (viewHeight - Mathf.Max(0f, topMargin));
            }
            case StopMode.ContentY:
                return content.anchoredPosition.y >= stopAtContentY;

            default:
                return false;
        }
    }

    // エディタで停止位置を見つけやすいようヘルパー（任意）
    [ContextMenu("Copy Current Y to stopAtContentY")]
    void CopyCurrentY()
    {
        if (content) stopAtContentY = content.anchoredPosition.y;
    }
}