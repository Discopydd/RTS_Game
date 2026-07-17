using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;

public enum BuildingPlacementType
{
    None,
    CommandCenter,
    Barracks
}

public class BuildingPlacementManager : MonoBehaviour
{
    public static BuildingPlacementManager Instance { get; private set; }

    [Header("Building Prefabs")]
    public GameObject commandCenterPrefab;
    public GameObject barracksPrefab;

    [Header("Unit Prefabs For Producers")]
    public GameObject workerPrefab;
    public GameObject soldierPrefab;

    [Header("Hotkeys")]
    public KeyCode commandCenterHotkey = KeyCode.C;
    public KeyCode barracksHotkey = KeyCode.B;

    [Header("Costs")]
    public int commandCenterCost = 0;
    public int barracksCost = 150;
    public int workerCost = 50;
    public int soldierCost = 75;

    [Header("Build Time")]
    public float commandCenterBuildTime = 6.0f;
    public float barracksBuildTime = 5.0f;

    [Header("Construction Start")]
    [Tooltip("工兵到达建筑外缘附近后才开始读条。数值越小，越接近建筑才开始。")]
    public float builderStartExtraDistance = 0.45f;

    [Header("Placement Rules")]
    public bool allowMultipleCommandCenters = false;
    public float commandCenterRadius = 2.1f;
    public float barracksRadius = 1.6f;
    public float placementExtraClearance = 0.35f;
    public float groundSampleRadius = 3.0f;

    [Header("Building Heights")]
    public float commandCenterY = 0.75f;
    public float barracksY = 0.6f;

    [Header("Building Collision")]
    public Vector3 buildingColliderSize = Vector3.one;
    public Vector3 buildingObstacleSize = new Vector3(0.85f, 1.0f, 0.85f);
    public bool addNavMeshObstacle = true;

    [Header("Placement Ghost")]
    public Color validGhostColor = new Color(0f, 1f, 0f, 0.38f);
    public Color invalidGhostColor = new Color(1f, 0f, 0f, 0.38f);

    [Header("Building Materials")]
    public Material commandCenterMaterial;
    public Material barracksMaterial;
    public bool forceApplyBuildingMaterials = true;
    public Color commandCenterFallbackColor = new Color(0.17f, 0.49f, 1f, 1f);
    public Color barracksFallbackColor = new Color(1f, 0.64f, 1f, 1f);

    [Header("Fallback Size")]
    public Vector3 commandCenterFallbackScale = new Vector3(3f, 1.5f, 3f);
    public Vector3 barracksFallbackScale = new Vector3(2.5f, 1.2f, 2.5f);

    [Header("Producer Settings")]
    public float commandCenterProduceTime = 2.0f;
    public float barracksProduceTime = 2.0f;
    public KeyCode trainWorkerHotkey = KeyCode.W;
    public KeyCode trainSoldierHotkey = KeyCode.S;

    private BuildingPlacementType currentPlacement = BuildingPlacementType.None;
    private Unit currentBuilder;
    private RTSController currentController;
    private GUIStyle infoStyle;
    private string lastMessage = "";

    private GameObject placementGhost;
    private Material placementGhostMaterial;
    private Vector3 currentGhostPosition;
    private bool currentGhostHasPosition;
    private bool currentGhostCanPlace;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    public bool HandleInputFromRTSController(RTSController controller)
    {
        currentController = controller;

        if (controller == null)
        {
            return false;
        }

        if (currentPlacement == BuildingPlacementType.None)
        {
            if (Input.GetKeyDown(commandCenterHotkey))
            {
                TryStartPlacement(controller, BuildingPlacementType.CommandCenter);
                return true;
            }

            if (Input.GetKeyDown(barracksHotkey))
            {
                TryStartPlacement(controller, BuildingPlacementType.Barracks);
                return true;
            }

            return false;
        }

        UpdatePlacementGhost(controller);

        if (Input.GetKeyDown(KeyCode.Escape) || Input.GetMouseButtonDown(1))
        {
            CancelPlacement();
            return true;
        }

        if (Input.GetMouseButtonDown(0))
        {
            TryPlaceCurrentBuilding(controller);
            return true;
        }

        // 配置建筑位置时，阻止 RTSController 同时选择/移动单位。
        return true;
    }

