using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class ResourceNode : MonoBehaviour
{
    [Header("Resource Settings")]
    public int maxAmount = 500;
    public float collisionRadius = 1.2f;

    private int currentAmount;
    private readonly List<Unit> gatherSlotOwners = new List<Unit>();

    private void Awake()
    {
        currentAmount = maxAmount;
    }

    public int CurrentAmount
    {
        get { return currentAmount; }
    }

    public int TakeResource(int amount)
    {
        if (currentAmount <= 0)
        {
            return 0;
        }

        int takenAmount = Mathf.Min(amount, currentAmount);
        currentAmount -= takenAmount;

        if (currentAmount <= 0)
        {
            Destroy(gameObject);
        }

        return takenAmount;
    }

    public bool IsEmpty()
    {
        return currentAmount <= 0;
    }

    /// <summary>
    /// 为工兵保留独立采集站位。preferredSlot 用来尽量保留玩家点击时的接近方向。
    /// 即使多名工兵同时下达命令，也不会获得相同站位。
    /// </summary>
    public int ReserveGatherSlot(Unit worker, int preferredSlot, int firstRingSlotCount)
    {
        if (worker == null)
        {
            return -1;
        }

        CleanupGatherSlots();

        for (int i = 0; i < gatherSlotOwners.Count; i++)
        {
            if (gatherSlotOwners[i] == worker)
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
        for (int i = outerStart; i < gatherSlotOwners.Count; i++)
        {
            if (TryClaimSlot(worker, i))
            {
                return i;
            }
        }

        int newSlot = Mathf.Max(outerStart, gatherSlotOwners.Count);
        while (gatherSlotOwners.Count < newSlot)
        {
            gatherSlotOwners.Add(null);
        }

        gatherSlotOwners.Add(worker);
        return newSlot;
    }

    /// <summary>
    /// 外圈等待中的工兵在第一圈出现空位时晋升；没有空位则保持原站位。
    /// </summary>
    public int TryPromoteToFirstRing(Unit worker, int preferredSlot, int firstRingSlotCount)
    {
        if (worker == null)
        {
            return -1;
        }

        CleanupGatherSlots();

        int currentSlot = -1;
        for (int i = 0; i < gatherSlotOwners.Count; i++)
        {
            if (gatherSlotOwners[i] == worker)
            {
                currentSlot = i;
                break;
            }
        }

        int ringCount = Mathf.Max(1, firstRingSlotCount);
        if (currentSlot >= 0 && currentSlot < ringCount)
        {
            return currentSlot;
        }

        preferredSlot = ((preferredSlot % ringCount) + ringCount) % ringCount;

        for (int distance = 0; distance < ringCount; distance++)
        {
            int clockwise = (preferredSlot + distance) % ringCount;
            int counterClockwise = (preferredSlot - distance + ringCount) % ringCount;

            int candidate = -1;
            if (clockwise < gatherSlotOwners.Count && gatherSlotOwners[clockwise] == null)
            {
                candidate = clockwise;
            }
            else if (counterClockwise != clockwise &&
                     counterClockwise < gatherSlotOwners.Count &&
                     gatherSlotOwners[counterClockwise] == null)
            {
                candidate = counterClockwise;
            }

            if (candidate < 0)
            {
                continue;
            }

            if (currentSlot >= 0 && currentSlot < gatherSlotOwners.Count)
            {
                gatherSlotOwners[currentSlot] = null;
            }

            gatherSlotOwners[candidate] = worker;
            TrimTrailingEmptySlots();
            return candidate;
        }

        return currentSlot;
    }

    public void ReleaseGatherSlot(Unit worker)
    {
        if (worker == null)
        {
            return;
        }

        for (int i = 0; i < gatherSlotOwners.Count; i++)
        {
            if (gatherSlotOwners[i] == worker)
            {
                gatherSlotOwners[i] = null;
            }
        }

        TrimTrailingEmptySlots();
    }

    private bool TryClaimSlot(Unit worker, int slotIndex)
    {
        while (gatherSlotOwners.Count <= slotIndex)
        {
            gatherSlotOwners.Add(null);
        }

        if (gatherSlotOwners[slotIndex] != null)
        {
            return false;
        }

        gatherSlotOwners[slotIndex] = worker;
        return true;
    }

    private void CleanupGatherSlots()
    {
        for (int i = 0; i < gatherSlotOwners.Count; i++)
        {
            if (gatherSlotOwners[i] == null)
            {
                gatherSlotOwners[i] = null;
            }
        }

        TrimTrailingEmptySlots();
    }

    private void TrimTrailingEmptySlots()
    {
        for (int i = gatherSlotOwners.Count - 1; i >= 0; i--)
        {
            if (gatherSlotOwners[i] != null)
            {
                break;
            }

            gatherSlotOwners.RemoveAt(i);
        }
    }
}
