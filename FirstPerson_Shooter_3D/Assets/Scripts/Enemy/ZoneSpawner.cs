using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Genera los enemigos de una zona SOLO cuando el jugador ya ha llegado al waypoint
/// (lo llama el RailManager).
///
/// BehindWalls:
///   1. Los enemigos aparecen sobre el NavMesh, DETRÁS de un muro y ocultos al jugador.
///   2. Cada uno recibe como destino una "posición de tiro": un punto del NavMesh delante del jugador,
///      a distancia de combate, con línea de visión libre (el jugador lo ve y puede dispararle,
///      y él puede disparar al jugador) y con ruta completa desde su punto de aparición.
///   3. Al llegar se paran (lo hace EnemyCombatMover).
/// Ring: aparecen en un arco y se juntan en un punto central (combate de la granada).
///
/// Los pies del enemigo se apoyan sobre el NavMesh ajustando el Base Offset del NavMeshAgent
/// (así colliders, modelo y barra de vida se mueven juntos). No hace falta EnemyVisualGrounding.
/// Selecciona el waypoint para ver la previsualización:
///   rojo = aparición · verde = posición de tiro · amarillo = línea de visión libre · rojo fino = sin línea de visión.
/// </summary>
public class ZoneSpawner : MonoBehaviour
{
    public enum SpawnMode { BehindWalls, Ring }
    public enum DestinationMode { FiringPositions, FixedPoint }

    [Header("Mode")]
    [SerializeField] private SpawnMode mode = SpawnMode.BehindWalls;

    [Header("Enemies")]
    [SerializeField] private GameObject[] enemyPrefabs;
    [Min(1)][SerializeField] private int enemyCount = 2;

    [Header("Cuándo aparecen")]
    [Tooltip("Si el RailManager aún está moviendo al jugador, espera a que llegue antes de generar.")]
    [SerializeField] private bool waitForRailArrival = true;
    [Tooltip("Evita que la misma zona genere enemigos dos veces.")]
    [SerializeField] private bool spawnOnlyOnce = true;

    [Header("Destination (BehindWalls)")]
    [Tooltip("FiringPositions: cada enemigo va a un punto con línea de tiro al jugador (recomendado). FixedPoint: todos van a 'Destination Point'.")]
    [SerializeField] private DestinationMode destinationMode = DestinationMode.FiringPositions;
    [Tooltip("Solo con FixedPoint: crea un GameObject vacío donde deben ir los enemigos y asígnalo aquí.")]
    [SerializeField] private Transform destinationPoint;

    [Header("Firing positions (a tiro del jugador)")]
    [Tooltip("Distancia mínima al jugador a la que se detienen.")]
    [Min(1f)][SerializeField] private float minCombatDistance = 6f;
    [Tooltip("Distancia máxima al jugador a la que se detienen.")]
    [Min(1f)][SerializeField] private float maxCombatDistance = 14f;
    [Tooltip("Abanico (grados a cada lado del frente del waypoint) en el que se colocan. Mantenlo dentro del campo de visión de la cámara.")]
    [Range(5f, 90f)][SerializeField] private float combatHalfAngle = 35f;
    [Tooltip("Separación mínima entre las posiciones de tiro de dos enemigos.")]
    [Min(0.5f)][SerializeField] private float combatSeparation = 1.8f;
    [Tooltip("Altura de los ojos del jugador sobre el suelo del NavMesh.")]
    [Min(0.5f)][SerializeField] private float playerEyeHeight = 1.6f;
    [Tooltip("Altura desde la que el enemigo dispara/ve (pecho o cabeza).")]
    [Min(0.5f)][SerializeField] private float enemyEyeHeight = 1.5f;
    [Tooltip("Capas que TAPAN la visión (muros, cajas, obstáculos). SIN el suelo ni jugador ni enemigos. Si está vacío se usa Obstruction Mask o Wall Mask.")]
    [SerializeField] private LayerMask lineOfSightMask;
    [Min(0.1f)][SerializeField] private float combatSampleRadius = 1f;

