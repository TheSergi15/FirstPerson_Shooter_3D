using System;
using System.Collections;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Sistema de movimiento por raíles.
///  - El jugador (playerParent) viaja por una lista de waypoints con aceleración/frenado suaves.
///  - Cada parada decide cómo se sale de ella (ver RailStop): matando enemigos, de forma manual
///    (la ordena otro script, p. ej. el tutorial) o automáticamente tras unos segundos.
///  - Compatible con lo que ya tenías: AddKill(), ZoneSpawner en el waypoint y los campos
///    playerParent / waypoints / moveSpeed / enemiesToKillForNextZone.
/// </summary>
public class RailManager : MonoBehaviour
{
    public static RailManager Instance { get; private set; }

    [Header("Configuración de Ruta")]
    [Tooltip("Arrastra aquí tu objeto Player1")]
    public Transform playerParent;
    [Tooltip("Puntos a los que se moverá el jugador en orden")]
    public Transform[] waypoints;
    [Tooltip("Velocidad media en metros por segundo (cada RailStop puede sobrescribirla)")]
    public float moveSpeed = 5f;
    [Tooltip("Aceleración y frenado. EaseInOut = arranca y llega suave; lineal = velocidad constante")]
    public AnimationCurve easeCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Posición inicial")]
    [Tooltip("Cómo se sale del punto de partida. En el tutorial: Manual")]
    public RailAdvanceCondition startCondition = RailAdvanceCondition.KillEnemies;
    [Tooltip("Solo con 'After Delay'")]
    public float startAutoDelay = 2f;

    [Header("Progreso de Zona (valor por defecto)")]
    [Tooltip("Enemigos a matar para salir del punto de partida y de las paradas sin componente RailStop")]
    public int enemiesToKillForNextZone = 3;

    [Header("Depuración")]
    public bool debugLog = true;

    // ───────── Estado público ─────────
    /// <summary>true mientras el jugador viaja hacia un waypoint.</summary>
    public bool IsMoving => moving;
    /// <summary>true cuando se ha completado la última parada.</summary>
    public bool IsFinished => finished;
    /// <summary>Índice del waypoint en el que está el jugador (-1 = punto de partida).</summary>
    public int CurrentStopIndex => currentStop;
    public int KillsInZone => kills;
    public int KillsRequired => GetKillsRequired();
    public bool Paused { get; set; }

    // ───────── Eventos ─────────
    /// <summary>Empieza a moverse hacia el waypoint con ese índice.</summary>
    public event Action<int> OnStartedMoving;
    /// <summary>Ha llegado al waypoint con ese índice.</summary>
    public event Action<int> OnArrived;
    /// <summary>(bajas, necesarias) cada vez que muere un enemigo de la zona.</summary>
    public event Action<int, int> OnKillProgress;
    /// <summary>Se ha completado la última parada: fin de la ruta.</summary>
    public event Action OnPathFinished;

    private RailStop[] stops;
    private int currentStop = -1;
    private int kills;
    private bool moving;
    private bool finished;
    private Vector3 startPos;
    private Coroutine autoLeaveRoutine;