    private void TryStartPlacement(RTSController controller, BuildingPlacementType placementType)
    {
        Unit worker = controller.GetFirstSelectedWorker();

        if (worker == null)
        {
            lastMessage = "需要先选中 Worker 工兵。";
            Debug.Log(lastMessage);
            return;
        }

        if (placementType == BuildingPlacementType.CommandCenter)
        {
            if (!allowMultipleCommandCenters && controller.HasPlayerCommandCenter())
            {
                lastMessage = "玩家基地已经存在。需要重新从工兵开局时，请先删除场景里的 CommandCenter。";
                Debug.Log(lastMessage);
                return;
            }
        }

        if (placementType == BuildingPlacementType.Barracks)
        {
            if (!controller.HasPlayerCommandCenter())
            {
                lastMessage = "需要先建造完成 CommandCenter，之后才能建造 Barracks。";
                Debug.Log(lastMessage);
                return;
            }
        }

        currentBuilder = worker;
        currentPlacement = placementType;
        lastMessage = GetBuildingName(currentPlacement) + " placement mode.";

        CreatePlacementGhost(currentPlacement);
        UpdatePlacementGhost(controller);
    }

    private void UpdatePlacementGhost(RTSController controller)
    {
        currentGhostHasPosition = TryGetGroundPoint(controller, out currentGhostPosition);

        if (!currentGhostHasPosition)
        {
            if (placementGhost != null)
            {
                placementGhost.SetActive(false);
            }

            currentGhostCanPlace = false;
            return;
        }

        float targetY = GetBuildingY(currentPlacement);
        currentGhostPosition = new Vector3(currentGhostPosition.x, targetY, currentGhostPosition.z);

        float placementRadius = GetPlacementRadius(currentPlacement);

        if (placementGhost != null)
        {
            // Prefab 的 Scale 改变后，放置判定也必须跟随实际模型尺寸。
            placementRadius = GetBuildingWorldRadius(placementGhost, placementRadius);
        }

        currentGhostCanPlace = IsPlacementPositionFree(currentGhostPosition, placementRadius);

        if (placementGhost == null)
        {
            CreatePlacementGhost(currentPlacement);
        }

        if (placementGhost != null)
        {
            placementGhost.SetActive(true);
            placementGhost.transform.position = currentGhostPosition;
            SetMaterialColor(placementGhostMaterial, currentGhostCanPlace ? validGhostColor : invalidGhostColor);
        }
    }

    private void CreatePlacementGhost(BuildingPlacementType placementType)
    {
        DestroyPlacementGhost();

        GameObject prefab = placementType == BuildingPlacementType.CommandCenter
            ? commandCenterPrefab
            : barracksPrefab;

        if (prefab != null)
        {
            placementGhost = Instantiate(prefab);
        }
        else
        {
            placementGhost = GameObject.CreatePrimitive(PrimitiveType.Cube);
            placementGhost.transform.localScale = placementType == BuildingPlacementType.CommandCenter
                ? commandCenterFallbackScale
                : barracksFallbackScale;
        }

        placementGhost.name = GetBuildingName(placementType) + "_PlacementGhost";

        float targetY = GetBuildingY(placementType);
        placementGhost.transform.position = new Vector3(0f, targetY, 0f);

        DisableGhostGameplayComponents(placementGhost);

        placementGhostMaterial = CreateTransparentMaterial(
            "PlacementGhostMaterial",
            validGhostColor
        );

        Renderer[] renderers = placementGhost.GetComponentsInChildren<Renderer>(true);

        foreach (Renderer renderer in renderers)
        {
            if (renderer == null || renderer is LineRenderer)
            {
                continue;
            }

            renderer.sharedMaterial = placementGhostMaterial;
        }
    }

