using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Spawns enemies behind the first wall in front of this waypoint.
/// Every spawned enemy receives the same user-placed destination Transform.
/// </summary>
public class ZoneSpawner : MonoBehaviour
{
    public enum SpawnMode { BehindWalls, Ring }

    [Header("Mode")]
    [SerializeField] private SpawnMode mode = SpawnMode.BehindWalls;

    [Header("Enemies")]
    [SerializeField] private GameObject[] enemyPrefabs;
    [Min(1)][SerializeField] private int enemyCount = 2;

    [Header("Fixed destination")]
    [Tooltip("Create an empty GameObject at the exact point where enemies should go, then assign it here.")]
    [SerializeField] private Transform destinationPoint;

    [Header("Behind walls")]
    [SerializeField] private LayerMask wallMask;
    [Min(0.1f)][SerializeField] private float maxWallDistance = 40f;
    [Min(0.05f)][SerializeField] private float maxWallThickness = 4f;
    [Min(0.01f)][SerializeField] private float defaultWallThickness = 0.5f;
    [Min(0.05f)][SerializeField] private float spawnClearance = 0.8f;
    [Range(0f, 180f)][SerializeField] private float spreadAngle = 50f;
    [Min(0f)][SerializeField] private float fallbackDistance = 14f;

    [Header("Ring mode")]
    [Tooltip("Optional center for the ring. If empty, the center is placed forward from this waypoint.")]
    [SerializeField] private Transform centerPoint;
    [Min(0f)][SerializeField] private float centerDistance = 10f;
    [Min(0f)][SerializeField] private float ringRadius = 6f;
    [Range(30f, 360f)][SerializeField] private float arcDegrees = 240f;
    [Min(0f)][SerializeField] private float gatherRadius = 1.5f;

    [Header("Placement")]
    [Tooltip("Extra height of the visible enemy above the NavMesh. This does not move the NavMeshAgent root.")]
    [SerializeField] private float spawnHeightOffset = 0.1f;
    [SerializeField] private LayerMask groundMask;
    [Tooltip("Solid geometry that must not overlap an enemy spawn. Usually walls and large obstacles; exclude the floor.")]
    [SerializeField] private LayerMask obstructionMask;
    [Tooltip("Existing enemies/actors to avoid when placing new enemies.")]
    [SerializeField] private LayerMask occupancyMask;
    [Min(0.1f)][SerializeField] private float navMeshSampleDistance = 1f;
    [Min(0f)][SerializeField] private float spawnSeparation = 0.2f;
    [Min(0.1f)][SerializeField] private float groundRayHeight = 5f;
    [Min(0.1f)][SerializeField] private float groundRayDistance = 30f;
    [Min(1)][SerializeField] private int placementAttempts = 8;
    [SerializeField] private bool debugLog = true;

    private struct SpawnPlan { public Vector3 position; public Vector3 destination; }
    private readonly List<SpawnPlan> previewPlans = new List<SpawnPlan>();
    private readonly List<Vector3> reservedPositions = new List<Vector3>();
    private readonly RaycastHit[] rayHits = new RaycastHit[32];
    private bool drawingGizmos;

    public int EnemyCount => Mathf.Max(0, enemyCount);

    private void Start()
    {
        RailStop stop = GetComponent<RailStop>();
        if (stop != null && stop.leaveCondition == RailAdvanceCondition.KillEnemies && stop.enemiesToKill != EnemyCount)
            Debug.LogWarning($"[ZoneSpawner] {name}: genera {enemyCount} enemigos, pero RailStop pide {stop.enemiesToKill}.", this);
    }