    // ───────── Ciclo de vida ─────────
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("[RailManager] Ya existe otro RailManager; se elimina este componente.", this);
            Destroy(this);
            return;
        }
        Instance = this;

        if (playerParent == null)
        {
            Debug.LogError("[RailManager] Falta asignar 'Player Parent'.", this);
            enabled = false;
            return;
        }

        startPos = playerParent.position;

        // Se guarda el RailStop de cada waypoint una sola vez
        waypoints = waypoints ?? new Transform[0];
        stops = new RailStop[waypoints.Length];
        for (int i = 0; i < waypoints.Length; i++)
        {
            if (waypoints[i] == null)
                Debug.LogError($"[RailManager] El waypoint {i} está vacío.", this);
            else
                stops[i] = waypoints[i].GetComponent<RailStop>();
        }
    }

    private void Start()
    {
        ScheduleAutoLeave(); // por si el punto de partida es 'After Delay'
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // ───────── API pública ─────────

    /// <summary>La llaman los enemigos al morir.</summary>
    public void AddKill()
    {
        if (finished) return;

        // Las bajas durante el viaje no cuentan: pertenecen a la zona anterior
        if (moving)
        {
            Log("Baja ignorada: el jugador se está moviendo.");
            return;
        }

        kills++;
        int required = GetKillsRequired();
        Log($"Progreso de zona: {kills} / {required}");
        OnKillProgress?.Invoke(kills, required);

        if (GetCondition() == RailAdvanceCondition.KillEnemies && kills >= required)
            LeaveCurrentStop();
    }

    /// <summary>
    /// Avanza al siguiente waypoint SIN necesidad de matar enemigos (tutorial, cinemáticas...).
    /// Devuelve false si ya se está moviendo o no quedan waypoints.
    /// </summary>
    public bool GoToNextWaypoint()
    {
        if (moving || finished || currentStop + 1 >= waypoints.Length) return false;
        LeaveCurrentStop();
        return true;
    }

    // ───────── Lógica interna ─────────
    private void LeaveCurrentStop()
    {
        if (autoLeaveRoutine != null)
        {
            StopCoroutine(autoLeaveRoutine);
            autoLeaveRoutine = null;
        }

        RailStop current = CurrentStop;
        if (current != null) current.onLeave.Invoke();

        int next = currentStop + 1;
        if (next >= waypoints.Length)
        {
            finished = true;
            Log("¡Has llegado al final de la ruta!");
            OnPathFinished?.Invoke();
            return;
        }

        StartCoroutine(MoveRoutine(next));
    }

    private IEnumerator MoveRoutine(int index)
    {
        moving = true; // primera línea: IsMoving es true en cuanto se ordena el movimiento
        kills = 0;
        OnStartedMoving?.Invoke(index);

        Transform target = waypoints[index];
        RailStop stop = stops[index];

        float speed = (stop != null && stop.overrideSpeed) ? stop.moveSpeed : moveSpeed;
        speed = Mathf.Max(0.01f, speed);

        Vector3 fromPos = playerParent.position;
        Quaternion fromRot = playerParent.rotation;
        float distance = Vector3.Distance(fromPos, target.position);
        float duration = Mathf.Max(distance / speed, 0.01f); // la curva suaviza, pero la media se mantiene

        Log($"Moviendo al waypoint {index} ({distance:F1} m)");

        float t = 0f;
        while (t < 1f)
        {
            if (!Paused)
            {
                t += Time.deltaTime / duration;
                float k = easeCurve.Evaluate(Mathf.Clamp01(t));
                playerParent.position = Vector3.LerpUnclamped(fromPos, target.position, k);
                playerParent.rotation = Quaternion.SlerpUnclamped(fromRot, target.rotation, k);
            }
            yield return null;
        }

        // Posición exacta al llegar
        playerParent.SetPositionAndRotation(target.position, target.rotation);

        currentStop = index;
        kills = 0;
        moving = false;

        if (stop != null) stop.onArrive.Invoke();
        OnArrived?.Invoke(index);

        // Genera los enemigos de la zona al llegar (tu ZoneSpawner)
        ZoneSpawner spawner = target.GetComponent<ZoneSpawner>();
        if (spawner != null)
        {
            if (stop != null && stop.spawnDelay > 0f)
                yield return new WaitForSeconds(stop.spawnDelay);

            Log("¡Llegamos al waypoint! Generando enemigos...");
            spawner.SpawnEnemies();
        }

        ScheduleAutoLeave();
    }

    private void ScheduleAutoLeave()
    {
        if (autoLeaveRoutine != null)
        {
            StopCoroutine(autoLeaveRoutine);
            autoLeaveRoutine = null;
        }

        if (GetCondition() == RailAdvanceCondition.AfterDelay)
            autoLeaveRoutine = StartCoroutine(AutoLeave(currentStop, GetAutoDelay()));
    }

    private IEnumerator AutoLeave(int stopWhenScheduled, float delay)
    {
        yield return new WaitForSeconds(delay);
        autoLeaveRoutine = null;
        if (!moving && !finished && currentStop == stopWhenScheduled)
            LeaveCurrentStop();
    }

    // ───────── Ajustes de la parada actual ─────────
    private RailStop CurrentStop =>
        currentStop >= 0 && stops != null && currentStop < stops.Length ? stops[currentStop] : null;

    private RailAdvanceCondition GetCondition()
    {
        if (currentStop < 0) return startCondition;
        RailStop s = CurrentStop;
        return s != null ? s.leaveCondition : RailAdvanceCondition.KillEnemies;
    }

    private int GetKillsRequired()
    {
        RailStop s = CurrentStop;
        int n = (currentStop >= 0 && s != null) ? s.enemiesToKill : enemiesToKillForNextZone;
        return Mathf.Max(1, n);
    }

    private float GetAutoDelay()
    {
        if (currentStop < 0) return startAutoDelay;
        RailStop s = CurrentStop;
        return s != null ? s.autoAdvanceDelay : 2f;
    }

    private void Log(string msg)
    {
        if (debugLog) Debug.Log("[RailManager] " + msg, this);
    }

    // ───────── Ayuda visual en el editor ─────────
    private void OnDrawGizmos()
    {
        if (waypoints == null || waypoints.Length == 0) return;

        Vector3 prev = Application.isPlaying
            ? startPos
            : (playerParent != null ? playerParent.position : waypoints[0] != null ? waypoints[0].position : Vector3.zero);

        for (int i = 0; i < waypoints.Length; i++)
        {
            if (waypoints[i] == null) continue;

            RailStop stop = waypoints[i].GetComponent<RailStop>();
            RailAdvanceCondition cond = stop != null ? stop.leaveCondition : RailAdvanceCondition.KillEnemies;
            Gizmos.color = cond == RailAdvanceCondition.Manual ? Color.cyan
                         : cond == RailAdvanceCondition.AfterDelay ? Color.yellow
                         : new Color(1f, 0.35f, 0.3f);

            Vector3 p = waypoints[i].position;
            Gizmos.DrawLine(prev, p);
            Gizmos.DrawWireSphere(p, 0.4f);
            Gizmos.DrawRay(p, waypoints[i].forward * 1.2f);   // hacia dónde mirará el jugador

#if UNITY_EDITOR
            Handles.Label(p + Vector3.up * 0.8f, i.ToString());
#endif
            prev = p;
        }
    }
}
