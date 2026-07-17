using UnityEngine;
using UnityEngine.Rendering;

public class BuildingConstruction : MonoBehaviour
{
    [Header("Construction Bar")]
    public Vector3 barOffset = new Vector3(0f, 1.8f, 0f);
    public float barWidth = 2.2f;
    public float barHeight = 0.14f;
    public float barDepth = 0.05f;

    public bool IsComplete { get; private set; }
    public bool IsUnderConstruction => isConstructing && !IsComplete;

    private float buildTime = 5f;
    private float buildTimer;
    private bool isConstructing;

    private ResourceDepot depot;
    private UnitProducer producer;
    private bool finalAcceptsResources;

    private Unit builder;
    private float builderStartSurfaceDistance = 0.45f;
    private bool builderHasArrived;

    private Camera mainCamera;
    private GameObject barRoot;
    private GameObject backgroundBar;
    private GameObject fillBar;
    private Material backgroundMaterial;
    private Material fillMaterial;

    public void BeginConstruction(
        float duration,
        ResourceDepot targetDepot,
        UnitProducer targetProducer,
        bool acceptsResourcesWhenComplete,
        Unit builderUnit = null,
        float requiredBuilderSurfaceDistance = 0.45f
    )
    {
        buildTime = Mathf.Max(0.1f, duration);
        buildTimer = 0f;
        depot = targetDepot;
        producer = targetProducer;
        finalAcceptsResources = acceptsResourcesWhenComplete;
        builder = builderUnit;
        builderStartSurfaceDistance = Mathf.Max(0.05f, requiredBuilderSurfaceDistance);
        builderHasArrived = builder == null;

        IsComplete = false;
        isConstructing = true;

        if (depot != null)
        {
            // 建造完成前，不允许当作资源交付基地。
            depot.acceptsResources = false;
        }

        if (producer != null)
        {
            // 建造完成前，不能生产单位。
            producer.enabled = false;
        }

        mainCamera = Camera.main;
        CreateConstructionBar();
    }

    private void Update()
    {
        if (!isConstructing)
        {
            return;
        }

        if (!builderHasArrived)
        {
            builderHasArrived = HasBuilderArrived();

            // 工兵还没到达建造地点时，进度保持 0，不开始读条。
            UpdateConstructionBar();
            return;
        }

        buildTimer += Time.deltaTime;

        UpdateConstructionBar();

        if (buildTimer >= buildTime)
        {
            CompleteConstruction();
        }
    }

    private bool HasBuilderArrived()
    {
        if (builder == null || builder.IsDead())
        {
            return false;
        }

        Collider[] buildingColliders = depot != null
            ? depot.GetComponentsInChildren<Collider>(true)
            : GetComponentsInChildren<Collider>(true);

        float distanceToSurface = GetDistanceToColliderSurface(
            builder.transform.position,
            buildingColliders
        );

        if (!float.IsInfinity(distanceToSurface))
        {
            float builderRadius = Mathf.Max(0.05f, builder.collisionRadius);
            return distanceToSurface <= builderRadius + builderStartSurfaceDistance;
        }

        // Collider が存在しない特殊な Prefab 用のフォールバック。
        Vector2 builderPosition = new Vector2(
            builder.transform.position.x,
            builder.transform.position.z
        );
        Vector2 buildingPosition = new Vector2(
            transform.position.x,
            transform.position.z
        );

        return Vector2.Distance(builderPosition, buildingPosition)
            <= Mathf.Max(1f, builder.collisionRadius + builderStartSurfaceDistance);
    }

