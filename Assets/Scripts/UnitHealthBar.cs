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

    private GameObject backgroundBar;
    private GameObject fillBar;

    private Material backgroundMaterial;
    private Material fillMaterial;

    private void Start()
    {
        unit = GetComponent<Unit>();
        mainCamera = Camera.main;

        CreateHealthBar();
    }

    private void LateUpdate()
    {
        if (unit == null || backgroundBar == null || fillBar == null)
        {
            return;
        }

        UpdateHealthBarPosition();
        UpdateHealthBarValue();
    }

    private void CreateHealthBar()
    {
        backgroundMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        backgroundMaterial.color = Color.black;

        fillMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));

        if (unit.team == UnitTeam.Player)
        {
            fillMaterial.color = Color.green;
        }
        else
        {
            fillMaterial.color = Color.red;
        }

        backgroundBar = GameObject.CreatePrimitive(PrimitiveType.Cube);
        backgroundBar.name = "HealthBar_Background";
        backgroundBar.transform.SetParent(transform);

        Destroy(backgroundBar.GetComponent<Collider>());

        backgroundBar.GetComponent<MeshRenderer>().material = backgroundMaterial;
        SetupBarRenderer(backgroundBar);

        fillBar = GameObject.CreatePrimitive(PrimitiveType.Cube);
        fillBar.name = "HealthBar_Fill";
        fillBar.transform.SetParent(transform);

        Destroy(fillBar.GetComponent<Collider>());

        fillBar.GetComponent<MeshRenderer>().material = fillMaterial;
        SetupBarRenderer(fillBar);
    }

    private void SetupBarRenderer(GameObject barObject)
    {
        MeshRenderer meshRenderer = barObject.GetComponent<MeshRenderer>();

        meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
        meshRenderer.receiveShadows = false;
    }

    private void UpdateHealthBarPosition()
    {
        Vector3 basePosition = transform.position + offset;

        backgroundBar.transform.position = basePosition;
        fillBar.transform.position = basePosition + new Vector3(0f, 0.01f, -0.01f);

        if (mainCamera != null)
        {
            backgroundBar.transform.rotation = mainCamera.transform.rotation;
            fillBar.transform.rotation = mainCamera.transform.rotation;
        }

        backgroundBar.transform.localScale = new Vector3(barWidth, barHeight, barDepth);
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

        fillBar.transform.position = backgroundBar.transform.position
            + backgroundBar.transform.right * offsetX
            + backgroundBar.transform.forward * -0.03f;
    }
}