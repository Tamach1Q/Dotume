using UnityEngine;

/// x座標のみターゲットに追従する（yは自身を維持）。
/// 追加: enableSplitMode をONにすると、Boundaryに触れた瞬間に
///       Player と me の座標を“入れ替え”ます。
///       以後、me はその場に待機し（移動不可・インタラクトも発生しない想定）、
///       Player は入れ替え先で通常どおり行動。x座標が再び一致したら元の追従に戻ります。
public class FollowXOnly2D : MonoBehaviour
{
    public Transform target;
    public float xOffset = 0f;
    bool warnedOnce;

    [Header("Split Mode (optional)")]
    [Tooltip("ONで境界より右はmeが主導、左はtargetに同期")]
    public bool enableSplitMode = false;
    [Tooltip("境界に使う薄い縦のBoxCollider2D(Trigger推奨)")]
    public Collider2D boundary;
    [Tooltip("ターゲット(Player)のメインCollider2D。未設定なら自動取得（子も探索）")]
    public Collider2D targetCollider;
    [Tooltip("再リンクの許容差(x座標)")]
    public float relinkEps = 0.05f;
    [Tooltip("復帰(再スワップ)は一度アンカーxから離れてからに制限する")]
    public bool requireLeaveOnceBeforeRelink = true;
    [Tooltip("再スワップにはPlayerとmeの物理接触(またはAABB重なり)も要求する")]
    public bool requireTouchForRelink = false;
    [Tooltip("接触の代わりに使うY方向の近接閾値（requireTouchForRelinkがONでも、Y差がこの値以下ならOK）")]
    public float relinkYEps = 0.2f;
    [Tooltip("入れ替え直前、Player が移動する先のxをこの値だけ + してから交換する（誤発火回避）。0で無効")] 
    public float preSwapPlayerDeltaX = 0.2f;
    [Tooltip("分離中は me を物理的にも完全停止（Rigidbody2Dを凍結）する")]
    public bool freezeMeWhileSwapped = true;
    [Tooltip("復帰時のmeのY座標: ON=Playerを戻す直前(復帰直前)のPlayerのYを使う")] 
    public bool rejoinUsePlayerCurrentY = true;
    [Tooltip("(旧) 復帰時のmeのY座標: ON=分離直前(初回スワップ前)のYに戻す, OFF=Playerの現在Yをコピー。rejoinUsePlayerCurrentYがONならこちらは無視されます")] 
    public bool rejoinUseSavedMeY = true;

    bool swapped;             // true=座標入れ替え中（meは待機）
    bool wasTouchingBoundary; // 接触の立ち上がりを拾うため
    Vector3 meAnchorPos;      // 入れ替え後にmeが待機する座標（= 入れ替え直後のme位置）
    bool relinkArmed;         // 一度アンカーxから離れたか
    float swappedAt;          // 入れ替え時刻（デバッグ用/安定化）
    float prevDxToAnchor;     // 直前フレームのアンカーとの差（x）
    bool hasPrevDx;
    float savedMeYBeforeSwap; // 分離前のmeのY（復帰時に戻す用）

    // 物理・自己コライダー
    Rigidbody2D selfRb;
    Collider2D selfCollider;

    [Header("Debug")]
    public bool debugLogs = false;
    public float debugInterval = 0.25f; // 秒
    float nextDebugAt;

    Rigidbody2D targetRb;

    // 凍結のための退避
    RigidbodyType2D _origBodyType;
    RigidbodyConstraints2D _origConstraints;
    bool _hasOrigRbState;

    void Awake()
    {
        selfRb = GetComponent<Rigidbody2D>();
        selfCollider = GetComponent<Collider2D>();
        if (!target)
        {
            var playerGo = GameObject.FindGameObjectWithTag("Player");
            if (playerGo) target = playerGo.transform;
            if (!target)
            {
                var pm = FindObjectOfType<PlayerMove>();
                if (pm) target = pm.transform;
            }
        }
        if (!targetRb && target) targetRb = target.GetComponent<Rigidbody2D>();
    }

