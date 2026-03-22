using System;
using UnityEngine;

/// シーン遷移で何を持ち越すかを宣言するデータクラス（デフォルト: 全部持ち越し）
[Serializable]
public class CarryPolicy
{
    [Header("Items")]
    public bool keepAllItems = true;
    [Tooltip("keepAllItems=false の時だけ有効。列挙した itemId だけ持ち越す")]
    public string[] keepItems;
    [Tooltip("ここに列挙した itemId は必ず捨てる（黒リスト）")]
    public string[] dropItems;

    [Header("Flags")]
    public bool keepAllFlags = true;
    [Tooltip("keepAllFlags=false の時だけ有効。列挙した flagId だけ持ち越す")]
    public string[] keepFlags;
    [Tooltip("ここに列挙した flagId は必ず捨てる（黒リスト）")]
    public string[] dropFlags;
}