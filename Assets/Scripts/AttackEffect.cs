using UnityEngine;

public class AttackEffect : MonoBehaviour
{
    public static void Create(Vector3 startPosition, Vector3 endPosition)
    {
        GameObject effectObject = new GameObject("AttackEffect");

        LineRenderer lineRenderer = effectObject.AddComponent<LineRenderer>();

        lineRenderer.useWorldSpace = true;
        lineRenderer.positionCount = 2;
        lineRenderer.widthMultiplier = 0.08f;

        Material material = new Material(Shader.Find("Sprites/Default"));
        material.color = Color.red;
        lineRenderer.material = material;

        startPosition += Vector3.up * 1.0f;
        endPosition += Vector3.up * 1.0f;

        lineRenderer.SetPosition(0, startPosition);
        lineRenderer.SetPosition(1, endPosition);

        Destroy(effectObject, 0.12f);
    }
}