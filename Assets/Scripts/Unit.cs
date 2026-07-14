using UnityEngine;
using UnityEngine.AI;

public enum UnitTeam
{
    Player,
    Enemy
}

public enum UnitRole
{
    Soldier,
    Worker
}

public enum UnitCommandState
{
    Idle,
    Move,
    AttackTarget,
    AttackMove,
    GatherResource,
    ReturnResource
}

[RequireComponent(typeof(CapsuleCollider))]
[RequireComponent(typeof(Rigidbody))]
public class Unit : MonoBehaviour
{
    [Header("Team Settings")]
    public UnitTeam team = UnitTeam.Player;

    [Header("Role Settings")]
    public UnitRole role = UnitRole.Soldier;

    [Header("Move Settings")]
    public float moveSpeed = 5f;
    public float stopDistance = 0.08f;
    public float acceleration = 28f;
    public float angularSpeed = 720f;

    [Header("Collision Settings")]
    public float collisionRadius = 0.6f;

    [Header("Pathfinding Settings")]
    public float pathSampleRadius = 3f;
    public float pathRecalculateDistance = 0.35f;
    public float pathRecalculateInterval = 0.25f;
    public float waypointReachDistance = 0.18f;

    [Header("Agent Avoidance Settings")]
    [Range(0, 99)] public int avoidancePriority = 50;
    public bool useHighQualityAvoidance = true;
    public float workerAgentRadiusMultiplier = 0.78f;

    [Header("Legacy Avoidance Settings")]
    [Tooltip("保留旧场景序列化数据。现在单位避让由 NavMeshAgent 处理。")]
    public float stoppedUnitObstacleExtraRadius = 0.08f;
    public float obstacleEnableDelay = 0.12f;
    public float movingUnitAvoidanceRadius = 1.4f;
    public float movingUnitAvoidanceStrength = 1.15f;
    public float movingUnitLookAheadDistance = 0.55f;
    public float buildingLookAheadDistance = 1.6f;
    public float buildingAvoidanceStrength = 1.8f;
    public float buildingAvoidanceClearance = 0.12f;

    [Header("Battle Settings")]
    public int maxHp = 100;
    public int attackDamage = 10;
    public float attackRange = 2f;
    public float detectRange = 5f;
    public float attackCooldown = 1f;

    [Header("Gather Settings")]
    public bool canGather = true;
    public float resourceSearchRange = 10f;
    public int gatherAmount = 5;
    public int carryCapacity = 20;
    public float gatherRange = 1.8f;
    public float depositRange = 2.0f;
    public float gatherCooldown = 1f;

    [Tooltip("工兵碰撞体与资源表面的间隔。为保证 NavMesh 稳定，负数会自动按最小安全距离处理。")]
    public float gatherSurfaceGap = 0.08f;

    [Tooltip("到达采集站位的容许距离。过小会导致多工兵互相等待。")]
    public float gatherArrivalDistance = 0.24f;

    [Tooltip("保留旧场景序列化数据。现在不会切换成直线冲刺。")]
    public float gatherDirectApproachDistance = 3f;

    [Tooltip("单位中心与基地 Collider 表面之间额外保留的距离。")]
    public float depotSurfaceGap = 0.12f;

    [Header("Worker Recovery")]
    public float stuckCheckInterval = 0.8f;
    public float stuckMoveThreshold = 0.06f;
    public int stuckChecksBeforeRepath = 2;

    [Header("Selection Circle")]
    public float selectionCircleRadius = 0.7f;
    public float selectionCircleHeight = -0.95f;

    private int currentHp;
    private int carriedMinerals;
    private float attackTimer;
    private float gatherTimer;

    private UnitCommandState commandState = UnitCommandState.Idle;
    private Unit attackTarget;
    private Vector3 attackOffsetFromTarget;
    private Vector3 attackMoveDestination;

    private ResourceNode resourceTarget;
    private ResourceDepot depotTarget;
    private Vector3 gatherDirectionFromResource = Vector3.forward;
    private float gatherAdditionalRingDistance;
    private Vector3 depositDirectionFromDepot = Vector3.forward;
    private float depositAdditionalRingDistance;

    private CapsuleCollider unitCollider;
    private Rigidbody rb;
    private NavMeshAgent agent;
    private GameObject selectionCircle;
    private MoveCommandLine currentMoveCommandLine;
    private DamageFlash damageFlash;

    private float movementPlaneY;
    private Vector3 requestedDestination;
    private Vector3 resolvedDestination;
    private bool hasResolvedDestination;
    private float nextDestinationUpdateTime;
    private float nextAttachAttemptTime;

    private Vector3 lastStuckCheckPosition;
    private float nextStuckCheckTime;
    private int consecutiveStuckChecks;

