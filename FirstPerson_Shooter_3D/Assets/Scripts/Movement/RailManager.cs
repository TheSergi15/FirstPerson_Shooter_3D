
using System;
using System.Collections;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Sistema de movimiento por raíles.
/// Utiliza Rigidbody.MovePosition y MoveRotation para sincronizar
/// el desplazamiento con el sistema de física de Unity.
/// </summary>
public class RailManager : MonoBehaviour
{
    public static RailManager Instance { get; private set; }

    [Header("Configuración de Ruta")]
    [Tooltip("Arrastra aquí tu objeto Player1")]
    public Transform playerParent;

    [Tooltip("Puntos a los que se moverá el jugador en orden")]
    public Transform[] waypoints;

    [Tooltip("Velocidad media en metros por segundo")]
    public float moveSpeed = 5f;

    [Tooltip("Curva de aceleración y frenado")]
    public AnimationCurve easeCurve =
        AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Posición inicial")]
    public RailAdvanceCondition startCondition =
        RailAdvanceCondition.KillEnemies;

    public float startAutoDelay = 2f;

    [Header("Progreso de Zona")]
    public int enemiesToKillForNextZone = 3;

    [Header("Depuración")]
    public bool debugLog = true;

    // Estado público
    public bool IsMoving => moving;
    public bool IsFinished => finished;
    public int CurrentStopIndex => currentStop;
    public int KillsInZone => kills;
    public int KillsRequired => GetKillsRequired();
    public bool Paused { get; set; }

    // Eventos
    public event Action<int> OnStartedMoving;
    public event Action<int> OnArrived;
    public event Action<int, int> OnKillProgress;
    public event Action OnPathFinished;

    // Referencias y estado interno
    private Rigidbody playerRb;

    private RailStop[] stops;

    private int currentStop = -1;
    private int kills;

    private bool moving;
    private bool finished;

    private Vector3 startPos;

    private Coroutine autoLeaveRoutine;

    // Ciclo de vida
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning(
                "[RailManager] Ya existe otro RailManager.",
                this
            );

