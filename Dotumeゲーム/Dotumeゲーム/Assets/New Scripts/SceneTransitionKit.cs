using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;

/// シーン遷移の安全ハブ。ポーズ/UIの片付け → ロード → 再バインド/可視更新 → カメラ再接続 →（任意）持ち越しポリシー適用
public class SceneTransitionKit : MonoBehaviour
{
    static SceneTransitionKit _i;
    static SceneTransitionKit I
    {
        get
        {
            if (_i == null)
            {
                var go = new GameObject("~SceneTransitionKit");
                DontDestroyOnLoad(go);
                _i = go.AddComponent<SceneTransitionKit>();
            }
            return _i;
        }
    }

    /// エントリポイント：このAPIで遷移する（SceneManager.LoadScene を直接使わない）
    public static void Load(string sceneName, CarryPolicy policy = null, float delayRealtime = 0f)
    {
        I.StartCoroutine(I.CoLoad(sceneName, policy, delayRealtime));
    }


    // 破棄済みでも落ちない安全ログ
    static string SafeSceneName(GameObject go)
    {
        try { return go == null ? "null" : go.scene.name; }
        catch (MissingReferenceException) { return "<destroyed>"; }
    }

    // DDOL所属判定（安全版）
    static bool IsInDDOL(GameObject go)
    {
        try { return go != null && go.scene.name == "DontDestroyOnLoad"; }
        catch (MissingReferenceException) { return false; }
    }

    // Unload前に必ず昇格
    static void PromoteToDDOLIfNeeded(GameObject go)
    {
        try
        {
            if (go && !IsInDDOL(go))
            {
                var root = go.transform.root.gameObject;
                DontDestroyOnLoad(root);
                Debug.Log($"[STK] Promoted '{root.name}' to DDOL");
            }
        }
        catch { /* ignore */ }
    }

    // 今いるDDOLオブジェクトのダンプ（調査用）
    static void DumpDDOL()
    {
        foreach (var g in Resources.FindObjectsOfTypeAll<GameObject>())
        {
            try { if (g.scene.name == "DontDestroyOnLoad") Debug.Log($"[DDOL] {g.name}"); }
            catch { /* ignore */ }
        }
    }


    IEnumerator CoLoad(string sceneName, CarryPolicy policy, float delayRealtime)
    {
        var player = GameObject.FindGameObjectWithTag("Player");
        var snap = TakeGameStateSnapshot();

        // 0) 事前片付け
        SafeResumeTime();
        SafeCloseAllUI();
        if (delayRealtime > 0f)
        {
            float t = 0f; while (t < delayRealtime) { t += Time.unscaledDeltaTime; yield return null; }
        }

        
        // 2) ADDITIVE で新シーンを非同期ロード
        var prev = SceneManager.GetActiveScene();
        AsyncOperation async = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
        yield return async; // 完了待ち

        // 3) 新シーンをアクティブに切替
        var newScene = SceneManager.GetSceneByName(sceneName);
        if (newScene.IsValid()) SceneManager.SetActiveScene(newScene);

        Debug.Log("SceneManager.SetActiveScene(newScene)");
        Debug.Log($"[SceneTransitionKit] Player found after load? {(player ? "YES" : "NO")} | scene={SafeSceneName(player)}");

        // 4) 旧シーンをアンロード（重複UI/EventSystemを回避）
        if (prev.IsValid())
            yield return SceneManager.UnloadSceneAsync(prev);
        Debug.Log("SceneManager.UnloadSceneAsync(prev)");
        Debug.Log($"[SceneTransitionKit] Player found after load? {(player ? "YES" : "NO")} | scene={SafeSceneName(player)}");

        // 5) 念のため再開（Startで再ポーズされる対策）
        SafeResumeTime();
        Time.timeScale = 1f;

        // 6) （任意）持ち越しポリシー適用
        if (policy != null) ApplyCarryPolicy(snap, policy);
        Debug.Log("ApplyCarryPolicy(snap, policy)");
        Debug.Log($"[SceneTransitionKit] Player found after load? {(player ? "YES" : "NO")} | scene={SafeSceneName(player)}");


        // 7) 再バインド & 表示更新 & カメラ再接続
        SafeUIRebind();
        SafeRefreshVisibility();
        SafePlayerReposition(); // ★ この行を追加！
        SafeCameraRebind();
        
        // 8) ロード完了フラグをリセット
        ResetLoadingFlag();
        
        // 9) 永続オブジェクトの参照を遅延設定（1フレーム後）
        StartCoroutine(DelayedReassignReferences());

        // ★ 追加：さらに1フレーム待ってから完全に解放
        yield return null;

        Debug.Log($"[SceneTransitionKit] Scene '{sceneName}' fully loaded and ready.");

        Debug.Log($"[SceneTransitionKit] Player found after load? {(player ? "YES" : "NO")} | scene={SafeSceneName(player)}");

    }

