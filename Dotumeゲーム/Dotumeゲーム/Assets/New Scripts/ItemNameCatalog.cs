using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "ItemNameCatalog", menuName = "Game/Item Name Catalog")]
public class ItemNameCatalog : ScriptableObject
{
    [System.Serializable]
    public struct Entry { public string id; public string displayName; }

    [SerializeField] List<Entry> entries = new();

    Dictionary<string,string> map;
    void OnEnable()
    {
        map = new Dictionary<string, string>();
        foreach (var e in entries)
            if (!string.IsNullOrEmpty(e.id))
                map[e.id] = string.IsNullOrEmpty(e.displayName) ? e.id : e.displayName;
    }

    public string GetDisplayName(string id)
    {
        if (string.IsNullOrEmpty(id)) return "";
        return (map != null && map.TryGetValue(id, out var name)) ? name : id;
    }
}
