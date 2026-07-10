using UnityEngine;

[RequireComponent(typeof(Unit))]
public class EnemyAI : MonoBehaviour
{
    private enum EnemyAIState
    {
        Idle,
        Patrol,
        Chase,
        Return
    }

    [Header("Detection Settings")]
    public float aggroRange = 9f;
    public float loseTargetRange = 13f;
    public float returnRange = 16f;
    public float allyCallRange = 8f;

    [Header("Update Frequency")]
    public float thinkInterval = 0.45f;
    public float commandInterval = 1.0f;
    public float repathDistance = 1.5f;

    [Header("Attack Position")]
    public float attackOffsetExtraDistance = 0.2f;

    [Header("Return Settings")]
    public bool returnToStartPosition = true;
    public float returnCompleteDistance = 0.7f;

    [Header("Patrol Settings")]
    public bool enablePatrol = true;
    public float patrolRadius = 4f;
    public float patrolMinWait = 5f;
    public float patrolMaxWait = 9f;
    public float patrolReachDistance = 0.8f;

    [Header("Gizmos")]
    public bool showGizmos = true;

    private Unit unit;
    private Unit currentTarget;

    private EnemyAIState state = EnemyAIState.Idle;

    private Vector3 startPosition;
    private Vector3 patrolTargetPosition;
    private Vector3 fixedAttackDirection;
    private Vector3 lastCommandPosition;

    private Unit lastCommandTarget;

    private float nextThinkTime = 0f;
    private float nextCommandTime = 0f;
    private float nextPatrolTime = 0f;
    private float nextAllyCallTime = 0f;

    private bool hasReturnCommand = false;

    private void Start()
    {
        unit = GetComponent<Unit>();
        startPosition = transform.position;
        patrolTargetPosition = startPosition;

        ScheduleNextPatrol();
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

        if (Time.time < nextThinkTime)
        {
            return;
        }

        nextThinkTime = Time.time + thinkInterval;

        Think();
    }

    private void Think()
    {
        if (currentTarget == null || currentTarget.IsDead())
        {
            currentTarget = FindBestPlayerTarget();

            if (currentTarget != null)
            {
                BeginChase(currentTarget);
                return;
            }
        }

        if (currentTarget != null)
        {
            if (ShouldGiveUpTarget(currentTarget))
            {
                currentTarget = null;
                StartReturn();
                return;
            }

            ChaseTarget();
            CallNearbyAllies(currentTarget);
            return;
        }

        if (returnToStartPosition && IsFarFromStart())
        {
            ContinueReturn();
            return;
        }

        HandlePatrol();
    }

    private void BeginChase(Unit target)
    {
        if (target == null || target.IsDead())
        {
            return;
        }

        state = EnemyAIState.Chase;
        currentTarget = target;
        hasReturnCommand = false;

        fixedAttackDirection = CalculateFixedAttackDirection(target);

        lastCommandTarget = null;
        IssueAttackCommand(true);
    }

    private void ChaseTarget()
    {
        if (currentTarget == null || currentTarget.IsDead())
        {
            currentTarget = null;
            StartReturn();
            return;
        }

        IssueAttackCommand(false);
    }

    private void IssueAttackCommand(bool force)
    {
        if (currentTarget == null || currentTarget.IsDead())
        {
            return;
        }

        Vector3 attackOffset = GetAttackOffset(currentTarget);
        Vector3 desiredPosition = currentTarget.transform.position + attackOffset;
        desiredPosition.y = transform.position.y;

        bool targetChanged = lastCommandTarget != currentTarget;
        bool targetMovedEnough = Vector3.Distance(lastCommandPosition, desiredPosition) >= repathDistance;

        if (!force && !targetChanged && !targetMovedEnough && Time.time < nextCommandTime)
        {
            return;
        }

        if (!force && Time.time < nextCommandTime)
        {
            return;
        }

        unit.AttackUnit(currentTarget, attackOffset);

        lastCommandTarget = currentTarget;
        lastCommandPosition = desiredPosition;
        nextCommandTime = Time.time + commandInterval;
    }

    private bool ShouldGiveUpTarget(Unit target)
    {
        if (target == null || target.IsDead())
        {
            return true;
        }

        float distanceToTarget = GetXZDistance(transform.position, target.transform.position);
        float enemyDistanceFromStart = GetXZDistance(transform.position, startPosition);
        float targetDistanceFromStart = GetXZDistance(target.transform.position, startPosition);

        if (enemyDistanceFromStart > returnRange)
        {
            return true;
        }

        if (targetDistanceFromStart > returnRange)
        {
            return true;
        }

        if (distanceToTarget > loseTargetRange)
        {
            return true;
        }

        return false;
    }

    private void StartReturn()
    {
        if (!returnToStartPosition)
        {
            state = EnemyAIState.Idle;
            return;
        }

        state = EnemyAIState.Return;
        hasReturnCommand = false;
        ContinueReturn();
    }