    public void SpawnEnemies()
    {
        if (mode == SpawnMode.BehindWalls && destinationPoint == null)
        {
            Debug.LogError($"[ZoneSpawner] {name}: asigna Destination Point en el Inspector.", this);
            return;
        }
        if (enemyPrefabs == null || enemyPrefabs.Length == 0)
        {
            Debug.LogError($"[ZoneSpawner] {name}: asigna al menos un prefab en Enemy Prefabs.", this);
            return;
        }

        BuildSpawnPlans(previewPlans);
        reservedPositions.Clear();
        int spawned = 0;

        for (int i = 0; i < previewPlans.Count; i++)
        {
            GameObject prefab = enemyPrefabs[i % enemyPrefabs.Length];
            if (prefab == null) continue;

            Vector3 spawn = previewPlans[i].position;
            if (!IsPositionFree(prefab, spawn, reservedPositions) &&
                !TryFindNearbyPosition(prefab, spawn, reservedPositions, out spawn))
            {
                if (debugLog) Debug.LogWarning($"[ZoneSpawner] {name}: no hay espacio libre para el enemigo {i + 1}; se omite.", this);
                continue;
            }

            reservedPositions.Add(spawn);
            Vector3 destination = mode == SpawnMode.BehindWalls ? destinationPoint.position : previewPlans[i].destination;
            Vector3 flatDirection = Vector3.ProjectOnPlane(destination - spawn, Vector3.up);
            Quaternion rotation = flatDirection.sqrMagnitude > 0.001f
                ? Quaternion.LookRotation(flatDirection)
                : Quaternion.identity;

            GameObject enemy = Instantiate(prefab, spawn, rotation);
            ConfigureAgent(enemy, spawn);
            EnemyVisualGrounding grounding = enemy.GetComponent<EnemyVisualGrounding>();
            if (grounding != null) grounding.SetHeightOffset(spawnHeightOffset);
            else if (Mathf.Abs(spawnHeightOffset) > 0.001f)
                Debug.LogWarning($"[ZoneSpawner] {enemy.name}: añade EnemyVisualGrounding al prefab para aplicar Spawn Height Offset sin sacar el agente del NavMesh.", enemy);

            EnemyMover mover = enemy.GetComponent<EnemyMover>();
            if (mover != null)
            {
                mover.SetDestination(mode == SpawnMode.BehindWalls ? destinationPoint : MakeDestination(destination));
            }
            else
            {
                NavMeshAgent agent = enemy.GetComponent<NavMeshAgent>();
                if (agent != null && agent.enabled && agent.isOnNavMesh)
                {
                    if (NavMesh.SamplePosition(destination, out NavMeshHit targetHit,
                            navMeshSampleDistance, agent.areaMask))
                        agent.SetDestination(targetHit.position);
                    else
                        Debug.LogWarning($"[ZoneSpawner] {name}: Destination Point no está cerca del NavMesh.", this);
                }
                else Debug.LogWarning($"[ZoneSpawner] {enemy.name}: falta un NavMeshAgent activo sobre el NavMesh.", enemy);
            }
            spawned++;
        }

        if (debugLog) Debug.Log($"[ZoneSpawner] {name}: generó {spawned}/{enemyCount} enemigos.", this);
        reservedPositions.Clear();
    }

    private void BuildSpawnPlans(List<SpawnPlan> output)
    {
        output.Clear();
        int count = Mathf.Max(1, enemyCount);
        Vector3 forward = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
        if (forward.sqrMagnitude < 0.001f) forward = Vector3.forward;

        if (mode == SpawnMode.Ring)
        {
            BuildRingPlans(output, count, forward);
            return;
        }

        for (int i = 0; i < count; i++)
        {
            float t = count == 1 ? 0f : (i / (float)(count - 1)) - 0.5f;
            Vector3 direction = Quaternion.AngleAxis(t * spreadAngle, Vector3.up) * forward;
            if (!TryFindSpawnBehindWall(direction, out Vector3 spawn))
            {
                Vector3 fallback = transform.position + direction * fallbackDistance;
                if (!TryFindGround(fallback, out spawn) || !TrySnapToNavMesh(spawn, out spawn))
                {
                    if (!drawingGizmos) Debug.LogWarning($"[ZoneSpawner] {name}: no se encontró spawn detrás de muro ni en el fallback.", this);
                    continue;
                }
            }

            // Keep planned spawns apart even before anything is instantiated.
            bool overlapsPlan = false;
            for (int p = 0; p < output.Count; p++)
            {
                if (Vector3.Distance(output[p].position, spawn) < spawnSeparation + 0.5f)
                { overlapsPlan = true; break; }
            }
            if (!overlapsPlan) output.Add(new SpawnPlan { position = spawn, destination = destinationPoint != null ? destinationPoint.position : spawn });
        }
    }

