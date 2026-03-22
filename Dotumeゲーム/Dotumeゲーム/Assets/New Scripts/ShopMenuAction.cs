using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class ShopMenuAction : ActionBase
{
    public enum ShopKind { Vending, Bar }

    [System.Serializable]
    public class Item
    {
        public string id;            // インベントリID（通常商品のみ）
        public string displayName;   // 表示名
        public int priceYen = 0;     // 価格

        // WordCut系（必要な場合のみ）
        public bool useWordCut = false;
        public string word;          // 例: "wine"
        public string expected;      // 例: "win"

        public Item() {}
        public Item(string id, string name, int price)
        { this.id = id; this.displayName = name; this.priceYen = price; this.useWordCut = false; }
        public static Item Cut(string name, int price, string word, string expected)
        { return new Item { id = "", displayName = name, priceYen = price, useWordCut = true, word = word, expected = expected }; }
    }

    [Header("Shop Setup")]
    public ShopKind kind = ShopKind.Vending;
    public bool useDefaultItems = true;
    public List<Item> items = new();

    [Header("UI")] public string owner = "Shop";

    [Header("Action Hooks")]
    [Tooltip("お金不足（購入不可）時に実行するアクション群。TomEncounterActionなどを指定可能。")]
    [SerializeField] ActionBase[] actionsOnInsufficientCash;
    [Tooltip("お金不足アクション実行前にショップUIを閉じる（推奨）")]
    [SerializeField] bool closeMenuBeforeInsufficientActions = true;

    [Tooltip("cash（cashews）を“初めて”購入した時、WordCut完了〜報酬付与の合間に実行するアクション群")]
    [SerializeField] ActionBase[] actionsOnFirstCashBetweenWordcut;
    [Tooltip("上記アクション実行前にメニューを閉じる（Tom会話を全面に出したい場合にON）")]
    [SerializeField] bool closeMenuBeforeFirstCashActions = false;

    [Header("Restart Carryover (optional)")]
    [Tooltip("再スタート時に持ち越すアイテム/フラグを明示指定したい場合にON")] 
    [SerializeField] bool overrideCarryOnRestart = false;
    [Tooltip("overrideCarryOnRestart がONの時、この配列に含まれる itemId だけを持ち越す")] 
    [SerializeField] string[] carryItemsOnRestart;
    [Tooltip("overrideCarryOnRestart がONの時、この配列に含まれる flagId だけを持ち越す")] 
    [SerializeField] string[] carryFlagsOnRestart;

    [Header("Custom Menu UI (optional)")]
    [SerializeField] GameObject menuRoot;         // メニュー一式をまとめたルート（任意）
    [SerializeField] TMP_Text menuText;           // 選択一覧の表示先（infoTextの代わり）
    [SerializeField] GameObject backgroundObject; // 背景テクスチャ（SpriteRenderer等をアタッチしておく）

    // Barの特殊分岐用フラグID
    const string FLAG_CASHEWS_SOLD = "cashews_sold_once";      // 初回購入済み
    const string FLAG_CASHEWS_RESTOCKED = "cashews_restocked"; // 再販済み

    bool lastWordCutCancelled = false;
    bool lastWordCutShouldCloseMenu = false;
    bool lastChoiceYes = false;
    static readonly KeyCode[] PurchaseYesKeys = { KeyCode.A, KeyCode.Return, KeyCode.KeypadEnter, KeyCode.E, KeyCode.JoystickButton0 };
    static readonly KeyCode[] PurchaseNoKeys = { KeyCode.B, KeyCode.N, KeyCode.JoystickButton1 };

    public override void Execute()
    {
        if (!isActiveAndEnabled) return;
        StartCoroutine(Run());
    }

    IEnumerator Run()
    {
        if (GameState.I == null) yield break;

        // デフォルト品揃え
        if (useDefaultItems) BuildDefaults();

        // メニュー用リスト（このセッション内での一時変更は list 側のみ）
        var list = new List<Item>(items);

        // UI開始（カスタムメニュー優先。未設定ならWordCutUIのPromptでフォールバック）
        var ui = WordCutUI.Instance;
        int index = 0;
        bool useCustomUi = (menuText != null);
        // インベントリの開閉をブロック
        InventoryOverlay.BlockOpen = true;
        if (useCustomUi)
        {
            if (menuRoot) menuRoot.SetActive(true);
            if (backgroundObject) backgroundObject.SetActive(true);
            menuText.text = Render(list, index);
            // フォールバックのinfoText側にメニューが残らないよう明示的に隠す
            if (ui) ui.HidePrompt();
        }
        else
        {
            if (ui) ui.ShowPrompt(Render(list, index));
        }
        // メニュー開放フラグ（トリガ/しゃがみ抑止用）
        GameState.I?.SetFlag("menu_open", true);
        GameDirector.I?.Pause(owner);

        // 開いたキーの“同フレームの確定”を無視する
        bool suppressConfirmUntilRelease = true;

        while (true)
        {
            // ナビ
            if (Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.LeftArrow))
            {
                index = (index - 1 + list.Count) % list.Count;
                if (useCustomUi) { if (menuText) menuText.text = Render(list, index); }
                else { if (ui) ui.ShowPrompt(Render(list, index)); }
            }
            if (Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.RightArrow))
            {
                index = (index + 1) % list.Count;
                if (useCustomUi) { if (menuText) menuText.text = Render(list, index); }
                else { if (ui) ui.ShowPrompt(Render(list, index)); }
            }

            // 開いた直後の確定キー離し待ち
            if (suppressConfirmUntilRelease)
            {
                bool anyHeld = Input.GetKey(KeyCode.Return) || Input.GetKey(KeyCode.KeypadEnter) || Input.GetKey(KeyCode.E);
                if (!anyHeld) suppressConfirmUntilRelease = false;
            }
            else if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.E))
            {
                var it = list[index];
                yield return ShowChoicePrompt($"buy {it.displayName}? A(A) / B(B)", PurchaseYesKeys, PurchaseNoKeys);
                if (useCustomUi) { if (menuText) menuText.text = Render(list, index); }
                else { if (ui) ui.ShowPrompt(Render(list, index)); }
                if (!lastChoiceYes)
                {
                    yield return ShowConfirmPopup("...");
                    if (useCustomUi) { if (menuText) menuText.text = Render(list, index); }
                    else { if (ui) ui.ShowPrompt(Render(list, index)); }
                    suppressConfirmUntilRelease = true;
                    continue;
                }
                if (GameState.I.CashYen < it.priceYen)
                {
                    if (IsCashewDeadlockAttempt(it))
                    {
                        CloseUI();
                        yield return ShowCashewDeadlockSequence();
                        yield break;
                    }
                    // 不足時: モーダルPopupで明示的に止め、Returnで閉じる
                    yield return ShowNeedMoreCashPopup();
                    if (useCustomUi) { if (menuText) menuText.text = Render(list, index); }
                    else { if (ui) ui.ShowPrompt(Render(list, index)); }
                    suppressConfirmUntilRelease = true;

                    // 追加: Inspectorから“購入不可時の後続アクション”を実行可能に
                    if (actionsOnInsufficientCash != null && actionsOnInsufficientCash.Length > 0)
                    {
                        if (closeMenuBeforeInsufficientActions) CloseUI();
                        foreach (var a in actionsOnInsufficientCash) if (a) a.Execute();
                        // UIを閉じた場合、ここで終了（後続アクションにUI/入力を任せる）
                        if (closeMenuBeforeInsufficientActions) yield break;
                    }
                }
                else
                {
                    if (it.useWordCut)
                    {
                        // wine / whiskey / beer / cashews の分岐
                        string n = it.displayName.ToLower();
                        if (n.Contains("wine"))
                        {
                            lastWordCutShouldCloseMenu = false;
                            yield return HandleWine(it);
                            if (lastWordCutCancelled)
                            {
                                lastWordCutCancelled = false;
                                if (useCustomUi) { if (menuText) menuText.text = Render(list, index); }
                                else { if (ui) ui.ShowPrompt(Render(list, index)); }
                                continue;
                            }
                            if (lastWordCutShouldCloseMenu)
                            {
                                lastWordCutShouldCloseMenu = false;
                                CloseUI();
                                yield break; // Titleへ
                            }
                            if (useCustomUi) { if (menuText) menuText.text = Render(list, index); }
                            else { if (ui) ui.ShowPrompt(Render(list, index)); }
                            continue;
                        }
                        else if (n.Contains("whiskey"))
                        {
                            lastWordCutShouldCloseMenu = false;
                            yield return HandleWhiskey(it);
                            if (lastWordCutCancelled)
                            {
                                lastWordCutCancelled = false;
                                if (useCustomUi) { if (menuText) menuText.text = Render(list, index); }
                                else { if (ui) ui.ShowPrompt(Render(list, index)); }
                                continue;
                            }
                            if (lastWordCutShouldCloseMenu)
                            {
                                lastWordCutShouldCloseMenu = false;
                                CloseUI();
                                yield break;
                            }
                            // 続けて買い物できる
                            if (useCustomUi) { if (menuText) menuText.text = Render(list, index); }
                            else { if (ui) ui.ShowPrompt(Render(list, index)); }
                            continue;
                        }
                        else if (n.Contains("beer"))
                        {
                            lastWordCutShouldCloseMenu = false;
                            yield return HandleBeer(it);
                            if (lastWordCutCancelled)
                            {
                                lastWordCutCancelled = false;
                                if (useCustomUi) { if (menuText) menuText.text = Render(list, index); }
                                else { if (ui) ui.ShowPrompt(Render(list, index)); }
                                continue;
                            }
                            if (lastWordCutShouldCloseMenu)
                            {
                                lastWordCutShouldCloseMenu = false;
                                CloseUI();
                                yield break;
                            }
                            if (useCustomUi) { if (menuText) menuText.text = Render(list, index); }
                            else { if (ui) ui.ShowPrompt(Render(list, index)); }
                            continue;
                        }
                        else if (n.Contains("cashew"))
                        {
                            lastWordCutShouldCloseMenu = false;
                            yield return HandleCashews(it);
                            if (lastWordCutCancelled)
                            {
                                lastWordCutCancelled = false;
                                if (useCustomUi) { if (menuText) menuText.text = Render(list, index); }
                                else { if (ui) ui.ShowPrompt(Render(list, index)); }
                                continue;
                            }
                            if (lastWordCutShouldCloseMenu)
                            {
                                lastWordCutShouldCloseMenu = false;
                                CloseUI();
                                yield break;
                            }
                            // 初回購入フラグ（再販の発動条件として使用）
                            GameState.I.SetFlag(FLAG_CASHEWS_SOLD, true);
                            // 一時的にメニューから消す（セッション内のみ）
                            list.RemoveAt(index);
                            if (list.Count == 0) { CloseUI(); yield break; }
                            index %= list.Count;
                            if (useCustomUi) { if (menuText) menuText.text = Render(list, index); }
                            else { if (ui) ui.ShowPrompt(Render(list, index)); }
                        }
                        else
                        {
                            lastWordCutShouldCloseMenu = false;
                            // デフォルト：成功したらアイテム付与
                            yield return HandleGenericWordcut(it);
                            if (lastWordCutCancelled)
                            {
                                lastWordCutCancelled = false;
                                if (useCustomUi) { if (menuText) menuText.text = Render(list, index); }
                                else { if (ui) ui.ShowPrompt(Render(list, index)); }
                                continue;
                            }
                            if (lastWordCutShouldCloseMenu)
                            {
                                lastWordCutShouldCloseMenu = false;
                                CloseUI();
                                yield break;
                            }
                            if (useCustomUi) { if (menuText) menuText.text = Render(list, index); }
                            else { if (ui) ui.ShowPrompt(Render(list, index)); }
                            continue;
                        }
                    }
                    else
                    {
                        // 通常アイテム
                        GameState.I.TrySpendCash(it.priceYen);
                        if (!string.IsNullOrEmpty(it.id)) GameState.I.Add(it.id);
                        if (useCustomUi) { if (menuText) menuText.text = Render(list, index); }
                        else { if (ui) ui.ShowPrompt(Render(list, index)); }
                        if (ui != null)
                        {
                            bool requireConfirm = (kind == ShopKind.Vending);
                            float duration = requireConfirm ? 0f : 1.0f;
                            ui.ShowToast($"got {it.displayName}!", requireConfirm, duration);
                        }
                    }
                }
            }

            // 閉じる（N / I / Esc）
            if (Input.GetKeyDown(KeyCode.N) || Input.GetKeyDown(KeyCode.Escape))
            {
                CloseUI();
                yield break;
            }

            yield return null;
        }
    }

    void CloseUI()
    {
        var ui = WordCutUI.Instance;
        // カスタムUIを閉じる
        if (menuRoot) menuRoot.SetActive(false);
        if (backgroundObject) backgroundObject.SetActive(false);
        // フォールバックUIを閉じる
        if (ui) ui.HidePrompt();
        // メニュー開放フラグ解除
        GameState.I?.SetFlag("menu_open", false);
        GameDirector.I?.Resume(owner);
        // インベントリブロック解除
        InventoryOverlay.BlockOpen = false;
    }

    bool IsCashewDeadlockAttempt(Item it)
    {
        if (it == null || GameState.I == null) return false;
        string name = it.displayName ?? "";
        bool isCashew = name.ToLower().Contains("cashew");
        if (!isCashew) return false;
        if (!GameState.I.HasFlag(FLAG_CASHEWS_SOLD)) return false;
        if (!GameState.I.HasFlag(FLAG_CASHEWS_RESTOCKED)) return false;
        return true;
    }

    string Render(List<Item> list, int index)
    {
        System.Text.StringBuilder sb = new();
        sb.AppendLine(kind == ShopKind.Vending ? "Vending Machine" : "Bar");
        sb.Append("\n");
        sb.AppendLine();
        for (int i = 0; i < list.Count; i++)
        {
            var it = list[i];
            string mark = (i == index) ? "> " : "  ";
            sb.Append(mark).Append(it.displayName).Append("  ¥").Append(it.priceYen).AppendLine();
        }
        sb.AppendLine();
        sb.Append("\n");
        sb.Append("A to Return, B to close");
        return sb.ToString();
    }

    void BuildDefaults()
    {
        items.Clear();
        if (kind == ShopKind.Vending)
        {
            items.Add(new Item("water",  "water ", 800));
            items.Add(new Item("soda",   "soda  ", 400));
            items.Add(new Item("cola",   "cola  ", 250));
            items.Add(new Item("tea",    "tea   ", 300));
            items.Add(new Item("coffee", "coffee", 300));
        }
        else // Bar
        {
            items.Add(new Item("water",    "water",    800));
            items.Add(new Item("vodka",    "vodka",    600));
            items.Add(Item.Cut("wine",      600, "wine",    "win"));
            items.Add(Item.Cut("whiskey",   600, "whiskey", "key"));
            items.Add(Item.Cut("beer",      600, "beer",    "bee"));
            // cashews は特殊ロジック：
            // - 初回購入後〜再販成立までは“品切れ”として非表示
            // - 再販成立後は価格 600 に変更して再度販売可能
            bool soldOnce = (GameState.I != null && GameState.I.HasFlag(FLAG_CASHEWS_SOLD));
            bool restocked = (GameState.I != null && GameState.I.HasFlag(FLAG_CASHEWS_RESTOCKED));
            if (!(soldOnce && !restocked))
            {
                int cashewPrice = restocked ? 600 : 200;
                items.Add(Item.Cut("cashewnsuts", cashewPrice, "cash", "cash"));
            }
        }
    }

    IEnumerator ShowNeedMoreCashPopup()
    {
        // 既存のモーダルがあれば閉じるまで待つ
        while (UIRouter.I != null && UIRouter.I.IsModalOpen()) yield return null;
        // Popupを開く（Return/Eで閉じる）。GameDirectorのPauseはUIRouter側で面倒を見ます。
        bool opened = UIRouter.I != null && UIRouter.I.ShowPopup(owner, "You need more cash.", true, 0f, null);
        if (opened)
        {
            // 閉じるまで待つ
            while (UIRouter.I != null && UIRouter.I.IsModalOpen()) yield return null;
        }
        else
        {
            // 予防：開けなかった場合はWordCutUIのトーストでフォールバック
            var ui = WordCutUI.Instance;
            ui?.ShowToast("You need more cash.", false, 1.2f);
            yield return null;
        }
    }

    IEnumerator HandleGenericWordcut(Item it)
    {
        UIRouter.I?.OpenWordCut(owner, it.word, it.expected, string.IsNullOrEmpty(it.id) ? it.expected : it.id, 1, "");
        yield return WaitForWordCutOrCancel(kind == ShopKind.Bar);
        if (lastWordCutCancelled) yield break;
        if (!TrySpendForWordcut(it)) yield break;
    }

    IEnumerator HandleWine(Item it)
    {
        // win 成功で Title へ（完全遷移）
        UIRouter.I?.OpenWordCut(owner, "wine", "win", "", 1, "");
        yield return WaitForWordCutOrCancel(kind == ShopKind.Bar);
        if (lastWordCutCancelled) yield break;
        if (!TrySpendForWordcut(it)) yield break;

        yield return ShowConfirmPopup("You have achieved a special victory.");
        yield return ShowChoicePrompt(
            "Continue? A / B",
            new[] { KeyCode.A, KeyCode.E, KeyCode.JoystickButton0 },
            new[] { KeyCode.B, KeyCode.N, KeyCode.JoystickButton1 });

        if (lastChoiceYes)
        {
            lastWordCutShouldCloseMenu = true;
            var pol = new CarryPolicy { keepAllItems = false, keepAllFlags = false };
            SceneTransitionKit.Load("ClearScene", pol, 0f);
        }
        else
        {
            yield return ShowConfirmPopup("...");
            Refund(it.priceYen);
        }
    }

    IEnumerator HandleWhiskey(Item it)
    {
        UIRouter.I?.OpenWordCut(owner, "whiskey", "key", "key", 1, "");
        yield return WaitForWordCutOrCancel(kind == ShopKind.Bar);
        if (lastWordCutCancelled) yield break;
        if (!TrySpendForWordcut(it)) yield break;
        // 成功時、GameState.Add("key") はWordCutUI内の処理で付与済み（itemId指定）。
        yield return null;
    }

    IEnumerator HandleBeer(Item it)
    {
        UIRouter.I?.OpenWordCut(owner, "beer", "bee", "bee", 1, "");
        yield return WaitForWordCutOrCancel(kind == ShopKind.Bar);
        if (lastWordCutCancelled) yield break;
        if (!TrySpendForWordcut(it)) yield break;

        yield return ShowConfirmPopup("got stung by a bee.");
        yield return ShowConfirmPopup("Press A to restart.");

        lastWordCutShouldCloseMenu = true;
        GameState.I?.SetCash(0);
        var pol = BuildRestartCarryPolicy();
        SceneTransitionKit.Load("Stage_Field4", pol, 0f);
    }

    IEnumerator HandleCashews(Item it)
    {
        UIRouter.I?.OpenWordCut(owner, "cashewnuts", "cash", "cash", 1, "");
        yield return WaitForWordCutOrCancel(kind == ShopKind.Bar);
        if (lastWordCutCancelled) yield break;

        // 初回購入時のみ、WordCut終了〜報酬の“合間”にアクションを差し込む（cashews専用）
        bool isFirstCash_cashew = false;
        try { isFirstCash_cashew = (GameState.I != null && !GameState.I.HasFlag(FLAG_CASHEWS_SOLD)); } catch { }
        if (isFirstCash_cashew && actionsOnFirstCashBetweenWordcut != null && actionsOnFirstCashBetweenWordcut.Length > 0)
        {
            if (closeMenuBeforeFirstCashActions)
            {
                CloseUI();
                // メインループ側に「閉じるべき」ことを知らせる
                lastWordCutShouldCloseMenu = true;
            }
            foreach (var a in actionsOnFirstCashBetweenWordcut) if (a) a.Execute();
            // Tomの会話などモーダルが閉じるまで待機
            while (UIRouter.I != null && UIRouter.I.IsModalOpen()) yield return null;
        }
        // 料金の徴収（失敗なら終了）
        if (!TrySpendForWordcut(it)) yield break;

        // 初回購入フラグを立てる（再販監視用）。以後は品切れ扱い（BuildDefaults側で非表示）。
        if (GameState.I != null && !GameState.I.HasFlag(FLAG_CASHEWS_SOLD))
            GameState.I.AddFlag(FLAG_CASHEWS_SOLD);

        // 報酬付与
        GameState.I?.AddCash(1000);
        if (UIRouter.I != null)
        {
            bool opened = UIRouter.I.ShowPopup(owner, "got ¥1000!", false, 1.2f, null);
            if (opened)
            {
                while (UIRouter.I != null && UIRouter.I.IsModalOpen()) yield return null;
            }
        }
        else
        {
            WordCutUI.Instance?.ShowToast("got ¥1000!", false, 1.2f);
            yield return null;
        }
    }

    IEnumerator ShowCashewDeadlockSequence()
    {
        // 「cashが買えない」告知
        while (UIRouter.I != null && UIRouter.I.IsModalOpen()) yield return null;
        bool opened = UIRouter.I != null && UIRouter.I.ShowPopup(owner, "cashが買えない", true, 0f, null);
        if (opened)
        {
            while (UIRouter.I != null && UIRouter.I.IsModalOpen()) yield return null;
        }
        else
        {
            WordCutUI.Instance?.ShowToast("cashが買えない", true, 0f);
            yield return null;
        }

        // 詰み演出：リスタート促し
        while (UIRouter.I != null && UIRouter.I.IsModalOpen()) yield return null;
        bool restartOpened = UIRouter.I != null && UIRouter.I.ShowPopup(owner, "Press A to restart.", true, 0f, onConfirm: () =>
        {
            GameState.I?.SetCash(0);
            var pol = BuildRestartCarryPolicy();
            SceneTransitionKit.Load("Stage_Field4", pol, 0f);
        });
        if (restartOpened)
        {
            while (UIRouter.I != null && UIRouter.I.IsModalOpen()) yield return null;
        }
        else
        {
            GameState.I?.SetCash(0);
            var pol = BuildRestartCarryPolicy();
            SceneTransitionKit.Load("Stage_Field4", pol, 0f);
        }
    }

    IEnumerator WaitForWordCutOrCancel(bool allowCancel)
    {
        lastWordCutCancelled = false;
        while (true)
        {
            var ui = WordCutUI.Instance;
            if (ui == null || !ui.IsActive) break;

            if (allowCancel && Input.GetKeyDown(KeyCode.N))
            {
                lastWordCutCancelled = true;
                UIRouter.I?.ForceCloseAll();
                break;
            }

            yield return null;
        }

        // WordCutが閉じ切るまで安全に待機
        while (!lastWordCutCancelled && WordCutUI.Instance != null && WordCutUI.Instance.IsActive)
            yield return null;
    }

    bool TrySpendForWordcut(Item it)
    {
        if (GameState.I == null || it == null) return false;
        if (!GameState.I.TrySpendCash(it.priceYen))
        {
            Debug.LogWarning($"[ShopMenuAction] Failed to spend ¥{it?.priceYen} for {it?.displayName}");
            return false;
        }
        return true;
    }

    IEnumerator ShowConfirmPopup(string message)
    {
        while (UIRouter.I != null && UIRouter.I.IsModalOpen()) yield return null;
        bool opened = UIRouter.I != null && UIRouter.I.ShowPopup(owner, message, true, 0f, null);
        if (opened)
        {
            while (UIRouter.I != null && UIRouter.I.IsModalOpen()) yield return null;
        }
        else
        {
            yield return ShowFallbackPrompt(message);
        }
    }

    IEnumerator ShowChoicePrompt(string message, KeyCode[] yesKeys, KeyCode[] noKeys)
    {
        lastChoiceYes = false;
        var ui = WordCutUI.Instance;
        if (ui)
        {
            ui.gameObject.SetActive(true);
            ui.SetInfoOnly(message);
        }
        // 直前の入力（購入キー押下）を無視するため、一度キーが離されるまで待つ
        bool waitForRelease = true;
        while (true)
        {
            if (waitForRelease)
            {
                bool stillHeld = IsAnyKeyHeld(yesKeys) || IsAnyKeyHeld(noKeys);
                if (!stillHeld) waitForRelease = false;
                yield return null;
                continue;
            }
            if (IsAnyKeyDown(yesKeys)) { lastChoiceYes = true; break; }
            if (IsAnyKeyDown(noKeys)) { lastChoiceYes = false; break; }
            yield return null;
        }

        if (ui)
        {
            ui.SetInfoOnly("");
            ui.gameObject.SetActive(false);
        }
        yield return null;
    }

    IEnumerator ShowFallbackPrompt(string message)
    {
        var ui = WordCutUI.Instance;
        if (ui)
        {
            ui.gameObject.SetActive(true);
            ui.SetInfoOnly(message + "\nPress A to continue.");
        }
        while (true)
        {
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.E))
                break;
            yield return null;
        }
        if (ui)
        {
            ui.SetInfoOnly("");
            ui.gameObject.SetActive(false);
        }
        yield return null;
    }

    bool IsAnyKeyDown(KeyCode[] keys)
    {
        if (keys == null) return false;
        foreach (var k in keys)
            if (Input.GetKeyDown(k)) return true;
        return false;
    }

    bool IsAnyKeyHeld(KeyCode[] keys)
    {
        if (keys == null) return false;
        foreach (var k in keys)
            if (Input.GetKey(k)) return true;
        return false;
    }

    void Refund(int amount)
    {
        if (GameState.I != null && amount > 0) GameState.I.AddCash(amount);
    }

    CarryPolicy BuildRestartCarryPolicy()
    {
        if (overrideCarryOnRestart)
        {
            return new CarryPolicy
            {
                keepAllItems = false,
                keepItems = carryItemsOnRestart,
                keepAllFlags = false,
                keepFlags = carryFlagsOnRestart
            };
        }
        // 既存挙動: 何も持ち越さない
        return new CarryPolicy { keepAllItems = false, keepAllFlags = false };
    }
}
