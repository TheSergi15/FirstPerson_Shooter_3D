using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Genera los enemigos de una zona cuando el RailManager llega al waypoint (SpawnEnemies).
/// Tres modos:
///  - Manual:      como antes, cada enemigo con su punto de aparición y destino.
///  - BehindWalls: detecta solo los muros que tienes delante y aparece justo detrás de ellos.
///  - Ring:        aparecen en un arco alrededor de un punto central y se juntan en él
///                 (ideal para el combate de la granada).
/// En los modos automáticos NO hay que colocar ningún punto a mano.
/// Selecciona el waypoint en la escena para ver la previsualización (esferas rojas = aparición, verdes = destino).
/// </summary>
public class ZoneSpawner : MonoBehaviour
{
    public enum SpawnMode { Manual, BehindWalls, Ring }

    // Usamos una clase serializable para agrupar los datos en el Inspector
    [System.Serializable]
    public class SpawnData
    {
        public GameObject enemyPrefab;
        public Transform spawnPoint;       // El "x lugar" donde respawnea
        public Transform destinationPoint; // El "x lugar" hacia donde se traslada
    }

    [Header("Modo")]
    public SpawnMode mode = SpawnMode.Manual;

    [Header("Manual")]
    public SpawnData[] enemiesToSpawn;

    [Header("Automático: enemigos")]
    [Tooltip("Prefabs a generar. Si hay menos que enemigos, se repiten en orden")]
    public GameObject[] enemyPrefabs;
    [Min(1)] public int enemyCount = 2;

    [Header("Detrás de muros (BehindWalls)")]
    [Tooltip("Capas que cuentan como muro. Deja fuera la del jugador y la de los enemigos")]
    public LayerMask wallMask = ~0;
    [Tooltip("Hasta dónde se buscan muros por delante del waypoint")]
    public float maxWallDistance = 40f;
    [Tooltip("Grosor máximo del muro para encontrar su cara trasera")]
    public float maxWallThickness = 4f;
    [Tooltip("Grosor que se supone si no se detecta la cara trasera")]
    public float defaultWallThickness = 0.5f;
    [Tooltip("Separación entre el muro y el enemigo al aparecer")]
    public float spawnClearance = 1f;
    [Tooltip("Abanico horizontal (grados) en el que se reparten los enemigos")]
    public float spreadAngle = 50f;
    [Tooltip("A qué distancia del waypoint se detienen los enemigos (se recorta si el muro está más cerca)")]
    public float destinationDistance = 5f;
    [Tooltip("Si no hay muro en alguna dirección, aparecen a esta distancia")]
    public float fallbackDistance = 14f;

    [Header("Alrededor de un punto (Ring)")]
    [Tooltip("Opcional. Si está vacío, el centro está 'Center Distance' metros por delante del waypoint")]
    public Transform centerPoint;
    public float centerDistance = 10f;
    [Tooltip("Distancia del centro a la que aparecen los enemigos")]
    public float ringRadius = 6f;
    [Range(30f, 360f)]
    [Tooltip("Arco del lado lejano al jugador en el que aparecen (360 = círculo completo)")]
    public float arcDegrees = 240f;
    [Tooltip("Radio en el que se agrupan al llegar. Debe ser MENOR que el radio de la granada")]
    public float gatherRadius = 1.5f;

    [Header("Común")]
    public LayerMask groundMask = ~0;
    [Tooltip("Apoya los pies del enemigo sobre el suelo y corrige la altura de su destino (evita que se hunda)")]
    public bool alignFeetToGround = true;
    [Tooltip("Margen entre los pies y el suelo")]
    public float feetClearance = 0.02f;
    [Tooltip("Ajusta las posiciones al NavMesh si existe (para enemigos con NavMeshAgent)")]
    public bool snapToNavMesh = true;
    public float navMeshSnapDistance = 3f;
    [Tooltip("Altura desde la que se lanzan los rayos para detectar muros")]
    public float originHeight = 1f;
    public bool debugLog = true;

    /// <summary>Cuántos enemigos genera (úsalo para igualar Enemies To Kill del RailStop).</summary>
    public int EnemyCount => mode == SpawnMode.Manual
        ? (enemiesToSpawn != null ? enemiesToSpawn.Length : 0)
        : enemyCount;

