using UnityEngine;

[RequireComponent(typeof(Collider))]
public class ResourceDepot : MonoBehaviour
{
    [Header("Depot Settings")]
    public string depotName = "Command Center";
    public UnitTeam team = UnitTeam.Player;
    public float collisionRadius = 1.8f;

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, collisionRadius);
    }
}