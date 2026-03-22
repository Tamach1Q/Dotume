using UnityEngine;

[CreateAssetMenu(menuName = "TOM/Dialogue Asset", fileName = "TomDialogueAsset")]
public class TomDialogueAsset : ScriptableObject
{
    [Header("Identity")]
    public string id = "tom_intro";          // 一意ID（再生済みフラグ等に使用）
    public string displayName = "TOM";        // 吹き出しの名前表示

    [Header("Content")]
    public TomLine[] lines;

    [Header("Policy / Rewards")]
    public bool playOnce = true;               // 一度きりの再生にする
    public string[] setFlagsOnComplete;        // 再生完了で立てるフラグ
    public string[] addItemsOnComplete;        // 再生完了で付与するアイテムID
}

[System.Serializable]
public class TomLine
{
    [TextArea(2, 4)] public string text;
    public Sprite portrait;                    // 立ち絵（任意）
    public AudioClip voice;                    // ボイス（任意）
}

