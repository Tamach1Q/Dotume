using UnityEngine;

/// 永続オブジェクト（DontDestroyOnLoad）の参照を自動で取得・設定するヘルパー
public static class PersistentObjectHelper
{
    /// PlayerのTransformを取得
    public static Transform GetPlayer()
    {
        var player = GameObject.FindWithTag("Player");
        return player ? player.transform : null;
    }

    /// PlayerのRigidbody2Dを取得
    public static Rigidbody2D GetPlayerRigidbody()
    {
        var player = GameObject.FindWithTag("Player");
        return player ? player.GetComponent<Rigidbody2D>() : null;
    }

    /// PlayerのBoxCollider2Dを取得
    public static BoxCollider2D GetPlayerBoxCollider()
    {
        var player = GameObject.FindWithTag("Player");
        return player ? player.GetComponent<BoxCollider2D>() : null;
    }

    /// Main Cameraを取得
    public static Camera GetMainCamera()
    {
        return Camera.main;
    }

    /// CameraFollow2D_RightScrollコンポーネントを取得
    public static CameraFollow2D_RightScroll GetCameraFollow()
    {
        var cam = GetMainCamera();
        if (cam) return cam.GetComponent<CameraFollow2D_RightScroll>();
        return null;
    }


    public static void AutoAssignPlayerReferences(MonoBehaviour component)
    {
        if (component == null) return;

        var type = component.GetType();

        // ★ UseZone系のコンポーネントは除外する（Triggerで自動的に取得するため）
        var typeName = type.Name;
        if (typeName.Contains("UseZone") || typeName.Contains("TriggerZone"))
        {
            Debug.Log($"[PersistentObjectHelper] Skipped auto-assign for {typeName} (uses trigger-based detection)");
            return;
        }

        var fields = type.GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        foreach (var field in fields)
        {
            // フィールドがnullの場合のみ設定（既存の参照を上書きしない）
            var currentValue = field.GetValue(component);
            if (currentValue != null) continue;

            if (field.FieldType == typeof(Transform) &&
                (field.Name.ToLower().Contains("player") || field.Name.ToLower().Contains("target")))
            {
                var player = GetPlayer();
                if (player != null)
                {
                    field.SetValue(component, player);
                    Debug.Log($"[PersistentObjectHelper] Auto-assigned Player to {component.name}.{field.Name}");
                }
            }
            else if (field.FieldType == typeof(Rigidbody2D) &&
                     field.Name.ToLower().Contains("player"))
            {
                var playerRb = GetPlayerRigidbody();
                if (playerRb != null)
                {
                    field.SetValue(component, playerRb);
                    Debug.Log($"[PersistentObjectHelper] Auto-assigned Player Rigidbody2D to {component.name}.{field.Name}");
                }
            }
            else if (field.FieldType == typeof(BoxCollider2D) &&
                     field.Name.ToLower().Contains("player"))
            {
                var playerCollider = GetPlayerBoxCollider();
                if (playerCollider != null)
                {
                    field.SetValue(component, playerCollider);
                    Debug.Log($"[PersistentObjectHelper] Auto-assigned Player BoxCollider2D to {component.name}.{field.Name}");
                }
            }
            else if (field.FieldType == typeof(Camera) &&
                     field.Name.ToLower().Contains("camera"))
            {
                var camera = GetMainCamera();
                if (camera != null)
                {
                    field.SetValue(component, camera);
                    Debug.Log($"[PersistentObjectHelper] Auto-assigned Main Camera to {component.name}.{field.Name}");
                }
            }
            else if (field.FieldType == typeof(CameraFollow2D_RightScroll) &&
                     field.Name.ToLower().Contains("camera"))
            {
                var cameraFollow = GetCameraFollow();
                if (cameraFollow != null)
                {
                    field.SetValue(component, cameraFollow);
                    Debug.Log($"[PersistentObjectHelper] Auto-assigned CameraFollow to {component.name}.{field.Name}");
                }
            }
        }
    }
}