    private void DisableGhostGameplayComponents(GameObject ghost)
    {
        Collider[] colliders = ghost.GetComponentsInChildren<Collider>(true);

        foreach (Collider collider in colliders)
        {
            if (collider != null)
            {
                collider.enabled = false;
            }
        }

        NavMeshObstacle[] obstacles = ghost.GetComponentsInChildren<NavMeshObstacle>(true);

        foreach (NavMeshObstacle obstacle in obstacles)
        {
            if (obstacle != null)
            {
                obstacle.enabled = false;
            }
        }

        MonoBehaviour[] behaviours = ghost.GetComponentsInChildren<MonoBehaviour>(true);

        foreach (MonoBehaviour behaviour in behaviours)
        {
            if (behaviour != null)
            {
                behaviour.enabled = false;
            }
        }

        Rigidbody[] rigidbodies = ghost.GetComponentsInChildren<Rigidbody>(true);

        foreach (Rigidbody rb in rigidbodies)
        {
            if (rb != null)
            {
                rb.isKinematic = true;
                rb.useGravity = false;
            }
        }
    }

    private void DestroyPlacementGhost()
    {
        if (placementGhost != null)
        {
            Destroy(placementGhost);
        }

        placementGhost = null;

        if (placementGhostMaterial != null)
        {
            Destroy(placementGhostMaterial);
        }

        placementGhostMaterial = null;
    }

    private void TryPlaceCurrentBuilding(RTSController controller)
    {
        if (currentPlacement == BuildingPlacementType.None)
        {
            return;
        }

        if (currentBuilder == null || currentBuilder.IsDead() || !currentBuilder.CanGatherResource())
        {
            lastMessage = "工兵不存在，已取消建造。";
            CancelPlacement();
            return;
        }

        if (!currentGhostHasPosition)
        {
            lastMessage = "没有点到地面。";
            Debug.Log(lastMessage);
            return;
        }

        if (!currentGhostCanPlace)
        {
            lastMessage = "这里太近了，不能放置建筑。";
            Debug.Log(lastMessage);
            return;
        }

        if (currentPlacement == BuildingPlacementType.Barracks && !controller.HasPlayerCommandCenter())
        {
            lastMessage = "需要先建造完成 CommandCenter。";
            Debug.Log(lastMessage);
            return;
        }

        int cost = GetPlacementCost(currentPlacement);

        if (cost > 0)
        {
            if (PlayerResources.Instance == null)
            {
                lastMessage = "场景中没有 PlayerResources。";
                Debug.LogWarning(lastMessage);
                return;
            }

            if (!PlayerResources.Instance.SpendMinerals(cost))
            {
                lastMessage = "Minerals 不足，需要 " + cost + "。";
                Debug.Log(lastMessage);
                return;
            }
        }

        BuildingPlacementType placedType = currentPlacement;
        Vector3 placePosition = currentGhostPosition;

        GameObject placedBuilding = CreateBuilding(placedType, placePosition);

        if (placedBuilding != null)
        {
            MoveBuilderNearBuilding(currentBuilder, placedBuilding);
        }

        lastMessage = GetBuildingName(placedType) + " construction started.";
        CancelPlacement();
    }

    private bool TryGetGroundPoint(RTSController controller, out Vector3 point)
    {
        point = Vector3.zero;

        Camera camera = controller.GetMainCamera();

        if (camera == null)
        {
            return false;
        }

        Ray ray = camera.ScreenPointToRay(Input.mousePosition);
        RaycastHit[] hits = Physics.RaycastAll(ray, Mathf.Infinity, ~0, QueryTriggerInteraction.Collide);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        foreach (RaycastHit hit in hits)
        {
            if (hit.collider.GetComponentInParent<Unit>() != null)
            {
                continue;
            }

            if (hit.collider.GetComponentInParent<ResourceDepot>() != null)
            {
                continue;
            }

            if (hit.collider.GetComponentInParent<ResourceNode>() != null)
            {
                continue;
            }

            point = hit.point;

            if (NavMesh.SamplePosition(point, out NavMeshHit navHit, groundSampleRadius, NavMesh.AllAreas))
            {
                point = navHit.position;
            }

            return true;
        }

        return false;
    }

