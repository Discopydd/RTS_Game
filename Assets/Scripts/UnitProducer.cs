using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(ResourceDepot))]
public class UnitProducer : MonoBehaviour
{
    [Header("Production Settings")]
    public string producerName = "Command Center";
    public GameObject unitPrefab;
    public UnitRole producedRole = UnitRole.Worker;
    public UnitTeam producedTeam = UnitTeam.Player;
    public int unitCost = 50;
    public float produceTime = 2.0f;
    public KeyCode produceHotkey = KeyCode.W;

    [Header("Spawn Settings")]
    public Transform spawnPoint;
    public float spawnSurfaceGap = 0.35f;
    public float spawnSearchRadius = 3.0f;

    [Header("Spawn Avoid Settings")]
    public float spawnedUnitY = 0.0f;
    public float spawnUnitRadius = 0.65f;
    public float spawnRingSpacing = 1.4f;
    public int maxSpawnRings = 5;
    public float rallyMoveDistance = 1.2f;

    private ResourceDepot depot;
    private bool isProducing;
    private float produceTimer;
    private GUIStyle infoStyle;

    private void Awake()
    {
        depot = GetComponent<ResourceDepot>();
    }

    private void Start()
    {
        if (spawnPoint == null)
        {
            GameObject spawnObject = new GameObject("SpawnPoint");
            spawnObject.transform.SetParent(transform);

            float defaultDistance = depot.collisionRadius + spawnSurfaceGap + spawnUnitRadius;
            spawnObject.transform.position = transform.position + Vector3.forward * defaultDistance;

            spawnPoint = spawnObject.transform;
        }
    }

    private void Update()
    {
        UpdateProduction();

        if (depot == null || !depot.IsSelected || depot.team != UnitTeam.Player)
        {
            return;
        }

        if (Input.GetKeyDown(produceHotkey))
        {
            TryStartProduction();
        }
    }

    private void TryStartProduction()
    {
        if (isProducing)
        {
            Debug.Log(producerName + " is already producing.");
            return;
        }

        if (unitPrefab == null)
        {
            Debug.LogWarning(producerName + " has no Unit Prefab assigned.");
            return;
        }

        if (PlayerResources.Instance == null)
        {
            Debug.LogWarning("PlayerResources does not exist in the scene.");
            return;
        }

        if (!PlayerResources.Instance.SpendMinerals(unitCost))
        {
            Debug.Log("Not enough Minerals. Need " + unitCost + ".");
            return;
        }

        isProducing = true;
        produceTimer = produceTime;
    }

    private void UpdateProduction()
    {
        if (!isProducing)
        {
            return;
        }

        produceTimer -= Time.deltaTime;

        if (produceTimer <= 0f)
        {
            SpawnUnit();
            isProducing = false;
        }
    }

    private void SpawnUnit()
    {
        Vector3 spawnPosition = GetSpawnPosition();

        GameObject newUnit = Instantiate(unitPrefab, spawnPosition, Quaternion.identity);

        newUnit.transform.position = new Vector3(
            newUnit.transform.position.x,
            spawnedUnitY,
            newUnit.transform.position.z
        );

        Unit unit = newUnit.GetComponent<Unit>();
        if (unit != null)
        {
            unit.team = producedTeam;
            unit.role = producedRole;

            if (producedRole == UnitRole.Worker)
            {
                unit.canGather = true;
                unit.attackDamage = 0;
                unit.attackRange = 0f;
            }
            else
            {
                unit.canGather = false;
            }

            // 生成后给一个很短的离开基地命令，避免后续单位堵在同一点。
            Vector3 rallyPosition = GetRallyPosition(newUnit.transform.position);
            unit.MoveTo(rallyPosition);
        }
    }

    private Vector3 GetSpawnPosition()
    {
        Vector3 basePosition = spawnPoint != null
            ? spawnPoint.position
            : transform.position + Vector3.forward * (depot.collisionRadius + spawnSurfaceGap + spawnUnitRadius);

        basePosition.y = spawnedUnitY;

        Vector3 sampledBasePosition = SampleGroundPosition(basePosition);

        if (IsPositionFree(sampledBasePosition))
        {
            return sampledBasePosition;
        }

        for (int ring = 1; ring <= maxSpawnRings; ring++)
        {
            float radius = spawnRingSpacing * ring;
            int pointCount = Mathf.Max(8, ring * 8);

            for (int i = 0; i < pointCount; i++)
            {
                float angle = Mathf.PI * 2f * i / pointCount;

                Vector3 candidate = basePosition + new Vector3(
                    Mathf.Cos(angle) * radius,
                    0f,
                    Mathf.Sin(angle) * radius
                );

                candidate = SampleGroundPosition(candidate);

                if (IsPositionFree(candidate))
                {
                    return candidate;
                }
            }
        }

        return sampledBasePosition;
    }

    private Vector3 SampleGroundPosition(Vector3 position)
    {
        if (NavMesh.SamplePosition(position, out NavMeshHit hit, spawnSearchRadius, NavMesh.AllAreas))
        {
            position = hit.position;
        }

        position.y = spawnedUnitY;
        return position;
    }

    private bool IsPositionFree(Vector3 position)
    {
        Unit[] allUnits = FindObjectsByType<Unit>(FindObjectsSortMode.None);

        foreach (Unit unit in allUnits)
        {
            if (unit == null || unit.IsDead())
            {
                continue;
            }

            float requiredDistance = spawnUnitRadius + unit.collisionRadius;
            float distance = GetXZDistance(position, unit.transform.position);

            if (distance < requiredDistance)
            {
                return false;
            }
        }

        return true;
    }

    private Vector3 GetRallyPosition(Vector3 spawnPosition)
    {
        Vector3 direction = spawnPosition - transform.position;
        direction.y = 0f;

        if (direction.sqrMagnitude < 0.01f)
        {
            direction = Vector3.forward;
        }

        direction.Normalize();

        Vector3 rallyPosition = spawnPosition + direction * rallyMoveDistance;
        rallyPosition = SampleGroundPosition(rallyPosition);
        rallyPosition.y = spawnedUnitY;

        return rallyPosition;
    }

    private float GetXZDistance(Vector3 a, Vector3 b)
    {
        Vector2 posA = new Vector2(a.x, a.z);
        Vector2 posB = new Vector2(b.x, b.z);

        return Vector2.Distance(posA, posB);
    }

    private void OnGUI()
    {
        if (depot == null || !depot.IsSelected || depot.team != UnitTeam.Player)
        {
            return;
        }

        EnsureGUIStyle();

        string roleName = producedRole == UnitRole.Worker ? "Worker" : "Soldier";
        string text = "Press " + produceHotkey + ": Train " + roleName + " (" + unitCost + " Minerals)";

        if (isProducing)
        {
            text = "Training " + roleName + "... " + Mathf.Max(0f, produceTimer).ToString("F1") + "s";
        }

        GUI.Label(new Rect(20, 155, 520, 30), text, infoStyle);
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
}