    // ================= ユーティリティ（存在すれば呼ぶ・無ければスキップ） =================

    void SafeResumeTime()
    {
        // GameDirector.I?.ForceResume() があればそれを呼び、最後に timeScale を 1 に戻す
        try
        {
            var t = Type.GetType("GameDirector");
            var inst = t?.GetProperty("I", BindingFlags.Public | BindingFlags.Static)?.GetValue(null);
            var m = t?.GetMethod("ForceResume", BindingFlags.Public | BindingFlags.Instance);
            if (inst != null && m != null) m.Invoke(inst, null);
        }
        catch { /* ignore */ }
        Time.timeScale = 1f;
    }

    void SafeCloseAllUI()
    {
        // UIRouter.I?.ForceCloseAll()
        try
        {
            var uiRouter = GetSingleton("UIRouter");
            uiRouter?.GetType().GetMethod("ForceCloseAll", BindingFlags.Public | BindingFlags.Instance)
                   ?.Invoke(uiRouter, null);
        }
        catch { /* ignore */ }

        // WordCutUI.Instance?.Close() + 非アクティブ化
        try
        {
            var wcuType = Type.GetType("WordCutUI");
            var instObj = wcuType?.GetField("Instance", BindingFlags.Public | BindingFlags.Static)?.GetValue(null) as Component;
            if (instObj != null)
            {
                var mClose = wcuType.GetMethod("Close", BindingFlags.Public | BindingFlags.Instance);
                mClose?.Invoke(instObj, null);
                //if (instObj is Behaviour b) b.enabled = false;
                instObj.gameObject.SetActive(false);
            }
        }
        catch { /* ignore */ }
    }

    (HashSet<string> items, HashSet<string> flags) TakeGameStateSnapshot()
    {
        var items = new HashSet<string>();
        var flags = new HashSet<string>();
        try
        {
            var gs = GetSingleton("GameState");
            if (gs != null)
            {
                var t = gs.GetType();

                // まずは公開プロパティ Items/Flags（IReadOnlyCollection<string> 想定）
                var pItems = t.GetProperty("Items", BindingFlags.Public | BindingFlags.Instance);
                var pFlags = t.GetProperty("Flags", BindingFlags.Public | BindingFlags.Instance);
                IEnumerable<string> srcItems = pItems?.GetValue(gs) as IEnumerable<string>;
                IEnumerable<string> srcFlags = pFlags?.GetValue(gs) as IEnumerable<string>;

                // 無ければ private フィールド（items/flags）から拝借
                if (srcItems == null) srcItems = GetPrivateStringSet(gs, "items");
                if (srcFlags == null) srcFlags = GetPrivateStringSet(gs, "flags");

                if (srcItems != null) foreach (var s in srcItems) items.Add(s);
                if (srcFlags != null) foreach (var s in srcFlags) flags.Add(s);
            }
        }
        catch { /* ignore */ }

        return (items, flags);
    }

