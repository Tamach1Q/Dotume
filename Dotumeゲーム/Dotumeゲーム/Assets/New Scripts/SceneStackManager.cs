using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

/// Keep previous scenes alive (inactive) and return to them without resetting state.
public class SceneStackManager : MonoBehaviour
{
    public static SceneStackManager I { get; private set; }

    class StackEntry
    {
        public string sceneName;
        public string reentrySpawnTag; // spawn tag to use when coming back to this scene (e.g., "PlayerReturn")
        public CarryPolicy reentryPolicy; // policy to apply when returning to this previous scene
    }

    readonly Stack<StackEntry> stack = new();
    bool isTransitioning = false;

    void Awake()
    {
        if (I != null && I != this) { Destroy(gameObject); return; }
        I = this;
        DontDestroyOnLoad(gameObject);
    }

    // ----------------- Public API -----------------

    /// Load next scene additively and keep current scene in a paused/inactive state.
    /// Optionally applies carry policies: enterPolicy when entering next, reentryPolicy when returning back.
    public void PushAndLoad(string nextSceneName, string reentrySpawnTag = "PlayerReturn", float delayRealtime = 0f,
                            CarryPolicy enterPolicy = null, CarryPolicy reentryPolicy = null)
    {
        if (isTransitioning) return;
        // If the target scene is exactly the one on top of the stack, this is effectively a "return"
        if (stack.Count > 0 && stack.Peek().sceneName == nextSceneName)
        {
            Debug.Log($"[SceneStack] Detected push to top scene '{nextSceneName}'. Interpreting as ReturnToPrevious.");
            StartCoroutine(CoReturnToPrevious(delayRealtime));
        }
        else
        {
            Debug.Log($"[SceneStack] Push current and load next. next='{nextSceneName}', reentryTag='{reentrySpawnTag}'");
            StartCoroutine(CoPushAndLoad(nextSceneName, reentrySpawnTag, delayRealtime, enterPolicy, reentryPolicy));
        }
    }

    /// Clear all kept scenes in the stack so future non-stack transitions
    /// won't accidentally carry over previously stacked scenes.
    public void ClearAndUnloadAllKeptScenes()
    {
        // fire-and-forget: do the work in a coroutine so callers (e.g. actions)
        // don't need to yield
        StartCoroutine(CoClearAndUnloadAllKeptScenes());
    }

    IEnumerator CoClearAndUnloadAllKeptScenes()
    {
        if (stack.Count == 0) yield break;

        // Copy then clear the stack first to avoid re-entrancy during unload
        var entries = stack.ToArray();
        stack.Clear();

        foreach (var e in entries)
        {
            // try/catch では AsyncOperation の取得までに留め、
            // yield はブロック外で行う（CS1626回避）
            AsyncOperation op = null;
            Scene sc = default;
            bool shouldUnload = false;
            try
            {
                sc = UnityEngine.SceneManagement.SceneManager.GetSceneByName(e.sceneName);
                shouldUnload = sc.IsValid() && sc.isLoaded;
            }
            catch { /* ignore */ }

            if (shouldUnload)
            {
                op = UnityEngine.SceneManagement.SceneManager.UnloadSceneAsync(sc);
            }

            if (op != null) yield return op;
        }

        // After clearing, it's safe to proceed with normal loads
        yield break;
    }

    /// Return to the previous kept scene, unloading the current scene.
    public void ReturnToPrevious(float delayRealtime = 0f)
    {
        if (isTransitioning) return;
        if (stack.Count == 0)
        {
            Debug.LogWarning("[SceneStack] No previous scene to return to.");
            return;
        }
        StartCoroutine(CoReturnToPrevious(delayRealtime));
    }

    // ----------------- Coroutines -----------------

