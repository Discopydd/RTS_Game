using UnityEngine;

public class MoveCommandLine : MonoBehaviour
{
    private Unit unit;
    private Vector3 clickPosition;

    private LineRenderer mainLine;
    private LineRenderer arrowLine;

    private float lineHeight;
    private float arrowLength;
    private float arrowWidth;

    public void Initialize(
        Unit targetUnit,
        Vector3 targetClickPosition,
        float height,
        float width,
        float length,
        float arrowHeadWidth,
        Material lineMaterial
    )
    {
        unit = targetUnit;
        clickPosition = targetClickPosition;

        lineHeight = height;
        arrowLength = length;
        arrowWidth = arrowHeadWidth;

        mainLine = gameObject.AddComponent<LineRenderer>();
        mainLine.useWorldSpace = true;
        mainLine.positionCount = 2;
        mainLine.widthMultiplier = width;
        mainLine.material = lineMaterial;

        GameObject arrowObject = new GameObject("ArrowHead");
        arrowObject.transform.SetParent(transform);

        arrowLine = arrowObject.AddComponent<LineRenderer>();
        arrowLine.useWorldSpace = true;
        arrowLine.positionCount = 3;
        arrowLine.widthMultiplier = width;
        arrowLine.material = lineMaterial;
    }

    private void Update()
    {
        if (unit == null)
        {
            Destroy(gameObject);
            return;
        }

        if (!unit.IsMoving())
        {
            unit.ClearMoveCommandLine(this);
            Destroy(gameObject);
            return;
        }

        UpdateLine();
    }

    private void OnDestroy()
    {
        if (unit != null)
        {
            unit.ClearMoveCommandLine(this);
        }
    }

    private void UpdateLine()
    {
        Vector3 startPosition = unit.transform.position;
        Vector3 endPosition = clickPosition;

        startPosition.y = lineHeight;
        endPosition.y = lineHeight;

        Vector3 direction = endPosition - startPosition;

        if (direction.magnitude < 0.1f)
        {
            return;
        }

        direction.Normalize();

        mainLine.SetPosition(0, startPosition);
        mainLine.SetPosition(1, endPosition);

        Vector3 right = new Vector3(-direction.z, 0f, direction.x);

        Vector3 leftPoint = endPosition - direction * arrowLength + right * arrowWidth;
        Vector3 rightPoint = endPosition - direction * arrowLength - right * arrowWidth;

        arrowLine.SetPosition(0, leftPoint);
        arrowLine.SetPosition(1, endPosition);
        arrowLine.SetPosition(2, rightPoint);
    }
}