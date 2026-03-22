using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TriggerZone2D : MonoBehaviour
{
    public enum ActivationMode { AutoOnEnter, OnKeyPress }

    [Header("Basics")]
    [SerializeField] LayerMask playerMask;
    [SerializeField] bool oneShot = true;
    [SerializeField] float minInterval = 0.2f;

    [Header("Conditions (ALL must be true)")]
    [SerializeField] ConditionBase[] conditions;

    [Header("Actions")] 
    [SerializeField] ActionBase[] actionsIfTrue;
    [SerializeField] ActionBase[] actionsIfFalse;

    [Header("Activation")]
    [SerializeField] ActivationMode activation = ActivationMode.AutoOnEnter;
    [SerializeField] KeyCode interactKey = KeyCode.Return;   // Enter
    [SerializeField] GameObject bubbleRoot;                  // 近接表示(任意)
    [SerializeField] float lingerSeconds = 0.12f;            // 退出猶予（取りこぼし防止）
    [Tooltip("ONで、meが主導(me_leads)中でもこのゾーンだけは発火を許可する")]
    [SerializeField] bool allowWhileMeLeads = false;
    
    [Header("Blocks")]
    [Tooltip("指定フラグが立っている間はキー入力を無視（例: しゃがみ中）")]
    [SerializeField] bool blockWhileFlagSet = false;
    [SerializeField] string blockFlagId = "vm_crouching";

    bool consumed;
    float lastAt;
    bool pendingAutoRetry = false; // AutoOnEnter 抑止時の再試行フラグ

    readonly HashSet<Collider2D> inside = new HashSet<Collider2D>();
    float insideUntil = 0f; // 退出猶予の終了時刻

    // —— UI ビジー判定 ——
    // 仕様:
    //  - Popup 表示中はビジー扱い（再トリガ抑止）
    //  - WordCut 表示中もビジー
    //  - それ以外で Modal だけ立っている“見えない”状態はステイルとしてバイパス
    bool IsUiBusy()
    {
        var r = UIRouter.I;
        var w = WordCutUI.Instance;

        if (r != null)
        {
            bool modal = r.IsModalOpen();
            bool busy = r.IsBusyForInteraction();
            bool wordcutVisible = (w != null && w.IsOpen && w.gameObject.activeInHierarchy);

            // 目に見えるUI（Popup or WordCut）が出ている → 抑止
            if (busy || wordcutVisible) return true;

            // モーダルだけ立っているのに何も見えていない → ステイルとしてバイパス
            if (modal && !busy && !wordcutVisible)
            {
                Debug.Log("[TriggerZone2D] bypass busy: stale modal (no visible UI)");
                UIDebug.DumpState("TriggerZone2D.IsUiBusy.bypass");
                return false;
            }

            return false; // modal=false && busy=false && WordCut非表示
        }

        // UIRouter が居ない場合、WordCut が見えていれば抑止
        return (w != null && w.IsOpen && w.gameObject.activeInHierarchy);
    }

    bool IsPlayer(Collider2D c)
    {
        // 子コライダ/剛体経由でもプレイヤー本体を取る
        var go = c.attachedRigidbody ? c.attachedRigidbody.gameObject : c.gameObject;
        if (go.CompareTag("Player")) return true;
        return (playerMask.value & (1 << go.layer)) != 0;
    }

    void OnEnable()
    {
        if (bubbleRoot) bubbleRoot.SetActive(false);
        inside.Clear();
        insideUntil = 0f;
        consumed = false;
        lastAt = 0f;
    }

    void OnDisable()
    {
        // ★ 非アクティブ化の途中でコルーチンが残らないように
        StopAllCoroutines();
        if (bubbleRoot) bubbleRoot.SetActive(false);
        inside.Clear();
        insideUntil = 0f;
    }

    // シーンスタックで“前のシーン”に戻ったとき、
    // oneShot 消費や内部滞在フラグが保持されてしまうと再実行できなくなることがある。
    // そのため SceneStackManager から呼び出せるランタイム初期化を用意する。
    public void RuntimeResetForSceneReturn()
    {
        // 直近の入力で誤発火しないよう最低限の状態のみをリセット
        consumed = false;           // oneShot を復活（演出や条件側が状態を管理する想定）
        lastAt = 0f;
        inside.Clear();
        insideUntil = 0f;
        if (bubbleRoot) bubbleRoot.SetActive(false);
        UIDebug.Log("TriggerZone2D.Reset", $"zone='{name}' oneShotReset done in scene return");
    }

    void OnTriggerEnter2D(Collider2D c)
    {
        if (!IsPlayer(c)) return;

        inside.Add(c);
        insideUntil = Mathf.Max(insideUntil, Time.unscaledTime + lingerSeconds);
        if (bubbleRoot)
        {
            // 乗車/リーダー中は常に非表示
            bool ridingVertical = false;
            bool meLeads = false;
            try { ridingVertical = (GameState.I != null && GameState.I.HasFlag("vertical_riding")); } catch { /* ignore */ }
            try { meLeads = (GameState.I != null && GameState.I.HasFlag("me_leads")); } catch { /* ignore */ }
            bubbleRoot.SetActive(!(ridingVertical || (meLeads && !allowWhileMeLeads)));
        }

        if (activation == ActivationMode.AutoOnEnter)
        {
            // meが主導中は自動発火を抑止
            try { if (GameState.I != null && GameState.I.HasFlag("me_leads") && !allowWhileMeLeads) return; } catch { }
            if (IsUiBusy())
            {
                Debug.Log("[TriggerZone2D] Auto suppressed: UI busy (will retry)");
                // UI が閉じたらもう一度だけ評価してみる
                StartCoroutine(RetryWhenUiFree());
                return;
            }
            EvaluateAndExecute(false);
        }
    }

    // テレポート等でゾーン内に“湧いた”ケースを補足
    void OnTriggerStay2D(Collider2D c)
    {
        if (!IsPlayer(c)) return;
        if (inside.Contains(c)) return; // 既に処理済み

        // Enterと同等の初期化
        inside.Add(c);
        insideUntil = Mathf.Max(insideUntil, Time.unscaledTime + lingerSeconds);
        if (bubbleRoot)
        {
            bool ridingVertical = false;
            bool meLeads = false;
            try { ridingVertical = (GameState.I != null && GameState.I.HasFlag("vertical_riding")); } catch { /* ignore */ }
            try { meLeads = (GameState.I != null && GameState.I.HasFlag("me_leads")); } catch { /* ignore */ }
            bubbleRoot.SetActive(!(ridingVertical || (meLeads && !allowWhileMeLeads)));
        }

        if (activation == ActivationMode.AutoOnEnter)
        {
            try { if (GameState.I != null && GameState.I.HasFlag("me_leads") && !allowWhileMeLeads) return; } catch { }
            if (IsUiBusy())
            {
                Debug.Log("[TriggerZone2D] Auto suppressed(stay): UI busy (will retry)");
                StartCoroutine(RetryWhenUiFree());
                return;
            }
            EvaluateAndExecute(false);
        }
    }

    void OnTriggerExit2D(Collider2D c)
    {
        if (!IsPlayer(c)) return;

        inside.Remove(c); 
        insideUntil = Time.unscaledTime + lingerSeconds;

        if (!bubbleRoot) return;

        if (inside.Count == 0)
        {
            // ★ ここがポイント：非アクティブならコルーチン禁止・即消灯
            if (!isActiveAndEnabled || !gameObject.activeInHierarchy)
            {
                bubbleRoot.SetActive(false);
                return;
            }
            StartCoroutine(HideBubbleAfterLinger());
        }
    }

    IEnumerator HideBubbleAfterLinger()
    {
        while (Time.unscaledTime < insideUntil) yield return null;
        if (inside.Count == 0 && bubbleRoot) bubbleRoot.SetActive(false);
    }

    void Update()
    {
        // 乗車中（vertical_riding）や me 主導ならBubblesは常に消しておく
        if (bubbleRoot)
        {
            bool ridingVertical = false;
            bool meLeads = false;
            try { ridingVertical = (GameState.I != null && GameState.I.HasFlag("vertical_riding")); } catch { /* ignore */ }
            try { meLeads = (GameState.I != null && GameState.I.HasFlag("me_leads")); } catch { /* ignore */ }
            if ((ridingVertical || (meLeads && !allowWhileMeLeads)) && bubbleRoot.activeSelf) bubbleRoot.SetActive(false);
        }

        if (activation != ActivationMode.OnKeyPress) return;

        // 退出猶予込みで“滞在扱い”
        bool virtuallyInside = inside.Count > 0 || Time.unscaledTime < insideUntil;
        if (!virtuallyInside) return;

        // カスタムメニュー（Shopメニュー）開放中は入力を抑止
        try { if (GameState.I != null && GameState.I.HasFlag("menu_open")) return; } catch { }

        // しゃがみ等の一時フラグでキー入力自体を抑止
        if (blockWhileFlagSet)
        {
            try { if (GameState.I != null && GameState.I.HasFlag(blockFlagId)) return; } catch { }
        }

        // ★ UI（WordCutUI等）が出ている間は入力を無視 → 再オープンを防止
        if (IsUiBusy()) {
            Debug.Log($"[TriggerZone2D] busyDetail modal={UIRouter.I?.IsModalOpen()} busyForInteract={UIRouter.I?.IsBusyForInteraction()} wordcutOpen={(WordCutUI.Instance ? WordCutUI.Instance.IsOpen : false)} wordcutActive={(WordCutUI.Instance ? WordCutUI.Instance.IsActive : false)} activeInHierarchy={(WordCutUI.Instance ? WordCutUI.Instance.gameObject.activeInHierarchy : false)}");
            Debug.Log("[TriggerZone2D] Key suppressed: UI busy");
            UIDebug.DumpState("TriggerZone2D.Update.busy");
            return;
        }

        // meが主導中はキーインタラクトを抑止（ただし許可オプションがONなら通す）
        try { if (GameState.I != null && GameState.I.HasFlag("me_leads") && !allowWhileMeLeads) return; } catch { }

        if (Input.GetKeyDown(interactKey) || Input.GetKeyDown(KeyCode.KeypadEnter))
        {
            UIDebug.DumpState("TriggerZone2D.KeyDown.beforeExec");
            EvaluateAndExecute(true); // キー発火はクールダウン無視
        }
    }

    IEnumerator RetryWhenUiFree()
    {
        if (pendingAutoRetry) yield break;
        pendingAutoRetry = true;
        // UI が閉じるのを待つ（数フレームで閉じる想定）
        while (IsUiBusy()) yield return null;

        // まだプレイヤーが滞在していれば再評価
        bool virtuallyInside = inside.Count > 0 || Time.unscaledTime < insideUntil;
        if (virtuallyInside && activation == ActivationMode.AutoOnEnter)
        {
            Debug.Log("[TriggerZone2D] Auto retry after UI became free");
            EvaluateAndExecute(false);
        }
        pendingAutoRetry = false;
    }

    void EvaluateAndExecute(bool fromKeyPress)
    {
        if (oneShot && consumed) return;

        // AutoOnEnter のときだけクールダウン適用
        if (!fromKeyPress)
        {
            if (Time.time - lastAt < minInterval) return;
            lastAt = Time.time;
        }

        bool ok = true;
        if (conditions != null)
        {
            System.Text.StringBuilder sb = null;
            foreach (var cond in conditions)
            {
                if (cond == null) continue;
                bool r = false;
                try { r = cond.Evaluate(); }
                catch (System.Exception e) { Debug.LogWarning($"[TriggerZone2D] Condition threw: {cond.GetType().Name} {e.Message}"); }
                if (sb == null) sb = new System.Text.StringBuilder();
                sb.Append(cond.GetType().Name)
                  .Append('(').Append(GetCondInfo(cond)).Append(')')
                  .Append('=')
                  .Append(r ? '1' : '0').Append(' ');
                if (!r) ok = false;
            }
            if (sb != null) UIDebug.Log("TriggerZone2D.Conditions", $"zone='{name}' -> " + sb.ToString());
        }

        var list = ok ? actionsIfTrue : actionsIfFalse;
        UIDebug.Log("TriggerZone2D.Execute", $"zone='{name}' fromKey={fromKeyPress} ok={ok} actions={(list != null ? list.Length : 0)} oneShot={oneShot} consumed={consumed}");

        if (list != null)
        {foreach (var a in list) if (a) a.Execute(); }

        // 成功時だけ消費
        if (oneShot && ok) consumed = true;
    }

    string GetCondInfo(ConditionBase cond)
    {
        try
        {
            var t = cond.GetType();
            var fItem = t.GetField("itemId", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (fItem != null)
            {
                var id = fItem.GetValue(cond) as string;
                return $"item='{id}' have={(GameState.I? GameState.I.Has(id) : false)}";
            }

            var fFlag = t.GetField("flagId", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (fFlag != null)
            {
                var id = fFlag.GetValue(cond) as string;
                return $"flag='{id}' set={(GameState.I? GameState.I.HasFlag(id) : false)}";
            }

            var fRide = t.GetField("ridingFlag", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (fRide != null)
            {
                var id = fRide.GetValue(cond) as string;
                return $"ridingFlag='{id}' set={(GameState.I? GameState.I.HasFlag(id) : false)}";
            }
        }
        catch { }
        return "";
    }
       
}
