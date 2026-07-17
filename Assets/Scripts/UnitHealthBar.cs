using UnityEngine;
using UnityEngine.Rendering;

[RequireComponent(typeof(Unit))]
public class UnitHealthBar : MonoBehaviour
{
    [Header("Health Bar Settings")]
    public Vector3 offset = new Vector3(0f, 2.3f, 0f);
    public float barWidth = 1.2f;
    public float barHeight = 0.12f;
    public float barDepth = 0.05f;

    private Unit unit;
    private Camera mainCamera;

    private GameObject healthBarRoot;
    private GameObject backgroundBar;
    private GameObject fillBar;

    private Material backgroundMaterial;
    private Material fillMaterial;

    private void Start()
    {
        unit = GetComponent<Unit>();
        mainCamera = Camera.main;

        ClearOldChildHealthBars();
        CreateHealthBar();
    }

    private void LateUpdate()
    {
        if (unit == null || healthBarRoot == null || backgroundBar == null || fillBar == null)
        {
            return;
        }

        UpdateHealthBarPosition();
        UpdateHealthBarValue();
    }

    private void OnDestroy()
    {
        if (healthBarRoot != null)
        {
            Destroy(healthBarRoot);
        }
    }

    private void ClearOldChildHealthBars()
    {
        Transform oldBackground = transform.Find("HealthBar_Background");
        if (oldBackground != null)
        {
            Destroy(oldBackground.gameObject);
        }

        Transform oldFill = transform.Find("HealthBar_Fill");
        if (oldFill != null)
        {
            Destroy(oldFill.gameObject);
        }

        Transform oldRoot = transform.Find("HealthBar_Root");
        if (oldRoot != null)
        {
            Destroy(oldRoot.gameObject);
        }
    }

    private void CreateHealthBar()
    {
        healthBarRoot = new GameObject(gameObject.name + "_HealthBar_Root");

        backgroundMaterial = CreateBarMaterial("HealthBar_Background_Material", Color.black);

        fillMaterial = CreateBarMaterial(
            "HealthBar_Fill_Material",
            unit != null && unit.team == UnitTeam.Enemy ? Color.red : Color.green
        );

        backgroundBar = GameObject.CreatePrimitive(PrimitiveType.Cube);
        backgroundBar.name = "HealthBar_Background";
        backgroundBar.transform.SetParent(healthBarRoot.transform);

        Destroy(backgroundBar.GetComponent<Collider>());

        MeshRenderer backgroundRenderer = backgroundBar.GetComponent<MeshRenderer>();
        backgroundRenderer.material = backgroundMaterial;
        SetupBarRenderer(backgroundRenderer);

        fillBar = GameObject.CreatePrimitive(PrimitiveType.Cube);
        fillBar.name = "HealthBar_Fill";
        fillBar.transform.SetParent(healthBarRoot.transform);

        Destroy(fillBar.GetComponent<Collider>());

        MeshRenderer fillRenderer = fillBar.GetComponent<MeshRenderer>();
        fillRenderer.material = fillMaterial;
        SetupBarRenderer(fillRenderer);
    }

    private Material CreateBarMaterial(string materialName, Color color)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");

        if (shader == null)
        {
            shader = Shader.Find("Standard");
        }

        if (shader == null)
        {
            shader = Shader.Find("Diffuse");
        }

        Material material = new Material(shader);
        material.name = materialName;
        material.color = color;

        if (material.HasProperty("_BaseColor"))
        {
            material.SetColor("_BaseColor", color);
        }

        if (material.HasProperty("_Color"))
        {
            material.SetColor("_Color", color);
        }

        return material;
    }

    private void SetupBarRenderer(MeshRenderer meshRenderer)
    {
        if (meshRenderer == null)
        {
            return;
        }

        meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
        meshRenderer.receiveShadows = false;
    }

    private void UpdateHealthBarPosition()
    {
        healthBarRoot.transform.position = transform.position + offset;

        if (mainCamera != null)
        {
            healthBarRoot.transform.rotation = mainCamera.transform.rotation;
        }

        backgroundBar.transform.localPosition = Vector3.zero;
        backgroundBar.transform.localRotation = Quaternion.identity;
        backgroundBar.transform.localScale = new Vector3(barWidth, barHeight, barDepth);

        fillBar.transform.localRotation = Quaternion.identity;
    }

    private void UpdateHealthBarValue()
    {
        float hpPercent = unit.GetHpPercent();
        hpPercent = Mathf.Clamp01(hpPercent);

        fillBar.transform.localScale = new Vector3(
            barWidth * hpPercent,
            barHeight,
            barDepth
        );

        float offsetX = -(barWidth * (1f - hpPercent)) / 2f;

        fillBar.transform.localPosition = new Vector3(
            offsetX,
            0.01f,
            -0.03f
        );
    }
}