    void ApplyCarryPolicy((HashSet<string> items, HashSet<string> flags) snap, CarryPolicy p)
    {
        try
        {
            var gs = GetSingleton("GameState");
            if (gs == null) return;

            // 1) Items
            HashSet<string> nextItems;
            if (p.keepAllItems)
            {
                nextItems = new HashSet<string>(snap.items);
            }
            else
            {
                var allow = new HashSet<string>(p.keepItems ?? Array.Empty<string>());
                nextItems = new HashSet<string>();
                foreach (var id in snap.items) if (allow.Contains(id)) nextItems.Add(id);
            }
            if (p.dropItems != null) foreach (var id in p.dropItems) nextItems.Remove(id);

            // 2) Flags
            HashSet<string> nextFlags;
            if (p.keepAllFlags)
            {
                nextFlags = new HashSet<string>(snap.flags);
            }
            else
            {
                var allowF = new HashSet<string>(p.keepFlags ?? Array.Empty<string>());
                nextFlags = new HashSet<string>();
                foreach (var id in snap.flags) if (allowF.Contains(id)) nextFlags.Add(id);
            }
            if (p.dropFlags != null) foreach (var id in p.dropFlags) nextFlags.Remove(id);

            // 3) 差分適用（既存の反射呼び出しをそのまま使用）
            var t = gs.GetType();
            var mAddItem = t.GetMethod("Add", BindingFlags.Public | BindingFlags.Instance);
            var mRemoveItem = t.GetMethod("Remove", BindingFlags.Public | BindingFlags.Instance);
            var mAddFlag = t.GetMethod("AddFlag", BindingFlags.Public | BindingFlags.Instance);
            var mRemoveFlag = t.GetMethod("RemoveFlag", BindingFlags.Public | BindingFlags.Instance);

            foreach (var cur in snap.items) if (!nextItems.Contains(cur)) mRemoveItem?.Invoke(gs, new object[] { cur });
            foreach (var nxt in nextItems) if (!snap.items.Contains(nxt)) mAddItem?.Invoke(gs, new object[] { nxt });

            foreach (var cur in snap.flags) if (!nextFlags.Contains(cur)) mRemoveFlag?.Invoke(gs, new object[] { cur });
            foreach (var nxt in nextFlags) if (!snap.flags.Contains(nxt)) mAddFlag?.Invoke(gs, new object[] { nxt });
        }
        catch { /* ignore */ }
    }

    void SafeUIRebind()
    {
        // UIRouter.I?.RebindInScene()
        try
        {
            var ui = GetSingleton("UIRouter");
            ui?.GetType().GetMethod("RebindInScene", BindingFlags.Public | BindingFlags.Instance)
              ?.Invoke(ui, null);
        }
        catch { /* ignore */ }
    }

    void SafeRefreshVisibility()
    {
        // EnableOnFlag.RefreshAll();
        try
        {
            var t1 = Type.GetType("EnableOnFlag");
            t1?.GetMethod("RefreshAll", BindingFlags.Public | BindingFlags.Static)
               ?.Invoke(null, null);
        }
        catch { /* ignore */ }

        // RevealOnItem.TryRefreshAll();
        try
        {
            var t2 = Type.GetType("RevealOnItem");
            t2?.GetMethod("TryRefreshAll", BindingFlags.Public | BindingFlags.Static)
               ?.Invoke(null, null);
        }
        catch { /* ignore */ }
    }

    void SafePlayerReposition()
    {
        try
        {
            var player = GameObject.FindWithTag("Player");
            var startPoint = GameObject.FindWithTag("PlayerStart");
            if (player && startPoint)
            {
                player.transform.position = startPoint.transform.position;
                // Rigidbodyの速度をリセットしておくとより安全
                var playerRb = player.GetComponent<Rigidbody2D>();
                if (playerRb) playerRb.velocity = Vector2.zero;
            }
        }
        catch { /* ignore */ }
    }