    [Header("Behind walls")]
    [Tooltip("Capas de los MUROS. Sin esto no se detecta ningún muro.")]
    [SerializeField] private LayerMask wallMask;
    [Tooltip("Altura (sobre el waypoint) desde la que sale el rayo que busca muros.")]
    [Min(0.1f)][SerializeField] private float wallRayHeight = 1f;
    [Min(0.1f)][SerializeField] private float maxWallDistance = 40f;
    [Min(0.05f)][SerializeField] private float maxWallThickness = 4f;
    [Min(0.01f)][SerializeField] private float defaultWallThickness = 0.5f;
    [Min(0.05f)][SerializeField] private float spawnClearance = 0.8f;
    [Range(0f, 180f)][SerializeField] private float spreadAngle = 50f;
    [Min(0f)][SerializeField] private float fallbackDistance = 14f;
    [Tooltip("Exige que el muro tape al enemigo desde el waypoint (que no se le vea aparecer).")]
    [SerializeField] private bool requireHiddenSpawn = true;

    [Header("Ring mode")]
    [Tooltip("Optional center for the ring. If empty, the center is placed forward from this waypoint.")]
    [SerializeField] private Transform centerPoint;
    [Min(0f)][SerializeField] private float centerDistance = 10f;
    [Min(0f)][SerializeField] private float ringRadius = 6f;
    [Range(30f, 360f)][SerializeField] private float arcDegrees = 240f;
    [Min(0f)][SerializeField] private float gatherRadius = 1.5f;

    [Header("Placement")]
    [Tooltip("Mide el modelo del enemigo y ajusta el Base Offset del NavMeshAgent para que los pies queden sobre el NavMesh.")]
    [SerializeField] private bool autoGroundFeet = true;
    [Tooltip("Elevación EXTRA (metros) sobre lo medido. 0 = pies justo sobre el NavMesh. Súbelo si el NavMesh queda por debajo del suelo visible.")]
    [SerializeField] private float spawnHeightOffset = 0f;
    [Tooltip("Capa del suelo físico (opcional). Si se asigna, se corrige la diferencia entre el NavMesh y el suelo visible.")]
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

    private struct SpawnPlan
    {
        public Vector3 position;
        public Vector3 destination;
        public bool hasLineOfSight;
    }

    private struct Candidate
    {
        public float angle;
        public float dist;
        public float score;
    }

    private readonly List<SpawnPlan> previewPlans = new List<SpawnPlan>();
    private readonly List<Vector3> reservedPositions = new List<Vector3>();
    private readonly List<Candidate> candidateBuf = new List<Candidate>();
    private readonly RaycastHit[] rayHits = new RaycastHit[32];
    private NavMeshPath pathBuf;
    private Coroutine waitRoutine;
    private bool hasSpawned;
    private bool drawingGizmos;

    public int EnemyCount => Mathf.Max(0, enemyCount);

    // Capas que tapan la visión: lineOfSightMask > obstructionMask > wallMask
    private int LosMask => lineOfSightMask.value != 0 ? lineOfSightMask.value
                         : obstructionMask.value != 0 ? obstructionMask.value
                         : wallMask.value;

    private void Start()
    {
        RailStop stop = GetComponent<RailStop>();
        if (stop != null && stop.leaveCondition == RailAdvanceCondition.KillEnemies && stop.enemiesToKill != EnemyCount)
            Debug.LogWarning($"[ZoneSpawner] {name}: genera {enemyCount} enemigos, pero RailStop pide {stop.enemiesToKill}.", this);

        if (mode == SpawnMode.BehindWalls && wallMask.value == 0)
            Debug.LogWarning($"[ZoneSpawner] {name}: 'Wall Mask' está vacío; no se detectará ningún muro y los enemigos aparecerán a {fallbackDistance} m por delante.", this);
    }

    // ───────── API que usa el RailManager ─────────

    /// <summary>Genera la oleada. Solo actúa cuando el jugador ya ha llegado al waypoint.</summary>
    public void SpawnEnemies()
    {
        if (spawnOnlyOnce && hasSpawned)
        {
            if (debugLog) Debug.Log($"[ZoneSpawner] {name}: ya generó sus enemigos; se ignora la petición.", this);
            return;
        }

        if (waitForRailArrival && RailManager.Instance != null && RailManager.Instance.IsMoving)
        {
            if (waitRoutine == null) waitRoutine = StartCoroutine(SpawnWhenArrived());
            return;
        }

        DoSpawn();
    }

    private IEnumerator SpawnWhenArrived()
    {
        while (RailManager.Instance != null && RailManager.Instance.IsMoving)
            yield return null;

        waitRoutine = null;
        DoSpawn();
    }