    private void Awake()
    {
        currentHp = maxHp;
        movementPlaneY = transform.position.y;
        rb = GetComponent<Rigidbody>();
        unitCollider = GetComponent<CapsuleCollider>();

        NavMeshObstacle oldObstacle = GetComponent<NavMeshObstacle>();
        if (oldObstacle != null)
        {
            oldObstacle.enabled = false;
            Destroy(oldObstacle);
        }

        if (gameObject.name.Contains("Worker"))
        {
            role = UnitRole.Worker;
            canGather = true;
        }

        SetupRigidbody();
        SetupCollider();
    }

    private void Start()
    {
        // RuntimeInitializeLoadType.AfterSceneLoad 会在 Awake 之后、Start 之前构建 NavMesh。
        // 在这里才创建 NavMeshAgent，避免 Agent 比 NavMesh 更早启用。
        RTSRuntimeNavMesh.EnsureBuilt();

        agent = GetComponent<NavMeshAgent>();
        if (agent == null)
        {
            agent = gameObject.AddComponent<NavMeshAgent>();
        }

        SetupAgent();
        currentHp = maxHp;
        attackMoveDestination = transform.position;
        requestedDestination = transform.position;
        resolvedDestination = transform.position;
        lastStuckCheckPosition = transform.position;
        nextStuckCheckTime = Time.time + stuckCheckInterval;

        IgnoreUnitPhysicsCollisions();
        CreateSelectionCircle();
        EnsureHealthBar();
        EnsureDamageFlash();
        SetSelected(false);
        TryAttachToNavMesh();
    }

    private void Update()
    {
        attackTimer -= Time.deltaTime;
        gatherTimer -= Time.deltaTime;

        if (!EnsureAgentOnNavMesh())
        {
            return;
        }

        switch (commandState)
        {
            case UnitCommandState.Move:
                UpdateMoveCommand();
                break;

            case UnitCommandState.AttackTarget:
                UpdateAttackTargetCommand();
                break;

            case UnitCommandState.AttackMove:
                UpdateAttackMoveCommand();
                break;

            case UnitCommandState.GatherResource:
                UpdateGatherCommand();
                break;

            case UnitCommandState.ReturnResource:
                UpdateReturnResourceCommand();
                break;

            default:
                UpdateIdleCombat();
                break;
        }

        UpdateStuckRecovery();
    }

    private void LateUpdate()
    {
        if (agent == null || !agent.enabled || !agent.isOnNavMesh)
        {
            return;
        }

        Vector3 synchronizedPosition = agent.nextPosition;
        synchronizedPosition.y = movementPlaneY;
        transform.position = synchronizedPosition;
    }

    private void UpdateMoveCommand()
    {
        MoveAgentTo(requestedDestination, Mathf.Max(0.03f, stopDistance), false);

        if (HasArrived(Mathf.Max(stopDistance, 0.12f)))
        {
            StopMovement(UnitCommandState.Idle);
        }
    }

    private void UpdateAttackTargetCommand()
    {
        if (attackTarget == null || attackTarget.IsDead())
        {
            attackTarget = null;
            StopMovement(UnitCommandState.Idle);
            return;
        }

        float distance = GetXZDistance(transform.position, attackTarget.transform.position);

        if (distance <= attackRange)
        {
            PauseAgentAtDestination();
            LookAtTarget(attackTarget.transform.position);

            if (attackTimer <= 0f)
            {
                Attack(attackTarget);
            }

            return;
        }

        Vector3 chasePosition = attackTarget.transform.position + attackOffsetFromTarget;
        chasePosition.y = transform.position.y;
        MoveAgentTo(chasePosition, Mathf.Max(0.05f, stopDistance), true);
    }

    private void UpdateAttackMoveCommand()
    {
        if (attackTarget == null || attackTarget.IsDead())
        {
            attackTarget = FindNearestEnemy(detectRange);
        }

        if (attackTarget != null)
        {
            float distance = GetXZDistance(transform.position, attackTarget.transform.position);

            if (distance > detectRange)
            {
                attackTarget = null;
            }
            else if (distance <= attackRange)
            {
                PauseAgentAtDestination();
                LookAtTarget(attackTarget.transform.position);

                if (attackTimer <= 0f)
                {
                    Attack(attackTarget);
                }

                return;
            }
            else
            {
                MoveAgentTo(attackTarget.transform.position, Mathf.Max(0.05f, attackRange * 0.75f), true);
                return;
            }
        }

        MoveAgentTo(attackMoveDestination, Mathf.Max(stopDistance, 0.08f), false);

        if (HasArrived(Mathf.Max(stopDistance * 2f, 0.18f)))
        {
            StopMovement(UnitCommandState.Idle);
        }
    }

