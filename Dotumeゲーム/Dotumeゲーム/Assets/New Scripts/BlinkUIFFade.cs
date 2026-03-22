using UnityEngine;

[RequireComponent(typeof(CanvasGroup))]
public class BlinkUIFade : MonoBehaviour
{
    [SerializeField] float speed = 10f;             // –¾–Å‚Ì‘¬‚³
    [SerializeField, Range(0f, 1f)] float minA = 0.2f;
    [SerializeField, Range(0f, 1f)] float maxA = 1f;

    CanvasGroup cg;

    void Awake()
    {
        cg = GetComponent<CanvasGroup>();
        if (!cg) cg = gameObject.AddComponent<CanvasGroup>();
    }

    void Update()
    {
        // TimeScale‚Ì‰e‹¿‚ðŽó‚¯‚È‚¢“_–Å
        float t = Mathf.PingPong(Time.unscaledTime * speed, 1f);
        cg.alpha = Mathf.Lerp(minA, maxA, t);
    }
}
