using System.Collections;
using UnityEngine;

public enum UIKind { None, WordCut, Popup, Dialogue }

public class UIRouter : MonoBehaviour
{
    public static UIRouter I { get; private set; }

    [Header("Refs")]
    [SerializeField] ModalGate modalGate;
    [SerializeField] WordCutUI wordCutUI; // 既存オブジェクトをアサイン
    [SerializeField] TomDialogueView tomDialogueView; // TOM会話ビュー（任意・未指定なら自動検索）

    UIKind current = UIKind.None;
    string owner = null;
    Coroutine popupCo;

    void Awake()
    {
        if (I != null && I != this) { Destroy(gameObject); return; }
        I = this;
        if (!modalGate) modalGate = FindObjectOfType<ModalGate>();
        if (!wordCutUI) wordCutUI = FindObjectOfType<WordCutUI>(includeInactive: true);
        if (!tomDialogueView) tomDialogueView = FindObjectOfType<TomDialogueView>(includeInactive: true);
        DontDestroyOnLoad(gameObject);
    }

    void Update()
    {
        // WordCut が実際に開いていない（または非アクティブ）ならゲートを解放
        if (current == UIKind.WordCut && !(wordCutUI && wordCutUI.IsOpen && wordCutUI.gameObject.activeInHierarchy))
        {
            UIDebug.Log("UIRouter.Update", "stale WordCut lock detected -> release");
            modalGate?.Release(owner);
            current = UIKind.None;
            owner = null;

            // 可視UIが無いのに timeScale が 0 のままなら安全網で復帰するが、
            // メニュー（ShopMenuActionなど）が開いている間は復帰しない。
            bool menuOpen = false;
            try { menuOpen = (GameState.I != null && GameState.I.HasFlag("menu_open")); } catch { }

            if (!menuOpen)
            {
                if (GameDirector.I && GameDirector.I.IsPaused)
                {
                    GameDirector.I.ForceResume();
                }
                if (Mathf.Approximately(Time.timeScale, 0f)) Time.timeScale = 1f;
            }
        }

        // Dialogue が実際に開いていない（または非アクティブ）ならゲートを解放
        if (current == UIKind.Dialogue && !(tomDialogueView && tomDialogueView.IsOpen && tomDialogueView.gameObject.activeInHierarchy))
        {
            UIDebug.Log("UIRouter.Update", "stale Dialogue lock detected -> release");
            modalGate?.Release(owner);
            current = UIKind.None;
            owner = null;
        }
    }
   
    public void ForceCloseAll()
    {
        // 実行中のポップアップ処理があれば停止
        if (popupCo != null) StopCoroutine(popupCo);
        popupCo = null;

        // WordCutUIが参照できていれば、閉じるよう命令
        if (wordCutUI)
        {
            wordCutUI.Close();
        }

        // ModalGateのロックを確実に解放（オーナー不一致でも強制）
        if (modalGate) modalGate.ForceRelease();

        // 自身の内部状態を初期化
        current = UIKind.None;
        owner = null;
        UIDebug.DumpState("UIRouter.ForceCloseAll.after");
    }

    bool TryAcquireWithRecover(string reqOwner)
    {
        if (!modalGate) return false;
        if (modalGate.TryAcquire(reqOwner)) return true;

        // UIが見えていないのにロックされている → ステイルな状態として解放して再試行
        bool noVisibleUI = !(
            (current == UIKind.WordCut && wordCutUI && wordCutUI.IsOpen && wordCutUI.gameObject.activeInHierarchy) ||
            (current == UIKind.Popup && popupCo != null)
        );

        if (noVisibleUI)
        {
            modalGate.ForceRelease();
            UIDebug.Log("UIRouter.AcquireRecover", $"force released stale lock; retry owner={reqOwner}");
            return modalGate.TryAcquire(reqOwner);
        }
        return false;
    }

