using System.Collections.Generic;
using UnityEngine;
using System;

public class RevealOnFlag : MonoBehaviour
{
    [SerializeField] string flagId;

    [Header("Flag を持っている時に Active にする")]
    [SerializeField] GameObject[] activeWhenHas;

    [Header("Flag を持っていない時に Active にする")]
    [SerializeField] GameObject[] activeWhenNot;

    // 非アクティブになっても再有効化できるよう、全インスタンスを保持
    static readonly HashSet<RevealOnFlag> all = new HashSet<RevealOnFlag>();

    GameState _subscribed; // イベント購読先

    void Awake()
    {
        all.Add(this);
        EnsureSubscribed();
    }

    void OnEnable()
    {
        EnsureSubscribed();
        Refresh();
    }

    void OnDestroy()
    {
        all.Remove(this);
        if (_subscribed != null) _subscribed.OnFlagChanged -= OnFlagChanged;
        _subscribed = null;
    }

    void EnsureSubscribed()
    {
        if (GameState.I == null) return;
        if (_subscribed == GameState.I) return;
        if (_subscribed != null) _subscribed.OnFlagChanged -= OnFlagChanged;
        _subscribed = GameState.I;
        _subscribed.OnFlagChanged += OnFlagChanged;
    }

    void OnFlagChanged(string id, bool value)
    {
        if (id == flagId) Refresh();
    }

    public void Refresh()
    {
        bool has = (GameState.I != null) && GameState.I.HasFlag(flagId);
        if (activeWhenHas != null)
            foreach (var go in activeWhenHas) if (go) go.SetActive(has);
        if (activeWhenNot != null)
            foreach (var go in activeWhenNot) if (go) go.SetActive(!has);
    }

    // 任意: 手動一括更新（非アクティブ含む）
    public static void TryRefreshAll()
    {
        foreach (var x in all) if (x) x.Refresh();
    }
}
