#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class MissingScriptScanner
{
    // ===== メニュー =====
    [MenuItem("Tools/Missing Scripts/Scan Open Scenes")]
    public static void ScanOpenScenes()
    {
        int total = 0, missing = 0;
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            var scene = SceneManager.GetSceneAt(i);
            if (!scene.isLoaded) continue;

            foreach (var go in scene.GetRootGameObjects())
            {
                Walk(go, GetScenePath(go), ref total, ref missing);
            }
        }
        Summary(total, missing, "Open Scenes");
    }

    [MenuItem("Tools/Missing Scripts/Scan All Prefabs")]
    public static void ScanAllPrefabs()
    {
        int total = 0, missing = 0;
        string[] guids = AssetDatabase.FindAssets("t:Prefab");
        foreach (var guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (!go) continue;
            Walk(go, $"[Prefab]{path}", ref total, ref missing);
        }
        Summary(total, missing, "All Prefabs");
    }

    [MenuItem("Tools/Missing Scripts/Remove Missing (Selection)")]
    public static void RemoveMissingOnSelection()
    {
        int count = 0;
        foreach (var obj in Selection.gameObjects)
        {
            count += RemoveMissingRecursive(obj);
        }
        Debug.Log($"[MissingScriptScanner] Removed {count} missing script component(s) from selection.");
    }

    // ===== 内部処理 =====
    static void Walk(GameObject go, string path, ref int total, ref int missing)
    {
        total++;
        var comps = go.GetComponents<Component>();
        for (int i = 0; i < comps.Length; i++)
        {
            if (comps[i] == null)
            {
                missing++;
                Debug.LogWarning($"[Missing] {path}", go);
            }
        }
        // 子も辿る
        foreach (Transform child in go.transform)
            Walk(child.gameObject, path + "/" + child.name, ref total, ref missing);
    }

    static int RemoveMissingRecursive(GameObject go)
    {
        int before = CountMissing(go);
        GameObjectUtility.RemoveMonoBehavioursWithMissingScript(go);
        int removed = before;

        foreach (Transform t in go.transform)
            removed += RemoveMissingRecursive(t.gameObject);

        return removed;
    }

    static int CountMissing(GameObject go)
    {
        int c = 0;
        var comps = go.GetComponents<Component>();
        foreach (var comp in comps) if (comp == null) c++;
        return c;
    }

    static string GetScenePath(GameObject go)
    {
        // ルートからの階層パスを作る
        var stack = new Stack<string>();
        var t = go.transform;
        while (t != null)
        {
            stack.Push(t.name);
            t = t.parent;
        }
        return string.Join("/", stack.ToArray());
    }

    static void Summary(int total, int missing, string scope)
    {
         Debug.Log($"[MissingScriptScanner] Scan '{scope}': Checked {total} GameObjects, Found {missing} missing script slot(s).");
    }
}
#endif