    IEnumerator CoPushAndLoad(string nextSceneName, string reentrySpawnTag, float delayRealtime, CarryPolicy enterPolicy, CarryPolicy reentryPolicy)
    {
        isTransitioning = true;

        // pre-close UIs and resume time
        SafeCloseAllUI();
        Time.timeScale = 1f;
        if (delayRealtime > 0f)
        {
            float t = 0f; while (t < delayRealtime) { t += Time.unscaledDeltaTime; yield return null; }
        }

        var prev = SceneManager.GetActiveScene();
        if (!prev.IsValid())
        {
            Debug.LogError("[SceneStack] Active scene invalid.");
            isTransitioning = false;
            yield break;
        }

        // Push current scene info for reentry
        stack.Push(new StackEntry { sceneName = prev.name, reentrySpawnTag = reentrySpawnTag, reentryPolicy = reentryPolicy });
        Debug.Log($"[SceneStack] Pushed scene='{prev.name}' with reentryTag='{reentrySpawnTag}'");

        // Avoid duplicate load if the scene is already present (shouldn't happen in normal flow)
        var already = SceneManager.GetSceneByName(nextSceneName);
        if (!already.IsValid() || !already.isLoaded)
        {
            // Load next scene additively
            AsyncOperation load = SceneManager.LoadSceneAsync(nextSceneName, LoadSceneMode.Additive);
            yield return load;
            already = SceneManager.GetSceneByName(nextSceneName);
        }

        var next = already;
        if (!next.IsValid())
        {
            Debug.LogError($"[SceneStack] Failed to load scene: {nextSceneName}");
            isTransitioning = false;
            yield break;
        }

        // Switch active scene
        SceneManager.SetActiveScene(next);

        // Deactivate previous scene root objects to avoid duplicate physics/logic
        SetSceneRootActive(prev, false);

        // Reposition player at start point in the new scene
        bool startOk = RepositionPlayerInScene(next, tag: "PlayerStart");
        Debug.Log($"[SceneStack] Entered '{next.name}', spawn tag='PlayerStart', success={startOk}");

        // Apply carry policy when entering the next scene (optional)
        if (enterPolicy != null)
        {
            ApplyCarryPolicyNow(enterPolicy);
            SafeRefreshVisibility();
        }

        // TriggerZone の oneShot 消費が残っていると再実行できないので軽く初期化
        try
        {
            foreach (var zone in FindObjectsOfType<TriggerZone2D>(true))
            {
                if (zone && zone.gameObject.scene == prev)
                    zone.RuntimeResetForSceneReturn();
            }
        }
        catch { /* ignore */ }

        // TriggerZone の oneShot 消費が残っていると再実行できないので軽く初期化
        try
        {
            foreach (var zone in FindObjectsOfType<TriggerZone2D>(true))
            {
                if (zone && zone.gameObject.scene == prev)
                    zone.RuntimeResetForSceneReturn();
            }
        }
        catch { /* ignore */ }

        // Rebind camera and persistent refs after a frame
        yield return null;
        SafeCameraRebind();
        SafeReassignPersistentReferences();

        isTransitioning = false;
        // 万一アクション側のガードが残っていても、ここで解除しておく
        try { LoadSceneWithStackAction.ResetGuard(); } catch { /* ignore */ }
    }

