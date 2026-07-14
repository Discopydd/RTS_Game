using System.Collections.Generic;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;

public static class RTSRuntimeNavMesh
{
    private static bool hasBuiltNavMesh;
    private static bool isBuildingNavMesh;

    // 关闭 Domain Reload 时也必须在每次进入 Play Mode 前重置。
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetRuntimeState()
    {
        hasBuiltNavMesh = false;
        isBuildingNavMesh = false;
    }

    // Unit.Awake 会先调用 EnsureBuilt；这里作为没有单位的场景兜底。
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void BuildRuntimeNavMeshAfterSceneLoad()
    {
        EnsureBuilt();
    }

    public static bool EnsureBuilt()
    {
        if (hasBuiltNavMesh)
        {
            if (NavMesh.CalculateTriangulation().vertices.Length > 0)
            {
                return true;
            }

            hasBuiltNavMesh = false;
        }

        if (isBuildingNavMesh)
        {
            return false;
        }

        isBuildingNavMesh = true;

        try
        {
            NavMeshSurface surface = Object.FindFirstObjectByType<NavMeshSurface>();

            if (surface == null)
            {
                GameObject navMeshObject = new GameObject("RTS Runtime NavMesh");
                surface = navMeshObject.AddComponent<NavMeshSurface>();
                surface.collectObjects = CollectObjects.All;
                surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
                surface.layerMask = ~0;
                surface.ignoreNavMeshAgent = true;
                surface.ignoreNavMeshObstacle = true;
            }

            // 单位、资源点和基地不直接烘焙进 NavMesh。
            // 资源点/基地会在烘焙后作为可移除的动态障碍加入。
            List<Collider> temporarilyDisabledColliders = DisableRuntimeNavigationColliders();

            try
            {
                surface.BuildNavMesh();
            }
            finally
            {
                RestoreColliders(temporarilyDisabledColliders);
            }

            AddDynamicObstacles();
            hasBuiltNavMesh = NavMesh.CalculateTriangulation().vertices.Length > 0;

            if (!hasBuiltNavMesh)
            {
                Debug.LogError(
                    "Runtime NavMesh build completed, but no walkable NavMesh was generated. " +
                    "Check that the Plane has an enabled Collider and is on a layer included by the NavMeshSurface.");
            }

            return hasBuiltNavMesh;
        }
        finally
        {
            isBuildingNavMesh = false;
        }
    }

    private static List<Collider> DisableRuntimeNavigationColliders()
    {
        List<Collider> disabledColliders = new List<Collider>();
        HashSet<Collider> uniqueColliders = new HashSet<Collider>();

        Unit[] units = Object.FindObjectsByType<Unit>(FindObjectsSortMode.None);
        foreach (Unit unit in units)
        {
            CollectEnabledColliders(unit, uniqueColliders, disabledColliders);
        }

        ResourceNode[] resources = Object.FindObjectsByType<ResourceNode>(FindObjectsSortMode.None);
        foreach (ResourceNode resource in resources)
        {
            CollectEnabledColliders(resource, uniqueColliders, disabledColliders);
        }

        ResourceDepot[] depots = Object.FindObjectsByType<ResourceDepot>(FindObjectsSortMode.None);
        foreach (ResourceDepot depot in depots)
        {
            CollectEnabledColliders(depot, uniqueColliders, disabledColliders);
        }

        return disabledColliders;
    }

    private static void CollectEnabledColliders(
        Component owner,
        HashSet<Collider> uniqueColliders,
        List<Collider> disabledColliders)
    {
        if (owner == null)
        {
            return;
        }

        Collider[] colliders = owner.GetComponentsInChildren<Collider>(true);

        foreach (Collider collider in colliders)
        {
            if (collider == null || !collider.enabled || !uniqueColliders.Add(collider))
            {
                continue;
            }

            collider.enabled = false;
            disabledColliders.Add(collider);
        }
    }

    private static void RestoreColliders(List<Collider> colliders)
    {
        foreach (Collider collider in colliders)
        {
            if (collider != null)
            {
                collider.enabled = true;
            }
        }
    }

    private static void AddDynamicObstacles()
    {
        ResourceDepot[] depots = Object.FindObjectsByType<ResourceDepot>(FindObjectsSortMode.None);

        foreach (ResourceDepot depot in depots)
        {
            if (depot != null)
            {
                EnsureObstacle(depot.gameObject, depot.GetComponent<Collider>());
            }
        }

        ResourceNode[] resources = Object.FindObjectsByType<ResourceNode>(FindObjectsSortMode.None);

        foreach (ResourceNode resource in resources)
        {
            if (resource != null)
            {
                EnsureObstacle(resource.gameObject, resource.GetComponent<Collider>());
            }
        }
    }

    private static void EnsureObstacle(GameObject target, Collider sourceCollider)
    {
        if (target == null || sourceCollider == null)
        {
            return;
        }

        NavMeshObstacle obstacle = target.GetComponent<NavMeshObstacle>();

        if (obstacle == null)
        {
            obstacle = target.AddComponent<NavMeshObstacle>();
        }

        if (sourceCollider is BoxCollider boxCollider)
        {
            obstacle.shape = NavMeshObstacleShape.Box;
            obstacle.center = boxCollider.center;
            obstacle.size = boxCollider.size + new Vector3(0.15f, 0f, 0.15f);
        }
        else if (sourceCollider is CapsuleCollider capsuleCollider)
        {
            obstacle.shape = NavMeshObstacleShape.Capsule;
            obstacle.center = capsuleCollider.center;
            obstacle.radius = capsuleCollider.radius;
            obstacle.height = capsuleCollider.height;
        }
        else
        {
            Bounds bounds = sourceCollider.bounds;
            obstacle.shape = NavMeshObstacleShape.Box;
            obstacle.center = target.transform.InverseTransformPoint(bounds.center);
            obstacle.size = new Vector3(
                bounds.size.x / Mathf.Max(Mathf.Abs(target.transform.lossyScale.x), 0.001f),
                bounds.size.y / Mathf.Max(Mathf.Abs(target.transform.lossyScale.y), 0.001f),
                bounds.size.z / Mathf.Max(Mathf.Abs(target.transform.lossyScale.z), 0.001f)
            );
        }

        obstacle.carving = true;
        obstacle.carveOnlyStationary = true;
    }
}