    private struct Plan
    {
        public Vector3 spawn;
        public Vector3 dest;
    }

    private readonly RaycastHit[] hitsBuf = new RaycastHit[16];
    private bool quiet; // sin avisos en consola (previsualización del editor)

    private void Start()
    {
        // Aviso si el RailStop de este waypoint pide un número distinto de bajas
        RailStop stop = GetComponent<RailStop>();
        if (stop != null && stop.leaveCondition == RailAdvanceCondition.KillEnemies
            && stop.enemiesToKill != EnemyCount)
        {
            Debug.LogWarning($"[ZoneSpawner] {name}: genera {EnemyCount} enemigos pero el RailStop pide matar " +
                             $"{stop.enemiesToKill}. Iguálalos o la ruta no avanzará bien.", this);
        }
    }

    // ───────── API que usa el RailManager ─────────
    public void SpawnEnemies()
    {
        if (mode == SpawnMode.Manual)
        {
            SpawnManual();
            return;
        }

        if (enemyPrefabs == null || enemyPrefabs.Length == 0)
        {
            Debug.LogError($"[ZoneSpawner] {name}: faltan 'Enemy Prefabs' para el modo {mode}.", this);
            return;
        }

        var plans = new List<Plan>();
        quiet = false;
        BuildPlan(new System.Random(), plans);

        for (int i = 0; i < plans.Count; i++)
        {
            GameObject prefab = enemyPrefabs[i % enemyPrefabs.Length];
            if (prefab == null) continue;

            Vector3 flat = Vector3.ProjectOnPlane(plans[i].dest - plans[i].spawn, Vector3.up);
            Quaternion rot = flat.sqrMagnitude > 0.001f ? Quaternion.LookRotation(flat) : Quaternion.identity;

            GameObject enemy = Instantiate(prefab, plans[i].spawn, rot);

            // Los pies sobre el suelo. Sin NavMeshAgent, el destino se sube la altura del pivote:
            // si no, el enemigo caminaría hasta que su pivote toque el suelo y se hundiría.
            bool hasAgent = enemy.GetComponent<NavMeshAgent>() != null;
            Vector3 dest = plans[i].dest;
            if (alignFeetToGround)
            {
                float pivotToGround = AlignFeetToGround(enemy, plans[i].spawn.y, hasAgent);
                if (!hasAgent) dest.y += pivotToGround;
            }

            EnemyMover mover = enemy.GetComponent<EnemyMover>();
            if (mover != null) mover.SetDestination(MakeDestination(dest));
        }

        if (debugLog) Debug.Log($"[ZoneSpawner] {name}: {plans.Count} enemigos generados ({mode}).", this);
    }

    private void SpawnManual()
    {
        if (enemiesToSpawn == null) return;

        foreach (var data in enemiesToSpawn)
        {
            if (data.enemyPrefab == null || data.spawnPoint == null) continue;

            // 1. Instanciamos al enemigo en el punto de aparición
            GameObject enemy = Instantiate(data.enemyPrefab, data.spawnPoint.position, data.spawnPoint.rotation);

            // 2. Buscamos su script de movimiento y le pasamos el destino
            EnemyMover mover = enemy.GetComponent<EnemyMover>();
            if (mover != null && data.destinationPoint != null)
                mover.SetDestination(data.destinationPoint);
        }
    }

