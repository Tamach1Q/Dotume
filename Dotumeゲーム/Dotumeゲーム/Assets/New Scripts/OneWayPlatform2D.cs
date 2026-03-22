using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[ExecuteAlways]
public class OneWayPlatform2D : MonoBehaviour
{
    [Header("Effector 基本設定")]
    [Tooltip("上方向（法線）がどちらか。0=ワールド上。足場を回転させているなら角度で調整")]
    [Range(-180f, 180f)]
    public float rotationalOffset = 0f;

    [Tooltip("上面として扱う角度幅（度）。広いほど斜めからも乗れる。120〜180 推奨")]
    [Range(10f, 180f)]
    public float surfaceArc = 160f;

    [Tooltip("重なった一方通行足場の衝突をグループ化（推奨 ON）")]
    public bool useOneWayGrouping = true;

    [Header("コライダー自動設定")]
    [Tooltip("このオブジェクトの Collider2D を Effector 対応にする（Used By Effector = ON）")]
    public bool autoMarkColliders = true;

    [Tooltip("子のコライダーも対象にする")]
    public bool includeChildrenColliders = false;

    [Header("任意：下抜け支援")]
    [Tooltip("DropThrough を呼んだときの一時無効化秒数")]
    public float dropThroughDuration = 0.3f;

    PlatformEffector2D effector;
    readonly List<Collider2D> _colliders = new();

    void Reset()
    {
        SetupEffector();
        SetupColliders();
    }

    void OnEnable()
    {
        SetupEffector();
        SetupColliders();
        ApplyEffectorSettings();
    }

    void OnValidate()
    {
        SetupEffector();
        SetupColliders();
        ApplyEffectorSettings();
    }

    void SetupEffector()
    {
        if (!effector) effector = GetComponent<PlatformEffector2D>();
        if (!effector) effector = gameObject.AddComponent<PlatformEffector2D>();
    }

    void SetupColliders()
    {
        _colliders.Clear();

        // 自身のコライダー
        GetComponents(_colliders);

        // 子も含める
        if (includeChildrenColliders)
        {
            var childCols = GetComponentsInChildren<Collider2D>(includeInactive: true);
            foreach (var c in childCols)
                if (!_colliders.Contains(c)) _colliders.Add(c);
        }

        // 実体コライダーを Effector 対応に
        foreach (var col in _colliders)
        {
            if (!col) continue;
            if (!col.isTrigger && autoMarkColliders)
                col.usedByEffector = true;
        }
    }

    void ApplyEffectorSettings()
    {
        if (!effector) return;

        // 一方通行 有効化
        effector.useOneWay = true;
        effector.useOneWayGrouping = useOneWayGrouping;

        // 上面の向き＆許容アーク
        effector.rotationalOffset = rotationalOffset;
        effector.surfaceArc = surfaceArc;

        // あるバージョンに確実に存在する2項目だけ（不要なら消してOK）
        // ※ 一部バージョンにない/挙動が変わる可能性のある項目は設定しない
        effector.useSideBounce = false;
        effector.useSideFriction = false;
    }

    /// <summary>
    /// 指定コライダー（通常はプレイヤー）を一時的にこの足場と無視して下に抜けさせる
    /// 例）platform.DropThrough(playerCollider);
    /// </summary>
    public void DropThrough(Collider2D who)
    {
        if (!who) return;
        StartCoroutine(CoDropThrough(who, dropThroughDuration));
    }

    IEnumerator CoDropThrough(Collider2D who, float seconds)
    {
        if (_colliders.Count == 0) SetupColliders();

        foreach (var col in _colliders)
        {
            if (!col || col.isTrigger) continue;
            Physics2D.IgnoreCollision(col, who, true);
        }

        yield return new WaitForSeconds(seconds);

        foreach (var col in _colliders)
        {
            if (!col || col.isTrigger) continue;
            Physics2D.IgnoreCollision(col, who, false);
        }
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        // rotationalOffset の可視化（ざっくり）
        Vector3 p = transform.position;
        float ang = rotationalOffset * Mathf.Deg2Rad;
        Vector3 up = new(Mathf.Cos(ang + Mathf.PI / 2f), Mathf.Sin(ang + Mathf.PI / 2f), 0f);

        Gizmos.color = new Color(0.1f, 0.8f, 1f, 0.6f);
        Gizmos.DrawLine(p, p + up * 1.0f);

        // surfaceArc の扇表示（ざっくり）
        const int seg = 20;
        float start = ang + Mathf.PI / 2f - Mathf.Deg2Rad * surfaceArc * 0.5f;
        Vector3 prev = p + new Vector3(Mathf.Cos(start), Mathf.Sin(start), 0f);
        Gizmos.color = new Color(0.1f, 0.8f, 1f, 0.25f);
        for (int i = 1; i <= seg; i++)
        {
            float t = start + Mathf.Deg2Rad * surfaceArc * (i / (float)seg);
            Vector3 cur = p + new Vector3(Mathf.Cos(t), Mathf.Sin(t), 0f);
            Gizmos.DrawLine(prev, cur);
            prev = cur;
        }
    }
#endif
}
