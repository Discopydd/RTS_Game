using UnityEngine;

[RequireComponent(typeof(CapsuleCollider))]
[RequireComponent(typeof(Rigidbody))]
public class Unit : MonoBehaviour
{
    [Header("Move Settings")]
    public float moveSpeed = 5f;
    public float stopDistance = 0.08f;

    [Header("Collision Settings")]
    public float collisionRadius = 0.6f;

    [Header("Selection Circle")]
    public float selectionCircleRadius = 0.7f;
    public float selectionCircleHeight = -0.95f;

    private Vector3 targetPosition;
    private bool isMoving = false;

    private Rigidbody rb;
    private GameObject selectionCircle;
    private MoveCommandLine currentMoveCommandLine;

    private void Start()
    {
        targetPosition = transform.position;

        rb = GetComponent<Rigidbody>();

        SetupRigidbody();
        SetupCollider();
        CreateSelectionCircle();
        SetSelected(false);
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

        Vector3 currentPosition = rb.position;
        Vector3 direction = targetPosition - currentPosition;
        direction.y = 0f;

        float distance = direction.magnitude;

        if (distance <= stopDistance)
        {
            isMoving = false;
            rb.linearVelocity = Vector3.zero;
            return;
        }

        direction.Normalize();

        Vector3 nextPosition = currentPosition + direction * moveSpeed * Time.fixedDeltaTime;
        nextPosition.y = currentPosition.y;

        rb.MovePosition(nextPosition);
    }

    public void MoveTo(Vector3 position)
    {
        position.y = transform.position.y;
        targetPosition = position;
        isMoving = true;
    }

    public bool IsMoving()
    {
        return isMoving;
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
    public void SetSelected(bool selected)
    {
        if (selectionCircle != null)
        {
            selectionCircle.SetActive(selected);
        }
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