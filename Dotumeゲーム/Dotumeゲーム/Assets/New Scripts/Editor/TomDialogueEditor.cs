using UnityEditor;
using UnityEngine;

// 代替の作成メニュー（Create > TOM が見えない場合のフォールバック）
public static class TomDialogueAssetMenu
{
    [MenuItem("Assets/Create/TOM/Dialogue Asset (Alt)", priority = 11)]
    public static void CreateDialogueAsset()
    {
        var asset = ScriptableObject.CreateInstance<TomDialogueAsset>();
        asset.id = "tom_new_dialogue";
        asset.displayName = "TOM";
        asset.lines = new TomLine[] { new TomLine { text = "..." } };

        // 選択中のフォルダを優先
        string path = "Assets";
        foreach (var obj in Selection.GetFiltered(typeof(Object), SelectionMode.Assets))
        {
            var p = AssetDatabase.GetAssetPath(obj);
            if (!string.IsNullOrEmpty(p))
            {
                if (System.IO.Directory.Exists(p)) { path = p; break; }
                else { path = System.IO.Path.GetDirectoryName(p); break; }
            }
        }

        string filePath = AssetDatabase.GenerateUniqueAssetPath(System.IO.Path.Combine(path, "TomDialogueAsset.asset"));
        AssetDatabase.CreateAsset(asset, filePath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        EditorGUIUtility.PingObject(asset);
        Selection.activeObject = asset;
        Debug.Log($"[TOM] Created Dialogue Asset: {filePath}");
    }
}

// TomEncounterAction のインスペクタに「新規作成して割当」ボタンを追加
[CustomEditor(typeof(TomEncounterAction))]
public class TomEncounterActionEditor : Editor
{
    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();

        var act = (TomEncounterAction)target;
        GUILayout.Space(8);
        EditorGUILayout.LabelField("TOM Dialogue Helpers", EditorStyles.boldLabel);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("新規アセット作成して割当"))
            {
                var asset = ScriptableObject.CreateInstance<TomDialogueAsset>();
                asset.id = "tom_new_dialogue";
                asset.displayName = "TOM";
                asset.lines = new TomLine[] { new TomLine { text = "..." } };

                string basePath = "Assets";
                var path = AssetDatabase.GenerateUniqueAssetPath(System.IO.Path.Combine(basePath, "TomDialogueAsset.asset"));
                AssetDatabase.CreateAsset(asset, path);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                // フィールドに割当
                Undo.RecordObject(act, "Assign Dialogue");
                var so = new SerializedObject(act);
                so.FindProperty("dialogue").objectReferenceValue = asset;
                so.ApplyModifiedProperties();
                EditorGUIUtility.PingObject(asset);
                Debug.Log($"[TOM] Created & Assigned: {path}");
            }

            using (new EditorGUI.DisabledScope(!(act && act.GetType().GetField("dialogue", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public) != null)))
            {
                if (GUILayout.Button("dialogueId をアセットIDに同期"))
                {
                    var so = new SerializedObject(act);
                    var dlgProp = so.FindProperty("dialogue");
                    var idProp = so.FindProperty("dialogueId");
                    var dlg = dlgProp != null ? dlgProp.objectReferenceValue as TomDialogueAsset : null;
                    if (dlg != null)
                    {
                        idProp.stringValue = dlg.id;
                        so.ApplyModifiedProperties();
                        Debug.Log($"[TOM] Synced dialogueId = {dlg.id}");
                    }
                    else
                    {
                        Debug.LogWarning("[TOM] dialogue にアセットが割り当てられていません");
                    }
                }
            }
        }
    }
}

