using System.Collections.Generic;
using UnityEngine;

public class GameDirector : MonoBehaviour
{
    public static GameDirector I { get; private set; }
    float prevTimeScale = 1f;
    readonly HashSet<string> owners = new HashSet<string>();

    void Awake()
    {
        if (I != null && I != this) { Destroy(gameObject); return; }
        I = this;
        DontDestroyOnLoad(gameObject);
    }

    public bool IsPaused => Time.timeScale == 0f;

    public void Pause(string owner)
    {
        if (owners.Count == 0) prevTimeScale = Time.timeScale;
        owners.Add(string.IsNullOrEmpty(owner) ? "anon" : owner);
        Time.timeScale = 0f;
    }

    public void Resume(string owner)
    {
        owners.Remove(string.IsNullOrEmpty(owner) ? "anon" : owner);
        if (owners.Count == 0) Time.timeScale = Mathf.Approximately(prevTimeScale, 0f) ? 1f : prevTimeScale;
    }

    public void ForceResume() { owners.Clear(); Time.timeScale = 1f; }
} 