    private void DoSpawn()
    {
        if (mode == SpawnMode.BehindWalls && destinationMode == DestinationMode.FixedPoint && destinationPoint == null)
        {
            Debug.LogError($"[ZoneSpawner] {name}: con Destination Mode = FixedPoint asigna Destination Point en el Inspector.", this);
            return;
        }
        if (enemyPrefabs == null || enemyPrefabs.Length == 0)
        {
            Debug.LogError($"[ZoneSpawner] {name}: asigna al menos un prefab en Enemy Prefabs.", this);
            return;
        }

        hasSpawned = true;

        BuildSpawnPlans(previewPlans);
        reservedPositions.Clear();
        int spawned = 0;

        for (int i = 0; i < previewPlans.Count; i++)
        {
            GameObject prefab = enemyPrefabs[i % enemyPrefabs.Length];
            if (prefab == null) continue;

            Vector3 spawn = previewPlans[i].position;

            if (!IsPositionFree(prefab, spawn, reservedPositions))
            {
                if (TryFindNearbyPosition(prefab, spawn, reservedPositions, out Vector3 free))
                    spawn = free;
                else if (debugLog)
                    Debug.LogWarning($"[ZoneSpawner] {name}: poco espacio libre para el enemigo {i + 1}; se genera igualmente para que la zona pueda completarse.", this);
            }

            reservedPositions.Add(spawn);

            Vector3 destination = previewPlans[i].destination;
            Vector3 flatDirection = Vector3.ProjectOnPlane(destination - spawn, Vector3.up);
            Quaternion rotation = flatDirection.sqrMagnitude > 0.001f
                ? Quaternion.LookRotation(flatDirection)
                : Quaternion.identity;

            GameObject enemy = Instantiate(prefab, spawn, rotation);
            PlaceEnemy(enemy, spawn, i == 0);
            AssignDestination(enemy, destination);
            spawned++;
        }

        if (debugLog) Debug.Log($"[ZoneSpawner] {name}: generó {spawned}/{enemyCount} enemigos.", this);
        if (spawned != enemyCount)
            Debug.LogError($"[ZoneSpawner] {name}: se generaron {spawned} enemigos pero se esperaban {enemyCount}. Revisa los prefabs; el RailStop podría no avanzar.", this);

        reservedPositions.Clear();
    }

    // ───────── Colocación del enemigo (pies sobre el NavMesh) ─────────

    private void PlaceEnemy(GameObject enemy, Vector3 navPos, bool logInfo)
    {
        NavMeshAgent agent = enemy.GetComponent<NavMeshAgent>();

        // Distancia del pivote a la parte más baja del modelo (pies)
        float pivotToFeet = 0f;
        bool measured = false;
        if (autoGroundFeet && TryGetLowestY(enemy, out float lowestY))
        {
            pivotToFeet = Mathf.Max(0f, enemy.transform.position.y - lowestY);
            measured = true;
        }

        float extra = GetPhysicalFloorDelta(navPos) + spawnHeightOffset;

        if (agent == null)
        {
            if (measured)
                enemy.transform.position = new Vector3(navPos.x, navPos.y + pivotToFeet + extra, navPos.z);
            return;
        }

        // El pivote queda 'baseOffset' por encima del NavMesh: con el pivote en el centro del cuerpo,
        // baseOffset = distancia del pivote a los pies, y el cuerpo entero queda sobre el suelo.
        float baseOffset = (measured ? pivotToFeet : agent.baseOffset) + extra;
        agent.baseOffset = baseOffset;

        if (!agent.enabled) agent.enabled = true;

        if (!agent.Warp(navPos))
        {
            Debug.LogError($"[ZoneSpawner] {enemy.name}: el NavMeshAgent no pudo colocarse sobre el NavMesh en {navPos}. " +
                           "Comprueba que la NavMeshSurface cubre esa zona y que el tipo de agente del prefab coincide con el del bake.", enemy);
            return;
        }

        if (logInfo && debugLog)
            Debug.Log($"[ZoneSpawner] {enemy.name}: pivote→pies {pivotToFeet:F2} m · Base Offset aplicado {baseOffset:F2} m.", enemy);
    }

    // Corrige la diferencia entre el NavMesh y el suelo físico visible (el NavMesh suele quedar algo por debajo)
    private float GetPhysicalFloorDelta(Vector3 navPos)
    {
        if (groundMask.value == 0) return 0f;

        if (Physics.Raycast(navPos + Vector3.up * 0.6f, Vector3.down, out RaycastHit hit, 1.5f,
                groundMask, QueryTriggerInteraction.Ignore))
            return Mathf.Clamp(hit.point.y - navPos.y, 0f, 0.5f);

        return 0f;
    }

