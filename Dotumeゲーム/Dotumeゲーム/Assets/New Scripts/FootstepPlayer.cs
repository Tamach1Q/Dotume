using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;
using System.Collections.Generic;

[RequireComponent(typeof(AudioSource))]
[DisallowMultipleComponent]
public class FootstepLooper : MonoBehaviour
{
    [System.Serializable]
    public class SceneFootstep { public string sceneName; public AudioClip[] clips; }

    public enum OnOverlap
    {
        KeepExisting,     // 再生中を優先（新規はスキップ）
        RestartWithNew    // 再生中を止めて新規を鳴らす（推奨）
    }

    // ====== 公開設定 ======
    [Header("シーンごとの足音セット")]
    public SceneFootstep[] sceneFootsteps;

    [Header("再生設定")]
    [Range(0f,1f)] public float volume = 1f;
    public float pitchVariance = 0.08f;

    [Header("テンポ（速度で補間）")]
    public float maxInterval = 0.45f;
    public float minInterval = 0.18f;
    public float maxSpeedForSteps = 6f;

    [Header("しきい値/スムージング（止まったら止む）")]
    public float startSpeed = 0.12f;
    public float stopSpeed  = 0.08f;
    public float speedSmooth = 12f;

    [Header("接地判定")]
    public bool onlyWhenGrounded = true;
    public LayerMask groundMask;
    public Vector2 groundBoxSize = new Vector2(0.5f, 0.08f);
    public float groundBoxDownOffset = 0.05f;

    [Header("インベントリ/装備などで足音停止")]
    public static int s_GlobalPauseCount = 0; // ネスト対応
    public static void GlobalPause(bool pause)
    {
        if (pause) s_GlobalPauseCount++;
        else       s_GlobalPauseCount = Mathf.Max(0, s_GlobalPauseCount - 1);
    }

    [Header("重なり時の挙動")]
    [Tooltip("足音再生中に新しい足音が来たときの処理")]
    public OnOverlap onOverlap = OnOverlap.RestartWithNew;

    [Header("デバッグ")]
    public bool debugLogs = false;

    // ====== 内部状態 ======
    AudioSource _src;
    Rigidbody2D _rb;
    Collider2D _col;

    float _timer, _currentInterval, _smoothedSpeed;
    bool _isStepping;
    Vector2 _lastPos;

    SceneFootstep _currentSet;
    Dictionary<string, SceneFootstep> _map;

    // ====== ライフサイクル ======
    void Awake()
    {
        _src = GetComponent<AudioSource>();
        _src.playOnAwake = false; _src.loop = false;

        _rb = GetComponent<Rigidbody2D>();
        _col = GetComponent<Collider2D>();
        _lastPos = transform.position;

        BuildMap();
        RefreshForScene(SceneManager.GetActiveScene().name);
        _currentInterval = maxInterval;
    }

    void OnEnable()
    {
        SceneManager.activeSceneChanged += OnActiveSceneChanged;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }
    void OnDisable()
    {
        SceneManager.activeSceneChanged -= OnActiveSceneChanged;
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    // ====== シーン切替（取りこぼし防止） ======
    void OnActiveSceneChanged(Scene prev, Scene next)
    {
        if (debugLogs) Debug.Log($"[FootstepLooper] activeSceneChanged -> {next.name}");
        RefreshForScene(next.name);
        StartCoroutine(RefreshNextFrame(next.name));
    }
    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (debugLogs) Debug.Log($"[FootstepLooper] sceneLoaded -> {scene.name} ({mode})");
        var active = SceneManager.GetActiveScene().name;
        RefreshForScene(active);
        StartCoroutine(RefreshNextFrame(active));
    }
    IEnumerator RefreshNextFrame(string sceneName) { yield return null; RefreshForScene(sceneName); }

