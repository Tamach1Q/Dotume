using UnityEngine;

// DDDL（常駐データ層）やシーン上に置いたレジストリから、IDで TomDialogueAsset を引くための簡易DB
public class TomDialogueDatabase : MonoBehaviour
{
    public static TomDialogueDatabase I { get; private set; }

    [Tooltip("ID解決に使う会話アセット一覧。DDDLシーンに置いておくと自動参照されます。")]
    [SerializeField] TomDialogueAsset[] entries;

    void Awake()
    {
        if (I != null && I != this) { Destroy(gameObject); return; }
        I = this;
        DontDestroyOnLoad(gameObject); // DDDL想定
    }

    public TomDialogueAsset FindById(string id)
    {
        if (string.IsNullOrEmpty(id) || entries == null) return null;
        foreach (var e in entries)
        {
            if (!e) continue;
            if (e.id == id) return e;
        }
        return null;
    }

    // シーン上のDB → Resources内のアセット の順で探索
    public static TomDialogueAsset Find(string id, string resourcesSearchRoot = "")
    {
        if (!string.IsNullOrEmpty(id))
        {
            if (I)
            {
                var a = I.FindById(id);
                if (a) return a;
            }

            // シーン内に複数DBがある場合も拾う
            var dbs = Object.FindObjectsOfType<TomDialogueDatabase>(includeInactive: true);
            foreach (var db in dbs)
            {
                if (db == null) continue;
                var a = db.FindById(id);
                if (a) return a;
            }

            // Resources から総当たりで検索（配置されている場合）
            TomDialogueAsset[] all = null;
            try
            {
                all = string.IsNullOrEmpty(resourcesSearchRoot)
                    ? Resources.LoadAll<TomDialogueAsset>("")
                    : Resources.LoadAll<TomDialogueAsset>(resourcesSearchRoot);
            }
            catch { /* Resources未使用でも安全に無視 */ }

            if (all != null)
            {
                foreach (var a in all)
                    if (a && a.id == id) return a;
            }
        }
        return null;
    }
}