    private void UpdateGatherCommand()
    {
        if (resourceTarget == null || resourceTarget.IsEmpty())
        {
            if (carriedMinerals > 0)
            {
                BeginReturnToDepot();
            }
            else if (!TrySwitchToNearbyResource())
            {
                resourceTarget = null;
                StopMovement(UnitCommandState.Idle);
            }

            return;
        }

        if (carriedMinerals >= carryCapacity)
        {
            BeginReturnToDepot();
            return;
        }

        Vector3 gatherPosition = GetCurrentGatherPosition();
        float arrivalTolerance = Mathf.Max(gatherArrivalDistance, AgentRadius * 0.42f);

        bool alreadyAtGatherSlot = hasResolvedDestination &&
            GetXZDistance(transform.position, resolvedDestination) <= arrivalTolerance;

        if (!alreadyAtGatherSlot)
        {
            MoveAgentTo(gatherPosition, 0.02f, false);

            if (!HasArrived(arrivalTolerance))
            {
                return;
            }
        }

        PauseAgentAtDestination();
        LookAtTarget(resourceTarget.transform.position);

        if (gatherTimer > 0f)
        {
            return;
        }

        int needed = Mathf.Max(0, carryCapacity - carriedMinerals);
        int amountToTake = Mathf.Min(gatherAmount, needed);
        int gathered = resourceTarget.TakeResource(amountToTake);

        carriedMinerals += gathered;
        gatherTimer = Mathf.Max(0.05f, gatherCooldown);

        if (carriedMinerals >= carryCapacity || resourceTarget == null || resourceTarget.IsEmpty())
        {
            BeginReturnToDepot();
        }
    }

    private void BeginReturnToDepot()
    {
        if (!EnsureDepotTarget())
        {
            Debug.LogWarning(gameObject.name + " has no available depot target.");
            StopMovement(UnitCommandState.Idle);
            return;
        }

        commandState = UnitCommandState.ReturnResource;
        InvalidateDestination();
        ResumeAgent();
    }

    private void UpdateReturnResourceCommand()
    {
        if (carriedMinerals <= 0)
        {
            ContinueGatheringOrIdle();
            return;
        }

        if (!EnsureDepotTarget())
        {
            StopMovement(UnitCommandState.Idle);
            return;
        }

        Vector3 depositPosition = GetCurrentDepositPosition();
        float arrivalTolerance = Mathf.Max(0.22f, AgentRadius * 0.45f);

        bool alreadyAtDepotSlot = hasResolvedDestination &&
            GetXZDistance(transform.position, resolvedDestination) <= arrivalTolerance;

        if (!alreadyAtDepotSlot)
        {
            MoveAgentTo(depositPosition, 0.02f, false);

            if (!HasArrived(arrivalTolerance))
            {
                return;
            }
        }

        PauseAgentAtDestination();
        LookAtTarget(depotTarget.transform.position);

        if (PlayerResources.Instance != null)
        {
            PlayerResources.Instance.AddMinerals(carriedMinerals);
        }

        carriedMinerals = 0;
        ContinueGatheringOrIdle();
    }

    private void ContinueGatheringOrIdle()
    {
        if (resourceTarget != null && !resourceTarget.IsEmpty())
        {
            commandState = UnitCommandState.GatherResource;
            InvalidateDestination();
            ResumeAgent();
            return;
        }

        if (TrySwitchToNearbyResource())
        {
            return;
        }

        resourceTarget = null;
        StopMovement(UnitCommandState.Idle);
    }

    private void UpdateIdleCombat()
    {
        if (attackTarget == null || attackTarget.IsDead())
        {
            attackTarget = FindNearestEnemy(attackRange);
        }

        if (attackTarget == null)
        {
            PauseAgentAtDestination();
            return;
        }

        float distance = GetXZDistance(transform.position, attackTarget.transform.position);

        if (distance > attackRange)
        {
            attackTarget = null;
            return;
        }

        PauseAgentAtDestination();
        LookAtTarget(attackTarget.transform.position);

        if (attackTimer <= 0f)
        {
            Attack(attackTarget);
        }
    }

