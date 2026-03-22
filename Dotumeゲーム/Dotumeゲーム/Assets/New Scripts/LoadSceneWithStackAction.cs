using UnityEngine;

/// Load a scene additively and keep current scene (inactive) on a stack for later return.
public class LoadSceneWithStackAction : ActionBase
{
    [Header("Destination")]
    public string sceneName;

    [Header("When Returning To This Scene")]
    [Tooltip("Which tag to spawn at when returning back here. Default: PlayerReturn")] 
    public string reentrySpawnTag = "PlayerReturn"; // e.g., place at goal door when coming back

    [Header("Carry Policy (optional)")]
    [Tooltip("次のシーンへ入る際に適用する持ち越しポリシー（nullなら未適用）")]
    public CarryPolicy policyToNext = null;
    [Tooltip("スタックから戻る際（このシーンへ戻る）に適用する持ち越しポリシー（nullなら未適用）")]
    public CarryPolicy policyOnReturnToThis = null;

    [Header("Timing")]
    [Min(0f)] public float delayRealtime = 0f;

    [Header("Required Items To Proceed (Stage2/3/4 用)")]
    [Tooltip("次シーンへ進むために 'key' と 'letterId' の同時所持を要求するか")]
    public bool requireKeyAndLetter = false;
    [Tooltip("キーの itemId（全シーン必須）")]
    public string keyItemId = "key";
    [Tooltip("レター（名称は自由に変更）の itemId")]
    public string letterItemId = "letter";
    [Tooltip("キーが無い時に実行する代替アクション")]
    public ActionBase onMissingKey;
    [Tooltip("レターが無い時に実行する代替アクション")]
    public ActionBase onMissingLetter;

    static bool isLoading; // duplicate guard similar to other loaders

    public override void Execute()
    {
        // 必要アイテムチェック（Stage2/3/4 でのみ有効化想定）
        if (requireKeyAndLetter)
        {
            bool hasKey = GameState.I != null && GameState.I.Has(keyItemId);
            bool hasLetter = GameState.I != null && GameState.I.Has(letterItemId);

            if (!hasKey && !hasLetter)
            {
                if (onMissingKey != null) { onMissingKey.Execute(); }
                else Debug.Log("[LoadSceneWithStack] Both missing; onMissingKey not set.");
                return;
            }
            if (!hasKey)
            {
                if (onMissingKey != null) { onMissingKey.Execute(); }
                else Debug.Log("[LoadSceneWithStack] Missing key; onMissingKey not set.");
                return;
            }
            if (!hasLetter)
            {
                if (onMissingLetter != null) { onMissingLetter.Execute(); }
                else Debug.Log("[LoadSceneWithStack] Missing letter; onMissingLetter not set.");
                return;
            }
        }

        if (isLoading) return;
        if (string.IsNullOrWhiteSpace(sceneName)) { Debug.LogWarning("[LoadWithStack] sceneName is empty"); return; }
        if (!SceneStackManager.I) { new GameObject("~SceneStackManager").AddComponent<SceneStackManager>(); }

        isLoading = true;
        SceneStackManager.I.PushAndLoad(
            sceneName,
            string.IsNullOrEmpty(reentrySpawnTag) ? "PlayerReturn" : reentrySpawnTag,
            delayRealtime,
            policyToNext,
            policyOnReturnToThis
        );
        // 注意: このActionは所属シーンが非アクティブ/Unloadされる可能性があるため、
        // 自身のコルーチンで解除すると止まってしまう。DDOLな管理者から解除する。
        SceneStackManager.I?.StartCoroutine(ReleaseGuardNextFrame());
    }

    public static void ResetGuard() { isLoading = false; }

    static System.Collections.IEnumerator ReleaseGuardNextFrame()
    {
        yield return null; isLoading = false;
    }

    // このアクションは TriggerZone2D からのみ実行させる（直接のOnTrigger経由では発火しない）
}
