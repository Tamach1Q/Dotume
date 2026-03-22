using UnityEngine;
using UnityEngine.InputSystem; // 新Input
using UnityEngine.InputSystem.Controls;

[RequireComponent(typeof(AudioSource))]
public class PlaySfxOnKey : MonoBehaviour
{
    [Header("Sound")]
    public AudioClip clip;
    [Range(0f, 1f)] public float volume = 1f;
    [Tooltip("連打防止のクールダウン(秒)")]
    public float cooldown = 0.1f;

    [Header("Key")]
    [Tooltip("押したら鳴らすキー（デフォルト:I）")]
    public Key key = Key.I;   // ← Inspector で変更可

    [Header("Options")]
    [Tooltip("有効化された瞬間にも鳴らす")]
    public bool playOnEnableOnce = false;

    AudioSource _src;
    InputAction _action;
    float _lastPlayTime = -999f;

    void Awake()
    {
        _src = GetComponent<AudioSource>();
        if (!_src) _src = gameObject.AddComponent<AudioSource>();
        _src.playOnAwake = false;
        _src.loop = false;
    }

    void OnEnable()
    {
        // 入力アクションを動的に作成（<Keyboard>/i など）
        var controlPath = $"<Keyboard>/{key.ToString().ToLower()}";
        _action = new InputAction("SfxKey", InputActionType.Button, controlPath);
        _action.performed += OnPerformed;
        _action.Enable();

        if (playOnEnableOnce) TryPlay();
    }

    void OnDisable()
    {
        if (_action != null)
        {
            _action.performed -= OnPerformed;
            _action.Disable();
            _action.Dispose();
            _action = null;
        }
    }

    void OnPerformed(InputAction.CallbackContext _)
    {
        TryPlay();
    }

    void TryPlay()
    {
        if (!clip) return;
        if (Time.unscaledTime - _lastPlayTime < cooldown) return;

        _src.PlayOneShot(clip, volume);
        _lastPlayTime = Time.unscaledTime;
    }
}