    // Punto más bajo del modelo: mallas visibles o, si no hay, colliders sólidos
    private static bool TryGetLowestY(GameObject enemy, out float lowestY)
    {
        lowestY = float.MaxValue;
        bool found = false;

        foreach (Renderer r in enemy.GetComponentsInChildren<Renderer>())
        {
            if (!r.enabled) continue;
            if (!(r is MeshRenderer) && !(r is SkinnedMeshRenderer)) continue;
            lowestY = Mathf.Min(lowestY, r.bounds.min.y);
            found = true;
        }
        if (found) return true;

        foreach (Collider c in enemy.GetComponentsInChildren<Collider>())
        {
            if (c.isTrigger || !c.enabled) continue;
            lowestY = Mathf.Min(lowestY, c.bounds.min.y);
            found = true;
        }
        return found;
    }

    private void AssignDestination(GameObject enemy, Vector3 destination)
    {
        // 1) EnemyCombatMover: va al punto, se detiene mirando al jugador y ataca
        EnemyCombatMover combat = enemy.GetComponent<EnemyCombatMover>();
        if (combat != null)
        {
            combat.SetDestination(destination);
            return;
        }

        // 2) Solo NavMeshAgent
        NavMeshAgent agent = enemy.GetComponent<NavMeshAgent>();
        if (agent != null && agent.enabled && agent.isOnNavMesh)
        {
            if (NavMesh.SamplePosition(destination, out NavMeshHit targetHit, navMeshSampleDistance * 2f, agent.areaMask))
                agent.SetDestination(targetHit.position);
            else
                Debug.LogWarning($"[ZoneSpawner] {name}: el destino no está cerca del NavMesh.", this);
        }
        else
        {
            Debug.LogWarning($"[ZoneSpawner] {enemy.name}: añade EnemyCombatMover (o un NavMeshAgent activo sobre el NavMesh) para que se mueva.", enemy);
        }
    }

    // ───────── Planificación ─────────

    private void BuildSpawnPlans(List<SpawnPlan> output)
    {
        output.Clear();
        int count = Mathf.Max(1, enemyCount);
        Vector3 forward = FlatForward();

        if (mode == SpawnMode.Ring)
        {
            BuildRingPlans(output, count, forward);
            return;
        }

        for (int i = 0; i < count; i++)
        {
            NavMeshQueryFilter filter = FilterFor(i);
            TryGetFloor(filter, out float floorY);

            float t = count == 1 ? 0f : (i / (float)(count - 1)) - 0.5f;
            Vector3 direction = Quaternion.AngleAxis(t * spreadAngle, Vector3.up) * forward;

            // 1) Punto de aparición detrás del muro, sobre el NavMesh
            if (!TrySpawnBehindWall(direction, filter, floorY, output, out Vector3 spawn))
            {
                spawn = FallbackSpawn(direction, filter, floorY, output);
                if (!drawingGizmos)
                    Debug.LogWarning($"[ZoneSpawner] {name}: no se encontró un punto válido detrás de un muro para el enemigo {i + 1}; " +
                                     $"aparece a {fallbackDistance} m. Revisa 'Wall Mask', la rotación del waypoint (flecha azul hacia los muros) " +
                                     "y que haya NavMesh detrás del muro.", this);
            }

            // 2) Destino
            Vector3 destination;
            bool hasLos = true;

            if (destinationMode == DestinationMode.FixedPoint && destinationPoint != null)
                destination = destinationPoint.position;
            else
                TryFindFiringPosition(i, count, spawn, filter, floorY, output, out destination, out hasLos);

            output.Add(new SpawnPlan { position = spawn, destination = destination, hasLineOfSight = hasLos });
        }
    }