    private bool IsPlacementPositionFree(Vector3 position, float buildingRadius)
    {
        Unit[] units = FindObjectsByType<Unit>(FindObjectsSortMode.None);

        foreach (Unit unit in units)
        {
            if (unit == null || unit.IsDead())
            {
                continue;
            }

            float requiredDistance = buildingRadius + unit.collisionRadius + placementExtraClearance;

            if (GetXZDistance(position, unit.transform.position) < requiredDistance)
            {
                return false;
            }
        }

        ResourceDepot[] depots = FindObjectsByType<ResourceDepot>(FindObjectsSortMode.None);

        foreach (ResourceDepot depot in depots)
        {
            if (depot == null)
            {
                continue;
            }

            // 放置预览虚影本身也可能带有 ResourceDepot。
            // 如果不排除它，鼠标下一帧会检测到“自己挡住自己”，所以会一直变红。
            if (IsPartOfPlacementGhost(depot.transform))
            {
                continue;
            }

            float requiredDistance = buildingRadius + depot.collisionRadius + placementExtraClearance;

            if (GetXZDistance(position, depot.transform.position) < requiredDistance)
            {
                return false;
            }
        }

        ResourceNode[] resources = FindObjectsByType<ResourceNode>(FindObjectsSortMode.None);

        foreach (ResourceNode resource in resources)
        {
            if (resource == null || resource.IsEmpty())
            {
                continue;
            }

            float requiredDistance = buildingRadius + resource.collisionRadius + placementExtraClearance;

            if (GetXZDistance(position, resource.transform.position) < requiredDistance)
            {
                return false;
            }
        }

        return true;
    }

    private GameObject CreateBuilding(BuildingPlacementType placementType, Vector3 position)
    {
        GameObject prefab = placementType == BuildingPlacementType.CommandCenter
            ? commandCenterPrefab
            : barracksPrefab;

        GameObject building;

        if (prefab != null)
        {
            building = Instantiate(prefab, position, prefab.transform.rotation);
        }
        else
        {
            building = GameObject.CreatePrimitive(PrimitiveType.Cube);
            building.transform.localScale = placementType == BuildingPlacementType.CommandCenter
                ? commandCenterFallbackScale
                : barracksFallbackScale;
        }

        ConfigurePlacedBuilding(building, placementType, position);
        return building;
    }

    private void ConfigurePlacedBuilding(
        GameObject building,
        BuildingPlacementType placementType,
        Vector3 position
    )
    {
        bool isCommandCenter = placementType == BuildingPlacementType.CommandCenter;

        building.name = isCommandCenter ? "CommandCenter" : "Barracks";

        float targetY = GetBuildingY(placementType);
        building.transform.position = new Vector3(position.x, targetY, position.z);

        ConfigureBuildingCollider(building);

        float actualBuildingRadius = GetBuildingWorldRadius(
            building,
            isCommandCenter ? commandCenterRadius : barracksRadius
        );

        ApplyBuildingMaterial(building, isCommandCenter);

        ResourceDepot depot = building.GetComponent<ResourceDepot>();

        if (depot == null)
        {
            depot = building.AddComponent<ResourceDepot>();
        }

        depot.depotName = isCommandCenter ? "Command Center" : "Barracks";
        depot.team = UnitTeam.Player;
        depot.acceptsResources = isCommandCenter;
        depot.collisionRadius = actualBuildingRadius;
        depot.RefreshCollisionRadius();
        depot.SetSelected(false);

        UnitProducer producer = building.GetComponent<UnitProducer>();

        if (producer == null)
        {
            producer = building.AddComponent<UnitProducer>();
        }

        ConfigureProducer(producer, placementType);

        BuildingConstruction construction = building.GetComponent<BuildingConstruction>();

        if (construction == null)
        {
            construction = building.AddComponent<BuildingConstruction>();
        }

        float buildTime = isCommandCenter ? commandCenterBuildTime : barracksBuildTime;

        construction.BeginConstruction(
            buildTime,
            depot,
            producer,
            isCommandCenter,
            currentBuilder,
            Mathf.Max(0.05f, builderStartExtraDistance)
        );
    }

    private bool IsPartOfPlacementGhost(Transform target)
    {
        if (placementGhost == null || target == null)
        {
            return false;
        }

        return target == placementGhost.transform || target.IsChildOf(placementGhost.transform);
    }

