using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>Scene-view marker for a Boss patrol checkpoint.</summary>
public sealed class BossPatrolWaypoint : MonoBehaviour
{
    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(0.1f, 0.85f, 1f, 1f);
        Gizmos.DrawSphere(transform.position, 0.2f);
        Gizmos.color = Color.white;
        Gizmos.DrawWireSphere(transform.position, 0.32f);

#if UNITY_EDITOR
        Handles.color = Color.cyan;
        Handles.Label(transform.position + Vector3.up * 0.45f, gameObject.name);
#endif
    }
}