    IEnumerator CoReturnToPrevious(float delayRealtime)
    {
        isTransitioning = true;

        SafeCloseAllUI();
        Time.timeScale = 1f;
        if (delayRealtime > 0f)
        {
            float t = 0f; while (t < delayRealtime) { t += Time.unscaledDeltaTime; yield return null; }
        }

        var current = SceneManager.GetActiveScene();
        if (!current.IsValid())
        {
            Debug.LogError("[SceneStack] Active scene invalid on return.");
            isTransitioning = false;
            yield break;
        }

        var entry = stack.Pop();
        var prev = SceneManager.GetSceneByName(entry.sceneName);
        if (!prev.IsValid())
        {
            Debug.LogError($"[SceneStack] Previous scene not found: {entry.sceneName}");
            isTransitioning = false;
            yield break;
        }

        // Reactivate previous scene roots first and switch active scene
        SetSceneRootActive(prev, true);
        SceneManager.SetActiveScene(prev);

        // Now unload the current scene to free memory
        AsyncOperation unload = SceneManager.UnloadSceneAsync(current);
        if (unload != null) yield return unload;

        // Apply carry policy when returning to the previous scene (optional)
        if (entry.reentryPolicy != null)
        {
            ApplyCarryPolicyNow(entry.reentryPolicy);
            SafeRefreshVisibility();
        }

        var tagsToTry = BuildReentryTagPriority(entry.reentrySpawnTag);
        Debug.Log($"[SceneStack] Returning to '{prev.name}'. Spawn tag candidates => {string.Join(", ", tagsToTry)}");

        string usedTag = null;
        foreach (var tag in tagsToTry)
        {
            if (RepositionPlayerInScene(prev, tag))
            {
                usedTag = tag;
                break;
            }
        }

        if (usedTag == null)
        {
            Debug.LogWarning($"[SceneStack] Could not find any of the spawn tags ({string.Join(", ", tagsToTry)}) in scene '{prev.name}'. Player kept current position.");
        }
        else if (!string.Equals(usedTag, entry.reentrySpawnTag, System.StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(entry.reentrySpawnTag))
        {
            Debug.Log($"[SceneStack] Reentry tag '{entry.reentrySpawnTag}' unavailable. Used fallback '{usedTag}' instead.");
        }

        // TriggerZone の oneShot 消費が残っていると再実行できないので軽く初期化
        try
        {
            foreach (var zone in FindObjectsOfType<TriggerZone2D>(true))
            {
                if (zone && zone.gameObject.scene == prev)
                    zone.RuntimeResetForSceneReturn();
            }
        }
        catch { /* ignore */ }

        // Rebind camera and persistent refs after a frame
        yield return null;
        SafeCameraRebind();
        SafeReassignPersistentReferences();

        isTransitioning = false;
        // 万一アクション側のガードが残っていても、ここで解除しておく
        try { LoadSceneWithStackAction.ResetGuard(); } catch { /* ignore */ }
    }

    // ----------------- Helpers -----------------

    void SetSceneRootActive(Scene scene, bool v)
    {
        if (!scene.IsValid()) return;
        foreach (var root in scene.GetRootGameObjects())
        {
            // Skip if object belongs to DDOL scene accidentally
            if (root.scene.name != scene.name) continue;
            root.SetActive(v);
        }
    }

    bool RepositionPlayerInScene(Scene scene, string tag)
    {
        try
        {
            var player = GameObject.FindWithTag("Player");
            if (!player)
            {
                Debug.LogWarning($"[SceneStack] Player not found when trying to reposition. scene='{scene.name}', tag='{tag}'");
                return false;
            }

            var t = FindInSceneByTag(scene, tag);
            if (!t)
            {
                Debug.Log($"[SceneStack] Tag '{tag}' not found in scene '{scene.name}'.");
                return false;
            }

            player.transform.position = t.position;
            var rb = player.GetComponent<Rigidbody2D>();
            if (rb) rb.velocity = Vector2.zero;
            Debug.Log($"[SceneStack] Repositioned player to '{GetTransformPath(t)}' (tag='{tag}') at {t.position} in scene '{scene.name}'.");
            return true;
        }
        catch { return false; }
    }

    System.Collections.Generic.List<string> BuildReentryTagPriority(string storedTag)
    {
        var tags = new System.Collections.Generic.List<string>();
        void Add(string tag)
        {
            if (string.IsNullOrWhiteSpace(tag)) return;
            if (tags.Any(x => string.Equals(x, tag, System.StringComparison.OrdinalIgnoreCase))) return;
            tags.Add(tag);
        }

        if (!string.IsNullOrWhiteSpace(storedTag) &&
            !string.Equals(storedTag, "PlayerStart", System.StringComparison.OrdinalIgnoreCase))
        {
            Add(storedTag);
        }

        Add("PlayerReturn");
        Add("PlayerStart");

        if (!string.IsNullOrWhiteSpace(storedTag) &&
            string.Equals(storedTag, "PlayerStart", System.StringComparison.OrdinalIgnoreCase))
        {
            Add(storedTag);
        }

        return tags;
    }

    Transform FindInSceneByTag(Scene scene, string tag)
    {
        if (!scene.IsValid() || string.IsNullOrEmpty(tag)) return null;
        foreach (var root in scene.GetRootGameObjects())
        {
            if (!root) continue;
            if (root.CompareTag(tag)) return root.transform;
            var tr = root.GetComponentsInChildren<Transform>(true)
                        .FirstOrDefault(x => x && x.gameObject.CompareTag(tag));
            if (tr) return tr;
        }
        return null;
    }

    string GetTransformPath(Transform t)
    {
        if (!t) return "(null)";
        var names = new System.Collections.Generic.List<string>();
        var cur = t;
        while (cur != null)
        {
            names.Add(cur.name);
            cur = cur.parent;
        }
        names.Reverse();
        return string.Join("/", names);
    }

    void SafeCameraRebind()
    {
        try
        {
            var player = GameObject.FindWithTag("Player");
            if (!player) return;

            var camGO = Camera.main ? Camera.main.gameObject : FindObjectOfType<Camera>()?.gameObject;
            MonoBehaviour follow = null;
            if (camGO)
            {
                foreach (var comp in camGO.GetComponents<MonoBehaviour>())
                {
                    if (comp && comp.GetType().Name.Contains("CameraFollow")) { follow = comp; break; }
                }
            }
            if (!follow)
            {
                foreach (var comp in FindObjectsOfType<MonoBehaviour>(true))
                {
                    if (comp && comp.GetType().Name.Contains("CameraFollow")) { follow = comp; break; }
                }
            }
            if (!follow) return;

            var ft = follow.GetType();
            var fTarget = ft.GetField("target", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var fTargetRb = ft.GetField("targetRb", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (fTarget != null) fTarget.SetValue(follow, player.transform);
            if (fTargetRb != null) fTargetRb.SetValue(follow, player.GetComponent<Rigidbody2D>());

            var mSnap = ft.GetMethod("SnapToTarget", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            mSnap?.Invoke(follow, null);
        }
        catch { /* ignore */ }
    }

    void SafeCloseAllUI()
    {
        try { UIRouter.I?.ForceCloseAll(); } catch { /* ignore */ }
        try
        {
            var w = FindObjectOfType<WordCutUI>(true);
            if (w) { w.Close(); w.gameObject.SetActive(false); }
        }
        catch { /* ignore */ }
    }

    void SafeReassignPersistentReferences()
    {
        try
        {
            var all = FindObjectsOfType<MonoBehaviour>(true);
            foreach (var mb in all)
            {
                if (!mb) continue;
                PersistentObjectHelper.AutoAssignPlayerReferences(mb);
            }
        }
        catch { /* ignore */ }
    }

    void SafeRefreshVisibility()
    {
        // EnableOnFlag.RefreshAll();
        try
        {
            var t1 = Type.GetType("EnableOnFlag");
            t1?.GetMethod("RefreshAll", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
               ?.Invoke(null, null);
        }
        catch { /* ignore */ }

        // RevealOnItem.TryRefreshAll();
        try
        {
            var t2 = Type.GetType("RevealOnItem");
            t2?.GetMethod("TryRefreshAll", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
               ?.Invoke(null, null);
        }
        catch { /* ignore */ }
    }

    void ApplyCarryPolicyNow(CarryPolicy p)
    {
        try
        {
            var gs = GameState.I;
            if (gs == null || p == null) return;

            // Snapshot current items/flags
            var snapItems = new HashSet<string>();
            try
            {
                foreach (var id in gs.GetAllItemIds()) snapItems.Add(id);
            }
            catch { /* ignore */ }

            var snapFlags = new HashSet<string>();
            try
            {
                var f = typeof(GameState).GetField("flags", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (f?.GetValue(gs) is System.Collections.IEnumerable en)
                {
                    foreach (var x in en) if (x is string s) snapFlags.Add(s);
                }
            }
            catch { /* ignore */ }

            // Compute next sets based on policy
            HashSet<string> nextItems;
            if (p.keepAllItems)
            {
                nextItems = new HashSet<string>(snapItems);
            }
            else
            {
                var allow = new HashSet<string>(p.keepItems ?? Array.Empty<string>());
                nextItems = new HashSet<string>(snapItems.Where(id => allow.Contains(id)));
            }
            if (p.dropItems != null) foreach (var id in p.dropItems) nextItems.Remove(id);

            HashSet<string> nextFlags;
            if (p.keepAllFlags)
            {
                nextFlags = new HashSet<string>(snapFlags);
            }
            else
            {
                var allowF = new HashSet<string>(p.keepFlags ?? Array.Empty<string>());
                nextFlags = new HashSet<string>(snapFlags.Where(id => allowF.Contains(id)));
            }
            if (p.dropFlags != null) foreach (var id in p.dropFlags) nextFlags.Remove(id);

            // Apply diffs
            foreach (var cur in snapItems) if (!nextItems.Contains(cur)) gs.Remove(cur);
            foreach (var nxt in nextItems) if (!snapItems.Contains(nxt)) gs.Add(nxt);

            foreach (var cur in snapFlags) if (!nextFlags.Contains(cur)) gs.RemoveFlag(cur);
            foreach (var nxt in nextFlags) if (!snapFlags.Contains(nxt)) gs.AddFlag(nxt);
        }
        catch { /* ignore */ }
    }
}
