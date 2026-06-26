using UnityEngine;

public class CameraController : MonoBehaviour
{
    [Header("Move Settings")]
    public float moveSpeed = 15f;
    public float edgeSize = 20f;

    [Header("Zoom Settings")]
    public float zoomSpeed = 5f;
    public float minZoom = 5f;
    public float maxZoom = 25f;

    private Camera cam;

    private void Start()
    {
        cam = GetComponent<Camera>();
    }

    private void Update()
    {
        MoveCameraByKeyboard();
        MoveCameraByMouseEdge();
        ZoomCamera();
    }

    private void MoveCameraByKeyboard()
    {
        float horizontal = 0f;
        float vertical = 0f;

        if (Input.GetKey(KeyCode.A))
        {
            horizontal = -1f;
        }
        else if (Input.GetKey(KeyCode.D))
        {
            horizontal = 1f;
        }

        if (Input.GetKey(KeyCode.W))
        {
            vertical = 1f;
        }
        else if (Input.GetKey(KeyCode.S))
        {
            vertical = -1f;
        }

        MoveCamera(horizontal, vertical);
    }

    private void MoveCameraByMouseEdge()
    {
        Vector3 mousePosition = Input.mousePosition;

        float horizontal = 0f;
        float vertical = 0f;

        // 鼠标靠近左边
        if (mousePosition.x <= edgeSize)
        {
            horizontal = -1f;
        }
        // 鼠标靠近右边
        else if (mousePosition.x >= Screen.width - edgeSize)
        {
            horizontal = 1f;
        }

        // 鼠标靠近下边
        if (mousePosition.y <= edgeSize)
        {
            vertical = -1f;
        }
        // 鼠标靠近上边
        else if (mousePosition.y >= Screen.height - edgeSize)
        {
            vertical = 1f;
        }

        MoveCamera(horizontal, vertical);
    }

    private void MoveCamera(float horizontal, float vertical)
    {
        if (horizontal == 0f && vertical == 0f)
        {
            return;
        }

        Vector3 forward = transform.forward;
        forward.y = 0f;
        forward.Normalize();

        Vector3 right = transform.right;
        right.y = 0f;
        right.Normalize();

        Vector3 moveDirection = forward * vertical + right * horizontal;

        transform.position += moveDirection.normalized * moveSpeed * Time.deltaTime;
    }

    private void ZoomCamera()
    {
        float scroll = Input.GetAxis("Mouse ScrollWheel");

        if (scroll != 0f)
        {
            cam.orthographicSize -= scroll * zoomSpeed;
            cam.orthographicSize = Mathf.Clamp(cam.orthographicSize, minZoom, maxZoom);
        }
    }
}