    // ───────── Cálculo de posiciones ─────────
    private void BuildPlan(System.Random rng, List<Plan> plans)
    {
        plans.Clear();
        int n = Mathf.Max(1, enemyCount);
        Vector3 fwd = FlatForward();

        if (mode == SpawnMode.BehindWalls)
        {
            Vector3 origin = transform.position + Vector3.up * originHeight;

            for (int i = 0; i < n; i++)
            {
                float t = n == 1 ? 0f : (i / (float)(n - 1)) - 0.5f;
                Vector3 dir = Quaternion.AngleAxis(t * spreadAngle, Vector3.up) * fwd;

                Vector3 spawn;
                float wallDist;
                if (!TryFindBehindWall(origin, dir, out spawn, out wallDist))
                {
                    wallDist = fallbackDistance;
                    spawn = Ground(transform.position + dir * fallbackDistance);
                    if (!quiet)
                        Debug.LogWarning($"[ZoneSpawner] {name}: no hay muro en la dirección {i}; " +
                                         $"aparece a {fallbackDistance} m. Revisa 'Wall Mask' y la rotación del waypoint (su flecha azul debe mirar a los muros).", this);
                }

                float destDist = Mathf.Clamp(destinationDistance, 1.5f, Mathf.Max(1.5f, wallDist - 1f));
                Vector3 dest = Ground(transform.position + dir * destDist);

                plans.Add(new Plan { spawn = SnapNav(spawn), dest = SnapNav(dest) });
            }
        }
        else // Ring
        {
            Vector3 center = centerPoint != null ? centerPoint.position
                                                 : transform.position + fwd * centerDistance;
            center = Ground(center);

            for (int i = 0; i < n; i++)
            {
                float t = n == 1 ? 0f : (i / (float)(n - 1)) - 0.5f;
                Vector3 dir = Quaternion.AngleAxis(t * arcDegrees, Vector3.up) * fwd;
                Vector3 spawn = Ground(center + dir * ringRadius);

                float ang = (float)(rng.NextDouble() * Mathf.PI * 2.0);
                float rad = Mathf.Sqrt((float)rng.NextDouble()) * gatherRadius;
                Vector3 dest = Ground(center + new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang)) * rad);

                plans.Add(new Plan { spawn = SnapNav(spawn), dest = SnapNav(dest) });
            }
        }
    }

    /// <summary>Busca el primer muro en esa dirección y devuelve un punto justo detrás de él.</summary>
    private bool TryFindBehindWall(Vector3 origin, Vector3 dir, out Vector3 spawn, out float wallDistance)
    {
        spawn = origin;
        wallDistance = 0f;

        int count = Physics.RaycastNonAlloc(new Ray(origin, dir), hitsBuf, maxWallDistance,
                                            wallMask, QueryTriggerInteraction.Ignore);
        float best = float.MaxValue;
        RaycastHit wall = default;
        bool found = false;

        for (int i = 0; i < count; i++)
        {
            if (!IsWallCandidate(hitsBuf[i].collider)) continue;
            if (hitsBuf[i].distance < best)
            {
                best = hitsBuf[i].distance;
                wall = hitsBuf[i];
                found = true;
            }
        }
        if (!found) return false;

        wallDistance = best;

        // Cara trasera del muro: se lanza un rayo desde el otro lado hacia el muro
        Vector3 exit;
        Vector3 farPoint = wall.point + dir * maxWallThickness;
        if (wall.collider.Raycast(new Ray(farPoint, -dir), out RaycastHit exitHit, maxWallThickness))
            exit = exitHit.point;
        else
            exit = wall.point + dir * defaultWallThickness;

        // Un poco más allá de la cara trasera, y si queda dentro de otro collider, se aleja
        Vector3 p = exit + dir * spawnClearance;
        for (int k = 0; k < 5; k++)
        {
            Vector3 g = Ground(p);
            if (!Physics.CheckSphere(g + Vector3.up * 0.9f, 0.4f, wallMask, QueryTriggerInteraction.Ignore))
            {
                spawn = g;
                return true;
            }
            p += dir * 0.6f;
        }

        spawn = Ground(p);
        return true;
    }

    // El muro no puede ser el jugador, un enemigo, un pickup ni un objeto físico que se mueve
    private static bool IsWallCandidate(Collider c)
    {
        if (c.isTrigger) return false;
        if (c.GetComponentInParent<CharacterController>() != null) return false;
        if (c.GetComponentInParent<EnemyHealth>() != null) return false;
        if (c.GetComponentInParent<HealthPickup>() != null) return false;
        if (c.attachedRigidbody != null && !c.attachedRigidbody.isKinematic) return false;
        return true;
    }

    /// <summary>
    /// Sube al enemigo (o su baseOffset si lleva NavMeshAgent) hasta que su parte más baja
    /// quede sobre el suelo. Devuelve la distancia del pivote al suelo, para ajustar el destino.
    /// </summary>
    private float AlignFeetToGround(GameObject enemy, float groundY, bool hasAgent)
    {
        Physics.SyncTransforms();
        if (!TryGetBottom(enemy, out float bottom)) return 0f;

        float pivotToBottom = enemy.transform.position.y - bottom;
        float lift = (groundY + feetClearance) - bottom;

        if (hasAgent)
        {
            NavMeshAgent agent = enemy.GetComponent<NavMeshAgent>();
            agent.baseOffset += lift;
        }
        else
        {
            enemy.transform.position += Vector3.up * lift;
        }

        return pivotToBottom + feetClearance;
    }

    // Punto más bajo del enemigo: colliders sólidos o, si no hay, sus mallas
    private static bool TryGetBottom(GameObject enemy, out float bottom)
    {
        bottom = float.MaxValue;
        bool found = false;

        foreach (Collider c in enemy.GetComponentsInChildren<Collider>())
        {
            if (c.isTrigger || !c.enabled) continue;
            bottom = Mathf.Min(bottom, c.bounds.min.y);
            found = true;
        }
        if (found) return true;

        foreach (Renderer r in enemy.GetComponentsInChildren<Renderer>())
        {
            if (!(r is MeshRenderer) && !(r is SkinnedMeshRenderer)) continue;
            bottom = Mathf.Min(bottom, r.bounds.min.y);
            found = true;
        }
        return found;
    }

    private Vector3 FlatForward()
    {
        Vector3 f = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
        return f.sqrMagnitude > 0.001f ? f.normalized : Vector3.forward;
    }

    // Baja el punto hasta el suelo
    private Vector3 Ground(Vector3 p)
    {
        if (Physics.Raycast(p + Vector3.up * 5f, Vector3.down, out RaycastHit hit, 30f,
                            groundMask, QueryTriggerInteraction.Ignore))
            return hit.point;
        return new Vector3(p.x, transform.position.y, p.z);
    }

    private Vector3 SnapNav(Vector3 p)
    {
        if (snapToNavMesh && NavMesh.SamplePosition(p, out NavMeshHit nh, navMeshSnapDistance, NavMesh.AllAreas))
            return nh.position;
        return p;
    }

    // El EnemyMover pide un Transform como destino: se crea uno vacío en ese punto
    private Transform MakeDestination(Vector3 pos)
    {
        var go = new GameObject("SpawnDestination");
        go.transform.SetParent(transform);
        go.transform.position = pos;
        return go.transform;
    }

    // ───────── Previsualización en el editor ─────────
    private void OnDrawGizmosSelected()
    {
        if (mode == SpawnMode.Manual) return;

        var plans = new List<Plan>();
        quiet = true;
        BuildPlan(new System.Random(7), plans);
        quiet = false;

        Vector3 fwd = FlatForward();

        if (mode == SpawnMode.BehindWalls)
        {
            Gizmos.color = new Color(1f, 1f, 1f, 0.25f);
            Vector3 o = transform.position + Vector3.up * originHeight;
            Gizmos.DrawRay(o, fwd * 3f);
        }
        else
        {
            Vector3 c = centerPoint != null ? centerPoint.position : transform.position + fwd * centerDistance;
            Gizmos.color = new Color(1f, 0.85f, 0.1f);
            Gizmos.DrawWireSphere(c, gatherRadius);
            Gizmos.color = new Color(1f, 0.85f, 0.1f, 0.35f);
            Gizmos.DrawWireSphere(c, ringRadius);
        }

        for (int i = 0; i < plans.Count; i++)
        {
            Gizmos.color = new Color(1f, 0.3f, 0.25f);
            Gizmos.DrawWireSphere(plans[i].spawn + Vector3.up * 0.5f, 0.5f);

            Gizmos.color = new Color(0.3f, 1f, 0.4f);
            Gizmos.DrawWireSphere(plans[i].dest + Vector3.up * 0.2f, 0.25f);

            Gizmos.color = new Color(1f, 1f, 1f, 0.5f);
            Gizmos.DrawLine(plans[i].spawn + Vector3.up * 0.5f, plans[i].dest + Vector3.up * 0.2f);
        }
    }
}