    private void ConfigureBuildingCollider(GameObject building)
    {
        if (building == null)
        {
            return;
        }

        BoxCollider boxCollider = building.GetComponent<BoxCollider>();

        if (boxCollider == null)
        {
            boxCollider = building.AddComponent<BoxCollider>();
        }

        if (TryCalculateLocalRendererBounds(building.transform, out Bounds localBounds))
        {
            boxCollider.center = localBounds.center;
            boxCollider.size = localBounds.size;
        }
        else
        {
            boxCollider.center = Vector3.zero;
            boxCollider.size = buildingColliderSize;
        }

        boxCollider.enabled = true;
        boxCollider.isTrigger = false;

        if (!addNavMeshObstacle)
        {
            return;
        }

        NavMeshObstacle obstacle = building.GetComponent<NavMeshObstacle>();

        if (obstacle == null)
        {
            obstacle = building.AddComponent<NavMeshObstacle>();
        }

        obstacle.enabled = true;
        obstacle.shape = NavMeshObstacleShape.Box;
        obstacle.center = boxCollider.center;

        // 稍微放大 XZ，避免单位视觉上贴入建筑。
        obstacle.size = new Vector3(
            boxCollider.size.x + 0.2f,
            boxCollider.size.y,
            boxCollider.size.z + 0.2f
        );

        obstacle.carving = true;
        obstacle.carveOnlyStationary = true;
    }

    private void ApplyBuildingMaterial(GameObject building, bool isCommandCenter)
    {
        if (building == null || !forceApplyBuildingMaterials)
        {
            return;
        }

        Material selectedMaterial = isCommandCenter
            ? commandCenterMaterial
            : barracksMaterial;

        if (selectedMaterial == null)
        {
            return;
        }

        Renderer[] renderers = building.GetComponentsInChildren<Renderer>(true);

        foreach (Renderer renderer in renderers)
        {
            if (renderer == null || renderer is LineRenderer)
            {
                continue;
            }

            Material[] materials = renderer.sharedMaterials;

            for (int i = 0; i < materials.Length; i++)
            {
                materials[i] = selectedMaterial;
            }

            renderer.sharedMaterials = materials;
        }
    }

    private Material CreateRuntimeMaterial(string materialName, Color color)
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

    private Material CreateTransparentMaterial(string materialName, Color color)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");

        if (shader == null)
        {
            shader = Shader.Find("Standard");
        }

        if (shader == null)
        {
            shader = Shader.Find("Sprites/Default");
        }

        Material material = new Material(shader);
        material.name = materialName;

        SetMaterialColor(material, color);

        if (material.HasProperty("_Surface"))
        {
            material.SetFloat("_Surface", 1f);
        }

        if (material.HasProperty("_Blend"))
        {
            material.SetFloat("_Blend", 0f);
        }

        if (material.HasProperty("_AlphaClip"))
        {
            material.SetFloat("_AlphaClip", 0f);
        }

        if (material.HasProperty("_SrcBlend"))
        {
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        }

        if (material.HasProperty("_DstBlend"))
        {
            material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        }

        if (material.HasProperty("_ZWrite"))
        {
            material.SetFloat("_ZWrite", 0f);
        }

        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.renderQueue = (int)RenderQueue.Transparent;