            Destroy(this);
            return;
        }

        Instance = this;

        if (playerParent == null)
        {
            Debug.LogError(
                "[RailManager] Falta asignar Player Parent.",
                this
            );

            enabled = false;
            return;
        }

        // Obtener el Rigidbody del mismo objeto Player.
        playerRb = playerParent.GetComponent<Rigidbody>();

        if (playerRb == null)
        {
            Debug.LogError(
                "[RailManager] Player Parent necesita un Rigidbody.",
                playerParent
            );

            enabled = false;
            return;
        }

        if (!playerRb.isKinematic)
        {
            Debug.LogWarning(
                "[RailManager] El Rigidbody no tiene Is Kinematic activado.",
                playerParent
            );
        }

        startPos = playerRb.position;

        // Guardar los RailStop de cada waypoint.
        waypoints = waypoints ?? new Transform[0];

        stops = new RailStop[waypoints.Length];

        for (int i = 0; i < waypoints.Length; i++)
        {
            if (waypoints[i] == null)
            {
                Debug.LogError(
                    $"[RailManager] El waypoint {i} está vacío.",
                    this
                );
            }
            else
            {
                stops[i] = waypoints[i].GetComponent<RailStop>();
            }
        }
    }

    private void Start()
    {
        ScheduleAutoLeave();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    // API pública

    public void AddKill()
    {
        if (finished)
            return;

        if (moving)
        {
            Log("Baja ignorada: el jugador se está moviendo.");
            return;
        }

        kills++;

        int required = GetKillsRequired();

        Log($"Progreso de zona: {kills} / {required}");

        OnKillProgress?.Invoke(kills, required);

        if (GetCondition() == RailAdvanceCondition.KillEnemies
            && kills >= required)
        {
            LeaveCurrentStop();
        }
    }

    public bool GoToNextWaypoint()
    {
        if (moving || finished || currentStop + 1 >= waypoints.Length)
            return false;

        LeaveCurrentStop();

        return true;
    }

    // Lógica interna

    private void LeaveCurrentStop()
    {
        if (autoLeaveRoutine != null)
        {
            StopCoroutine(autoLeaveRoutine);
            autoLeaveRoutine = null;
        }

        RailStop current = CurrentStop;

        if (current != null)
            current.onLeave.Invoke();

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

    /// <summary>
    /// Movimiento sincronizado con los pasos de física.
    /// </summary>
    private IEnumerator MoveRoutine(int index)
    {
        moving = true;
        kills = 0;

        OnStartedMoving?.Invoke(index);

        Transform target = waypoints[index];

        RailStop stop = stops[index];

        float speed =
            (stop != null && stop.overrideSpeed)
            ? stop.moveSpeed
            : moveSpeed;

        speed = Mathf.Max(0.01f, speed);

        Vector3 fromPos = playerRb.position;
        Quaternion fromRot = playerRb.rotation;

        float distance = Vector3.Distance(
            fromPos,
            target.position
        );

        float duration = Mathf.Max(
            distance / speed,
            0.01f
        );

        Log($"Moviendo al waypoint {index} ({distance:F1} m)");

        float t = 0f;

        while (t < 1f)
        {
            // Esperar al siguiente paso de física.
            yield return new WaitForFixedUpdate();

            // Si está pausado, no avanzar.
            if (Paused)
                continue;

            // Usar el tiempo fijo de física.
            t += Time.fixedDeltaTime / duration;

            float normalizedTime = Mathf.Clamp01(t);

            float k = easeCurve.Evaluate(normalizedTime);

            Vector3 nextPosition = Vector3.LerpUnclamped(
                fromPos,
                target.position,
                k
            );

            Quaternion nextRotation = Quaternion.SlerpUnclamped(
                fromRot,
                target.rotation,
                k
            );

            // Movimiento físico del Rigidbody cinemático.
            playerRb.MovePosition(nextPosition);
            playerRb.MoveRotation(nextRotation);
        }

        // Esperar a que se aplique el último movimiento físico.
        yield return new WaitForFixedUpdate();

        currentStop = index;

        kills = 0;

        moving = false;

        if (stop != null)
            stop.onArrive.Invoke();

        OnArrived?.Invoke(index);

        // Generación de enemigos.
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

    // Avance automático

    private void ScheduleAutoLeave()
    {
        if (autoLeaveRoutine != null)
        {
            StopCoroutine(autoLeaveRoutine);
            autoLeaveRoutine = null;
        }

        if (GetCondition() == RailAdvanceCondition.AfterDelay)
        {
            autoLeaveRoutine = StartCoroutine(
                AutoLeave(currentStop, GetAutoDelay())
            );
        }
    }

    private IEnumerator AutoLeave(int stopWhenScheduled, float delay)
    {
        yield return new WaitForSeconds(delay);

        autoLeaveRoutine = null;

        if (!moving && !finished && currentStop == stopWhenScheduled)
            LeaveCurrentStop();
    }

    // Ajustes de la parada actual

    private RailStop CurrentStop =>
        currentStop >= 0 &&
        stops != null &&
        currentStop < stops.Length
        ? stops[currentStop]
        : null;

    private RailAdvanceCondition GetCondition()
    {
        if (currentStop < 0)
            return startCondition;

        RailStop s = CurrentStop;

        return s != null
            ? s.leaveCondition
            : RailAdvanceCondition.KillEnemies;
    }

    private int GetKillsRequired()
    {
        RailStop s = CurrentStop;

        int n = (currentStop >= 0 && s != null)
            ? s.enemiesToKill
            : enemiesToKillForNextZone;

        return Mathf.Max(1, n);
    }

    private float GetAutoDelay()
    {
        if (currentStop < 0)
            return startAutoDelay;

        RailStop s = CurrentStop;

        return s != null
            ? s.autoAdvanceDelay
            : 2f;
    }

    private void Log(string msg)
    {
        if (debugLog)
            Debug.Log("[RailManager] " + msg, this);
    }

    // Gizmos

    private void OnDrawGizmos()
    {
        if (waypoints == null || waypoints.Length == 0)
            return;

        Vector3 prev = Application.isPlaying
            ? startPos
            : (playerParent != null
                ? playerParent.position
                : waypoints[0] != null
                    ? waypoints[0].position
                    : Vector3.zero);

        for (int i = 0; i < waypoints.Length; i++)
        {
            if (waypoints[i] == null)
                continue;

            RailStop stop = waypoints[i].GetComponent<RailStop>();

            RailAdvanceCondition cond = stop != null
                ? stop.leaveCondition
                : RailAdvanceCondition.KillEnemies;

            Gizmos.color =
                cond == RailAdvanceCondition.Manual
                ? Color.cyan
                : cond == RailAdvanceCondition.AfterDelay
                    ? Color.yellow
                    : new Color(1f, 0.35f, 0.3f);

            Vector3 p = waypoints[i].position;

            Gizmos.DrawLine(prev, p);
            Gizmos.DrawWireSphere(p, 0.4f);
            Gizmos.DrawRay(p, waypoints[i].forward * 1.2f);

#if UNITY_EDITOR
            Handles.Label(p + Vector3.up * 0.8f, i.ToString());
#endif

            prev = p;
        }
    }
}