    private void BuildRingPlans(List<SpawnPlan> output, int count, Vector3 forward)
    {
        for (int i = 0; i < count; i++)
        {
            NavMeshQueryFilter filter = FilterFor(i);
            TryGetFloor(filter, out float floorY);

            Vector3 desiredCenter = centerPoint != null ? centerPoint.position : transform.position + forward * centerDistance;
            float centerY = centerPoint != null ? centerPoint.position.y : floorY;
            if (!SampleNavWide(desiredCenter, centerY, filter, out Vector3 center))
                center = new Vector3(desiredCenter.x, centerY, desiredCenter.z);

            float t = count == 1 ? 0f : (i / (float)(count - 1)) - 0.5f;
            Vector3 direction = Quaternion.AngleAxis(t * arcDegrees, Vector3.up) * forward;
            if (!SampleNavWide(center + direction * ringRadius, center.y, filter, out Vector3 spawn))
                spawn = center;

            // Destinos repartidos uniformemente dentro del círculo de reunión (valores estables para los gizmos)
            float angle = i * 2.39996323f;
            float radius = gatherRadius * Mathf.Sqrt((i + 0.5f) / count);
            Vector3 desiredDestination = center + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;
            Vector3 destination = SampleNavAt(desiredDestination, center.y, filter, navMeshSampleDistance, out Vector3 navDestination)
                ? navDestination
                : center;

            output.Add(new SpawnPlan { position = spawn, destination = destination, hasLineOfSight = true });
        }
    }

    // ───────── BehindWalls: punto de aparición ─────────

    private bool TrySpawnBehindWall(Vector3 direction, NavMeshQueryFilter filter, float floorY,
                                    List<SpawnPlan> plans, out Vector3 spawn)
    {
        spawn = default;

        Vector3 origin = transform.position + Vector3.up * wallRayHeight;
        if (!TryFindWall(origin, direction, out float wallDistance, out Vector3 exit)) return false;

        Vector3 perp = Vector3.Cross(Vector3.up, direction);
        Vector3 eye = PlayerEye(floorY);

        // Pasada 0: oculto al jugador. Pasada 1: sin exigir ocultación.
        for (int pass = 0; pass < 2; pass++)
        {
            if (pass == 1 && !requireHiddenSpawn) break;
            bool needHidden = pass == 0 && requireHiddenSpawn;

            Vector3 candidate = exit + direction * spawnClearance;

            for (int a = 0; a < placementAttempts; a++)
            {
                // Orden lateral: 0, +1, -1, +2, -2
                for (int s = 0; s < 5; s++)
                {
                    float offset = ((s + 1) / 2) * 0.7f * (s % 2 == 1 ? 1f : -1f);
                    if (!SampleNavAt(candidate + perp * offset, floorY, filter, navMeshSampleDistance, out Vector3 nav))
                        continue;

                    // Debe quedar realmente DETRÁS del muro, no delante
                    float along = Vector3.Dot(Vector3.ProjectOnPlane(nav - origin, Vector3.up), direction);
                    if (along < wallDistance + 0.2f) continue;

                    if (TooCloseToPlans(nav, plans)) continue;
                    if (IsBlockedByWorld(nav)) continue;
                    if (needHidden && HasLineOfSight(eye, nav + Vector3.up * 1.2f)) continue;

                    if (pass == 1 && LosMask != 0 && !drawingGizmos)
                        Debug.LogWarning($"[ZoneSpawner] {name}: no se encontró un punto oculto tras el muro; un enemigo aparecerá a la vista.", this);

                    spawn = nav;
                    return true;
                }

                candidate += direction * Mathf.Max(0.25f, spawnClearance);
            }
        }

        return false;
    }

    // Aparición de respaldo: por delante, a 'fallbackDistance', sobre el NavMesh y separada de las demás
    private Vector3 FallbackSpawn(Vector3 direction, NavMeshQueryFilter filter, float floorY, List<SpawnPlan> plans)
    {
        Vector3 wanted = transform.position + direction * fallbackDistance;
        if (!SampleNavWide(wanted, floorY, filter, out Vector3 nav))
            nav = new Vector3(wanted.x, floorY, wanted.z);

        Vector3 perp = Vector3.Cross(Vector3.up, direction);
        for (int k = 1; k <= 6 && TooCloseToPlans(nav, plans); k++)
        {
            float offset = ((k + 1) / 2) * 0.9f * (k % 2 == 1 ? 1f : -1f);
            if (SampleNavWide(wanted + perp * offset, floorY, filter, out Vector3 alt))
                nav = alt;
        }
        return nav;
    }

    // ───────── Posiciones de tiro (destino) ─────────