    private float GetDistanceToColliderSurface(Vector3 worldPosition, Collider[] colliders)
    {
        if (colliders == null || colliders.Length == 0)
        {
            return Mathf.Infinity;
        }

        float nearestDistance = Mathf.Infinity;

        // 实体 Collider 优先；没有实体 Collider 时才使用 Trigger。
        for (int pass = 0; pass < 2; pass++)
        {
            bool useTriggers = pass == 1;

            foreach (Collider current in colliders)
            {
                if (current == null || !current.enabled || current.isTrigger != useTriggers)
                {
                    continue;
                }

                Vector3 probe = worldPosition;
                probe.y = current.bounds.center.y;
                Vector3 closest = current.ClosestPoint(probe);
                closest.y = worldPosition.y;

                Vector2 flatDelta = new Vector2(
                    worldPosition.x - closest.x,
                    worldPosition.z - closest.z
                );
                nearestDistance = Mathf.Min(nearestDistance, flatDelta.magnitude);
            }

            if (!float.IsInfinity(nearestDistance))
            {
                return nearestDistance;
            }
        }

        return nearestDistance;
    }

    private void LateUpdate()
    {
        UpdateConstructionBarPosition();
    }

    private void CompleteConstruction()
    {
        isConstructing = false;
        IsComplete = true;

        if (depot != null)
        {
            depot.acceptsResources = finalAcceptsResources;
        }

        if (producer != null)
        {
            producer.enabled = true;
        }

        DestroyConstructionBar();
    }

    private void CreateConstructionBar()
    {
        DestroyConstructionBar();

        barRoot = new GameObject(gameObject.name + "_ConstructionBar_Root");

        backgroundMaterial = CreateBarMaterial("ConstructionBar_Background", Color.black);
        fillMaterial = CreateBarMaterial("ConstructionBar_Fill", Color.green);

        backgroundBar = GameObject.CreatePrimitive(PrimitiveType.Cube);
        backgroundBar.name = "ConstructionBar_Background";
        backgroundBar.transform.SetParent(barRoot.transform);
        Destroy(backgroundBar.GetComponent<Collider>());

        MeshRenderer backgroundRenderer = backgroundBar.GetComponent<MeshRenderer>();
        backgroundRenderer.material = backgroundMaterial;
        SetupRenderer(backgroundRenderer);

        fillBar = GameObject.CreatePrimitive(PrimitiveType.Cube);
        fillBar.name = "ConstructionBar_Fill";
        fillBar.transform.SetParent(barRoot.transform);
        Destroy(fillBar.GetComponent<Collider>());

        MeshRenderer fillRenderer = fillBar.GetComponent<MeshRenderer>();
        fillRenderer.material = fillMaterial;
        SetupRenderer(fillRenderer);

        UpdateConstructionBarPosition();
        UpdateConstructionBar();
    }

    private void UpdateConstructionBar()
    {
        if (backgroundBar == null || fillBar == null)
        {
            return;
        }

        float percent = Mathf.Clamp01(buildTimer / buildTime);

        backgroundBar.transform.localScale = new Vector3(barWidth, barHeight, barDepth);
        backgroundBar.transform.localPosition = Vector3.zero;
        backgroundBar.transform.localRotation = Quaternion.identity;

        fillBar.transform.localScale = new Vector3(barWidth * percent, barHeight, barDepth);

        float offsetX = -(barWidth * (1f - percent)) / 2f;

        fillBar.transform.localPosition = new Vector3(
            offsetX,
            0.01f,
            -0.03f
        );

        fillBar.transform.localRotation = Quaternion.identity;
    }

    private void UpdateConstructionBarPosition()
    {
        if (barRoot == null)
        {
            return;
        }

        barRoot.transform.position = transform.position + barOffset;

        if (mainCamera == null)
        {
            mainCamera = Camera.main;
        }

        if (mainCamera != null)
        {
            barRoot.transform.rotation = mainCamera.transform.rotation;
        }
    }

    private void DestroyConstructionBar()
    {
        if (barRoot != null)
        {
            Destroy(barRoot);
        }

        barRoot = null;
        backgroundBar = null;
        fillBar = null;
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

    private void SetupRenderer(MeshRenderer meshRenderer)
    {
        if (meshRenderer == null)
        {
            return;
        }

        meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
        meshRenderer.receiveShadows = false;
    }

    private void OnDestroy()
    {
        DestroyConstructionBar();
    }
}
