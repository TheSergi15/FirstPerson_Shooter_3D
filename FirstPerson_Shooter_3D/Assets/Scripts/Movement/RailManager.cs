using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Splines;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Sistema de movimiento por raíles. El jugador viaja por un Spline (el objeto de los raíles)
/// de parada en parada. Cada waypoint se proyecta sobre el raíl, así que las esquinas y curvas
/// las hace el propio raíl. Si no hay Spline asignado, va en línea recta entre waypoints.
/// Utiliza Rigidbody.MovePosition y MoveRotation para sincronizar con la física.
/// </summary>
public class RailManager : MonoBehaviour
{
    public enum RotationMode
    {
        WaypointRotation, // gira hacia la rotación del waypoint durante todo el trayecto (como antes)
        FollowRail        // mira hacia delante por el raíl y se orienta al waypoint al llegar
    }

    public static RailManager Instance { get; private set; }

    [Header("Configuración de Ruta")]
    [Tooltip("Arrastra aquí tu objeto Player1")]
    public Transform playerParent;

    [Tooltip("Paradas en orden de recorrido. Colócalas sobre el raíl (o muy cerca de él)")]
    public Transform[] waypoints;

    [Tooltip("Velocidad media en metros por segundo")]
    public float moveSpeed = 5f;

    [Tooltip("Curva de aceleración y frenado")]
    public AnimationCurve easeCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Raíl (Spline)")]
    [Tooltip("Objeto con el SplineContainer de los raíles. Vacío = línea recta entre waypoints")]
    public SplineContainer rail;

    [Tooltip("Al empezar, coloca al jugador en el inicio del raíl")]
    public bool snapPlayerToRailStart = true;

    [Tooltip("Desplazamiento del jugador respecto al raíl (por ejemplo, subirlo a la altura del vagón)")]
    public Vector3 positionOffset = Vector3.zero;

    public RotationMode rotationMode = RotationMode.FollowRail;

    [Tooltip("FollowRail: fracción inicial del trayecto en la que gira hacia delante")]
    [Range(0.05f, 0.5f)] public float turnInFraction = 0.25f;

    [Tooltip("FollowRail: fracción final del trayecto en la que gira hacia la rotación del waypoint")]
    [Range(0.05f, 0.5f)] public float turnOutFraction = 0.3f;

    [Tooltip("Precisión con la que se mide el raíl (más = más exacto)")]
    [Range(64, 2048)] public int railSamples = 512;

    [Header("Posición inicial")]
    public RailAdvanceCondition startCondition = RailAdvanceCondition.KillEnemies;

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

    // Tabla del raíl (posiciones y distancia acumulada)
    private bool railReady;
    private Vector3[] samplePos;
    private float[] sampleDist;
    private float railLength;
    private float[] stopDist;     // distancia sobre el raíl de cada waypoint
    private float currentDist;    // distancia sobre el raíl donde está el jugador

