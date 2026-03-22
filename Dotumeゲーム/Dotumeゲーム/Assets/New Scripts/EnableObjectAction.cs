using UnityEngine;

public class EnableObjectAction : ActionBase
{
    [SerializeField] GameObject target;
    [SerializeField] bool value = true;
    public override void Execute()
    {
        if (target)
        {
            Debug.Log($"[EnableObjectAction] setActive target='{target.name}' value={value} timeScale={Time.timeScale:0.###}");
            target.SetActive(value);
        }
        else
        {
            Debug.LogWarning("[EnableObjectAction] target is null");
        }
    }
}
