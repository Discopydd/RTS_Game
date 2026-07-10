using System.Collections.Generic;
using UnityEngine;

public class RTSController : MonoBehaviour
{
    public Camera mainCamera;

    [Header("Move Line")]
    public float lineHeight = 0.08f;
    public float lineWidth = 0.06f;
    public float arrowLength = 0.5f;
    public float arrowWidth = 0.25f;

    [Header("Target Position")]
    public float positionSpacingMultiplier = 2.2f;
    public int maxSearchRing = 10;

    [Header("Attack Surround")]
    public float surroundExtraDistance = 0.15f;
    public float surroundSpacingMultiplier = 1.2f;
    public int maxSurroundRing = 5;

    [Header("Attack Target Circle")]
    public float attackTargetCircleRadius = 0.9f;
    public float attackTargetCircleHeight = -0.95f;
    public float attackTargetCircleWidth = 0.08f;

    private GameObject attackTargetCircle;
    private Unit currentAttackTarget;
    private bool isAttackMoveMode = false;

    private List<Unit> selectedUnits = new List<Unit>();

    private Vector3 dragStartPosition;
    private Vector3 dragEndPosition;
    private bool isDragging = false;

    private void Update()
    {
        CleanupSelectedUnits();
        CleanupAttackTargetCircle();

        HandleAttackMoveHotkey();

        if (isAttackMoveMode)
        {
            HandleAttackMoveClick();
            return;
        }

        HandleSelection();
        HandleMovement();
    }

    private void CleanupSelectedUnits()
    {
        selectedUnits.RemoveAll(unit => unit == null || unit.IsDead());
    }

    private void HandleSelection()
    {
        if (Input.GetMouseButtonDown(0))
        {
            dragStartPosition = Input.mousePosition;
            isDragging = true;
        }

        if (Input.GetMouseButtonUp(0))
        {
            dragEndPosition = Input.mousePosition;
            isDragging = false;

            float dragDistance = Vector3.Distance(dragStartPosition, dragEndPosition);

            if (dragDistance < 10f)
            {
                SelectSingleUnit();
            }
            else
            {
                SelectUnitsInDragBox();
            }
        }
    }

    private void SelectSingleUnit()
    {
        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);

        ClearSelection();