    /// <summary>
    /// Busca un punto del NavMesh delante del jugador, a distancia de combate, con línea de visión libre
    /// entre el jugador y el enemigo y con ruta completa desde su punto de aparición.
    /// Pasada 0: línea de visión + ruta. Pasada 1: solo ruta. Pasada 2: cualquier punto.
    /// </summary>
    private bool TryFindFiringPosition(int index, int count, Vector3 spawn, NavMeshQueryFilter filter, float floorY,
                                       List<SpawnPlan> plans, out Vector3 firing, out bool hasLos)
    {
        Vector3 center = new Vector3(transform.position.x, floorY, transform.position.z);
        Vector3 forward = FlatForward();
        Vector3 eye = PlayerEye(floorY);

        // Cada enemigo prefiere un ángulo y una distancia distintos (reparto en abanico y en dos filas)
        float t = count == 1 ? 0f : (index / (float)(count - 1)) - 0.5f;
        float desiredAngle = t * 2f * combatHalfAngle * 0.8f;
        float desiredDist = Mathf.Lerp(minCombatDistance, maxCombatDistance, index % 2 == 0 ? 0.35f : 0.7f);
        float distRange = Mathf.Max(1f, maxCombatDistance - minCombatDistance);

        candidateBuf.Clear();
        for (float d = minCombatDistance; d <= maxCombatDistance + 0.01f; d += 1.5f)
        {
            for (float a = -combatHalfAngle; a <= combatHalfAngle + 0.01f; a += 6f)
            {
                candidateBuf.Add(new Candidate
                {
                    angle = a,
                    dist = d,
                    score = Mathf.Abs(a - desiredAngle) / combatHalfAngle
                          + 0.6f * Mathf.Abs(d - desiredDist) / distRange
                });
            }
        }
        candidateBuf.Sort((x, y) => x.score.CompareTo(y.score));

        for (int pass = 0; pass < 3; pass++)
        {
            for (int k = 0; k < candidateBuf.Count; k++)
            {
                Candidate c = candidateBuf[k];
                Vector3 dir = Quaternion.AngleAxis(c.angle, Vector3.up) * forward;

                if (!SampleNavAt(center + dir * c.dist, floorY, filter, combatSampleRadius, out Vector3 nav))
                    continue;

                // El NavMesh puede "arrastrar" el punto: no debe quedar demasiado cerca del jugador
                float flat = Vector3.ProjectOnPlane(nav - center, Vector3.up).magnitude;
                if (flat < minCombatDistance * 0.85f) continue;

                if (TooCloseToDestinations(nav, plans)) continue;

                bool los = HasLineOfSight(eye, nav + Vector3.up * enemyEyeHeight);
                if (pass == 0 && !los) continue;
                if (pass <= 1 && !CanReach(spawn, nav, filter)) continue;

                if (pass > 0 && !drawingGizmos)
                {
                    Debug.LogWarning($"[ZoneSpawner] {name}: enemigo {index + 1}: " +
                                     (pass == 1 ? "no hay posición con línea de visión alcanzable; se usa una sin visión directa. "
                                                : "no hay ninguna posición de tiro con ruta completa; se usa un punto cercano. ") +
                                     "Revisa 'Line Of Sight Mask', el NavMesh delante del waypoint y el rango de distancias.", this);
                }

                firing = nav;
                hasLos = los;
                return true;
            }
        }

        // Nada válido: punto por delante del waypoint
        firing = center + forward * minCombatDistance;
        if (SampleNavWide(firing, floorY, filter, out Vector3 snapped)) firing = snapped;
        hasLos = false;

        if (!drawingGizmos)
            Debug.LogWarning($"[ZoneSpawner] {name}: enemigo {index + 1}: no se encontró ninguna posición de tiro sobre el NavMesh delante del waypoint.", this);
        return false;
    }

    // ───────── Utilidades NavMesh y visión ─────────

    // Filtro del NavMesh según el NavMeshAgent del prefab (tipo de agente y áreas permitidas)
    private NavMeshQueryFilter FilterFor(int index)
    {
        GameObject prefab = (enemyPrefabs != null && enemyPrefabs.Length > 0)
            ? enemyPrefabs[index % enemyPrefabs.Length]
            : null;
        return FilterFor(prefab);
    }

    private static NavMeshQueryFilter FilterFor(GameObject prefab)
    {
        var filter = new NavMeshQueryFilter { agentTypeID = 0, areaMask = NavMesh.AllAreas };

        if (prefab != null)
        {
            NavMeshAgent agent = prefab.GetComponent<NavMeshAgent>();
            if (agent != null)
            {
                filter.agentTypeID = agent.agentTypeID;
                filter.areaMask = agent.areaMask;
            }
        }
        return filter;
    }

