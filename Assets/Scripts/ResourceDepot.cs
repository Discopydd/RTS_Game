using UnityEngine;

[RequireComponent(typeof(Collider))]
public class ResourceDepot : MonoBehaviour
{
    [Header("Depot Settings")]
    public string depotName = "Command Center";
    public UnitTeam team = UnitTeam.Player;
    public float collisionRadius = 1.8f;

    /// <summary>
    /// 根据基地真实 Collider 的外缘计算单位站位。
    /// 返回值是相对于基地中心的世界空间偏移量。
    /// </summary>
    public Vector3 GetStandingOffsetForDirection(
        Vector3 directionFromDepot,
        float unitRadius,
        float surfaceGap
    )
    {
        Vector3 direction = directionFromDepot;
        direction.y = 0f;

        if (direction.sqrMagnitude < 0.0001f)
        {
            direction = Vector3.forward;
        }

        direction.Normalize();

        Vector3 depotCenter = transform.position;

        if (TryGetSurfacePoint(direction, out Vector3 surfacePoint))
        {
            Vector3 standingPosition =
                surfacePoint + direction * (Mathf.Max(0f, unitRadius) + surfaceGap);

            standingPosition.y = depotCenter.y;

            Vector3 offset = standingPosition - depotCenter;
            offset.y = 0f;
            return offset;
        }

        float fallbackDistance = Mathf.Max(
            0.05f,
            collisionRadius + Mathf.Max(0f, unitRadius) + surfaceGap
        );

        return direction * fallbackDistance;
    }

    private bool TryGetSurfacePoint(Vector3 directionFromDepot, out Vector3 surfacePoint)
    {
        Collider[] colliders = GetComponentsInChildren<Collider>(true);

        if (TryGetSurfacePointFromColliders(
            colliders,
            directionFromDepot,
            false,
            out surfacePoint
        ))
        {
            return true;
        }

        return TryGetSurfacePointFromColliders(
            colliders,
            directionFromDepot,
            true,
            out surfacePoint
        );
    }

    private bool TryGetSurfacePointFromColliders(
        Collider[] colliders,
        Vector3 directionFromDepot,
        bool useTriggerColliders,
        out Vector3 surfacePoint
    )
    {
        Vector3 depotCenter = transform.position;
        surfacePoint = depotCenter;

        if (colliders == null || colliders.Length == 0)
        {
            return false;
        }

        bool found = false;
        float bestProjection = float.NegativeInfinity;
        Vector3 probePoint = depotCenter + directionFromDepot * 1000f;

        foreach (Collider depotCollider in colliders)
        {
            if (depotCollider == null ||
                !depotCollider.enabled ||
                depotCollider.isTrigger != useTriggerColliders)
            {
                continue;
            }

            Vector3 currentProbe = probePoint;
            currentProbe.y = depotCollider.bounds.center.y;

            Vector3 candidate = depotCollider.ClosestPoint(currentProbe);
            candidate.y = depotCenter.y;

            float projection = Vector3.Dot(
                candidate - depotCenter,
                directionFromDepot
            );

            if (!found || projection > bestProjection)
            {
                found = true;
                bestProjection = projection;
                surfacePoint = candidate;
            }
        }

        return found;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, collisionRadius);
    }
}
