using System;
using UnityEngine;

public class ModalGate : MonoBehaviour
{
    public string CurrentOwner { get; private set; } = null;
    public event Action<string, string> OnOwnerChanged; // (oldOwner, newOwner)

    public bool TryAcquire(string owner)
    {
        if (string.IsNullOrEmpty(owner)) owner = "anon";
        if (CurrentOwner != null && CurrentOwner != owner) { UIDebug.Log("ModalGate.TryAcquire", $"blocked owner={owner} current={CurrentOwner}"); return false; }
        var prev = CurrentOwner;
        CurrentOwner = owner;
        if (prev != CurrentOwner) OnOwnerChanged?.Invoke(prev, CurrentOwner);
        UIDebug.Log("ModalGate.TryAcquire", $"acquired owner={owner}");
        return true;
    }

    public void Release(string owner)
    {
        if (string.IsNullOrEmpty(owner)) owner = "anon";
        if (CurrentOwner == owner)
        {
            var prev = CurrentOwner;
            CurrentOwner = null;
            OnOwnerChanged?.Invoke(prev, null);
            UIDebug.Log("ModalGate.Release", $"released owner={owner}");
        }
    }

    // 強制解放: オーナー不一致や不明なロック状態でも必ず開放する
    public void ForceRelease()
    {
        if (CurrentOwner != null)
        {
            var prev = CurrentOwner;
            CurrentOwner = null;
            OnOwnerChanged?.Invoke(prev, null);
            UIDebug.Log("ModalGate.ForceRelease", $"forced from owner={prev}");
        }
    }

    public bool IsOwner(string owner) => CurrentOwner == (string.IsNullOrEmpty(owner) ? "anon" : owner);

}