        if (Physics.Raycast(ray, out RaycastHit hit))
        {
            Unit unit = hit.collider.GetComponentInParent<Unit>();

            if (unit != null && !unit.IsDead() && unit.team == UnitTeam.Player)
            {
                selectedUnits.Add(unit);
                unit.SetSelected(true);
            }
        }
    }

    private void SelectUnitsInDragBox()
    {
        ClearSelection();

        Rect selectionRect = GetScreenRect(dragStartPosition, dragEndPosition);

        Unit[] allUnits = FindObjectsByType<Unit>(FindObjectsSortMode.None);

        foreach (Unit unit in allUnits)
        {
            if (unit == null || unit.IsDead())
            {
                continue;
            }

            if (unit.team != UnitTeam.Player)
            {
                continue;
            }

            Vector3 screenPosition = mainCamera.WorldToScreenPoint(unit.transform.position);

            if (selectionRect.Contains(screenPosition))
            {
                selectedUnits.Add(unit);
                unit.SetSelected(true);
            }
        }
    }

    private void HandleAttackMoveHotkey()
    {
        if (Input.GetKeyDown(KeyCode.A))
        {
            CleanupSelectedUnits();

            if (selectedUnits.Count > 0)
            {
                isAttackMoveMode = true;
            }
        }

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            isAttackMoveMode = false;
        }
    }

    private void HandleAttackMoveClick()
    {
        if (Input.GetMouseButtonDown(1))
        {
            isAttackMoveMode = false;
            return;
        }

        if (!Input.GetMouseButtonDown(0))
        {
            return;
        }

        CleanupSelectedUnits();

        if (selectedUnits.Count == 0)
        {
            isAttackMoveMode = false;
            return;
        }

        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);

        if (Physics.Raycast(ray, out RaycastHit hit))
        {
            Unit clickedUnit = hit.collider.GetComponentInParent<Unit>();

            if (clickedUnit != null &&
                !clickedUnit.IsDead() &&
                clickedUnit.team != UnitTeam.Player)
            {
                ShowAttackTargetCircle(clickedUnit);
                AttackSelectedUnits(clickedUnit);
            }
            else
            {
                ClearAttackTargetCircle();
                AttackMoveSelectedUnits(hit.point);
            }
        }

        isAttackMoveMode = false;
    }

    private void HandleMovement()
    {
        if (!Input.GetMouseButtonDown(1))
        {
            return;
        }

        CleanupSelectedUnits();

        if (selectedUnits.Count == 0)
        {
            return;
        }

        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);

        if (Physics.Raycast(ray, out RaycastHit hit))
        {
            Unit clickedUnit = hit.collider.GetComponentInParent<Unit>();

            if (clickedUnit != null &&
                !clickedUnit.IsDead() &&
                clickedUnit.team != UnitTeam.Player)
            {
                ShowAttackTargetCircle(clickedUnit);
                AttackSelectedUnits(clickedUnit);
                return;
            }
            ClearAttackTargetCircle();
            MoveSelectedUnits(hit.point);
        }
    }

    private void AttackSelectedUnits(Unit enemyTarget)
    {
        CleanupSelectedUnits();

        List<Unit> unitsToAttack = new List<Unit>(selectedUnits);

        unitsToAttack.RemoveAll(unit => unit == null || unit.IsDead());

        if (unitsToAttack.Count == 0 || enemyTarget == null || enemyTarget.IsDead())
        {
            return;
        }

        unitsToAttack.Sort((a, b) =>
        {
            float distanceA = Vector3.Distance(a.transform.position, enemyTarget.transform.position);
            float distanceB = Vector3.Distance(b.transform.position, enemyTarget.transform.position);

            return distanceA.CompareTo(distanceB);
        });

        List<Vector3> availableOffsets = GenerateSurroundOffsets(enemyTarget, unitsToAttack);

        List<Vector3> usedOffsets = new List<Vector3>();

        foreach (Unit unit in unitsToAttack)
        {
            Vector3 bestOffset = FindBestSurroundOffset(
                unit,
                enemyTarget,
                availableOffsets,
                usedOffsets
            );

            usedOffsets.Add(bestOffset);

            unit.AttackUnit(enemyTarget, bestOffset);
        }
    }

    private List<Vector3> GenerateSurroundOffsets(Unit enemyTarget, List<Unit> attackingUnits)
    {
        List<Vector3> offsets = new List<Vector3>();

        float attackerRadius = 0.6f;

        if (attackingUnits.Count > 0)
        {
            attackerRadius = attackingUnits[0].collisionRadius;
        }

        float baseRadius = enemyTarget.collisionRadius + attackerRadius + surroundExtraDistance;

        for (int ring = 1; ring <= maxSurroundRing; ring++)
        {
            float ringRadius = baseRadius + (ring - 1) * attackerRadius * 2f * surroundSpacingMultiplier;

            int pointCount = Mathf.Max(
                8,
                Mathf.CeilToInt((2f * Mathf.PI * ringRadius) / (attackerRadius * 2f * surroundSpacingMultiplier))
            );

            for (int i = 0; i < pointCount; i++)
            {
                float angle = Mathf.PI * 2f * i / pointCount;

                Vector3 offset = new Vector3(
                    Mathf.Cos(angle) * ringRadius,
                    0f,
                    Mathf.Sin(angle) * ringRadius
                );

                offsets.Add(offset);

                if (offsets.Count >= attackingUnits.Count)
                {
                    return offsets;
                }
            }
        }

        return offsets;
    }

    private Vector3 FindBestSurroundOffset(
        Unit unit,
        Unit enemyTarget,
        List<Vector3> availableOffsets,
        List<Vector3> usedOffsets
    )
    {
        Vector3 bestOffset = Vector3.zero;
        float bestDistance = Mathf.Infinity;

        foreach (Vector3 offset in availableOffsets)
        {
            if (usedOffsets.Contains(offset))
            {
                continue;
            }

            Vector3 worldPosition = enemyTarget.transform.position + offset;

            float distance = Vector3.Distance(unit.transform.position, worldPosition);

            if (distance < bestDistance)
            {
                bestDistance = distance;
                bestOffset = offset;
            }
        }

        return bestOffset;
    }

    private void MoveSelectedUnits(Vector3 clickPosition)
    {
        CleanupSelectedUnits();

        List<Unit> unitsToMove = new List<Unit>(selectedUnits);

        if (unitsToMove.Count == 0)
        {
            return;
        }

        unitsToMove.Sort((a, b) =>
        {
            float distanceA = Vector3.Distance(a.transform.position, clickPosition);
            float distanceB = Vector3.Distance(b.transform.position, clickPosition);

            return distanceA.CompareTo(distanceB);
        });

        List<Vector3> reservedPositions = GetReservedPositions(unitsToMove);

        foreach (Unit unit in unitsToMove)
        {
            if (unit == null || unit.IsDead())
            {
                continue;
            }

            Vector3 finalTargetPosition = FindClosestFreePosition(
                clickPosition,
                reservedPositions,
                unit.collisionRadius
            );

            reservedPositions.Add(finalTargetPosition);

            CreateMoveLine(unit, clickPosition);

            unit.MoveTo(finalTargetPosition);
        }
    }

    private void AttackMoveSelectedUnits(Vector3 clickPosition)
    {
        CleanupSelectedUnits();

        List<Unit> unitsToMove = new List<Unit>(selectedUnits);

        if (unitsToMove.Count == 0)
        {
            return;
        }

        unitsToMove.Sort((a, b) =>
        {
            float distanceA = Vector3.Distance(a.transform.position, clickPosition);
            float distanceB = Vector3.Distance(b.transform.position, clickPosition);

            return distanceA.CompareTo(distanceB);
        });

        List<Vector3> reservedPositions = GetReservedPositions(unitsToMove);

        foreach (Unit unit in unitsToMove)
        {
            if (unit == null || unit.IsDead())
            {
                continue;
            }

            Vector3 finalTargetPosition = FindClosestFreePosition(
                clickPosition,
                reservedPositions,
                unit.collisionRadius
            );

            reservedPositions.Add(finalTargetPosition);

            CreateMoveLine(unit, clickPosition);

            unit.AttackMoveTo(finalTargetPosition);
        }
    }
    private List<Vector3> GetReservedPositions(List<Unit> movingUnits)
    {
        List<Vector3> reservedPositions = new List<Vector3>();

        Unit[] allUnits = FindObjectsByType<Unit>(FindObjectsSortMode.None);

        foreach (Unit unit in allUnits)
        {
            if (unit == null || unit.IsDead())
            {
                continue;
            }

            if (!movingUnits.Contains(unit))
            {
                Vector3 position = unit.transform.position;
                position.y = 0f;
                reservedPositions.Add(position);
            }
        }

        return reservedPositions;
    }

    private Vector3 FindClosestFreePosition(
        Vector3 centerPosition,
        List<Vector3> reservedPositions,
        float unitRadius
    )
    {
        centerPosition.y = 0f;

        if (IsPositionFree(centerPosition, reservedPositions, unitRadius))
        {
            return centerPosition;
        }

        float spacing = unitRadius * positionSpacingMultiplier;

        for (int ring = 1; ring <= maxSearchRing; ring++)
        {
            float currentRadius = spacing * ring;
            int pointCount = Mathf.Max(8, ring * 8);

            for (int i = 0; i < pointCount; i++)
            {
                float angle = Mathf.PI * 2f * i / pointCount;

                Vector3 candidatePosition = centerPosition + new Vector3(
                    Mathf.Cos(angle) * currentRadius,
                    0f,
                    Mathf.Sin(angle) * currentRadius
                );

                if (IsPositionFree(candidatePosition, reservedPositions, unitRadius))
                {
                    return candidatePosition;
                }
            }
        }

        return centerPosition;
    }

    private bool IsPositionFree(
        Vector3 targetPosition,
        List<Vector3> reservedPositions,
        float unitRadius
    )
    {
        float minimumDistance = unitRadius * 2f;

        foreach (Vector3 reservedPosition in reservedPositions)
        {
            float distance = Vector2.Distance(
                new Vector2(targetPosition.x, targetPosition.z),
                new Vector2(reservedPosition.x, reservedPosition.z)
            );

            if (distance < minimumDistance)
            {
                return false;
            }
        }

        return true;
    }

    private void CreateMoveLine(Unit unit, Vector3 clickPosition)
    {
        if (unit == null || unit.IsDead())
        {
            return;
        }

        GameObject lineObject = new GameObject("MoveCommandLine");

        Material material = new Material(Shader.Find("Sprites/Default"));
        material.color = Color.green;

        MoveCommandLine moveCommandLine = lineObject.AddComponent<MoveCommandLine>();
        moveCommandLine.Initialize(
            unit,
            clickPosition,
            lineHeight,
            lineWidth,
            arrowLength,
            arrowWidth,
            material
        );

        unit.SetMoveCommandLine(moveCommandLine);
    }

    private void ShowAttackTargetCircle(Unit target)
    {
        ClearAttackTargetCircle();

        if (target == null || target.IsDead())
        {
            return;
        }

        currentAttackTarget = target;

        attackTargetCircle = new GameObject("AttackTargetCircle");
        attackTargetCircle.transform.SetParent(target.transform);
        attackTargetCircle.transform.localPosition = new Vector3(0f, attackTargetCircleHeight, 0f);
        attackTargetCircle.transform.localRotation = Quaternion.identity;

        LineRenderer lineRenderer = attackTargetCircle.AddComponent<LineRenderer>();

        lineRenderer.useWorldSpace = false;
        lineRenderer.loop = true;
        lineRenderer.positionCount = 64;
        lineRenderer.widthMultiplier = attackTargetCircleWidth;

        Material material = new Material(Shader.Find("Sprites/Default"));
        material.color = Color.red;
        lineRenderer.material = material;

        for (int i = 0; i < 64; i++)
        {
            float angle = Mathf.PI * 2f * i / 64f;

            float x = Mathf.Cos(angle) * attackTargetCircleRadius;
            float z = Mathf.Sin(angle) * attackTargetCircleRadius;

            lineRenderer.SetPosition(i, new Vector3(x, 0f, z));
        }
    }

    private void ClearAttackTargetCircle()
    {
        if (attackTargetCircle != null)
        {
            Destroy(attackTargetCircle);
        }

        attackTargetCircle = null;
        currentAttackTarget = null;
    }

    private void CleanupAttackTargetCircle()
    {
        if (currentAttackTarget == null || currentAttackTarget.IsDead())
        {
            ClearAttackTargetCircle();
        }
    }
    private void ClearSelection()
    {
        foreach (Unit unit in selectedUnits)
        {
            if (unit != null)
            {
                unit.SetSelected(false);
            }
        }

        selectedUnits.Clear();
    }

    private Rect GetScreenRect(Vector3 start, Vector3 end)
    {
        float xMin = Mathf.Min(start.x, end.x);
        float xMax = Mathf.Max(start.x, end.x);
        float yMin = Mathf.Min(start.y, end.y);
        float yMax = Mathf.Max(start.y, end.y);

        return Rect.MinMaxRect(xMin, yMin, xMax, yMax);
    }

    private void OnGUI()
    {
        if (isDragging)
        {
            Rect rect = GetScreenRectForGUI(dragStartPosition, Input.mousePosition);
            GUI.Box(rect, "");
        }

        if (isAttackMoveMode)
        {
            GUI.Label(new Rect(20, 20, 300, 30), "Attack Move Mode: Left Click");
        }
    }

    private Rect GetScreenRectForGUI(Vector3 start, Vector3 end)
    {
        start.y = Screen.height - start.y;
        end.y = Screen.height - end.y;

        float xMin = Mathf.Min(start.x, end.x);
        float xMax = Mathf.Max(start.x, end.x);
        float yMin = Mathf.Min(start.y, end.y);
        float yMax = Mathf.Max(start.y, end.y);

        return Rect.MinMaxRect(xMin, yMin, xMax, yMax);
    }
}