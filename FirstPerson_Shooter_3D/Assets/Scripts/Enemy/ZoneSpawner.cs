
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class ZoneSpawner : MonoBehaviour
{
    public enum EnemyKind { Murcielago, Goblin }

    [System.Serializable]
    public class EnemyTypeConfig
    {
        public EnemyKind kind;
        public GameObject prefab;

        [Tooltip("Altura visual independiente del NavMesh")]
        public float heightOffset;
    }

    [System.Serializable]
    public class EnemyGroup
    {
        public EnemyKind kind = EnemyKind.Goblin;
        [Min(1)] public int count = 1;
    }

    [System.Serializable]
    public class Wave
    {
        public string waveName = "Oleada";
        [Min(0f)] public float delay = 0.5f;
        public EnemyGroup[] groups = { new EnemyGroup() };
    }

    [System.Serializable]
    public class SpawnSlot
    {
        public Transform spawnPoint;
        public Transform destinationPoint;
    }

    [Header("ENEMY TYPES")]
    [SerializeField] private EnemyTypeConfig[] enemyTypes;

    [Header("SPAWN POINTS AND DESTINATIONS")]
    [SerializeField] private SpawnSlot[] spawnSlots;

    [Header("WAVES")]
    [SerializeField] private Wave[] waves = { new Wave() };
    [Min(0f)]
    [SerializeField] private float staggerTime = 0.15f;

    [Header("RAIL STOP")]
    [SerializeField] private bool autoConfigureRailStop = true;

    [Header("NAVMESH")]
    [Min(0.1f)]
    [SerializeField] private float sampleDistance = 1f;

    [SerializeField] private LayerMask obstructionMask;
    [SerializeField] private LayerMask occupancyMask;

    [Header("SPAWN SAFETY")]
    [Min(0f)]
    [SerializeField] private float spawnSeparation = 0.3f;

    [SerializeField] private bool checkObstacles = true;
    [SerializeField] private bool checkOccupancy = true;

    [Header("DEBUG")]
    [SerializeField] private bool debugLog = true;
    [SerializeField] private bool showGizmos = true;

    private readonly List<Vector3> reservedPositions = new List<Vector3>();

    private NavMeshPath navPath;
    private Coroutine waveRoutine;
    private bool spawning;

    public int EnemyCount => GetTotalEnemies();

    // --------------------------------------------------
    // INITIALIZATION
    // --------------------------------------------------

    private void Awake()
    {
        navPath = new NavMeshPath();
        SyncRailStop();
    }

    private void Start()
    {
        ValidateConfiguration();
    }

    // --------------------------------------------------
    // PUBLIC SPAWN METHOD
    // --------------------------------------------------

    public void SpawnEnemies()
    {
        if (spawning)
        {
            if (debugLog)
                Debug.LogWarning("[ZoneSpawner] Ya existe una generación activa.", this);

            return;
        }

        if (waves == null || waves.Length == 0)
        {
            Debug.LogError("[ZoneSpawner] No hay oleadas configuradas.", this);
            return;
        }

        waveRoutine = StartCoroutine(SpawnWaves());
    }

    // --------------------------------------------------
    // WAVE SYSTEM
    // --------------------------------------------------

    private IEnumerator SpawnWaves()
    {
        spawning = true;

        RailManager rail = RailManager.Instance;
        int stopIndex = rail != null ? rail.CurrentStopIndex : -1;

        SyncRailStop();

        int total = GetTotalEnemies();
        int cumulativeKills = 0;

        for (int w = 0; w < waves.Length; w++)
        {
            Wave wave = waves[w];

            if (wave == null || wave.groups == null)
                continue;

            // Esperar a que se elimine la oleada anterior.
            if (cumulativeKills > 0 && rail != null)
            {
                while (rail.CurrentStopIndex == stopIndex &&
                       !rail.IsMoving &&
                       !rail.IsFinished &&
                       rail.KillsInZone < cumulativeKills)
                {
                    yield return null;
                }

                if (rail.CurrentStopIndex != stopIndex ||
                    rail.IsMoving || rail.IsFinished)
                    break;
            }

            if (wave.delay > 0f)
                yield return new WaitForSeconds(wave.delay);

            if (rail != null &&
                (rail.CurrentStopIndex != stopIndex ||
                 rail.IsMoving || rail.IsFinished))
                break;

            List<EnemyKind> queue = new List<EnemyKind>();

            foreach (EnemyGroup group in wave.groups)
            {
                if (group == null)
                    continue;

                for (int i = 0; i < Mathf.Max(1, group.count); i++)
                    queue.Add(group.kind);
            }

            reservedPositions.Clear();

            for (int i = 0; i < queue.Count; i++)
            {
                // Cada enemigo utiliza su propio SpawnSlot.
                if (i >= spawnSlots.Length)
                {
                    CountAsKilled("No hay suficientes SpawnSlots para esta oleada");
                }
                else
                {
                    SpawnEnemy(queue[i], spawnSlots[i]);
                }

                if (staggerTime > 0f && i < queue.Count - 1)
                    yield return new WaitForSeconds(staggerTime);

                if (rail != null &&
                    (rail.CurrentStopIndex != stopIndex ||
                     rail.IsMoving || rail.IsFinished))
                    break;
            }

            cumulativeKills += queue.Count;

            if (debugLog)
                Debug.Log(
                    $"[ZoneSpawner] {name}: oleada {wave.waveName} generada.",
                    this);
        }

        reservedPositions.Clear();
        spawning = false;
        waveRoutine = null;
    }

    // --------------------------------------------------
    // ENEMY SPAWNING
    // --------------------------------------------------

    private void SpawnEnemy(EnemyKind kind, SpawnSlot slot)
    {
        if (slot == null ||
            slot.spawnPoint == null ||
            slot.destinationPoint == null)
        {
            CountAsKilled("SpawnSlot incompleto");
            return;
        }

        EnemyTypeConfig type = GetEnemyType(kind);

        if (type == null || type.prefab == null)
        {
            CountAsKilled("Falta el prefab de " + kind);
            return;
        }

        GameObject prefab = type.prefab;

        NavMeshAgent prefabAgent = prefab.GetComponent<NavMeshAgent>();

        if (prefabAgent == null)
        {
            CountAsKilled(prefab.name + " no tiene NavMeshAgent en la raíz");
            return;
        }

        // Comprobar que ambos Emptys estén sobre el NavMesh.
        if (!NavMesh.SamplePosition(
            slot.spawnPoint.position,
            out NavMeshHit spawnHit,
            sampleDistance,
            prefabAgent.areaMask))
        {
            CountAsKilled("SpawnPoint fuera del NavMesh");
            return;
        }

        if (!NavMesh.SamplePosition(
            slot.destinationPoint.position,
            out NavMeshHit destinationHit,
            sampleDistance,
            prefabAgent.areaMask))
        {
            CountAsKilled("DestinationPoint fuera del NavMesh");
            return;
        }

        Vector3 spawn = spawnHit.position;
        Vector3 destination = destinationHit.position;

        // Evitar paredes y otros enemigos.
        if (!IsPositionFree(prefab, spawn))
        {
            CountAsKilled("SpawnPoint ocupado u obstruido");
            return;
        }

        // Exigir una ruta completa y válida.
        if (!NavMesh.CalculatePath(
            spawn,
            destination,
            prefabAgent.areaMask,
            navPath) ||
            navPath.status != NavMeshPathStatus.PathComplete)
        {
            CountAsKilled("No existe una ruta completa entre Spawn y Destination");
            return;
        }

        reservedPositions.Add(spawn);

        // Orientar al enemigo hacia su destino individual.
        Vector3 direction = destination - spawn;
        direction.y = 0f;

        Quaternion rotation = direction.sqrMagnitude > 0.001f
            ? Quaternion.LookRotation(direction)
            : slot.spawnPoint.rotation;

        GameObject enemy = Instantiate(prefab, spawn, rotation);

        // Configurar NavMeshAgent.
        NavMeshAgent agent = enemy.GetComponent<NavMeshAgent>();

        if (!agent.enabled)
            agent.enabled = true;

        if (!agent.Warp(spawn) || !agent.isOnNavMesh)
        {
            Debug.LogError(
                $"[ZoneSpawner] {enemy.name}: no se pudo colocar sobre el NavMesh.",
                enemy);

            Destroy(enemy);
            CountAsKilled("Error al inicializar NavMeshAgent");
            return;
        }

        agent.ResetPath();

        // Altura visual: especialmente útil para los murciélagos 2D HD.
        EnemyVisualGrounding grounding =
            enemy.GetComponent<EnemyVisualGrounding>();

        if (grounding != null)
            grounding.SetHeightOffset(type.heightOffset);

        else if (Mathf.Abs(type.heightOffset) > 0.001f)
            Debug.LogWarning(
                $"[ZoneSpawner] {enemy.name}: falta EnemyVisualGrounding.",
                enemy);

        // Asignar el destino individual.
        EnemyMover mover = enemy.GetComponent<EnemyMover>();

        if (mover != null)
        {
            mover.SetDestination(slot.destinationPoint);
        }
        else
        {
            agent.SetDestination(destination);
        }

        if (debugLog)
            Debug.Log(
                $"[ZoneSpawner] {enemy.name}: Spawn correcto. Destino: {slot.destinationPoint.name}",
                enemy);
    }

    // --------------------------------------------------
    // SPAWN VALIDATION
    // --------------------------------------------------

    private bool IsPositionFree(GameObject prefab, Vector3 position)
    {
        NavMeshAgent agent = prefab.GetComponent<NavMeshAgent>();

        float radius = agent != null ? agent.radius : 0.35f;
        float height = agent != null ? agent.height : 1.8f;

        radius = Mathf.Max(0.1f, radius);

        Vector3 bottom = position + Vector3.up * radius;
        Vector3 top = position + Vector3.up *
            Mathf.Max(radius, height - radius);

        if (checkObstacles && obstructionMask.value != 0)
        {
            if (Physics.CheckCapsule(
                bottom, top, radius,
                obstructionMask,
                QueryTriggerInteraction.Ignore))
                return false;
        }

        if (checkOccupancy && occupancyMask.value != 0)
        {
            if (Physics.CheckCapsule(
                bottom, top, radius + spawnSeparation,
                occupancyMask,
                QueryTriggerInteraction.Ignore))
                return false;
        }

        foreach (Vector3 reserved in reservedPositions)
        {
            float distance = Vector2.Distance(
                new Vector2(position.x, position.z),
                new Vector2(reserved.x, reserved.z));

            if (distance < radius * 2f + spawnSeparation)
                return false;
        }

        return true;
    }

    // --------------------------------------------------
    // ENEMY TYPES
    // --------------------------------------------------

    private EnemyTypeConfig GetEnemyType(EnemyKind kind)
    {
        if (enemyTypes == null)
            return null;

        foreach (EnemyTypeConfig type in enemyTypes)
        {
            if (type != null && type.kind == kind)
                return type;
        }

        return null;
    }

    // --------------------------------------------------
    // RAIL MANAGER
    // --------------------------------------------------

    private int GetTotalEnemies()
    {
        int total = 0;

        if (waves == null)
            return 0;

        foreach (Wave wave in waves)
        {
            if (wave == null || wave.groups == null)
                continue;

            foreach (EnemyGroup group in wave.groups)
            {
                if (group != null)
                    total += Mathf.Max(1, group.count);
            }
        }

        return total;
    }

    private void SyncRailStop()
    {
        if (!autoConfigureRailStop)
            return;

        RailStop stop = GetComponent<RailStop>();

        if (stop == null)
            return;

        if (stop.leaveCondition == RailAdvanceCondition.KillEnemies)
        {
            stop.enemiesToKill = GetTotalEnemies();

            if (debugLog)
                Debug.Log(
                    $"[ZoneSpawner] {name}: Enemies To Kill = {stop.enemiesToKill}",
                    this);
        }
    }

    private void CountAsKilled(string reason)
    {
        Debug.LogWarning(
            $"[ZoneSpawner] {name}: {reason}. Se registra como baja para evitar bloquear el vagón.",
            this);

        if (RailManager.Instance != null)
            RailManager.Instance.AddKill();
    }

    // --------------------------------------------------
    // VALIDATION
    // --------------------------------------------------

    private void ValidateConfiguration()
    {
        if (spawnSlots == null || spawnSlots.Length == 0)
        {
            Debug.LogError(
                $"[ZoneSpawner] {name}: no has asignado SpawnSlots.",
                this);
            return;
        }

        for (int i = 0; i < spawnSlots.Length; i++)
        {
            SpawnSlot slot = spawnSlots[i];

            if (slot == null ||
                slot.spawnPoint == null ||
                slot.destinationPoint == null)
            {
                Debug.LogWarning(
                    $"[ZoneSpawner] {name}: SpawnSlot {i + 1} está incompleto.",
                    this);
            }
        }

        if (enemyTypes == null || enemyTypes.Length == 0)
        {
            Debug.LogError(
                $"[ZoneSpawner] {name}: no hay tipos de enemigos.",
                this);
        }

        if (debugLog)
            Debug.Log(
                $"[ZoneSpawner] {name}: configuración validada. Enemigos: {GetTotalEnemies()}.",
                this);
    }

    // --------------------------------------------------
    // GIZMOS
    // --------------------------------------------------

    private void OnDrawGizmosSelected()
    {
        if (!showGizmos || spawnSlots == null)
            return;

        for (int i = 0; i < spawnSlots.Length; i++)
        {
            SpawnSlot slot = spawnSlots[i];

            if (slot == null)
                continue;

            if (slot.spawnPoint != null)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawWireSphere(slot.spawnPoint.position, 0.35f);
            }

            if (slot.destinationPoint != null)
            {
                Gizmos.color = Color.green;
                Gizmos.DrawWireSphere(slot.destinationPoint.position, 0.3f);
            }

            if (slot.spawnPoint != null &&
                slot.destinationPoint != null)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawLine(
                    slot.spawnPoint.position,
                    slot.destinationPoint.position);
            }
        }
    }
}