    private void MoveAgentTo(Vector3 destination, float stoppingDistance, bool movingTarget)
    {
        if (agent == null || !agent.enabled || !agent.isOnNavMesh)
        {
            return;
        }

        destination.y = transform.position.y;

        bool destinationChanged = !hasResolvedDestination ||
            GetXZDistance(destination, requestedDestination) > Mathf.Max(0.08f, pathRecalculateDistance);

        requestedDestination = destination;

        // 移动目标允许定时更新；静态目标只有目标改变或路径失效时才更新，避免每帧重算造成抖动。
        bool timeToRefresh = movingTarget && Time.time >= nextDestinationUpdateTime;
        float pathArrivalTolerance = Mathf.Max(0.1f, stoppingDistance + 0.08f);
        bool pathNeedsRefresh = agent.pathStatus == NavMeshPathStatus.PathInvalid ||
            (!agent.hasPath && Time.time >= nextDestinationUpdateTime && !HasArrived(pathArrivalTolerance));

        if (!destinationChanged && !timeToRefresh && !pathNeedsRefresh)
        {
            ResumeAgent();
            return;
        }

        if (!TryResolveDestination(destination, out Vector3 navPoint))
        {
            return;
        }

        if (hasResolvedDestination &&
            GetXZDistance(navPoint, resolvedDestination) < Mathf.Max(0.08f, pathRecalculateDistance) &&
            !pathNeedsRefresh && !timeToRefresh)
        {
            ResumeAgent();
            return;
        }

        resolvedDestination = navPoint;
        hasResolvedDestination = true;
        agent.stoppingDistance = Mathf.Max(0f, stoppingDistance);
        agent.isStopped = false;

        if (!agent.SetDestination(resolvedDestination))
        {
            hasResolvedDestination = false;
            return;
        }

        nextDestinationUpdateTime = Time.time + Mathf.Max(0.08f, pathRecalculateInterval);
    }

    private bool TryResolveDestination(Vector3 desired, out Vector3 result)
    {
        result = desired;

        float sampleRadius = Mathf.Max(0.5f, pathSampleRadius);
        if (NavMesh.SamplePosition(desired, out NavMeshHit hit, sampleRadius, NavMesh.AllAreas))
        {
            result = hit.position;
            return true;
        }

        for (int ring = 1; ring <= 3; ring++)
        {
            float radius = sampleRadius * ring;
            int pointCount = 8 + ring * 4;

            for (int i = 0; i < pointCount; i++)
            {
                float angle = (Mathf.PI * 2f * i / pointCount) + GetInstanceID() * 0.01f;
                Vector3 candidate = desired + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;

                if (NavMesh.SamplePosition(candidate, out hit, sampleRadius * 0.6f, NavMesh.AllAreas))
                {
                    result = hit.position;
                    return true;
                }
            }
        }

        return false;
    }

    private bool HasArrived(float extraTolerance)
    {
        if (agent == null || !agent.enabled || !agent.isOnNavMesh || !hasResolvedDestination)
        {
            return false;
        }

        if (agent.pathPending)
        {
            return false;
        }

        float worldDistance = GetXZDistance(transform.position, resolvedDestination);
        float allowed = Mathf.Max(extraTolerance, agent.stoppingDistance + 0.05f);

        if (worldDistance <= allowed)
        {
            return true;
        }

        return agent.hasPath &&
               agent.remainingDistance <= allowed &&
               agent.velocity.sqrMagnitude <= 0.04f;
    }

    private void PauseAgentAtDestination()
    {
        if (agent == null || !agent.enabled || !agent.isOnNavMesh)
        {
            return;
        }

        agent.isStopped = true;

        if (agent.hasPath)
        {
            agent.ResetPath();
        }

        consecutiveStuckChecks = 0;
    }

    private void StopAgentOnly()
    {
        if (agent == null || !agent.enabled || !agent.isOnNavMesh)
        {
            return;
        }

        if (!agent.isStopped)
        {
            agent.isStopped = true;
        }

        if (agent.hasPath)
        {
            agent.ResetPath();
        }

        hasResolvedDestination = false;
        consecutiveStuckChecks = 0;
    }

    private void ResumeAgent()
    {
        if (agent != null && agent.enabled && agent.isOnNavMesh && agent.isStopped)
        {
            agent.isStopped = false;
        }
    }

    private void StopMovement(UnitCommandState nextState)
    {
        commandState = nextState;
        StopAgentOnly();
    }

    private void InvalidateDestination()
    {
        hasResolvedDestination = false;
        nextDestinationUpdateTime = 0f;

        if (agent != null && agent.enabled && agent.isOnNavMesh && agent.hasPath)
        {
            agent.ResetPath();
        }
    }

