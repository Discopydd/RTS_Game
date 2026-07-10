using UnityEngine;

[RequireComponent(typeof(Collider))]
public class ResourceNode : MonoBehaviour
{
    [Header("Resource Settings")]
    public int maxAmount = 500;
    public float collisionRadius = 1.2f;

    private int currentAmount;

    private void Start()
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
}
