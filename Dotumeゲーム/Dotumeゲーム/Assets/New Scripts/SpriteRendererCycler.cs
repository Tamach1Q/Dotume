using UnityEngine;

public class SpriteRendererCycler : MonoBehaviour
{
    [Header("3 Renderers (in order)")]
    [SerializeField] SpriteRenderer[] frames = new SpriteRenderer[3]; // 3つ想定

    [Header("Timing")]
    [SerializeField] float frameTime = 0.2f;
    [SerializeField] bool useUnscaledTime = false;
    [SerializeField] bool randomStart = false;

    int idx = 0;
    float t = 0f;
    bool flipX = false;

    void OnEnable()
    {
        if (frames == null || frames.Length == 0) return;
        if (randomStart) idx = Random.Range(0, frames.Length);
        ApplyEnableState();
        ApplyFlip(); // 既定の向きを反映
        t = 0f;
    }

    void Update()
    {
        if (frames == null || frames.Length == 0) return;
        if (frameTime <= 0f) return;

        t += useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
        if (t >= frameTime)
        {
            t -= frameTime;
            idx = (idx + 1) % frames.Length;
            ApplyEnableState();
            // flip は全フレームにかけ続ける（切替時に反映されるように）
            ApplyFlip();
        }
    }

    void ApplyEnableState()
    {
        for (int i = 0; i < frames.Length; i++)
            if (frames[i]) frames[i].enabled = (i == idx);
    }

    void ApplyFlip()
    {
        for (int i = 0; i < frames.Length; i++)
            if (frames[i]) frames[i].flipX = flipX;
    }

    /// <summary>左右反転を外部（AI等）から指示</summary>
    public void SetFlipX(bool value)
    {
        flipX = value;
        ApplyFlip();
    }

    // Inspectorから動作確認したい時用（任意）
    [ContextMenu("Next Frame")]
    void NextFrame()
    {
        idx = (idx + 1) % Mathf.Max(1, frames.Length);
        ApplyEnableState();
        ApplyFlip();
    }
}