    // ───────── Ciclo de vida ─────────
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("[RailManager] Ya existe otro RailManager.", this);
            Destroy(this);
            return;
        }

        Instance = this;

        if (playerParent == null)
        {
            Debug.LogError("[RailManager] Falta asignar Player Parent.", this);
            enabled = false;
            return;
        }

        playerRb = playerParent.GetComponent<Rigidbody>();

        if (playerRb == null)
        {
            Debug.LogError("[RailManager] Player Parent necesita un Rigidbody.", playerParent);
            enabled = false;
            return;
        }

        if (!playerRb.isKinematic)
            Debug.LogWarning("[RailManager] El Rigidbody no tiene Is Kinematic activado.", playerParent);

        startPos = playerRb.position;

        waypoints = waypoints ?? new Transform[0];
        stops = new RailStop[waypoints.Length];

        for (int i = 0; i < waypoints.Length; i++)
        {
            if (waypoints[i] == null)
                Debug.LogError($"[RailManager] El waypoint {i} está vacío.", this);
            else
                stops[i] = waypoints[i].GetComponent<RailStop>();
        }

        BuildRail();
    }

    private void Start()
    {
        if (railReady && snapPlayerToRailStart)
        {
            Vector3 p = RailPointAtDistance(0f) + positionOffset;
            playerParent.position = p;
            playerRb.position = p;
            startPos = p;
        }

        ScheduleAutoLeave();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    // ───────── Raíl ─────────
    private void BuildRail()
    {
        railReady = false;
        if (rail == null) return;

        if (rail.Spline == null || rail.Spline.Count < 2)
        {
            Debug.LogWarning("[RailManager] El Spline necesita al menos 2 puntos. Uso línea recta.", this);
            return;
        }

        int n = Mathf.Max(16, railSamples);
        samplePos = new Vector3[n + 1];
        sampleDist = new float[n + 1];

        for (int i = 0; i <= n; i++)
        {
            samplePos[i] = RailPointAtT(i / (float)n);
            if (i > 0)
                sampleDist[i] = sampleDist[i - 1] + Vector3.Distance(samplePos[i - 1], samplePos[i]);
        }

        railLength = sampleDist[n];

        if (railLength < 0.1f)
        {
            Debug.LogWarning("[RailManager] El Spline mide casi 0 m. Uso línea recta.", this);
            return;
        }

        // Cada waypoint se proyecta sobre el raíl, siempre por delante del anterior
        float prev = snapPlayerToRailStart ? 0f : NearestDistance(startPos - positionOffset, 0f, out _);
        currentDist = prev;

        stopDist = new float[waypoints.Length];

        for (int i = 0; i < waypoints.Length; i++)
        {
            if (waypoints[i] == null) { stopDist[i] = prev; continue; }

            float d = NearestDistance(waypoints[i].position - positionOffset, prev, out float gap);
            stopDist[i] = d;

            if (gap > 2f)
                Debug.LogWarning($"[RailManager] El waypoint {i} está a {gap:F1} m del raíl. Acércalo al Spline.", waypoints[i]);

            if (i > 0 && Mathf.Abs(d - prev) < 0.05f)
                Debug.LogWarning($"[RailManager] El waypoint {i} cae en el mismo punto del raíl que el anterior. " +
                                 "Revisa que estén en orden a lo largo del raíl.", waypoints[i]);

            prev = d;
        }

        railReady = true;
        Log($"Raíl listo: {railLength:F1} m, {waypoints.Length} paradas.");
    }

    // Posición del raíl en el mundo para un parámetro t (0-1)
    private Vector3 RailPointAtT(float t)
    {
        Vector3 local = SplineUtility.EvaluatePosition(rail.Spline, t);
        return rail.transform.TransformPoint(local);
    }

    // Dirección del raíl en el mundo para un parámetro t (0-1)
    private Vector3 RailTangentAtT(float t)
    {
        Vector3 local = SplineUtility.EvaluateTangent(rail.Spline, t);
        return rail.transform.TransformDirection(local);
    }

    /// <summary>Convierte una distancia sobre el raíl (en metros) en el parámetro t del Spline.</summary>
    private float DistanceToT(float d)
    {
        d = Mathf.Clamp(d, 0f, railLength);

        int lo = 0, hi = sampleDist.Length - 1;
        while (hi - lo > 1)
        {
            int mid = (lo + hi) / 2;
            if (sampleDist[mid] <= d) lo = mid; else hi = mid;
        }

        float seg = sampleDist[hi] - sampleDist[lo];
        float f = seg > 0.0001f ? (d - sampleDist[lo]) / seg : 0f;
        return (lo + f) / (sampleDist.Length - 1f);
    }

    private Vector3 RailPointAtDistance(float d)
    {
        return RailPointAtT(DistanceToT(d));
    }

    /// <summary>Distancia sobre el raíl del punto más cercano a 'point', buscando desde minDist hacia delante.</summary>
    private float NearestDistance(Vector3 point, float minDist, out float gap)
    {
        int first = 0;
        while (first < sampleDist.Length - 1 && sampleDist[first] < minDist - 0.001f) first++;

        float best = float.MaxValue;
        int bestIndex = first;

        for (int i = first; i < samplePos.Length; i++)
        {
            float sq = (samplePos[i] - point).sqrMagnitude;
            if (sq < best) { best = sq; bestIndex = i; }
        }

        gap = Mathf.Sqrt(best);
        return sampleDist[bestIndex];
    }

    // ───────── API pública ─────────
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

        if (GetCondition() == RailAdvanceCondition.KillEnemies && kills >= required)
            LeaveCurrentStop();
    }

    public bool GoToNextWaypoint()
    {
        if (moving || finished || currentStop + 1 >= waypoints.Length)
            return false;

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

    /// <summary>Movimiento sincronizado con los pasos de física.</summary>
    private IEnumerator MoveRoutine(int index)
    {
        moving = true;
        kills = 0;

        OnStartedMoving?.Invoke(index);

        Transform target = waypoints[index];
        RailStop stop = stops[index];

        float speed = (stop != null && stop.overrideSpeed) ? stop.moveSpeed : moveSpeed;
        speed = Mathf.Max(0.01f, speed);

        Vector3 fromPos = playerRb.position;
        Quaternion fromRot = playerRb.rotation;

        bool onRail = railReady;
        float d0 = currentDist;
        float d1 = onRail ? Mathf.Max(stopDist[index], d0) : 0f;

        float distance = onRail ? (d1 - d0) : Vector3.Distance(fromPos, target.position);
        float duration = Mathf.Max(distance / speed, 0.01f);

        Log($"Moviendo al waypoint {index} ({distance:F1} m{(onRail ? " por el raíl" : "")})");

        float t = 0f;

        while (t < 1f)
        {
            yield return new WaitForFixedUpdate();

            if (Paused)
                continue;

            t += Time.fixedDeltaTime / duration;

            float k = easeCurve.Evaluate(Mathf.Clamp01(t));

            Vector3 nextPosition;
            Quaternion nextRotation;

            if (onRail)
                EvaluateOnRail(d0, d1, k, fromRot, target.rotation, out nextPosition, out nextRotation);
            else
            {
                nextPosition = Vector3.LerpUnclamped(fromPos, target.position, k);
                nextRotation = Quaternion.SlerpUnclamped(fromRot, target.rotation, k);
            }

            playerRb.MovePosition(nextPosition);
            playerRb.MoveRotation(nextRotation);
        }

        yield return new WaitForFixedUpdate();

        currentStop = index;
        if (onRail) currentDist = d1;

        kills = 0;
        moving = false;

        if (stop != null)
            stop.onArrive.Invoke();

        OnArrived?.Invoke(index);

        // Generación de enemigos
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

    private void EvaluateOnRail(float d0, float d1, float k, Quaternion fromRot, Quaternion targetRot,
                                out Vector3 pos, out Quaternion rot)
    {
        float t = DistanceToT(Mathf.Lerp(d0, d1, k));
        pos = RailPointAtT(t) + positionOffset;

        if (rotationMode == RotationMode.WaypointRotation)
        {
            rot = Quaternion.SlerpUnclamped(fromRot, targetRot, k);
            return;
        }

        // FollowRail: sale mirando hacia delante por el raíl y al final se orienta al waypoint
        Vector3 tan = RailTangentAtT(t);
        Quaternion look = tan.sqrMagnitude > 0.000001f
            ? Quaternion.LookRotation(tan.normalized, Vector3.up)
            : fromRot;

        float a = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(k / turnInFraction));
        float b = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((k - (1f - turnOutFraction)) / turnOutFraction));

        rot = Quaternion.Slerp(fromRot, look, a);
        rot = Quaternion.Slerp(rot, targetRot, b);
    }

    // ───────── Avance automático ─────────
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
        currentStop >= 0 && stops != null && currentStop < stops.Length
            ? stops[currentStop]
            : null;

    private RailAdvanceCondition GetCondition()
    {
        if (currentStop < 0)
            return startCondition;

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
        if (currentStop < 0)
            return startAutoDelay;

        RailStop s = CurrentStop;
        return s != null ? s.autoAdvanceDelay : 2f;
    }

    private void Log(string msg)
    {
        if (debugLog)
            Debug.Log("[RailManager] " + msg, this);
    }

    // ───────── Gizmos ─────────
    private void OnDrawGizmos()
    {
        if (waypoints == null || waypoints.Length == 0)
            return;

        Vector3 prev = Application.isPlaying
            ? startPos
            : (playerParent != null
                ? playerParent.position
                : waypoints[0] != null ? waypoints[0].position : Vector3.zero);

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

            // Con raíl, el camino lo dibuja el propio Spline: solo se pintan las paradas
            if (rail == null)
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