    void OnEnable()
    {
        // 非アクティブ→有効化の運用でもバインドを再試行
        if (!target)
        {
            var playerGo = GameObject.FindGameObjectWithTag("Player");
            if (playerGo) target = playerGo.transform;
            if (!target)
            {
                var pm = FindObjectOfType<PlayerMove>();
                if (pm) target = pm.transform;
            }
            if (target) Debug.Log($"[FollowXOnly2D] bound target='{target.name}' on enable");
        }
        if (!targetRb && target) targetRb = target.GetComponent<Rigidbody2D>();
        // Collider 自動取得（子も含めて）
        if (!targetCollider && target)
        {
            targetCollider = target.GetComponent<Collider2D>();
            if (!targetCollider) targetCollider = target.GetComponentInChildren<Collider2D>(true);
        }
        // 立ち上がり検知の初期化
        wasTouchingBoundary = false;
        if (debugLogs)
        {
            Debug.Log($"[FollowXOnly2D] OnEnable selfRb={(selfRb? selfRb.bodyType.ToString():"-")} selfCol={(selfCollider? selfCollider.name: "-")} target={(target? target.name: "-")} targetCol={(targetCollider? targetCollider.name:"-")} boundary={(boundary? boundary.name:"-")}");
        }
    }

    void LateUpdate()
    {
        if (!target)
        {
            // まだ見つからない場合は軽く再試行（DDOLプレイヤー対策）
            var playerGo = GameObject.FindGameObjectWithTag("Player");
            if (playerGo) target = playerGo.transform;
            if (!target)
            {
                var pm = FindObjectOfType<PlayerMove>();
                if (pm) target = pm.transform;
            }
            if (target && !targetRb) targetRb = target.GetComponent<Rigidbody2D>();
            if (target && !targetCollider)
            {
                targetCollider = target.GetComponent<Collider2D>();
                if (!targetCollider) targetCollider = target.GetComponentInChildren<Collider2D>(true);
            }
            if (!target)
            {
                if (!warnedOnce)
                {
                    Debug.Log("[FollowXOnly2D] target not found yet. waiting...");
                    warnedOnce = true;
                }
                return;
            }
        }
        warnedOnce = false;
        if (!targetCollider && target)
        {
            // 遅延バインド時にも確実にコライダーを補完
            targetCollider = target.GetComponent<Collider2D>();
            if (!targetCollider) targetCollider = target.GetComponentInChildren<Collider2D>(true);
        }

        // 分割モードがOFFなら従来通り同期
        if (!enableSplitMode || !boundary)
        {
            var pos0 = transform.position;
            pos0.x = target.position.x + xOffset;
            ApplyPosition(pos0);
            swapped = false;
            SetMeLeadsFlag(false);
            SetMeSplitFlag(false);
            return;
        }

        // 触れた“瞬間”に分離へ。物理接触優先、だめならAABB重なりで代替
        bool nowTouching = false;
        if (targetCollider)
        {
            try
            {
                // TriggerでもAABBで代替
                nowTouching = boundary.IsTouching(targetCollider) || boundary.bounds.Intersects(targetCollider.bounds);
            }
            catch { nowTouching = boundary.bounds.Intersects(targetCollider.bounds); }
        }
        else
        {
            // コライダー未指定時は簡易判定（xのみ）
            float dx = Mathf.Abs(target.position.x - boundary.bounds.center.x);
            nowTouching = dx <= (boundary.bounds.extents.x + 0.01f);
        }

        // “接触の立ち上がり”で座標入れ替えを実施
        if (!swapped && !wasTouchingBoundary && nowTouching)
        {
            SwapPositions();
        }

        var pos = transform.position;
        if (!swapped)
        {
            // 左側: 同期
            pos.x = target.position.x + xOffset;
        }
        else
        {
            // 入れ替え中: me はアンカー位置に固定
            pos = new Vector3(meAnchorPos.x, meAnchorPos.y, transform.position.z);
            // x が再び一致 or 交差 したら復帰（条件付き）
            float rawDx = target.position.x - meAnchorPos.x;
            float dxAnchor = Mathf.Abs(rawDx);
            bool crossed = hasPrevDx && (Mathf.Sign(rawDx) != Mathf.Sign(prevDxToAnchor));
            if (!relinkArmed && dxAnchor > relinkEps) relinkArmed = true; // 一度離れた

            bool touchingPlayer = true; // デフォルト緩め
            if (requireTouchForRelink)
            {
                touchingPlayer = false;
                bool overlap = false;
                if (selfCollider && targetCollider)
                {
                    try { overlap = selfCollider.IsTouching(targetCollider) || selfCollider.bounds.Intersects(targetCollider.bounds); }
                    catch { overlap = selfCollider.bounds.Intersects(targetCollider.bounds); }
                }
                // y が十分近ければ“接触とみなす”
                bool yClose = Mathf.Abs(target.position.y - meAnchorPos.y) <= relinkYEps;
                touchingPlayer = overlap || yClose;
            }

            bool canRelink = (dxAnchor <= relinkEps || crossed)
                              && (!requireLeaveOnceBeforeRelink || relinkArmed)
                              && (!requireTouchForRelink || touchingPlayer);
            if (canRelink)
            {
            // いったん“元に戻す”ために再スワップ（x,yともに入れ替え）
            SwapBackToResumeFollow();
            swapped = false;
            SetMeLeadsFlag(false);
            SetMeSplitFlag(false);
            // このフレームは再スワップで確定させる（次フレームから追従へ）
            wasTouchingBoundary = nowTouching;
            return;
            }
            prevDxToAnchor = rawDx; hasPrevDx = true;
        }
        ApplyPosition(pos);

        // 接触状態を記録
        wasTouchingBoundary = nowTouching;

        // デバッグ出力
        if (debugLogs && Time.unscaledTime >= nextDebugAt)
        {
            nextDebugAt = Time.unscaledTime + debugInterval;
            bool selfTouchingBoundary = false;
            try
            {
                if (selfCollider && boundary)
                    selfTouchingBoundary = selfCollider.IsTouching(boundary) || selfCollider.bounds.Intersects(boundary.bounds);
            }
            catch { }

            float dxA = Mathf.Abs(target.position.x - meAnchorPos.x);
            bool touchingPlayer = false;
            bool overlap = false;
            try { if (selfCollider && targetCollider) overlap = selfCollider.IsTouching(targetCollider) || selfCollider.bounds.Intersects(targetCollider.bounds); } catch { }
            bool yCloseDbg = Mathf.Abs(target.position.y - meAnchorPos.y) <= relinkYEps;
            touchingPlayer = overlap || yCloseDbg;
            Debug.Log($"[FollowXOnly2D] swapped={swapped} nowTouch={nowTouching} selfTouch={selfTouchingBoundary} tgt=({target.position.x:F2},{target.position.y:F2}) me=({transform.position.x:F2},{transform.position.y:F2}) anchor=({meAnchorPos.x:F2},{meAnchorPos.y:F2}) dxA={dxA:F3} relinkArmed={relinkArmed} touching={touchingPlayer} overlap={overlap} yClose={yCloseDbg} yEps={relinkYEps:F2} requireTouch={requireTouchForRelink} rb={(selfRb? selfRb.bodyType.ToString():"-")}");
        }
    }