    private void BuildRingPlans(List<SpawnPlan> output, int count, Vector3 forward)
    {
        Vector3 desiredCenter = centerPoint != null ? centerPoint.position : transform.position + forward * centerDistance;
        if (!TryFindGround(desiredCenter, out Vector3 center)) center = desiredCenter;
        for (int i = 0; i < count; i++)
        {
            float t = count == 1 ? 0f : (i / (float)(count - 1)) - 0.5f;
            Vector3 direction = Quaternion.AngleAxis(t * arcDegrees, Vector3.up) * forward;
            Vector3 desiredSpawn = center + direction * ringRadius;
            if (!TryFindGround(desiredSpawn, out Vector3 spawn) || !TrySnapToNavMesh(spawn, out spawn)) continue;

            // Distribute gathering destinations evenly inside a disk; stable values keep Edit Mode gizmos still.
            float angle = i * 2.39996323f;
            float radius = gatherRadius * Mathf.Sqrt((i + 0.5f) / count);
            Vector3 desiredDestination = center + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;
            Vector3 destination = center;
            if (TryFindGround(desiredDestination, out Vector3 groundedDestination) && TrySnapToNavMesh(groundedDestination, out Vector3 navDestination))
                destination = navDestination;

            bool overlapsPlan = false;
            for (int p = 0; p < output.Count; p++)
                if (Vector3.Distance(output[p].position, spawn) < spawnSeparation + 0.5f) { overlapsPlan = true; break; }
            if (!overlapsPlan) output.Add(new SpawnPlan { position = spawn, destination = destination });
        }
    }

    private bool TryFindSpawnBehindWall(Vector3 direction, out Vector3 spawn)
    {
        spawn = default;
        Vector3 origin = transform.position + Vector3.up * groundRayHeight;
        int hitCount = Physics.RaycastNonAlloc(origin, direction, rayHits, maxWallDistance,
            wallMask, QueryTriggerInteraction.Ignore);

        float nearestDistance = float.MaxValue;
        RaycastHit wallHit = default;
        for (int i = 0; i < hitCount; i++)
        {
            Collider col = rayHits[i].collider;
            if (col == null || col.isTrigger || col.GetComponentInParent<EnemyMover>() != null ||
                col.GetComponentInParent<CharacterController>() != null) continue;
            if (col.attachedRigidbody != null && !col.attachedRigidbody.isKinematic) continue;
            if (rayHits[i].distance < nearestDistance)
            { nearestDistance = rayHits[i].distance; wallHit = rayHits[i]; }
        }
        if (nearestDistance == float.MaxValue) return false;

        float thickness = defaultWallThickness;
        float reverseProbeDistance = Mathf.Max(defaultWallThickness, maxWallThickness);
        Vector3 beyondWall = wallHit.point + direction * reverseProbeDistance;
        if (wallHit.collider.Raycast(new Ray(beyondWall, -direction), out RaycastHit backFace, reverseProbeDistance))
            thickness = Vector3.Distance(wallHit.point, backFace.point);
        thickness = Mathf.Clamp(thickness, 0.01f, maxWallThickness);

        Vector3 candidate = wallHit.point + direction * (thickness + spawnClearance);
        for (int i = 0; i < placementAttempts; i++)
        {
            if (TryFindGround(candidate, out Vector3 grounded) &&
                TrySnapToNavMesh(grounded, out spawn) &&
                !IsBlockedByWorld(spawn)) return true;
            candidate += direction * Mathf.Max(0.25f, spawnClearance);
        }
        return false;
    }

    private bool TryFindNearbyPosition(GameObject prefab, Vector3 origin, List<Vector3> reserved, out Vector3 result)
    {
        float radius = GetAgentRadius(prefab);
        for (int i = 0; i < placementAttempts * 2; i++)
        {
            float angle = i * 45f;
            float distance = radius * 2f + spawnSeparation + (i / 8) * radius;
            Vector3 candidate = origin + Quaternion.AngleAxis(angle, Vector3.up) * Vector3.forward * distance;
            if (!TryFindGround(candidate, out candidate) || !TrySnapToNavMesh(candidate, out candidate)) continue;
            if (IsPositionFree(prefab, candidate, reserved)) { result = candidate; return true; }
        }
        result = default;
        return false;
    }

    private bool IsPositionFree(GameObject prefab, Vector3 point, List<Vector3> reserved)
    {
        float radius = GetAgentRadius(prefab);
        float height = GetAgentHeight(prefab);
        Vector3 lower = point + Vector3.up * (radius + 0.02f);
        Vector3 upper = point + Vector3.up * Mathf.Max(radius + 0.03f, height - radius);
        if (obstructionMask.value != 0 && Physics.CheckCapsule(lower, upper, radius,
                obstructionMask, QueryTriggerInteraction.Ignore)) return false;
        if (occupancyMask.value != 0 && Physics.CheckCapsule(lower, upper, radius + spawnSeparation,
                occupancyMask, QueryTriggerInteraction.Ignore)) return false;
        foreach (Vector3 other in reserved)
            if (Vector2.Distance(new Vector2(point.x, point.z), new Vector2(other.x, other.z)) < radius * 2f + spawnSeparation)
                return false;
        return true;
    }

