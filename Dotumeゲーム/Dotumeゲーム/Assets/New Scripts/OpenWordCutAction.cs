using UnityEngine;

public class OpenWordCutAction : ActionBase
{
    [SerializeField] string owner = "WordCut";   // 表示権限ラベル（何でもOK）
    [SerializeField] string word = "magnet";     // 表示する単語
    [SerializeField] string expected = "net";    // 正解セグメント
    [SerializeField] string itemId = "net";      // 成功時に付与（不要なら空）
    [SerializeField] bool addToInventory = true; // インベントリに入れるか（falseでも内部ロジックはitemIdで管理）
    [SerializeField] int cutsRequired = 1;       // カット回数
    [TextArea] [SerializeField] string guide = "";

    public override void Execute()
    {
        UIRouter.I?.OpenWordCut(owner, word, expected, itemId, cutsRequired, guide, addToInventory);
    }
}