    void ApplyPosition(Vector3 pos)
    {
        if (selfRb && selfRb.bodyType == RigidbodyType2D.Dynamic)
        {
            // 物理に従うオブジェクトはMovePositionの方が破綻が少ない
            selfRb.MovePosition(new Vector2(pos.x, pos.y));
        }
        else
        {
            transform.position = pos;
        }
    }

    void OnDisable()
    {
        // 念のためフラグを片付ける
        SetMeLeadsFlag(false);
        SetMeSplitFlag(false);
    }

    void SwapPositions()
    {
        if (!target) return;
        Vector3 mePos = transform.position;
        Vector3 plPos = target.position;
        // 分離前の me のYを保持（復帰時に戻すため）
        savedMeYBeforeSwap = mePos.y;

        // 先にPlayerを me の位置へ（x,yともに）。
        // 交換直前に Player が向かうxを僅かに+して、直後のx一致による誤発火を避ける。
        if (preSwapPlayerDeltaX != 0f)
            mePos.x += preSwapPlayerDeltaX;

        // 先にPlayerを me の位置へ（x,yともに）
        if (targetRb)
        {
            targetRb.velocity = Vector2.zero;
            targetRb.position = new Vector2(mePos.x, mePos.y);
        }
        else
        {
            target.position = new Vector3(mePos.x, mePos.y, target.position.z);
        }

        // me を Player の元の位置へ（x,yともに）
        // 注意: Rigidbody2D.position の代入は Transform の反映が同フレームで遅れる場合があるため、
        //       アンカーは plPos ベースで確定し、必要に応じ Transform も同期する。
        if (selfRb)
        {
            selfRb.velocity = Vector2.zero;
            // Dynamic/Kinematic いずれも position 代入でワープ
            selfRb.position = new Vector2(plPos.x, plPos.y);
            // Transform も同期（物理と同位置）。一度だけのテレポートなので競合リスクは低い。
            transform.position = new Vector3(plPos.x, plPos.y, transform.position.z);
        }
        else
        {
            transform.position = new Vector3(plPos.x, plPos.y, transform.position.z);
        }

        // 入れ替え後、me はその場に固定（アンカーは plPos 起点で確定）
        meAnchorPos = new Vector3(plPos.x, plPos.y, transform.position.z);
        swapped = true;
        relinkArmed = false;
        swappedAt = Time.unscaledTime;
        prevDxToAnchor = 0f; hasPrevDx = false; // 次フレームから交差判定開始
        SetMeLeadsFlag(true);
        SetMeSplitFlag(true);

        // 分離中は me を物理的にも完全停止
        if (freezeMeWhileSwapped)
            FreezeMe(true);

        if (debugLogs)
            Debug.Log($"[FollowXOnly2D] SwapPositions done: me@{meAnchorPos.x:F2},{meAnchorPos.y:F2} player@{(targetRb? targetRb.position.x : target.position.x):F2},{(targetRb? targetRb.position.y : target.position.y):F2}");
    }

