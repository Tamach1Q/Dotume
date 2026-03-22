using UnityEngine;

public class ClearWindFlagsOnStart : MonoBehaviour
{
    [SerializeField]
    string[] flags = {
        "wind_lvl1","wind_lvl2","wind_lvl3",
        "wind_dir_left","wind_dir_right",
        // 念のため、シーン開始時に操作ロックも解除
        "me_leads",
        // FollowXOnly2Dの分離フラグも安全にクリア
        "me_split_active"
    };

    void Start()
    {
        var gs = GameState.I;
        if (gs == null) return;
        foreach (var f in flags)
            gs.RemoveFlag(f);
    }
}
