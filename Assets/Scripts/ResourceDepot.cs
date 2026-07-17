using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class ResourceDepot : MonoBehaviour
{
    [Header("Depot Settings")]
    public string depotName = "Command Center";
    public UnitTeam team = UnitTeam.Player;

    [Tooltip("只有真正的基地需要勾选。兵营这类生产建筑不要勾选，避免工人把资源送到兵营。")]
    public bool acceptsResources = true;

    public float collisionRadius = 1.8f;

    public bool IsSelected { get; private set; }

    private readonly List<Unit> depositSlotOwners = new List<Unit>();

    public void SetSelected(bool selected)
    {
        IsSelected = selected;
    }

    /// <summary>
    /// 为返回基地的工兵保留独立卸载站位，防止多名工兵挤在同一点。
    /// </summary>
    public int ReserveDepositSlot(Unit worker, int preferredSlot, int firstRingSlotCount)
    {
        if (worker == null)
        {
            return -1;
        }

        CleanupDepositSlots();

        for (int i = 0; i < depositSlotOwners.Count; i++)
        {
            if (depositSlotOwners[i] == worker)
            {
                return i;
            }
        }

        int ringCount = Mathf.Max(1, firstRingSlotCount);
        preferredSlot = ((preferredSlot % ringCount) + ringCount) % ringCount;

        // 必须先搜索完整的第一圈。旧逻辑会先选择 preferred + 1，
        // 在首选站位位于末尾时可能直接跳到第二圈，造成工兵无法接触资源。
        for (int distance = 0; distance < ringCount; distance++)
        {
            int clockwise = (preferredSlot + distance) % ringCount;
            if (TryClaimSlot(worker, clockwise))
            {
                return clockwise;
            }

            if (distance > 0)
            {
                int counterClockwise = (preferredSlot - distance + ringCount) % ringCount;
                if (counterClockwise != clockwise && TryClaimSlot(worker, counterClockwise))
                {
                    return counterClockwise;
                }
            }
        }

        // 第一圈已满时才进入外圈等待区。
        int outerStart = ringCount;
        for (int i = outerStart; i < depositSlotOwners.Count; i++)
        {
            if (TryClaimSlot(worker, i))
            {
                return i;
            }
        }

        int newSlot = Mathf.Max(outerStart, depositSlotOwners.Count);
        while (depositSlotOwners.Count < newSlot)
        {
            depositSlotOwners.Add(null);
        }

        depositSlotOwners.Add(worker);
        return newSlot;
    }

    public void ReleaseDepositSlot(Unit worker)
    {
        if (worker == null)
        {
            return;
        }

        for (int i = 0; i < depositSlotOwners.Count; i++)
        {
            if (depositSlotOwners[i] == worker)
            {
                depositSlotOwners[i] = null;
            }
        }

        TrimTrailingEmptySlots();
    }

    private bool TryClaimSlot(Unit worker, int slotIndex)
    {
        while (depositSlotOwners.Count <= slotIndex)
        {
            depositSlotOwners.Add(null);
        }

        if (depositSlotOwners[slotIndex] != null)
        {
            return false;
        }

        depositSlotOwners[slotIndex] = worker;
        return true;
    }

    private void CleanupDepositSlots()
    {
        for (int i = 0; i < depositSlotOwners.Count; i++)
        {
            if (depositSlotOwners[i] == null)
            {
                depositSlotOwners[i] = null;
            }
        }

        TrimTrailingEmptySlots();
    }

    private void TrimTrailingEmptySlots()
    {
        for (int i = depositSlotOwners.Count - 1; i >= 0; i--)
        {
            if (depositSlotOwners[i] != null)
            {
                break;
            }

            depositSlotOwners.RemoveAt(i);
        }
    }

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
