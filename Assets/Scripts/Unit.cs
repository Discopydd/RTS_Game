using UnityEngine;

public enum UnitTeam
{
    Player,
    Enemy
}

public enum UnitCommandState
{
    Idle,
    Move,
    AttackTarget,
    AttackMove,
    GatherResource
}

[RequireComponent(typeof(CapsuleCollider))]
[RequireComponent(typeof(Rigidbody))]
public class Unit : MonoBehaviour
{
    [Header("Team Settings")]
    public UnitTeam team = UnitTeam.Player;

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
    public int gatherAmount = 5;
    public float gatherRange = 1.8f;
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
    private Vector3 gatherOffsetFromResource;
    private float gatherTimer = 0f;

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
                commandState = UnitCommandState.Idle;
                resourceTarget = null;
                isMoving = false;
                rb.linearVelocity = Vector3.zero;
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
            resourceTarget = null;
            commandState = UnitCommandState.Idle;
            isMoving = false;
            rb.linearVelocity = Vector3.zero;
            return;
        }

        Vector3 gatherPosition = GetCurrentGatherPosition();
        float distanceToGatherPosition = GetXZDistance(transform.position, gatherPosition);
        float distanceToResource = GetXZDistance(transform.position, resourceTarget.transform.position);
        float effectiveGatherRange = GetEffectiveGatherRange();

        if (distanceToGatherPosition <= Mathf.Max(stopDistance * 4f, collisionRadius * 0.75f) ||
            distanceToResource <= effectiveGatherRange)
        {
            isMoving = false;
            rb.linearVelocity = Vector3.zero;

            LookAtTarget(resourceTarget.transform.position);

            if (gatherTimer <= 0f)
            {
                int gatheredAmount = resourceTarget.TakeResource(gatherAmount);

                if (PlayerResources.Instance != null)
                {
                    PlayerResources.Instance.AddMinerals(gatheredAmount);
                }

                gatherTimer = gatherCooldown;
            }
        }
        else
        {
            targetPosition = gatherPosition;
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
            resourceTarget.collisionRadius + collisionRadius + 0.35f
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
        commandState = UnitCommandState.Move;
    }

    public void GatherResource(ResourceNode target, Vector3 offsetFromResource)
    {
        if (!canGather)
        {
            return;
        }

        if (target == null || target.IsEmpty())
        {
            return;
        }

        CancelMoveCommandLine();

        resourceTarget = target;
        gatherOffsetFromResource = offsetFromResource;

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
        resourceTarget = null;

        commandState = UnitCommandState.AttackTarget;

        targetPosition = attackTarget.transform.position + attackOffsetFromTarget;
        targetPosition.y = transform.position.y;

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