using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Renderer))]
public class SetSortingLayer : MonoBehaviour
{
    public string sortingLayerName = "UI";
    public int sortingOrder = 200;

    void Awake()
    {
        var r = GetComponent<Renderer>();
        r.sortingLayerName = sortingLayerName;
        r.sortingOrder = sortingOrder;
    }
}