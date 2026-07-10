using UnityEngine;

[RequireComponent(typeof(Unit))]
public class EnemyAI : MonoBehaviour
{
    [Header("AI Settings")]
    public float aggroRange = 8f;
    public float returnRange = 12f;
    public float checkInterval = 0.3f;

    [Header("Return Settings")]
    public bool returnToStartPosition = true;

    private Unit unit;
    private Unit currentTarget;

    private Vector3 startPosition;
    private float checkTimer = 0f;

    private void Start()
    {
        unit = GetComponent<Unit>();
        startPosition = transform.position;
    }

    private void Update()
    {
        if (unit == null || unit.IsDead())
        {
            return;
        }

        if (unit.team != UnitTeam.Enemy)
        {
            return;
        }

        checkTimer -= Time.deltaTime;

        if (checkTimer <= 0f)
        {
            checkTimer = checkInterval;
            UpdateAI();
        }
    }

    private void UpdateAI()
    {
        if (currentTarget == null || currentTarget.IsDead())
        {
            currentTarget = FindNearestPlayerUnit();
        }

        if (currentTarget != null)
        {
            float distanceFromStart = GetXZDistance(startPosition, transform.position);

            if (distanceFromStart > returnRange)
            {
                currentTarget = null;

                if (returnToStartPosition)
                {
                    unit.MoveTo(startPosition);
                }

                return;
            }

            Vector3 attackOffset = GetAttackOffset(currentTarget);
            unit.AttackUnit(currentTarget, attackOffset);
            return;
        }

        if (returnToStartPosition)
        {
            float distanceToStart = GetXZDistance(transform.position, startPosition);

            if (distanceToStart > 0.5f)
            {
                unit.MoveTo(startPosition);
            }
        }
    }

    private Unit FindNearestPlayerUnit()
    {
        Unit[] allUnits = FindObjectsByType<Unit>(FindObjectsSortMode.None);

        Unit nearestPlayer = null;
        float nearestDistance = Mathf.Infinity;

        foreach (Unit targetUnit in allUnits)
        {
            if (targetUnit == null || targetUnit.IsDead())
            {
                continue;
            }

            if (targetUnit.team != UnitTeam.Player)
            {
                continue;
            }

            float distance = GetXZDistance(transform.position, targetUnit.transform.position);

            if (distance < nearestDistance && distance <= aggroRange)
            {
                nearestDistance = distance;
                nearestPlayer = targetUnit;
            }
        }

        return nearestPlayer;
    }

    private Vector3 GetAttackOffset(Unit target)
    {
        Vector3 direction = transform.position - target.transform.position;
        direction.y = 0f;

        if (direction.sqrMagnitude < 0.01f)
        {
            direction = Vector3.forward;
        }

        direction.Normalize();

        float distance = unit.collisionRadius + target.collisionRadius + 0.15f;

        return direction * distance;
    }

    private float GetXZDistance(Vector3 a, Vector3 b)
    {
        Vector2 posA = new Vector2(a.x, a.z);
        Vector2 posB = new Vector2(b.x, b.z);

        return Vector2.Distance(posA, posB);
    }
}