    // ---- WordCut ----
    public bool OpenWordCut(string reqOwner, string word, string expected, string itemId, int cutsRequired, string guide = "", bool addToInventory = true)
    {
        if (!modalGate || !wordCutUI) return false;
        if (!TryAcquireWithRecover(reqOwner)) return false;

        UIDebug.Log("UIRouter.OpenWordCut", $"req owner={reqOwner} word={word} expected={expected} item={itemId} addInv={addToInventory}");
        owner = reqOwner;
        current = UIKind.WordCut;
        // ※WordCutUIは内部でTime.timeScale=0を扱う（既存仕様のまま）
        wordCutUI.Open(word, expected, itemId, cutsRequired, guide, addToInventory);
        UIDebug.DumpState("UIRouter.OpenWordCut.after");
        return true;
    }

    // ---- Popup（InfoTextを流用）----
    public bool ShowPopup(string reqOwner, string message, bool requireConfirm, float durationSeconds = 1.0f, System.Action onConfirm = null)
    {
        if (!modalGate) return false;
        if (!TryAcquireWithRecover(reqOwner)) return false;

        owner = reqOwner;
        current = UIKind.Popup;

        if (popupCo != null) StopCoroutine(popupCo);
        UIDebug.Log("UIRouter.ShowPopup", $"owner={reqOwner} requireConfirm={requireConfirm} sec={durationSeconds}");
        popupCo = StartCoroutine(PopupRoutine(message, requireConfirm, durationSeconds, onConfirm));
        return true;
    }

    // UIRouter.cs
    public bool IsModalOpen() => current != UIKind.None; // 追加（任意の位置）

    // 実際に入力をブロックすべき UI が表示中か？
    // - WordCut: 画面が開いていて、かつ GameObject が有効
    // - Popup:   実行中コルーチンがある間
    public bool IsBusyForInteraction()
    {
        if (current == UIKind.WordCut)
            return wordCutUI && wordCutUI.IsOpen && wordCutUI.gameObject.activeInHierarchy;
        if (current == UIKind.Popup)
            return popupCo != null;
        if (current == UIKind.Dialogue)
            return tomDialogueView && tomDialogueView.IsOpen && tomDialogueView.gameObject.activeInHierarchy;
        return false;
    }

    IEnumerator PopupRoutine(string msg, bool requireConfirm, float seconds, System.Action onConfirm)
    {
        var myOwner = owner;
        UIDebug.DumpState($"UIRouter.PopupRoutine.start owner={myOwner}");
        // 既に WordCut などで timeScale=0 の可能性がある。
        // その場合にさらに Pause すると prevTimeScale=0 を記憶してしまい、
        // Resume 後も 0 のままになる。そこで「自分が止めたか」を記憶しておく。
        bool pausedByMe = false;
        if (GameDirector.I)
        {
            if (!Mathf.Approximately(Time.timeScale, 0f))
            {
                pausedByMe = true;
                GameDirector.I.Pause(myOwner);
            }
        }
        if (wordCutUI) { wordCutUI.gameObject.SetActive(true); wordCutUI.SetInfoOnly(msg); }

        if (requireConfirm)
        {
            // 直前の決定キー（トリガ入力）を無視し、次の確定押下を待つ
            // 確定キー: Enter / KeypadEnter / E（= コントローラーA）
            yield return null; // 一旦フレームを跨ぐ
            while (Input.GetKey(KeyCode.Return) || Input.GetKey(KeyCode.KeypadEnter) || Input.GetKey(KeyCode.E))
                yield return null; // キーを離すまで待つ
            while (!(Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.E)))
                yield return null; // 次の押下を待つ
        }
        else
        {
            if (seconds < 0.01f) seconds = 1.0f;
            float t = 0f; while (t < seconds) { t += Time.unscaledDeltaTime; yield return null; }
        }

