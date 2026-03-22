using UnityEngine;

[ExecuteAlways]
public class LevelBoundsFitter : MonoBehaviour
{
    [Header("Source")]
    [SerializeField] Renderer sourceRenderer;   // 例: 背景やタイルマップなど“縦に十分高い”Renderer
    [SerializeField] BoxCollider2D box;         // LevelBounds の BoxCollider2D

    [Header("Mode")]
    [SerializeField] bool fitInEditMode = true; // エディタ編集時は自動で合わせる
    [SerializeField] bool fitInPlayMode = false;// ←再生中はデフォルトで触らない
    [SerializeField] Vector2 extraPadding = Vector2.zero; // 上下左右に足したい量

    void Reset()
    { 
        box = GetComponent<BoxCollider2D>();
        if (!box) box = gameObject.AddComponent<BoxCollider2D>();
        box.isTrigger = true;
    }

    void OnEnable() { TryFit(); }
    void OnValidate() { TryFit(); }
    void Start() { TryFit(); }

    void TryFit()
    {
        if (!Application.isPlaying && fitInEditMode) Fit();
        else if (Application.isPlaying && fitInPlayMode) Fit();
    }

    [ContextMenu("Fit Now")]
    public void Fit()
    {
        if (!sourceRenderer || !box) return;
        var b = sourceRenderer.bounds; // ワールド境界
        transform.position = new Vector3(b.center.x, b.center.y, 0f);
        box.size = new Vector2(b.size.x + extraPadding.x, b.size.y + extraPadding.y);
        box.offset = Vector2.zero;
    }
}