    private void ContinueReturn()
    {
        float distanceToStart = GetXZDistance(transform.position, startPosition);

        if (distanceToStart <= returnCompleteDistance)
        {
            state = EnemyAIState.Idle;
            hasReturnCommand = false;
            ScheduleNextPatrol();
            return;
        }

        if (!hasReturnCommand && Time.time >= nextCommandTime)
        {
            unit.MoveTo(startPosition);
            hasReturnCommand = true;
            nextCommandTime = Time.time + commandInterval;
        }
    }

    private void HandlePatrol()
    {
        if (!enablePatrol)
        {
            state = EnemyAIState.Idle;
            return;
        }

        if (state == EnemyAIState.Patrol)
        {
            float distanceToPatrolPoint = GetXZDistance(transform.position, patrolTargetPosition);

            if (distanceToPatrolPoint <= patrolReachDistance)
            {
                state = EnemyAIState.Idle;
                ScheduleNextPatrol();
            }

            return;
        }

        if (Time.time < nextPatrolTime)
        {
            return;
        }

        patrolTargetPosition = GetRandomPatrolPosition();
        unit.MoveTo(patrolTargetPosition);

        state = EnemyAIState.Patrol;
        nextCommandTime = Time.time + commandInterval;
    }

    private void ScheduleNextPatrol()
    {
        nextPatrolTime = Time.time + Random.Range(patrolMinWait, patrolMaxWait);
    }

    private Vector3 GetRandomPatrolPosition()
    {
        Vector2 randomCircle = Random.insideUnitCircle * patrolRadius;

        Vector3 position = startPosition + new Vector3(
            randomCircle.x,
            0f,
            randomCircle.y
        );

        position.y = transform.position.y;

        return position;
    }

    private Unit FindBestPlayerTarget()
    {
        Unit[] allUnits = FindObjectsByType<Unit>(FindObjectsSortMode.None);

        Unit bestTarget = null;
        float bestScore = Mathf.Infinity;

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
            float targetDistanceFromStart = GetXZDistance(startPosition, targetUnit.transform.position);

            if (distance > aggroRange)
            {
                continue;
            }

            if (targetDistanceFromStart > returnRange)
            {
                continue;
            }

            float hpScore = targetUnit.GetHpPercent() * 2f;
            float score = distance + hpScore;

            if (score < bestScore)
            {
                bestScore = score;
                bestTarget = targetUnit;
            }
        }

        return bestTarget;
    }

    private void CallNearbyAllies(Unit target)
    {
        if (target == null || target.IsDead())
        {
            return;
        }

        if (Time.time < nextAllyCallTime)
        {
            return;
        }

        nextAllyCallTime = Time.time + 1.5f;

        EnemyAI[] allEnemies = FindObjectsByType<EnemyAI>(FindObjectsSortMode.None);

        foreach (EnemyAI enemyAI in allEnemies)
        {
            if (enemyAI == null || enemyAI == this)
            {
                continue;
            }

            float distance = GetXZDistance(transform.position, enemyAI.transform.position);

            if (distance <= allyCallRange)
            {
                enemyAI.ReceiveAllyCall(target);
            }
        }
    }

    public void ReceiveAllyCall(Unit target)
    {
        if (unit == null || unit.IsDead())
        {
            return;
        }

        if (target == null || target.IsDead())
        {
            return;
        }

        if (currentTarget != null && !currentTarget.IsDead())
        {
            return;
        }

        float distanceFromStartToTarget = GetXZDistance(startPosition, target.transform.position);

        if (distanceFromStartToTarget > returnRange)
        {
            return;
        }

        BeginChase(target);
    }

    private Vector3 CalculateFixedAttackDirection(Unit target)
    {
        Vector3 direction = transform.position - target.transform.position;
        direction.y = 0f;

        if (direction.sqrMagnitude < 0.01f)
        {
            float angle = GetInstanceID() * 37f * Mathf.Deg2Rad;

            direction = new Vector3(
                Mathf.Cos(angle),
                0f,
                Mathf.Sin(angle)
            );
        }

        direction.Normalize();
        return direction;
    }

    private Vector3 GetAttackOffset(Unit target)
    {
        float distance = unit.collisionRadius + target.collisionRadius + attackOffsetExtraDistance;

        return fixedAttackDirection * distance;
    }

    private bool IsFarFromStart()
    {
        float distance = GetXZDistance(transform.position, startPosition);
        return distance > returnCompleteDistance;
    }

    private float GetXZDistance(Vector3 a, Vector3 b)
    {
        Vector2 posA = new Vector2(a.x, a.z);
        Vector2 posB = new Vector2(b.x, b.z);

        return Vector2.Distance(posA, posB);
    }

    private void OnDrawGizmosSelected()
    {
        if (!showGizmos)
        {
            return;
        }

        Vector3 center = Application.isPlaying ? startPosition : transform.position;

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(center, aggroRange);

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(center, returnRange);

        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(center, patrolRadius);
    }
}