    void SafeCameraRebind()
    {
        try
        {
            var player = GameObject.FindWithTag("Player");
            if (!player) return;

            // 1) メインカメラ or シーン上のカメラ
            var camGO = Camera.main ? Camera.main.gameObject : FindAny<Camera>()?.gameObject;
            MonoBehaviour follow = null;

            // 2) カメラにアタッチされた「CameraFollow」系を探す
            if (camGO != null)
            {
                foreach (var comp in camGO.GetComponents<MonoBehaviour>())
                {
                    if (comp && comp.GetType().Name.Contains("CameraFollow")) { follow = comp; break; }
                }
            }
            // 3) 無ければシーン全体から検索
            if (!follow)
            {
                foreach (var comp in FindAll<MonoBehaviour>())
                {
                    if (comp && comp.GetType().Name.Contains("CameraFollow")) { follow = comp; break; }
                }
            }
            if (!follow) return;

            var ft = follow.GetType();
            var fTarget = ft.GetField("target", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            var fTargetRb = ft.GetField("targetRb", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (fTarget != null) fTarget.SetValue(follow, player.transform);
            if (fTargetRb != null) fTargetRb.SetValue(follow, player.GetComponent<Rigidbody2D>());

            var mSnap = ft.GetMethod("SnapToTarget", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            mSnap?.Invoke(follow, null);
        }
        catch { /* ignore */ }
    }

    System.Collections.IEnumerator DelayedReassignReferences()
    {
        // 1フレーム待機してから参照を再設定
        yield return null;
        SafeReassignPersistentReferences();
    }

    void SafeReassignPersistentReferences()
    {
        // 永続オブジェクトの参照を再設定
        try
        {
            var helperType = Type.GetType("PersistentObjectHelper");
            if (helperType == null) return;

            // シーン内の全MonoBehaviourを取得して参照を再設定
            var allMonoBehaviours = FindAll<MonoBehaviour>();
            foreach (var mb in allMonoBehaviours)
            {
                if (mb == null) continue;
                
                var method = helperType.GetMethod("AutoAssignPlayerReferences", 
                    BindingFlags.Public | BindingFlags.Static);
                method?.Invoke(null, new object[] { mb });
            }
        }
        catch { /* ignore */ }
    }

    void ResetLoadingFlag()
    {
        // LoadSceneWithPolicyActionのロードフラグをリセット
        try
        {
            var t = Type.GetType("LoadSceneWithPolicyAction");
            var f1 = t?.GetField("isLoading", BindingFlags.NonPublic | BindingFlags.Static);
            var f2 = t?.GetField("currentLoadingScene", BindingFlags.NonPublic | BindingFlags.Static);
            f1?.SetValue(null, false);
            f2?.SetValue(null, "");
        }
        catch { /* ignore */ }
    }

    // ---------- 低依存のための小ユーティリティ ----------

    object GetSingleton(string typeName)
    {
        try
        {
            var t = Type.GetType(typeName);
            var p = t?.GetProperty("I", BindingFlags.Public | BindingFlags.Static);
            return p?.GetValue(null);
        }
        catch { return null; }
    }

    // SceneTransitionKit.cs 内の GetPrivateStringSet を差し替え
    IEnumerable<string> GetPrivateStringSet(object obj, string fieldName)
    {
        System.Collections.IEnumerable en = null;

        // 取得処理だけ try-catch に閉じ込める（ここでは yield を使わない）
        try
        {
            var f = obj.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            en = f?.GetValue(obj) as System.Collections.IEnumerable;
        }
        catch
        {
            // 何もしない（後段で null チェック）
        }

        if (en == null) yield break;

        // 列挙は try-catch の外で実行（yield を安全に使える）
        foreach (var x in en)
        {
            if (x is string s) yield return s;
        }
    }

    // Unity バージョン差異に優しい探索
    T FindAny<T>() where T : UnityEngine.Object
    {
#if UNITY_2023_1_OR_NEWER
        return GameObject.FindFirstObjectByType<T>();
#else
        return GameObject.FindObjectOfType<T>();
#endif
    }

    T[] FindAll<T>() where T : UnityEngine.Object
    {
#if UNITY_2023_1_OR_NEWER
        return GameObject.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None);
#else
        return GameObject.FindObjectsOfType<T>(true);
#endif
    }
}
