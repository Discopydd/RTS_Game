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

    private List<Unit> selectedUnits = new List<Unit>();

    private Vector3 dragStartPosition;
    private Vector3 dragEndPosition;
    private bool isDragging = false;

    private void Update()
    {
        HandleSelection();
        HandleMovement();
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
            Unit unit = hit.collider.GetComponent<Unit>();

            if (unit != null)
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
            Vector3 screenPosition = mainCamera.WorldToScreenPoint(unit.transform.position);

            if (selectionRect.Contains(screenPosition))
            {
                selectedUnits.Add(unit);
                unit.SetSelected(true);
            }
        }
    }

    private void HandleMovement()
    {
        if (!Input.GetMouseButtonDown(1))
        {
            return;
        }

        if (selectedUnits.Count == 0)
        {
            return;
        }

        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);

        if (Physics.Raycast(ray, out RaycastHit hit))
        {
            MoveSelectedUnits(hit.point);
        }
    }

    private void MoveSelectedUnits(Vector3 clickPosition)
    {
        List<Unit> unitsToMove = new List<Unit>(selectedUnits);

        unitsToMove.Sort((a, b) =>
        {
            float distanceA = Vector3.Distance(a.transform.position, clickPosition);
            float distanceB = Vector3.Distance(b.transform.position, clickPosition);

            return distanceA.CompareTo(distanceB);
        });

        List<Vector3> reservedPositions = GetReservedPositions(unitsToMove);

        foreach (Unit unit in unitsToMove)
        {
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

    private List<Vector3> GetReservedPositions(List<Unit> movingUnits)
    {
        List<Vector3> reservedPositions = new List<Vector3>();

        Unit[] allUnits = FindObjectsByType<Unit>(FindObjectsSortMode.None);

        foreach (Unit unit in allUnits)
        {
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

    private void ClearSelection()
    {
        foreach (Unit unit in selectedUnits)
        {
            unit.SetSelected(false);
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