        return material;
    }

    private void SetMaterialColor(Material material, Color color)
    {
        if (material == null)
        {
            return;
        }

        material.color = color;

        if (material.HasProperty("_BaseColor"))
        {
            material.SetColor("_BaseColor", color);
        }

        if (material.HasProperty("_Color"))
        {
            material.SetColor("_Color", color);
        }
    }

    private void ConfigureProducer(UnitProducer producer, BuildingPlacementType placementType)
    {
        if (producer == null)
        {
            return;
        }

        if (placementType == BuildingPlacementType.CommandCenter)
        {
            producer.producerName = "Command Center";

            if (workerPrefab != null)
            {
                producer.unitPrefab = workerPrefab;
            }

            producer.producedRole = UnitRole.Worker;
            producer.producedTeam = UnitTeam.Player;
            producer.unitCost = workerCost;
            producer.produceTime = commandCenterProduceTime;
            producer.produceHotkey = trainWorkerHotkey;
        }
        else if (placementType == BuildingPlacementType.Barracks)
        {
            producer.producerName = "Barracks";

            if (soldierPrefab != null)
            {
                producer.unitPrefab = soldierPrefab;
            }

            producer.producedRole = UnitRole.Soldier;
            producer.producedTeam = UnitTeam.Player;
            producer.unitCost = soldierCost;
            producer.produceTime = barracksProduceTime;
            producer.produceHotkey = trainSoldierHotkey;
        }
    }

    private void MoveBuilderNearBuilding(Unit builder, GameObject building)
    {
        if (builder == null || building == null)
        {
            return;
        }

        Vector3 direction = builder.transform.position - building.transform.position;
        direction.y = 0f;

        if (direction.sqrMagnitude < 0.01f)
        {
            direction = Vector3.back;
        }

        direction.Normalize();

        ResourceDepot depot = building.GetComponent<ResourceDepot>();
        Vector3 target;

        if (depot != null)
        {
            // 从建筑真实 Collider 表面向外计算工兵站位，不再使用固定半径。
            Vector3 offset = depot.GetStandingOffsetForDirection(
                direction,
                builder.collisionRadius,
                Mathf.Max(0.05f, builderStartExtraDistance)
            );
            target = building.transform.position + offset;
        }
        else
        {
            float buildingRadius = GetBuildingWorldRadius(building, 1f);
            target = building.transform.position + direction * (
                buildingRadius + builder.collisionRadius + builderStartExtraDistance
            );
        }

        target.y = builder.transform.position.y;

        // 建筑的 NavMeshObstacle 可能刚完成 carving。先把目标吸附到最近的有效 NavMesh。
        float sampleRadius = Mathf.Max(1.0f, builder.pathSampleRadius);
        if (NavMesh.SamplePosition(target, out NavMeshHit navHit, sampleRadius, NavMesh.AllAreas))
        {
            target = navHit.position;
            target.y = builder.transform.position.y;
        }

        builder.MoveTo(target);
    }

    private void CancelPlacement()
    {
        currentPlacement = BuildingPlacementType.None;
        currentBuilder = null;
        currentGhostHasPosition = false;
        currentGhostCanPlace = false;
        DestroyPlacementGhost();
    }

    private string GetBuildingName(BuildingPlacementType placementType)
    {
        if (placementType == BuildingPlacementType.CommandCenter)
        {
            return "CommandCenter";
        }

        if (placementType == BuildingPlacementType.Barracks)
        {
            return "Barracks";
        }

        return "Building";
    }

    private int GetPlacementCost(BuildingPlacementType placementType)
    {
        if (placementType == BuildingPlacementType.CommandCenter)
        {
            return commandCenterCost;
        }

        if (placementType == BuildingPlacementType.Barracks)
        {
            return barracksCost;
        }

        return 0;
    }

    private float GetPlacementRadius(BuildingPlacementType placementType)
    {
        if (placementType == BuildingPlacementType.CommandCenter)
        {
            return commandCenterRadius;
        }

        if (placementType == BuildingPlacementType.Barracks)
        {
            return barracksRadius;
        }

        return 1f;
    }

    private float GetBuildingWorldRadius(GameObject building, float fallbackRadius)
    {
        if (building == null)
        {
            return Mathf.Max(0.1f, fallbackRadius);
        }

        Vector3 center = building.transform.position;
        bool foundBounds = false;
        Bounds combinedBounds = new Bounds(center, Vector3.zero);

        Collider[] colliders = building.GetComponentsInChildren<Collider>(true);
        foreach (Collider current in colliders)
        {
            if (current == null)
            {
                continue;
            }

            Bounds currentBounds = current.bounds;
            if (currentBounds.size.sqrMagnitude < 0.000001f)
            {
                continue;
            }

            if (!foundBounds)
            {
                combinedBounds = currentBounds;
                foundBounds = true;
            }
            else
            {
                combinedBounds.Encapsulate(currentBounds);
            }
        }

        if (!foundBounds)
        {
            Renderer[] renderers = building.GetComponentsInChildren<Renderer>(true);
            foreach (Renderer renderer in renderers)
            {
                if (renderer == null || renderer is LineRenderer || renderer is ParticleSystemRenderer)
                {
                    continue;
                }

                if (!foundBounds)
                {
                    combinedBounds = renderer.bounds;
                    foundBounds = true;
                }
                else
                {
                    combinedBounds.Encapsulate(renderer.bounds);
                }
            }
        }

        if (!foundBounds)
        {
            return Mathf.Max(0.1f, fallbackRadius);
        }

        float xRadius = Mathf.Max(
            Mathf.Abs(combinedBounds.min.x - center.x),
            Mathf.Abs(combinedBounds.max.x - center.x)
        );
        float zRadius = Mathf.Max(
            Mathf.Abs(combinedBounds.min.z - center.z),
            Mathf.Abs(combinedBounds.max.z - center.z)
        );

        return Mathf.Max(0.1f, xRadius, zRadius);
    }

    private float GetBuildingY(BuildingPlacementType placementType)
    {
        if (placementType == BuildingPlacementType.CommandCenter)
        {
            return commandCenterY;
        }

        if (placementType == BuildingPlacementType.Barracks)
        {
            return barracksY;
        }

        return 0f;
    }

    private float GetXZDistance(Vector3 a, Vector3 b)
    {
        Vector2 posA = new Vector2(a.x, a.z);
        Vector2 posB = new Vector2(b.x, b.z);

        return Vector2.Distance(posA, posB);
    }

    private void OnGUI()
    {
        EnsureGUIStyle();

        if (currentPlacement != BuildingPlacementType.None)
        {
            string text =
                "Placing " + GetBuildingName(currentPlacement) +
                " / Left Click: Build / Right Click or Esc: Cancel";

            if (currentGhostHasPosition && !currentGhostCanPlace)
            {
                text += " / Cannot place here";
            }

            GUI.Label(new Rect(20, 185, 900, 30), text, infoStyle);
            return;
        }

        if (currentController == null)
        {
            return;
        }

        if (!currentController.HasSelectedWorker())
        {
            if (!string.IsNullOrEmpty(lastMessage))
            {
                GUI.Label(new Rect(20, 185, 900, 30), lastMessage, infoStyle);
            }

            return;
        }

        if (!currentController.HasPlayerCommandCenter())
        {
            GUI.Label(
                new Rect(20, 185, 900, 30),
                "Worker Build: Press " + commandCenterHotkey + " to place CommandCenter",
                infoStyle
            );
        }
        else
        {
            GUI.Label(
                new Rect(20, 185, 900, 30),
                "Worker Build: Press " + barracksHotkey + " to place Barracks (" + barracksCost + " Minerals)",
                infoStyle
            );
        }
    }

    private void EnsureGUIStyle()
    {
        if (infoStyle != null)
        {
            return;
        }

        infoStyle = new GUIStyle(GUI.skin.label);
        infoStyle.fontSize = 20;
        infoStyle.normal.textColor = Color.white;
        infoStyle.fontStyle = FontStyle.Bold;
    }
    private bool TryCalculateLocalRendererBounds(
    Transform root,
    out Bounds localBounds
)
    {
        localBounds = new Bounds(Vector3.zero, Vector3.zero);

        if (root == null)
        {
            return false;
        }

        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        bool hasBounds = false;

        foreach (Renderer renderer in renderers)
        {
            if (renderer == null ||
                renderer is LineRenderer ||
                renderer is ParticleSystemRenderer)
            {
                continue;
            }

            Bounds worldBounds = renderer.bounds;

            Vector3 min = worldBounds.min;
            Vector3 max = worldBounds.max;

            Vector3[] corners =
            {
            new Vector3(min.x, min.y, min.z),
            new Vector3(min.x, min.y, max.z),
            new Vector3(min.x, max.y, min.z),
            new Vector3(min.x, max.y, max.z),
            new Vector3(max.x, min.y, min.z),
            new Vector3(max.x, min.y, max.z),
            new Vector3(max.x, max.y, min.z),
            new Vector3(max.x, max.y, max.z)
        };

            foreach (Vector3 corner in corners)
            {
                Vector3 localPoint = root.InverseTransformPoint(corner);

                if (!hasBounds)
                {
                    localBounds = new Bounds(localPoint, Vector3.zero);
                    hasBounds = true;
                }
                else
                {
                    localBounds.Encapsulate(localPoint);
                }
            }
        }

        return hasBounds;
    }
}
