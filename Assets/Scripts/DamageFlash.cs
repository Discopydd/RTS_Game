using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Renderer))]
public class DamageFlash : MonoBehaviour
{
    public Color flashColor = Color.white;
    private float flashDuration = 0.08f;

    private Renderer targetRenderer;
    private Material targetMaterial;
    private Color originalColor;
    private Coroutine flashCoroutine;
    private void Awake()
    {
        targetRenderer = GetComponent<Renderer>();

        if (targetRenderer != null)
        {
            targetMaterial = targetRenderer.material;
            originalColor = targetMaterial.color;
        }
    }

    public void Flash()
    {
        if (targetMaterial == null)
        {
            return;
        }
        if (flashCoroutine != null)
        {
            StopCoroutine(flashCoroutine);
        }

        flashCoroutine = StartCoroutine(FlashRoutine());
    }
    private IEnumerator FlashRoutine()
    {
        targetMaterial.color = flashColor;

        yield return new WaitForSeconds(flashDuration);

        targetMaterial.color = originalColor;
        flashCoroutine = null;
    }
}