    // Altura del suelo con NavMesh bajo el waypoint (el waypoint puede estar a la altura de la cabeza)
    private bool TryGetFloor(NavMeshQueryFilter filter, out float floorY)
    {
        if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 6f, filter))
        {
            floorY = hit.position.y;
            return true;
        }

        floorY = transform.position.y;
        return false;
    }

    private static bool SampleNavAt(Vector3 point, float y, NavMeshQueryFilter filter, float radius, out Vector3 result)
    {
        Vector3 q = new Vector3(point.x, y, point.z);
        if (NavMesh.SamplePosition(q, out NavMeshHit hit, radius, filter))
        {
            result = hit.position;
            return true;
        }

        result = q;
        return false;
    }

    // Igual, pero reintenta con un radio mayor
    private bool SampleNavWide(Vector3 point, float y, NavMeshQueryFilter filter, out Vector3 result)
    {
        return SampleNavAt(point, y, filter, navMeshSampleDistance, out result)
            || SampleNavAt(point, y, filter, navMeshSampleDistance * 3f, out result);
    }

    private bool CanReach(Vector3 from, Vector3 to, NavMeshQueryFilter filter)
    {
        if (pathBuf == null) pathBuf = new NavMeshPath();

        return NavMesh.CalculatePath(from, to, filter, pathBuf)
               && pathBuf.status == NavMeshPathStatus.PathComplete;
    }

    private Vector3 PlayerEye(float floorY)
    {
        return new Vector3(transform.position.x, floorY + playerEyeHeight, transform.position.z);
    }

    private bool HasLineOfSight(Vector3 from, Vector3 to)
    {
        return !Physics.Linecast(from, to, LosMask, QueryTriggerInteraction.Ignore);
    }

    private bool TooCloseToPlans(Vector3 pos, List<SpawnPlan> plans)
    {
        float min = spawnSeparation + 0.5f;
        for (int i = 0; i < plans.Count; i++)
            if ((plans[i].position - pos).sqrMagnitude < min * min) return true;
        return false;
    }

    private bool TooCloseToDestinations(Vector3 pos, List<SpawnPlan> plans)
    {
        for (int i = 0; i < plans.Count; i++)
            if ((plans[i].destination - pos).sqrMagnitude < combatSeparation * combatSeparation) return true;
        return false;
    }

    // ───────── Búsqueda de muros ─────────

    private bool TryFindWall(Vector3 origin, Vector3 direction, out float wallDistance, out Vector3 exit)
    {
        wallDistance = 0f;
        exit = origin;

        int hitCount = Physics.RaycastNonAlloc(origin, direction, rayHits, maxWallDistance,
            wallMask, QueryTriggerInteraction.Ignore);

        float nearest = float.MaxValue;
        RaycastHit wallHit = default;
        for (int i = 0; i < hitCount; i++)
        {
            if (!IsWallCandidate(rayHits[i].collider)) continue;
            if (rayHits[i].distance < nearest)
            {
                nearest = rayHits[i].distance;
                wallHit = rayHits[i];
            }
        }
        if (nearest == float.MaxValue) return false;

        wallDistance = nearest;

        // Cara trasera del muro: rayo desde el otro lado hacia el muro
        float thickness = defaultWallThickness;
        float probe = Mathf.Max(defaultWallThickness, maxWallThickness);
        Vector3 beyond = wallHit.point + direction * probe;
        if (wallHit.collider.Raycast(new Ray(beyond, -direction), out RaycastHit backFace, probe))
            thickness = Vector3.Distance(wallHit.point, backFace.point);
        thickness = Mathf.Clamp(thickness, 0.01f, maxWallThickness);

        exit = wallHit.point + direction * thickness;
        return true;
    }

    // El muro no puede ser el jugador, un enemigo, un pickup ni un objeto físico que se mueve
    private bool IsWallCandidate(Collider col)
    {
        if (col == null || col.isTrigger) return false;
        if (col.GetComponentInParent<EnemyCombatMover>() != null) return false;
        if (col.GetComponentInParent<EnemyHealth>() != null) return false;
        if (col.GetComponentInParent<HealthPickup>() != null) return false;
        if (col.GetComponentInParent<CharacterController>() != null) return false;

        if (RailManager.Instance != null && RailManager.Instance.playerParent != null &&
            col.transform.IsChildOf(RailManager.Instance.playerParent)) return false;

        if (col.attachedRigidbody != null && !col.attachedRigidbody.isKinematic) return false;
        return true;
    }

    // ───────── Espacio libre ─────────

    private bool TryFindNearbyPosition(GameObject prefab, Vector3 origin, List<Vector3> reserved, out Vector3 result)
    {
        NavMeshQueryFilter filter = FilterFor(prefab);
        float radius = GetAgentRadius(prefab);

        for (int i = 0; i < placementAttempts * 2; i++)
        {
            float angle = i * 45f;
            float distance = radius * 2f + spawnSeparation + (i / 8) * radius;
            Vector3 candidate = origin + Quaternion.AngleAxis(angle, Vector3.up) * Vector3.forward * distance;

            if (!SampleNavAt(candidate, origin.y, filter, navMeshSampleDistance, out candidate)) continue;
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

    private Vector3 FlatForward()
    {
        Vector3 f = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
        return f.sqrMagnitude > 0.001f ? f.normalized : Vector3.forward;
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

    // ───────── Previsualización en el editor ─────────

    private void OnDrawGizmosSelected()
    {
        drawingGizmos = true;
        BuildSpawnPlans(previewPlans);
        drawingGizmos = false;

        TryGetFloor(FilterFor(0), out float floorY);
        Vector3 eye = PlayerEye(floorY);

        if (mode == SpawnMode.Ring)
        {
            Vector3 forward = FlatForward();
            Vector3 center = centerPoint != null ? centerPoint.position : transform.position + forward * centerDistance;
            Gizmos.color = new Color(1f, 0.8f, 0.1f, 0.85f);
            Gizmos.DrawWireSphere(center, ringRadius);
            Gizmos.DrawWireSphere(center, gatherRadius);
        }
        else if (destinationMode == DestinationMode.FiringPositions)
        {
            DrawCombatFan(floorY);
        }
        else if (destinationPoint != null)
        {
            Gizmos.color = new Color(0.3f, 1f, 0.4f, 0.95f);
            Gizmos.DrawSphere(destinationPoint.position, 0.22f);
        }

        // Ojos del jugador
        Gizmos.color = new Color(0.4f, 0.8f, 1f, 0.9f);
        Gizmos.DrawWireSphere(eye, 0.12f);

        for (int i = 0; i < previewPlans.Count; i++)
        {
            SpawnPlan plan = previewPlans[i];

            // Aparición
            Gizmos.color = new Color(1f, 0.3f, 0.25f, 0.9f);
            Gizmos.DrawWireSphere(plan.position, 0.3f);

            // Destino
            Gizmos.color = new Color(0.3f, 1f, 0.4f, 0.95f);
            Gizmos.DrawSphere(plan.destination, 0.22f);

            // Ruta recta de referencia
            Gizmos.color = new Color(1f, 1f, 1f, 0.5f);
            Gizmos.DrawLine(plan.position, plan.destination);

            // Línea de visión desde la posición de tiro hasta el jugador
            if (mode == SpawnMode.BehindWalls)
            {
                Gizmos.color = plan.hasLineOfSight ? new Color(1f, 0.95f, 0.2f, 0.9f) : new Color(1f, 0.2f, 0.2f, 0.5f);
                Gizmos.DrawLine(plan.destination + Vector3.up * enemyEyeHeight, eye);
            }
        }
    }

    private void DrawCombatFan(float floorY)
    {
        Vector3 c = new Vector3(transform.position.x, floorY + 0.05f, transform.position.z);
        Vector3 f = FlatForward();

        Gizmos.color = new Color(1f, 0.85f, 0.1f, 0.6f);

        Vector3 left = Quaternion.AngleAxis(-combatHalfAngle, Vector3.up) * f;
        Vector3 right = Quaternion.AngleAxis(combatHalfAngle, Vector3.up) * f;
        Gizmos.DrawLine(c + left * minCombatDistance, c + left * maxCombatDistance);
        Gizmos.DrawLine(c + right * minCombatDistance, c + right * maxCombatDistance);

        DrawArc(c, f, minCombatDistance);
        DrawArc(c, f, maxCombatDistance);
    }

    private void DrawArc(Vector3 c, Vector3 forward, float radius)
    {
        const int segments = 12;
        Vector3 prev = c + Quaternion.AngleAxis(-combatHalfAngle, Vector3.up) * forward * radius;

        for (int i = 1; i <= segments; i++)
        {
            float a = Mathf.Lerp(-combatHalfAngle, combatHalfAngle, i / (float)segments);
            Vector3 next = c + Quaternion.AngleAxis(a, Vector3.up) * forward * radius;
            Gizmos.DrawLine(prev, next);
            prev = next;
        }
    }
}