// FollowTransform2D.cs
using UnityEngine;

public class FollowTransform2D : MonoBehaviour
{
    public Transform target;
    public Vector2 offset;

    void LateUpdate()
    {
        if (!target) return;    
        var p = target.position;
        transform.position = new Vector3(p.x + offset.x, p.y + offset.y, transform.position.z);
    }
}