    private bool IsBlockedByWorld(Vector3 point)
    {
        const float radius = 0.35f;
        return obstructionMask.value != 0 && Physics.CheckCapsule(
            point + Vector3.up * radius,
            point + Vector3.up * 1.8f,
            radius,
            obstructionMask,
            QueryTriggerInteraction.Ignore);
    }

    private void ConfigureAgent(GameObject enemy, Vector3 spawn)
    {
        NavMeshAgent agent = enemy.GetComponent<NavMeshAgent>();
        if (agent == null) return;
        if (!NavMesh.SamplePosition(spawn, out NavMeshHit hit, navMeshSampleDistance, agent.areaMask))
        {
            Debug.LogError($"[ZoneSpawner] {enemy.name}: el spawn no está sobre un NavMesh compatible.", enemy);
            agent.enabled = false;
            return;
        }
        if (!agent.enabled) agent.enabled = true;
        if (!agent.isOnNavMesh && !agent.Warp(hit.position))
            Debug.LogError($"[ZoneSpawner] {enemy.name}: NavMeshAgent no pudo colocarse sobre el NavMesh.", enemy);
    }

    private bool TryFindGround(Vector3 point, out Vector3 groundPoint)
    {
        Vector3 origin = point + Vector3.up * groundRayHeight;
        if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, groundRayHeight + groundRayDistance,
                groundMask, QueryTriggerInteraction.Ignore))
        { groundPoint = hit.point; return true; }
        groundPoint = default;
        return false;
    }

    private bool TrySnapToNavMesh(Vector3 point, out Vector3 navPoint)
    {
        if (NavMesh.SamplePosition(point, out NavMeshHit hit, navMeshSampleDistance, NavMesh.AllAreas))
        { navPoint = hit.position; return true; }
        navPoint = default;
        return false;
    }

    private Transform MakeDestination(Vector3 position)
    {
        GameObject target = new GameObject("RingEnemyDestination");
        target.transform.SetParent(transform);
        target.transform.position = position;
        return target.transform;
    }

    private static float GetAgentRadius(GameObject prefab)
    {
        NavMeshAgent agent = prefab.GetComponent<NavMeshAgent>();
        return agent != null ? Mathf.Max(0.1f, agent.radius) : 0.35f;
    }

    private static float GetAgentHeight(GameObject prefab)
    {
        NavMeshAgent agent = prefab.GetComponent<NavMeshAgent>();
        return agent != null ? Mathf.Max(agent.height, agent.radius * 2f) : 1.8f;
    }

    private void OnDrawGizmosSelected()
    {
        drawingGizmos = true;
        BuildSpawnPlans(previewPlans);
        drawingGizmos = false;

        if (mode == SpawnMode.BehindWalls && destinationPoint != null)
        {
            Gizmos.color = new Color(0.3f, 1f, 0.4f, 0.95f);
            Gizmos.DrawSphere(destinationPoint.position, 0.22f);
        }
        if (mode == SpawnMode.Ring)
        {
            Vector3 forward = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
            if (forward.sqrMagnitude < 0.001f) forward = Vector3.forward;
            Vector3 center = centerPoint != null ? centerPoint.position : transform.position + forward * centerDistance;
            Gizmos.color = new Color(1f, 0.8f, 0.1f, 0.85f);
            Gizmos.DrawWireSphere(center, ringRadius);
            Gizmos.DrawWireSphere(center, gatherRadius);
        }

        for (int i = 0; i < previewPlans.Count; i++)
        {
            Gizmos.color = new Color(1f, 0.3f, 0.25f, 0.9f);
            Gizmos.DrawWireSphere(previewPlans[i].position, 0.3f);
            if (Mathf.Abs(spawnHeightOffset) > 0.001f)
            {
                Gizmos.color = new Color(1f, 0.8f, 0.1f, 0.9f);
                Gizmos.DrawLine(previewPlans[i].position,
                    previewPlans[i].position + Vector3.up * spawnHeightOffset);
            }
            if (mode == SpawnMode.BehindWalls && destinationPoint != null)
            {
                Gizmos.color = Color.white;
                Gizmos.DrawLine(previewPlans[i].position, destinationPoint.position);
            }
            else if (mode == SpawnMode.Ring)
            {
                Gizmos.color = new Color(0.3f, 1f, 0.4f, 0.95f);
                Gizmos.DrawSphere(previewPlans[i].destination, 0.22f);
                Gizmos.color = Color.white;
                Gizmos.DrawLine(previewPlans[i].position, previewPlans[i].destination);
            }
        }
    }
}
