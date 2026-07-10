using UnityEngine;

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

    [Header("Collision Settings")]
    public float collisionRadius = 0.6f;

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

    [Header("Selection Circle")]
    public float selectionCircleRadius = 0.7f;
    public float selectionCircleHeight = -0.95f;

    private int currentHp;
    private float attackTimer = 0f;

    private Unit attackTarget;
    private Vector3 attackOffsetFromTarget;
    private Vector3 attackMoveDestination;

    private ResourceNode resourceTarget;
    private ResourceDepot depotTarget;

    private Vector3 gatherOffsetFromResource;
    private Vector3 depositOffsetFromDepot;

    private float gatherTimer = 0f;
    private int carriedMinerals = 0;

    private Vector3 targetPosition;
    private bool isMoving = false;

    private UnitCommandState commandState = UnitCommandState.Idle;

    private Rigidbody rb;
    private GameObject selectionCircle;
    private MoveCommandLine currentMoveCommandLine;
    private DamageFlash damageFlash;

    private void Start()
    {
        currentHp = maxHp;
        targetPosition = transform.position;
        attackMoveDestination = transform.position;

        rb = GetComponent<Rigidbody>();

        // 如果场景里的对象名字是 Worker / Worker (1) / Worker (2)，
        // 运行时自动识别成工人，避免 Inspector 里 Role 还保持 Soldier 时无法采集。
        if (gameObject.name.Contains("Worker"))
        {
            role = UnitRole.Worker;
            canGather = true;
        }

        SetupRigidbody();
        SetupCollider();
        CreateSelectionCircle();
        EnsureHealthBar();
        EnsureDamageFlash();
        SetSelected(false);
    }

    private void Update()
    {
        HandleAttack();
    }

    private void FixedUpdate()
    {
        MoveWithCollision();
    }

    private void MoveWithCollision()
    {
        if (!isMoving)
        {
            rb.linearVelocity = Vector3.zero;
            return;
        }

        if (commandState == UnitCommandState.AttackTarget)
        {
            if (attackTarget == null || attackTarget.IsDead())
            {
                commandState = UnitCommandState.Idle;
                attackTarget = null;
                isMoving = false;
                rb.linearVelocity = Vector3.zero;
                return;
            }

            targetPosition = attackTarget.transform.position + attackOffsetFromTarget;
            targetPosition.y = transform.position.y;
        }

        if (commandState == UnitCommandState.GatherResource)
        {
            if (resourceTarget == null || resourceTarget.IsEmpty())
            {
                if (carriedMinerals > 0 && depotTarget != null)
                {
                    BeginReturnToDepot();
                }
                else if (!TrySwitchToNearbyResource())
                {
                    commandState = UnitCommandState.Idle;
                    resourceTarget = null;
                    isMoving = false;
                    rb.linearVelocity = Vector3.zero;
                }

                return;
            }

            targetPosition = GetCurrentGatherPosition();
        }

        Vector3 currentPosition = rb.position;
        Vector3 direction = targetPosition - currentPosition;
        direction.y = 0f;

        float distance = direction.magnitude;

        if (distance <= stopDistance)
        {
            isMoving = false;
            rb.linearVelocity = Vector3.zero;

            if (commandState == UnitCommandState.Move)
            {
                commandState = UnitCommandState.Idle;
            }

            if (commandState == UnitCommandState.AttackMove &&
                GetXZDistance(transform.position, attackMoveDestination) <= stopDistance * 2f)
            {
                commandState = UnitCommandState.Idle;
            }

            return;
        }

        direction.Normalize();

        Vector3 nextPosition = currentPosition + direction * moveSpeed * Time.fixedDeltaTime;
        nextPosition.y = currentPosition.y;

        rb.MovePosition(nextPosition);
    }

    private void HandleAttack()
    {
        attackTimer -= Time.deltaTime;
        gatherTimer -= Time.deltaTime;

        if (commandState == UnitCommandState.GatherResource)
        {
            HandleGatherCommand();
            return;
        }

        if (commandState == UnitCommandState.ReturnResource)
        {
            HandleReturnResourceCommand();
            return;
        }

        if (commandState == UnitCommandState.Move)
        {
            attackTarget = null;
            return;
        }

        if (commandState == UnitCommandState.AttackTarget)
        {
            HandleAttackTargetCommand();
            return;
        }

        if (commandState == UnitCommandState.AttackMove)
        {
            HandleAttackMoveCommand();
            return;
        }

        HandleAutoAttack();
    }

    private void HandleAttackTargetCommand()
    {
        if (attackTarget == null || attackTarget.IsDead())
        {
            attackTarget = null;
            commandState = UnitCommandState.Idle;
            isMoving = false;
            return;
        }

        float distance = GetXZDistance(transform.position, attackTarget.transform.position);

        if (distance <= attackRange)
        {
            isMoving = false;
            rb.linearVelocity = Vector3.zero;

            LookAtTarget(attackTarget.transform.position);

            if (attackTimer <= 0f)
            {
                Attack(attackTarget);
            }
        }
        else
        {
            targetPosition = attackTarget.transform.position + attackOffsetFromTarget;
            targetPosition.y = transform.position.y;
            isMoving = true;
        }
    }

    private void HandleAttackMoveCommand()
    {
        if (attackTarget == null || attackTarget.IsDead())
        {
            attackTarget = FindNearestEnemy();
        }

        if (attackTarget == null)
        {
            targetPosition = attackMoveDestination;
            targetPosition.y = transform.position.y;

            if (GetXZDistance(transform.position, attackMoveDestination) <= stopDistance * 2f)
            {
                isMoving = false;
                commandState = UnitCommandState.Idle;
            }
            else
            {
                isMoving = true;
            }

            return;
        }

        float distance = GetXZDistance(transform.position, attackTarget.transform.position);

        if (distance > detectRange)
        {
            attackTarget = null;
            targetPosition = attackMoveDestination;
            targetPosition.y = transform.position.y;
            isMoving = true;
            return;
        }

        if (distance <= attackRange)
        {
            isMoving = false;
            rb.linearVelocity = Vector3.zero;

            LookAtTarget(attackTarget.transform.position);

            if (attackTimer <= 0f)
            {
                Attack(attackTarget);
            }
        }
        else
        {
            targetPosition = attackTarget.transform.position;
            targetPosition.y = transform.position.y;
            isMoving = true;
        }
    }

    private void HandleGatherCommand()
    {
        if (resourceTarget == null || resourceTarget.IsEmpty())
        {
            if (carriedMinerals > 0)
            {
                BeginReturnToDepot();
                return;
            }

            if (TrySwitchToNearbyResource())
            {
                return;
            }

            resourceTarget = null;
            commandState = UnitCommandState.Idle;
            isMoving = false;
            return;
        }

        if (carriedMinerals >= carryCapacity)
        {
            BeginReturnToDepot();
            return;
        }

        float distance = GetXZDistance(transform.position, resourceTarget.transform.position);
        float effectiveGatherRange = GetEffectiveGatherRange();

        if (distance <= effectiveGatherRange)
        {
            isMoving = false;
            rb.linearVelocity = Vector3.zero;

            LookAtTarget(resourceTarget.transform.position);

            if (gatherTimer <= 0f)
            {
                int needAmount = carryCapacity - carriedMinerals;
                int takeAmount = Mathf.Min(gatherAmount, needAmount);

                int gatheredAmount = resourceTarget.TakeResource(takeAmount);
                carriedMinerals += gatheredAmount;

                gatherTimer = gatherCooldown;

                if (carriedMinerals >= carryCapacity || resourceTarget == null || resourceTarget.IsEmpty())
                {
                    BeginReturnToDepot();
                }
            }
        }
        else
        {
            targetPosition = resourceTarget.transform.position + gatherOffsetFromResource;
            targetPosition.y = transform.position.y;
            isMoving = true;
        }
    }

    private void BeginReturnToDepot()
    {
        if (depotTarget == null)
        {
            Debug.LogWarning(gameObject.name + " has no depot target.");
            commandState = UnitCommandState.Idle;
            isMoving = false;
            return;
        }

        targetPosition = depotTarget.transform.position + depositOffsetFromDepot;
        targetPosition.y = transform.position.y;

        commandState = UnitCommandState.ReturnResource;
        isMoving = true;
    }

    private void HandleReturnResourceCommand()
    {
        if (carriedMinerals <= 0)
        {
            commandState = UnitCommandState.Idle;
            isMoving = false;
            return;
        }

        if (depotTarget == null)
        {
            Debug.LogWarning(gameObject.name + " cannot find depot.");
            commandState = UnitCommandState.Idle;
            isMoving = false;
            return;
        }

        float distance = GetXZDistance(transform.position, depotTarget.transform.position);

        if (distance <= GetEffectiveDepositRange())
        {
            isMoving = false;
            rb.linearVelocity = Vector3.zero;

            LookAtTarget(depotTarget.transform.position);

            if (PlayerResources.Instance != null)
            {
                PlayerResources.Instance.AddMinerals(carriedMinerals);
            }

            carriedMinerals = 0;

            if (resourceTarget != null && !resourceTarget.IsEmpty())
            {
                commandState = UnitCommandState.GatherResource;
                targetPosition = resourceTarget.transform.position + gatherOffsetFromResource;
                targetPosition.y = transform.position.y;
                isMoving = true;
            }
            else if (TrySwitchToNearbyResource())
            {
                return;
            }
            else
            {
                resourceTarget = null;
                commandState = UnitCommandState.Idle;
                isMoving = false;
            }
        }
        else
        {
            targetPosition = depotTarget.transform.position + depositOffsetFromDepot;
            targetPosition.y = transform.position.y;
            isMoving = true;
        }
    }

    private Vector3 GetCurrentGatherPosition()
    {
        if (resourceTarget == null)
        {
            return transform.position;
        }

        Vector3 position = resourceTarget.transform.position + gatherOffsetFromResource;
        position.y = transform.position.y;
        return position;
    }

    private float GetEffectiveGatherRange()
    {
        if (resourceTarget == null)
        {
            return gatherRange;
        }

        return Mathf.Max(
            gatherRange,
            resourceTarget.collisionRadius + collisionRadius + 0.85f
        );
    }

    private float GetEffectiveDepositRange()
    {
        if (depotTarget == null)
        {
            return depositRange;
        }

        return Mathf.Max(
            depositRange,
            depotTarget.collisionRadius + collisionRadius + 0.85f
        );
    }

    private void HandleAutoAttack()
    {
        if (attackTarget == null || attackTarget.IsDead())
        {
            attackTarget = FindNearestEnemy();
        }

        if (attackTarget == null)
        {
            return;
        }

        float distance = GetXZDistance(transform.position, attackTarget.transform.position);

        if (distance > detectRange)
        {
            attackTarget = null;
            return;
        }

        if (distance <= attackRange)
        {
            isMoving = false;
            rb.linearVelocity = Vector3.zero;

            LookAtTarget(attackTarget.transform.position);

            if (attackTimer <= 0f)
            {
                Attack(attackTarget);
            }
        }
    }

    private Unit FindNearestEnemy()
    {
        Unit[] allUnits = FindObjectsByType<Unit>(FindObjectsSortMode.None);

        Unit nearestEnemy = null;
        float nearestDistance = Mathf.Infinity;

        foreach (Unit unit in allUnits)
        {
            if (unit == this)
            {
                continue;
            }

            if (unit == null || unit.IsDead())
            {
                continue;
            }

            if (unit.team == team)
            {
                continue;
            }

            float distance = GetXZDistance(transform.position, unit.transform.position);

            if (distance < nearestDistance && distance <= detectRange)
            {
                nearestDistance = distance;
                nearestEnemy = unit;
            }
        }

        return nearestEnemy;
    }

    public void MoveTo(Vector3 position)
    {
        position.y = transform.position.y;

        targetPosition = position;
        isMoving = true;
        attackTarget = null;
        resourceTarget = null;
        depotTarget = null;
        commandState = UnitCommandState.Move;
    }

    public void GatherResource(
    ResourceNode target,
    ResourceDepot depot,
    Vector3 offsetFromResource,
    Vector3 offsetFromDepot
)
    {
        if (!CanGatherResource())
        {
            return;
        }

        if (target == null || target.IsEmpty())
        {
            return;
        }

        if (depot == null)
        {
            Debug.LogWarning("No ResourceDepot found.");
            return;
        }

        CancelMoveCommandLine();

        resourceTarget = target;
        depotTarget = depot;

        gatherOffsetFromResource = offsetFromResource;
        depositOffsetFromDepot = offsetFromDepot;

        attackTarget = null;
        commandState = UnitCommandState.GatherResource;

        targetPosition = resourceTarget.transform.position + gatherOffsetFromResource;
        targetPosition.y = transform.position.y;

        isMoving = true;
    }

    public void AttackMoveTo(Vector3 position)
    {
        position.y = transform.position.y;

        attackMoveDestination = position;
        targetPosition = position;
        attackTarget = null;
        resourceTarget = null;
        depotTarget = null;

        isMoving = true;
        commandState = UnitCommandState.AttackMove;
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

        commandState = UnitCommandState.AttackTarget;

        targetPosition = attackTarget.transform.position + attackOffsetFromTarget;
        targetPosition.y = transform.position.y;

        resourceTarget = null;
        depotTarget = null;

        isMoving = true;
    }

    public bool IsMoving()
    {
        return isMoving;
    }

    public bool IsDead()
    {
        return currentHp <= 0;
    }

    public float GetHpPercent()
    {
        return (float)currentHp / maxHp;
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
        attackTimer = attackCooldown;
    }

    public void TakeDamage(int damage)
    {
        currentHp -= damage;
        currentHp = Mathf.Clamp(currentHp, 0, maxHp);

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
        CancelMoveCommandLine();

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

    private void LookAtTarget(Vector3 target)
    {
        Vector3 direction = target - transform.position;
        direction.y = 0f;

        if (direction.sqrMagnitude <= 0.01f)
        {
            return;
        }

        transform.rotation = Quaternion.LookRotation(direction);
    }

    private float GetXZDistance(Vector3 a, Vector3 b)
    {
        Vector2 posA = new Vector2(a.x, a.z);
        Vector2 posB = new Vector2(b.x, b.z);

        return Vector2.Distance(posA, posB);
    }

    private ResourceNode FindNearestAvailableResource(Vector3 fromPosition, float searchRange)
    {
        ResourceNode[] allResources = FindObjectsByType<ResourceNode>(FindObjectsSortMode.None);

        ResourceNode nearest = null;
        float nearestDistance = Mathf.Infinity;

        foreach (ResourceNode node in allResources)
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
        ResourceNode nextResource = FindNearestAvailableResource(transform.position, resourceSearchRange);

        if (nextResource == null)
        {
            return false;
        }

        resourceTarget = nextResource;
        gatherOffsetFromResource = GetDefaultGatherOffset(nextResource);

        commandState = UnitCommandState.GatherResource;
        targetPosition = resourceTarget.transform.position + gatherOffsetFromResource;
        targetPosition.y = transform.position.y;
        isMoving = true;

        return true;
    }

    private Vector3 GetDefaultGatherOffset(ResourceNode node)
    {
        if (node == null)
        {
            return Vector3.zero;
        }

        Vector3 direction = transform.position - node.transform.position;
        direction.y = 0f;

        if (direction.sqrMagnitude < 0.01f)
        {
            direction = Vector3.forward;
        }

        direction.Normalize();

        float distance = node.collisionRadius + collisionRadius + 0.05f;
        return direction * distance;
    }

    private void SetupRigidbody()
    {
        rb.useGravity = false;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
        rb.constraints =
            RigidbodyConstraints.FreezeRotationX |
            RigidbodyConstraints.FreezeRotationY |
            RigidbodyConstraints.FreezeRotationZ |
            RigidbodyConstraints.FreezePositionY;
    }

    private void SetupCollider()
    {
        CapsuleCollider capsuleCollider = GetComponent<CapsuleCollider>();

        capsuleCollider.radius = collisionRadius;
        capsuleCollider.height = 2f;
        capsuleCollider.center = Vector3.zero;
        capsuleCollider.isTrigger = false;
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
            float x = Mathf.Cos(angle) * selectionCircleRadius;
            float z = Mathf.Sin(angle) * selectionCircleRadius;

            lineRenderer.SetPosition(i, new Vector3(x, 0f, z));
        }
    }
}