    private void UpdateStuckRecovery()
    {
        if (Time.time < nextStuckCheckTime)
        {
            return;
        }

        nextStuckCheckTime = Time.time + Mathf.Max(0.25f, stuckCheckInterval);

        bool shouldMove = commandState == UnitCommandState.Move ||
                          commandState == UnitCommandState.AttackTarget ||
                          commandState == UnitCommandState.AttackMove ||
                          commandState == UnitCommandState.GatherResource ||
                          commandState == UnitCommandState.ReturnResource;

        float moved = GetXZDistance(transform.position, lastStuckCheckPosition);
        lastStuckCheckPosition = transform.position;

        if (!shouldMove || agent == null || !agent.enabled || !agent.isOnNavMesh ||
            agent.isStopped || !agent.hasPath || agent.pathPending || HasArrived(0.25f))
        {
            consecutiveStuckChecks = 0;
            return;
        }

        if (moved <= Mathf.Max(0.01f, stuckMoveThreshold) && agent.remainingDistance > AgentRadius * 1.2f)
        {
            consecutiveStuckChecks++;
        }
        else
        {
            consecutiveStuckChecks = 0;
        }

        if (consecutiveStuckChecks < Mathf.Max(1, stuckChecksBeforeRepath))
        {
            return;
        }

        consecutiveStuckChecks = 0;

        if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, pathSampleRadius, NavMesh.AllAreas))
        {
            agent.Warp(hit.position);
        }

        InvalidateDestination();
        ResumeAgent();
    }

    private bool EnsureAgentOnNavMesh()
    {
        if (agent != null && agent.enabled && agent.isOnNavMesh)
        {
            return true;
        }

        if (Time.time < nextAttachAttemptTime)
        {
            return false;
        }

        nextAttachAttemptTime = Time.time + 0.5f;
        return TryAttachToNavMesh();
    }

    private bool TryAttachToNavMesh()
    {
        if (agent == null)
        {
            return false;
        }

        if (!agent.enabled)
        {
            agent.enabled = true;
        }

        if (agent.isOnNavMesh)
        {
            return true;
        }

        if (!NavMesh.SamplePosition(transform.position, out NavMeshHit hit, Mathf.Max(1f, pathSampleRadius), NavMesh.AllAreas))
        {
            return false;
        }

        agent.baseOffset = 0f;
        agent.Warp(hit.position);
        return agent.isOnNavMesh;
    }

    public void MoveTo(Vector3 position)
    {
        attackTarget = null;
        resourceTarget = null;
        depotTarget = null;
        position.y = transform.position.y;
        requestedDestination = position;
        commandState = UnitCommandState.Move;
        InvalidateDestination();
        ResumeAgent();
    }

    public void MoveToDepot(ResourceDepot target, Vector3 offsetFromDepot)
    {
        if (target == null || target.team != team)
        {
            return;
        }

        if (CanGatherResource() && carriedMinerals > 0)
        {
            CancelMoveCommandLine();
            depotTarget = target;
            AssignDepositOffset(target, offsetFromDepot);
            attackTarget = null;
            commandState = UnitCommandState.ReturnResource;
            InvalidateDestination();
            ResumeAgent();
            return;
        }

        MoveTo(target.transform.position + offsetFromDepot);
    }

    public void GatherResource(
        ResourceNode target,
        ResourceDepot depot,
        Vector3 offsetFromResource,
        Vector3 offsetFromDepot)
    {
        if (!CanGatherResource() || target == null || target.IsEmpty() || depot == null)
        {
            return;
        }

        CancelMoveCommandLine();
        resourceTarget = target;
        depotTarget = depot;

        Vector3 assignedOffset = offsetFromResource;
        assignedOffset.y = 0f;

        if (assignedOffset.sqrMagnitude < 0.0001f)
        {
            assignedOffset = transform.position - resourceTarget.transform.position;
            assignedOffset.y = 0f;
        }

        if (assignedOffset.sqrMagnitude < 0.0001f)
        {
            assignedOffset = Vector3.forward;
        }

        gatherDirectionFromResource = assignedOffset.normalized;
        Vector3 baseOffset = GetGatherOffsetForDirection(resourceTarget, gatherDirectionFromResource);
        gatherAdditionalRingDistance = Mathf.Max(0f, assignedOffset.magnitude - baseOffset.magnitude);

        AssignDepositOffset(depotTarget, offsetFromDepot);

        attackTarget = null;
        commandState = UnitCommandState.GatherResource;
        gatherTimer = 0f;
        InvalidateDestination();
        ResumeAgent();
    }

    public void AttackMoveTo(Vector3 position)
    {
        position.y = transform.position.y;
        attackMoveDestination = position;
        requestedDestination = position;
        attackTarget = null;
        resourceTarget = null;
        depotTarget = null;
        commandState = UnitCommandState.AttackMove;
        InvalidateDestination();
        ResumeAgent();
    }

    public void AttackUnit(Unit target, Vector3 offsetFromTarget)
    {
        if (target == null || target.IsDead())
        {
            return;
        }

        CancelMoveCommandLine();
        attackTarget = target;
        attackOffsetFromTarget = offsetFromTarget;
        resourceTarget = null;
        depotTarget = null;
        commandState = UnitCommandState.AttackTarget;
        InvalidateDestination();
        ResumeAgent();
    }

    public bool IsMoving()
    {
        return !IsDead() && commandState != UnitCommandState.Idle;
    }

    public bool IsDead()
    {
        return currentHp <= 0;
    }

    public float GetHpPercent()
    {
        return maxHp <= 0 ? 0f : (float)currentHp / maxHp;
    }

    public bool IsWorker()
    {
        return role == UnitRole.Worker;
    }

    public bool CanGatherResource()
    {
        return team == UnitTeam.Player && canGather && role == UnitRole.Worker;
    }

    public int GetCarriedMinerals()
    {
        return carriedMinerals;
    }

    private void Attack(Unit target)
    {
        if (target == null || target.IsDead())
        {
            return;
        }

        CancelMoveCommandLine();
        AttackEffect.Create(transform.position, target.transform.position);
        target.TakeDamage(attackDamage);
        attackTimer = Mathf.Max(0.05f, attackCooldown);
    }

    public void TakeDamage(int damage)
    {
        currentHp = Mathf.Clamp(currentHp - damage, 0, maxHp);

        if (damageFlash != null)
        {
            damageFlash.Flash();
        }

        if (currentHp <= 0)
        {
            Die();
        }
    }

    private void Die()
    {
        commandState = UnitCommandState.Idle;
        CancelMoveCommandLine();

        if (agent != null && agent.enabled && agent.isOnNavMesh)
        {
            agent.isStopped = true;
            agent.ResetPath();
        }

        DeathEffect.Create(transform.position, team);
        Destroy(gameObject);
    }

    public void SetMoveCommandLine(MoveCommandLine newLine)
    {
        if (currentMoveCommandLine != null)
        {
            Destroy(currentMoveCommandLine.gameObject);
        }

        currentMoveCommandLine = newLine;
    }

    public void ClearMoveCommandLine(MoveCommandLine line)
    {
        if (currentMoveCommandLine == line)
        {
            currentMoveCommandLine = null;
        }
    }

    private void CancelMoveCommandLine()
    {
        if (currentMoveCommandLine != null)
        {
            Destroy(currentMoveCommandLine.gameObject);
            currentMoveCommandLine = null;
        }
    }

    public void SetSelected(bool selected)
    {
        if (selectionCircle != null)
        {
            selectionCircle.SetActive(selected);
        }
    }

    private Unit FindNearestEnemy(float maximumRange)
    {
        Unit[] allUnits = FindObjectsByType<Unit>(FindObjectsSortMode.None);
        Unit nearest = null;
        float nearestDistance = Mathf.Infinity;

        foreach (Unit unit in allUnits)
        {
            if (unit == null || unit == this || unit.IsDead() || unit.team == team)
            {
                continue;
            }

            float distance = GetXZDistance(transform.position, unit.transform.position);
            if (distance <= maximumRange && distance < nearestDistance)
            {
                nearestDistance = distance;
                nearest = unit;
            }
        }

        return nearest;
    }

    private ResourceNode FindNearestAvailableResource(Vector3 fromPosition, float searchRange)
    {
        ResourceNode[] resources = FindObjectsByType<ResourceNode>(FindObjectsSortMode.None);
        ResourceNode nearest = null;
        float nearestDistance = Mathf.Infinity;

        foreach (ResourceNode node in resources)
        {
            if (node == null || node.IsEmpty())
            {
                continue;
            }

            float distance = GetXZDistance(fromPosition, node.transform.position);
            if (distance <= searchRange && distance < nearestDistance)
            {
                nearestDistance = distance;
                nearest = node;
            }
        }

        return nearest;
    }

    private bool TrySwitchToNearbyResource()
    {
        if (!EnsureDepotTarget())
        {
            return false;
        }

        ResourceNode next = FindNearestAvailableResource(transform.position, resourceSearchRange);
        if (next == null)
        {
            return false;
        }

        resourceTarget = next;
        gatherDirectionFromResource = transform.position - next.transform.position;
        gatherDirectionFromResource.y = 0f;

        if (gatherDirectionFromResource.sqrMagnitude < 0.0001f)
        {
            gatherDirectionFromResource = Vector3.forward;
        }

        gatherDirectionFromResource.Normalize();
        gatherAdditionalRingDistance = 0f;
        commandState = UnitCommandState.GatherResource;
        gatherTimer = 0f;
        InvalidateDestination();
        ResumeAgent();
        return true;
    }

    private bool EnsureDepotTarget()
    {
        if (depotTarget != null && depotTarget.team == team)
        {
            return true;
        }

        ResourceDepot[] depots = FindObjectsByType<ResourceDepot>(FindObjectsSortMode.None);
        ResourceDepot nearest = null;
        float nearestDistance = Mathf.Infinity;

        foreach (ResourceDepot depot in depots)
        {
            if (depot == null || depot.team != team)
            {
                continue;
            }

            float distance = GetXZDistance(transform.position, depot.transform.position);
            if (distance < nearestDistance)
            {
                nearestDistance = distance;
                nearest = depot;
            }
        }

        depotTarget = nearest;

        if (depotTarget != null)
        {
            Vector3 direction = transform.position - depotTarget.transform.position;
            AssignDepositOffset(depotTarget, direction);
        }

        return depotTarget != null;
    }

    private Vector3 GetCurrentGatherPosition()
    {
        if (resourceTarget == null)
        {
            return transform.position;
        }

        Vector3 baseOffset = GetGatherOffsetForDirection(resourceTarget, gatherDirectionFromResource);
        Vector3 offset = baseOffset + gatherDirectionFromResource.normalized * Mathf.Max(0f, gatherAdditionalRingDistance);
        Vector3 position = resourceTarget.transform.position + offset;
        position.y = transform.position.y;
        return position;
    }

    private Vector3 GetCurrentDepositPosition()
    {
        if (depotTarget == null)
        {
            return transform.position;
        }

        Vector3 baseOffset = GetDepotOffsetForDirection(depotTarget, depositDirectionFromDepot);
        Vector3 offset = baseOffset + depositDirectionFromDepot.normalized * Mathf.Max(0f, depositAdditionalRingDistance);
        Vector3 position = depotTarget.transform.position + offset;
        position.y = transform.position.y;
        return position;
    }

    public Vector3 GetDepotOffsetForDirection(ResourceDepot depot, Vector3 directionFromDepot)
    {
        if (depot == null)
        {
            return Vector3.zero;
        }

        return depot.GetStandingOffsetForDirection(
            directionFromDepot,
            AgentRadius,
            Mathf.Max(0.05f, depotSurfaceGap));
    }

    private void AssignDepositOffset(ResourceDepot depot, Vector3 assignedOffset)
    {
        Vector3 flatOffset = assignedOffset;
        flatOffset.y = 0f;

        if (flatOffset.sqrMagnitude < 0.0001f && depot != null)
        {
            flatOffset = transform.position - depot.transform.position;
            flatOffset.y = 0f;
        }

        if (flatOffset.sqrMagnitude < 0.0001f)
        {
            flatOffset = Vector3.forward;
        }

        depositDirectionFromDepot = flatOffset.normalized;
        Vector3 baseOffset = GetDepotOffsetForDirection(depot, depositDirectionFromDepot);
        depositAdditionalRingDistance = Mathf.Max(0f, flatOffset.magnitude - baseOffset.magnitude);
    }

    public float GetGatherStandingDistance(ResourceNode node)
    {
        return GetGatherOffsetForDirection(node, Vector3.forward).magnitude;
    }

    public Vector3 GetGatherOffsetForDirection(ResourceNode node, Vector3 directionFromResource)
    {
        if (node == null)
        {
            return Vector3.zero;
        }

        Vector3 direction = directionFromResource;
        direction.y = 0f;

        if (direction.sqrMagnitude < 0.0001f)
        {
            direction = Vector3.forward;
        }

        direction.Normalize();
        Vector3 resourceCenter = node.transform.position;

        if (!TryGetResourceSurfacePoint(node, direction, out Vector3 surfacePoint))
        {
            float fallbackDistance = node.collisionRadius + AgentRadius + Mathf.Max(0.05f, gatherSurfaceGap);
            return direction * fallbackDistance;
        }

        float safeGap = Mathf.Max(0.05f, gatherSurfaceGap);
        Vector3 standingPosition = surfacePoint + direction * (AgentRadius + safeGap);
        standingPosition.y = transform.position.y;

        Vector3 offset = standingPosition - resourceCenter;
        offset.y = 0f;
        return offset;
    }

    private bool TryGetResourceSurfacePoint(ResourceNode node, Vector3 direction, out Vector3 surfacePoint)
    {
        Collider[] colliders = node.GetComponentsInChildren<Collider>(true);

        if (TryGetSurfacePointFromColliders(colliders, direction, false, node.transform.position, out surfacePoint))
        {
            return true;
        }

        return TryGetSurfacePointFromColliders(colliders, direction, true, node.transform.position, out surfacePoint);
    }

    private bool TryGetSurfacePointFromColliders(
        Collider[] colliders,
        Vector3 direction,
        bool useTriggers,
        Vector3 center,
        out Vector3 surfacePoint)
    {
        surfacePoint = center;

        if (colliders == null || colliders.Length == 0)
        {
            return false;
        }

        bool found = false;
        float bestProjection = float.NegativeInfinity;
        Vector3 probe = center + direction * 1000f;

        foreach (Collider current in colliders)
        {
            if (current == null || !current.enabled || current.isTrigger != useTriggers)
            {
                continue;
            }

            Vector3 currentProbe = probe;
            currentProbe.y = current.bounds.center.y;
            Vector3 candidate = current.ClosestPoint(currentProbe);
            candidate.y = center.y;
            float projection = Vector3.Dot(candidate - center, direction);

            if (!found || projection > bestProjection)
            {
                found = true;
                bestProjection = projection;
                surfacePoint = candidate;
            }
        }

        return found;
    }

    private void SetupAgent()
    {
        float radius = IsWorker() ? collisionRadius * Mathf.Clamp(workerAgentRadiusMultiplier, 0.5f, 1f) : collisionRadius;

        agent.radius = Mathf.Max(0.1f, radius);
        agent.height = 2f;
        agent.speed = Mathf.Max(0.1f, moveSpeed);
        agent.acceleration = Mathf.Max(1f, acceleration);
        agent.angularSpeed = Mathf.Max(90f, angularSpeed);
        agent.stoppingDistance = Mathf.Max(0f, stopDistance);
        agent.autoBraking = true;
        agent.autoRepath = true;
        // 项目单位模型的 Pivot 位于身体中心，场景又是固定平面。
        // 手动同步 XZ，保留原本 Y，避免 Agent 把模型吸到地面内部。
        agent.updatePosition = false;
        agent.updateRotation = true;
        agent.avoidancePriority = Mathf.Clamp(avoidancePriority + Mathf.Abs(GetInstanceID()) % 21 - 10, 0, 99);
        agent.obstacleAvoidanceType = useHighQualityAvoidance
            ? ObstacleAvoidanceType.HighQualityObstacleAvoidance
            : ObstacleAvoidanceType.MedQualityObstacleAvoidance;
    }

    private void SetupRigidbody()
    {
        rb.useGravity = false;
        rb.isKinematic = true;
        rb.interpolation = RigidbodyInterpolation.None;
        rb.collisionDetectionMode = CollisionDetectionMode.Discrete;
        rb.constraints = RigidbodyConstraints.FreezeRotation | RigidbodyConstraints.FreezePositionY;
    }

    private void SetupCollider()
    {
        unitCollider.radius = collisionRadius;
        unitCollider.height = 2f;
        unitCollider.center = Vector3.zero;
        // 单位移动完全由 NavMeshAgent 控制，Trigger 只用于点击选择，
        // 避免刚体接触把单位卡在建筑或其他单位边缘。
        unitCollider.isTrigger = true;
    }

    private void IgnoreUnitPhysicsCollisions()
    {
        Unit[] units = FindObjectsByType<Unit>(FindObjectsSortMode.None);

        foreach (Unit other in units)
        {
            if (other == null || other == this)
            {
                continue;
            }

            Collider otherCollider = other.GetComponent<Collider>();
            if (otherCollider != null)
            {
                Physics.IgnoreCollision(unitCollider, otherCollider, true);
            }
        }
    }

    private float AgentRadius
    {
        get
        {
            if (agent != null)
            {
                return agent.radius;
            }

            return Mathf.Max(0.1f, collisionRadius);
        }
    }

    private void EnsureHealthBar()
    {
        if (GetComponent<UnitHealthBar>() == null)
        {
            gameObject.AddComponent<UnitHealthBar>();
        }
    }

    private void EnsureDamageFlash()
    {
        damageFlash = GetComponent<DamageFlash>();

        if (damageFlash == null)
        {
            damageFlash = gameObject.AddComponent<DamageFlash>();
        }
    }

    private void CreateSelectionCircle()
    {
        selectionCircle = new GameObject("SelectionCircle");
        selectionCircle.transform.SetParent(transform);
        selectionCircle.transform.localPosition = new Vector3(0f, selectionCircleHeight, 0f);
        selectionCircle.transform.localRotation = Quaternion.identity;

        LineRenderer lineRenderer = selectionCircle.AddComponent<LineRenderer>();
        lineRenderer.useWorldSpace = false;
        lineRenderer.loop = true;
        lineRenderer.widthMultiplier = 0.08f;
        lineRenderer.positionCount = 64;

        Material material = new Material(Shader.Find("Sprites/Default"));
        material.color = Color.yellow;
        lineRenderer.material = material;

        for (int i = 0; i < 64; i++)
        {
            float angle = i * Mathf.PI * 2f / 64f;
            lineRenderer.SetPosition(i, new Vector3(
                Mathf.Cos(angle) * selectionCircleRadius,
                0f,
                Mathf.Sin(angle) * selectionCircleRadius));
        }
    }

    private void LookAtTarget(Vector3 target)
    {
        Vector3 direction = target - transform.position;
        direction.y = 0f;

        if (direction.sqrMagnitude > 0.001f)
        {
            transform.rotation = Quaternion.LookRotation(direction);
        }
    }

    private float GetXZDistance(Vector3 a, Vector3 b)
    {
        return Vector2.Distance(new Vector2(a.x, a.z), new Vector2(b.x, b.z));
    }
}