        // 片付け
        if (wordCutUI) { wordCutUI.SetInfoOnly(""); wordCutUI.gameObject.SetActive(false); }
        if (pausedByMe) GameDirector.I?.Resume(myOwner);
        modalGate?.Release(myOwner);
        UIDebug.DumpState($"UIRouter.PopupRoutine.afterRelease owner={myOwner}");

        // ここを変更: confirm の有無に関わらず onConfirm を呼ぶ
        if (onConfirm != null) { yield return null; onConfirm.Invoke(); }  // ←重要

        // 後始末
        if (current == UIKind.Popup && owner == myOwner) { current = UIKind.None; owner = null; popupCo = null; }
        UIDebug.DumpState($"UIRouter.PopupRoutine.end owner={myOwner}");
    }

    // === UIRouter.cs の末尾付近に追加 ===
    public bool OpenWordCutMulti(string reqOwner, string word, string[] expectedOptions, int cutsRequired, string guide = "")
    {
        if (!modalGate || !wordCutUI) return false;
        // 複数候補版でも、ステイルなモーダルロックを検出して回復してから取得する
        if (!TryAcquireWithRecover(reqOwner))
        {
            UIDebug.Log("UIRouter.OpenWordCutMulti", $"acquire failed owner={reqOwner}");
            return false;
        }

        UIDebug.Log("UIRouter.OpenWordCutMulti", $"owner={reqOwner} word={word} options={(expectedOptions!=null?expectedOptions.Length:0)}");
        owner = reqOwner;
        current = UIKind.WordCut;
        wordCutUI.OpenMulti(word, expectedOptions, cutsRequired, guide);
        UIDebug.DumpState("UIRouter.OpenWordCutMulti.after");
        return true;
    }

    // ---- Dialogue (TOM) ----
    public bool OpenTomDialogue(string reqOwner, TomDialogueAsset asset, System.Action onComplete, float yNormalized = -1f)
    {
        if (!modalGate) return false;
        if (!tomDialogueView) tomDialogueView = FindObjectOfType<TomDialogueView>(includeInactive: true);
        if (!tomDialogueView) return false;
        if (!TryAcquireWithRecover(reqOwner)) return false;

        owner = reqOwner;
        current = UIKind.Dialogue;
        // GameDirectorのポーズはビュー側が担当
        if (yNormalized >= 0f) { try { tomDialogueView.SetNormalizedY(yNormalized); } catch { } }
        tomDialogueView.Open(asset, reqOwner, () =>
        {
            // 閉じ時にモーダル解放
            modalGate?.Release(reqOwner);
            if (current == UIKind.Dialogue && owner == reqOwner)
            {
                current = UIKind.None;
                owner = null;
            }
            // 念のため：他UIが開いていないのにポーズが残っていれば復帰
            bool anyBusy = false;
            try { anyBusy = IsBusyForInteraction(); } catch { }
            bool menuOpen = false;
            try { menuOpen = (GameState.I != null && GameState.I.HasFlag("menu_open")); } catch { }
            if (!anyBusy && !menuOpen)
            {
                if (GameDirector.I && GameDirector.I.IsPaused)
                {
                    GameDirector.I.ForceResume();
                }
                if (Mathf.Approximately(Time.timeScale, 0f)) Time.timeScale = 1f;
            }
            onComplete?.Invoke();
        });
        return true;
    }



    public void RebindInScene()
    {
        // FindObjectOfTypeを使ってWordCutUIを再検索し、参照を更新する
        // ※WordCutUIはPersistent Sceneにいるため、この処理は不要です。
        //   UI Routerの参照を確実にするため、ここは本来WordCutUIを再検索する場所ではありませんでした。

        // 実際には、UIRouterのRefは手動アサインで問題ありません。
        // 代わりに、シーンロード後の初期化が必要なUI処理があればここに書きます。
        // 今回のケースでは、手動アサインを活かすため、このメソッドは空で構いません。
        // SafeUIRebind()を動作させるためだけに置いておきます。
    }
}  