    // ====== メイン更新 ======
    void Update()
    {
        // インベントリ/装備で一時停止中は即停止
        if (s_GlobalPauseCount > 0)
        {
            if (_isStepping) { _isStepping = false; _timer = 0f; _src.Stop(); }
            return;
        }

        if (_currentSet == null || _currentSet.clips == null || _currentSet.clips.Length == 0) return;

        // 水平速度
        float rawSpeed = 0f;
        if (_rb != null) rawSpeed = Mathf.Abs(_rb.velocity.x);
        else
        {
            var p = (Vector2)transform.position;
            rawSpeed = Mathf.Abs((p.x - _lastPos.x) / Mathf.Max(Time.deltaTime, 0.0001f));
            _lastPos = p;
        }

        // スムージング
        float k = 1f - Mathf.Exp(-Mathf.Max(0f, speedSmooth) * Time.deltaTime);
        _smoothedSpeed = Mathf.Lerp(_smoothedSpeed, rawSpeed, k);

        bool groundedOK = !onlyWhenGrounded || IsGrounded();

        // ヒステリシスで歩き状態
        if (_isStepping)
        {
            if (!groundedOK || _smoothedSpeed <= stopSpeed)
            {
                _isStepping = false; _timer = 0f; _src.Stop();
                if (debugLogs) Debug.Log("[FootstepLooper] stop stepping");
            }
        }
        else
        {
            if (groundedOK && _smoothedSpeed >= startSpeed)
            {
                _isStepping = true; _timer = 0f;
                if (debugLogs) Debug.Log("[FootstepLooper] start stepping");
            }
        }

        if (!_isStepping) return;

        // テンポ補間
        float t = Mathf.Clamp01((_smoothedSpeed - stopSpeed) / Mathf.Max(0.001f, (maxSpeedForSteps - stopSpeed)));
        _currentInterval = Mathf.Lerp(maxInterval, minInterval, t);

        _timer += Time.deltaTime;
        if (_timer >= _currentInterval) { _timer = 0f; PlayOneStep(); }
    }

    void PlayOneStep()
    {
        var clips = _currentSet.clips;
        if (clips == null || clips.Length == 0) return;
        var clip = clips[Random.Range(0, clips.Length)];
        if (!clip) return;

        // ★ 重なり制御：いま鳴っていたらどうするか
        if (_src.isPlaying)
        {
            if (onOverlap == OnOverlap.KeepExisting)
            {
                if (debugLogs) Debug.Log("[FootstepLooper] skipped (already playing)");
                return; // いまの音を生かす
            }
            else // RestartWithNew
            {
                _src.Stop(); // いまの足音を止めて差し替え
            }
        }

        _src.pitch = 1f + Random.Range(-pitchVariance, pitchVariance);

        // PlayOneShot でも OK（Stop 済みなら重なりません）
        _src.PlayOneShot(clip, volume);

        // あるいはクリップ再生にする場合は以下でもOK（どちらでも動作）
        // _src.clip = clip;
        // _src.volume = volume;
        // _src.Play();

        if (debugLogs) Debug.Log($"[FootstepLooper] step '{clip.name}' set='{_currentSet.sceneName}'");
    }

    // ====== 補助 ======
    static string Normalize(string s) => (s ?? string.Empty).Trim().ToLowerInvariant();

    void BuildMap()
    {
        _map = new Dictionary<string, SceneFootstep>();
        if (sceneFootsteps == null) return;
        foreach (var s in sceneFootsteps)
        {
            if (s == null) continue;
            var key = Normalize(s.sceneName);
            if (string.IsNullOrEmpty(key)) continue;
            _map[key] = s;
        }
    }

    void RefreshForScene(string sceneName)
    {
        _map.TryGetValue(Normalize(sceneName), out _currentSet);

        if (_currentSet == null && sceneFootsteps != null && sceneFootsteps.Length > 0)
        {
            _currentSet = sceneFootsteps[0]; // フォールバック
            if (debugLogs) Debug.LogWarning($"[FootstepLooper] set not found for '{sceneName}' -> use 0 ('{_currentSet.sceneName}')");
        }
        if (debugLogs) Debug.Log($"[FootstepLooper] current set = '{_currentSet?.sceneName ?? "NULL"}' (scene='{sceneName}')");
    }

    bool IsGrounded()
    {
        if (groundMask.value == 0) return true;
        Vector2 center, size;
        if (_col)
        {
            var b = _col.bounds;
            center = new Vector2(b.center.x, b.min.y - groundBoxDownOffset);
            size   = new Vector2(Mathf.Max(0.05f, b.size.x * 0.6f), groundBoxSize.y);
        }
        else
        {
            center = (Vector2)transform.position + Vector2.down * groundBoxDownOffset;
            size   = groundBoxSize;
        }
        return Physics2D.OverlapBox(center, size, 0f, groundMask) != null;
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        if (!onlyWhenGrounded) return;
        Gizmos.color = new Color(0.2f, 1f, 0.8f, 0.6f);
        var c = GetComponent<Collider2D>();
        Vector2 center, size;
        if (c) { var b = c.bounds; center = new Vector2(b.center.x, b.min.y - groundBoxDownOffset); size = new Vector2(Mathf.Max(0.05f, b.size.x * 0.6f), groundBoxSize.y); }
        else { center = (Vector2)transform.position + Vector2.down * groundBoxDownOffset; size = groundBoxSize; }
        Gizmos.DrawWireCube(center, size);
    }
#endif
}
