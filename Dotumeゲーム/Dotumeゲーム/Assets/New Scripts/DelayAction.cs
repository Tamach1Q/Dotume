using UnityEngine;
using UnityEngine.Events;

public class DelayAction : ActionBase
{
    [SerializeField] float seconds = 1.0f;   // リアルタイム秒
    [SerializeField] UnityEvent onDone;

    public override void Execute()
    {
        if (!isActiveAndEnabled) return;
        StartCoroutine(Run());
    }

    System.Collections.IEnumerator Run()
    {
        float t = 0f;
        while (t < seconds) { t += Time.unscaledDeltaTime; yield return null; }
        onDone?.Invoke();
    }
}