    // 復帰用：プレイヤーをアンカーへ、meをプレイヤーの現在位置へ（x,yともに）
    void SwapBackToResumeFollow()
    {
        if (!target) return;
        Vector3 curMe = transform.position;
        Vector3 curPl = target.position;

        // Player → アンカー（元のPlayer位置）
        if (targetRb)
        {
            targetRb.velocity = Vector2.zero;
            targetRb.position = new Vector2(meAnchorPos.x, meAnchorPos.y);
        }
        else
        {
            target.position = new Vector3(meAnchorPos.x, meAnchorPos.y, target.position.z);
        }

        // me → 復帰時のXはPlayerの現在Xに合わせる。Yは優先順位で決定：
        //  1) rejoinUsePlayerCurrentY がONなら、Playerを戻す直前のY（curPl.y）
        //  2) それ以外で rejoinUseSavedMeY がONなら、分離直前の me のY
        //  3) デフォルトは curPl.y
        float meY = rejoinUsePlayerCurrentY ? curPl.y : (rejoinUseSavedMeY ? savedMeYBeforeSwap : curPl.y);
        if (selfRb)
        {
            selfRb.velocity = Vector2.zero;
            selfRb.position = new Vector2(curPl.x, meY);
            // Transform も同期（同フレームに見た目が正しくなるように）
            transform.position = new Vector3(curPl.x, meY, transform.position.z);
        }
        else
        {
            transform.position = new Vector3(curPl.x, meY, transform.position.z);
        }

        if (debugLogs)
            Debug.Log($"[FollowXOnly2D] SwapBack done: me@{transform.position.x:F2},{transform.position.y:F2} player@{(targetRb? targetRb.position.x : target.position.x):F2},{(targetRb? targetRb.position.y : target.position.y):F2}");

        // 凍結解除（元の物理設定に戻す）
        if (freezeMeWhileSwapped)
            FreezeMe(false);
    }

    void SetMeLeadsFlag(bool v)
    {
        try
        {
            if (GameState.I != null) GameState.I.SetFlag("me_leads", v);
        }
        catch { /* ignore */ }
    }

    void SetMeSplitFlag(bool v)
    {
        try
        {
            if (GameState.I != null) GameState.I.SetFlag("me_split_active", v);
        }
        catch { /* ignore */ }
    }

    void FreezeMe(bool freeze)
    {
        if (!selfRb) return;

        if (freeze)
        {
            if (!_hasOrigRbState)
            {
                _origBodyType = selfRb.bodyType;
                _origConstraints = selfRb.constraints;
                _hasOrigRbState = true;
            }

            selfRb.velocity = Vector2.zero;
            selfRb.angularVelocity = 0f;
            // Kinematic + FreezeAll で“完全停止”（外力で動かない）
            selfRb.bodyType = RigidbodyType2D.Kinematic;
            selfRb.constraints = RigidbodyConstraints2D.FreezeAll;
        }
        else
        {
            if (_hasOrigRbState)
            {
                selfRb.constraints = _origConstraints;
                selfRb.bodyType = _origBodyType;
            }
        }
    }
}
