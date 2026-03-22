using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class WindForceApplier : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] Rigidbody2D rb;

    [Header("Force Magnitude (per FixedUpdate)")]
    [SerializeField] float level1 = 5f;   // そよ風
    [SerializeField] float level2 = 12f;  // 強風
    [SerializeField] float level3 = 26f;  // 突風

    [Header("Flags")]
    [SerializeField] string lvl1Flag = "wind_lvl1";
    [SerializeField] string lvl2Flag = "wind_lvl2";
    [SerializeField] string lvl3Flag = "wind_lvl3";
    [SerializeField] string leftFlag = "wind_dir_left";
    [SerializeField] string rightFlag = "wind_dir_right";

    [Header("Defaults")]
    [Tooltip("L1/L2で方向フラグが無いとき、この向きに押す（true=左へ押す）")]
    [SerializeField] bool defaultLeftForL12 = true;

    void Reset() { rb = GetComponent<Rigidbody2D>(); }

    void Awake()
    {
        // ① もともとの自己参照
        if (!rb) rb = GetComponent<Rigidbody2D>();

        // ② 参照が欠落していた場合のフォールバック（永続Playerを拾い直す）
        if (!rb)
        {
            var p = GameObject.FindWithTag("Player");   // ← PlayerにTag=Playerを必ず付ける
            if (p) rb = p.GetComponent<Rigidbody2D>();
        }

        if (!rb)
            Debug.LogWarning("[WindForceApplier] PlayerのRigidbody2Dが見つかりません。Playerにこのスクリプトを付けるか、rbを配線してください。");
    }

    void FixedUpdate()
    {
        var gs = GameState.I;
        if (gs == null || rb == null) return;

        // 強さ（L3優先）
        float mag =
            gs.HasFlag(lvl3Flag) ? level3 :
            gs.HasFlag(lvl2Flag) ? level2 :
            gs.HasFlag(lvl1Flag) ? level1 : 0f;

        if (mag <= 0f) return;
         
        // 向き（右風は +1 に修正）
        int dir = 0;
        if (gs.HasFlag(leftFlag)) dir = -1;
        else if (gs.HasFlag(rightFlag)) dir = -1;   // ← ここが肝

        if (dir == 0) dir = (defaultLeftForL12 ? -1 : 1);

        rb.AddForce(new Vector2(dir * mag, 0f), ForceMode2D.Force);
    }
}