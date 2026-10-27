using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Lanzador de granadas con indicador de apuntado:
///  - Mantén la tecla (G) para apuntar: se dibuja la trayectoria y un círculo con el radio de la explosión.
///  - El círculo es ROJO si la explosión alcanzaría enemigos, AMARILLO si solo alcanza estructuras
///    y BLANCO si no alcanza nada. Los enemigos afectados se marcan con un aro rojo bajo sus pies.
///  - Suelta la tecla para lanzar. Con la tecla de cancelar (X) anulas el lanzamiento.
/// Usa el Input System nuevo. Ponlo en el jugador.
/// </summary>
public class GrenadeThrower : MonoBehaviour
{
    /// <summary>true mientras el jugador está apuntando la granada (útil para bloquear el disparo del arma).</summary>
    public static bool IsAiming { get; private set; }

    [Header("Referencias")]
    public Grenade grenadePrefab;
    [Tooltip("Desde dónde sale la granada (mano, arma, cámara...). Si está vacío usa este objeto")]
    public Transform throwPoint;
    [Tooltip("Si está vacío usa Camera.main")]
    public Camera aimCamera;

    [Header("Controles")]
    [Header("Avisos en pantalla")]
    public bool showAimHint = true;
    [Tooltip("{0} = tecla de lanzar, {1} = tecla de cancelar")]
    public string aimHintMessage = "Suelta {0} para lanzar  ·  {1} para cancelar";
    public Key throwKey = Key.G;
    public Key cancelKey = Key.X;
    [Tooltip("Mantén la tecla para apuntar y suéltala para lanzar. Si lo desmarcas, lanza al pulsar")]
    public bool holdToAim = true;

    [Header("Lanzamiento")]
    public float cooldown = 1f;
    public int grenades = 5;
    [Tooltip("La granada cae donde marca el cursor (calcula la velocidad necesaria)")]
    public bool landOnCursor = true;
    [Range(15f, 75f)] public float throwAngle = 40f;
    public float minThrowSpeed = 5f;
    public float maxThrowSpeed = 22f;
    [Tooltip("Solo se usa si 'Land On Cursor' está desmarcado")]
    public float throwSpeed = 15f;
    public float upwardBoost = 2.5f;

    [Header("Puntería")]
    public bool useMouseAim = true;
    [Tooltip("Capas con las que choca la trayectoria. Excluye aquí la capa del jugador")]
    public LayerMask aimMask = ~0;

    [Header("Indicador")]
    public bool showTrajectory = true;
    public bool markEnemies = true;
    public float maxSimTime = 5f;
    public float grenadeCastRadius = 0.1f;
    [Tooltip("Opcional: capas que cuentan como estructuras (paredes, props...). Si está vacío se detectan solas")]
    public LayerMask structureLayers;
    public float scanInterval = 0.05f;

    [Header("Colores")]
    public Color enemyColor = new Color(1f, 0.15f, 0.1f);
    public Color structureColor = new Color(1f, 0.85f, 0.1f);
    public Color neutralColor = new Color(0.85f, 0.92f, 1f);

    const int MaxEnemyMarks = 10;

    // Estado
    float nextThrowTime;
    bool aiming;
    float scanTimer;
    Color currentColor;

    // Resultados del último escaneo
    readonly List<EnemyHealth> enemiesInRange = new List<EnemyHealth>();
    int structuresInRange;

    // Trayectoria
    readonly List<Vector3> pathPoints = new List<Vector3>();

    // Elementos visuales
    GameObject visualsRoot;
    Transform markerTr;
    Material markerMat;
    LineRenderer line;
    Material lineMat;
    Material enemyMarkMat;
    readonly List<Transform> enemyMarks = new List<Transform>();

    static Texture2D markerTex, dashTex;

    bool CanThrow => grenadePrefab != null && grenades > 0 && Time.time >= nextThrowTime;

    // =====================================================================
    //  Ciclo de vida
    // =====================================================================
    void Update()
    {
        if (Keyboard.current == null) return;
        var key = Keyboard.current[throwKey];

        if (!aiming)
        {
            if (!key.wasPressedThisFrame || !CanThrow) return;

            if (holdToAim) StartAiming();
            else ThrowNow();
            return;
        }

        // --- Apuntando ---
        if (Keyboard.current[cancelKey].wasPressedThisFrame)
        {
            StopAiming();
            return;
        }

        UpdateAim();

        if (key.wasReleasedThisFrame)
        {
            ThrowNow();
            StopAiming();
        }
    }

    void OnDisable()
    {
        if (aiming) StopAiming();
    }

    void OnDestroy()
    {
        IsAiming = false;
        if (visualsRoot != null) Destroy(visualsRoot);
    }

    // =====================================================================
    //  Apuntado
    // =====================================================================
    void StartAiming()
    {
        BuildVisuals();
        visualsRoot.SetActive(true);
        aiming = true;
        IsAiming = true;
        scanTimer = 0f;
        currentColor = neutralColor;

        if (showAimHint && ControlsHintUI.Instance != null)
        {
            string text = string.Format(aimHintMessage,
                ControlsHintUI.Key(throwKey.ToString()),
                ControlsHintUI.Key(cancelKey.ToString()));
            ControlsHintUI.Instance.Show(text, 0f, true);   // 0 = se queda hasta ocultarlo
        }
    }

    void StopAiming()
    {
        aiming = false;
        IsAiming = false;
        if (visualsRoot != null) visualsRoot.SetActive(false);

        if (showAimHint && ControlsHintUI.Instance != null)
            ControlsHintUI.Instance.Hide();
    }

    void UpdateAim()
    {
        if (!ComputeThrow(out Vector3 spawn, out Vector3 velocity)) return;

        bool landed = Simulate(spawn, velocity, out RaycastHit hit);

        if (!landed)
        {
            // No llega a ningún sitio: solo trayectoria en gris
            markerTr.gameObject.SetActive(false);
            HideEnemyMarks(0);
            DrawLine(neutralColor * 0.7f);
            return;
        }

        // Punto de impacto. Si choca con una pared, se marca el suelo bajo ella
        Vector3 point = hit.point;
        Vector3 normal = hit.normal;
        Collider landing = hit.collider;

        if (normal.y < 0.6f)
        {
            Vector3 o = point + normal * 0.2f;
            if (Physics.Raycast(o, Vector3.down, out RaycastHit floor, 15f, aimMask, QueryTriggerInteraction.Ignore))
            {
                point = floor.point;
                normal = floor.normal;
                landing = floor.collider;
            }
        }

        Vector3 center = point + normal * 0.1f;
        float radius = grenadePrefab.radius;

        // ¿Qué alcanzaría la explosión? (cada pocos frames para ahorrar rendimiento)
        scanTimer -= Time.deltaTime;
        if (scanTimer <= 0f)
        {
            scanTimer = scanInterval;
            Grenade.Scan(center, radius, grenadePrefab.affectedLayers, grenadePrefab.blockingLayers,
                         structureLayers, landing, transform, enemiesInRange, out structuresInRange);
        }

        // Color según lo que se alcanza
        Color target = enemiesInRange.Count > 0 ? enemyColor
                     : structuresInRange > 0 ? structureColor
                     : neutralColor;
        currentColor = Color.Lerp(currentColor, target, 1f - Mathf.Exp(-18f * Time.deltaTime));
        float pulse = 0.85f + 0.15f * Mathf.Sin(Time.time * 7f);

        // Círculo en el suelo
        markerTr.gameObject.SetActive(true);
        markerTr.position = point + normal * 0.04f;
        markerTr.rotation = Quaternion.LookRotation(-normal, FlatUp(normal));
        float diameter = radius * 2f;
        markerTr.localScale = new Vector3(diameter, diameter, 1f);
        markerMat.color = new Color(currentColor.r, currentColor.g, currentColor.b, pulse);

        // Aros bajo los enemigos afectados
        UpdateEnemyMarks(pulse);

        DrawLine(currentColor);
    }

    // =====================================================================
    //  Cálculo del lanzamiento
    // =====================================================================
    bool ComputeThrow(out Vector3 spawn, out Vector3 velocity)
    {
        spawn = transform.position;
        velocity = Vector3.zero;

        Camera cam = aimCamera != null ? aimCamera : Camera.main;
        if (cam == null || grenadePrefab == null) return false;

        Vector3 origin = throwPoint != null ? throwPoint.position : transform.position;

        // Punto al que apunta el jugador
        Vector3 target;
        if (useMouseAim && Mouse.current != null)
        {
            Ray ray = cam.ScreenPointToRay(Mouse.current.position.ReadValue());
            target = Physics.Raycast(ray, out RaycastHit hit, 100f, aimMask, QueryTriggerInteraction.Ignore)
                ? hit.point
                : ray.GetPoint(30f);
        }
        else
        {
            target = cam.transform.position + cam.transform.forward * 30f;
        }

        if (landOnCursor)
        {
            Vector3 flat = target - origin;
            flat.y = 0f;
            Vector3 flatDir = flat.sqrMagnitude > 0.0001f
                ? flat.normalized
                : Vector3.ProjectOnPlane(cam.transform.forward, Vector3.up).normalized;

            spawn = origin + flatDir * 0.5f;
            velocity = SolveVelocity(spawn, target, flatDir);
        }
        else
        {
            Vector3 dir = (target - origin).normalized;
            spawn = origin + dir * 0.5f;
            velocity = dir * throwSpeed + Vector3.up * upwardBoost;
        }

        return true;
    }

    // Velocidad necesaria para que, con un ángulo fijo, la granada llegue al objetivo
    Vector3 SolveVelocity(Vector3 from, Vector3 target, Vector3 flatDir)
    {
        Vector3 delta = target - from;
        float h = delta.y;
        delta.y = 0f;
        float d = Mathf.Max(delta.magnitude, 0.5f);

        float g = Mathf.Abs(Physics.gravity.y);
        float angle = throwAngle * Mathf.Deg2Rad;
        float cos = Mathf.Cos(angle);
        float sin = Mathf.Sin(angle);
        float tan = Mathf.Tan(angle);

        float denom = 2f * cos * cos * (d * tan - h);
        float v = denom > 0.001f ? Mathf.Sqrt(g * d * d / denom) : maxThrowSpeed;
        v = Mathf.Clamp(v, minThrowSpeed, maxThrowSpeed);

        return flatDir * (v * cos) + Vector3.up * (v * sin);
    }

    // Simula la trayectoria con la gravedad de Unity hasta el primer impacto
    bool Simulate(Vector3 start, Vector3 velocity, out RaycastHit hit)
    {
        pathPoints.Clear();
        pathPoints.Add(start);

        const float dt = 0.02f;
        int steps = Mathf.CeilToInt(maxSimTime / dt);
        Vector3 p = start;
        Vector3 v = velocity;

        for (int i = 0; i < steps; i++)
        {
            v += Physics.gravity * dt;
            Vector3 step = v * dt;
            float len = step.magnitude;
            if (len < 0.0001f) break;
            Vector3 dir = step / len;

            if (Physics.SphereCast(p, grenadeCastRadius, dir, out hit, len, aimMask, QueryTriggerInteraction.Ignore))
            {
                if (hit.distance <= 0f) hit.point = p; // empezó dentro de un collider
                pathPoints.Add(p + dir * hit.distance);
                return true;
            }

            p += step;
            pathPoints.Add(p);
        }

        hit = default;
        return false;
    }

    // =====================================================================
    //  Lanzar
    // =====================================================================
    void ThrowNow()
    {
        if (!CanThrow) return;
        if (!ComputeThrow(out Vector3 spawn, out Vector3 velocity)) return;

        Grenade g = Instantiate(grenadePrefab, spawn, Quaternion.identity);
        g.Launch(velocity);

        grenades--;
        nextThrowTime = Time.time + cooldown;
    }

    /// <summary>Útil para pickups de munición de granadas.</summary>
    public void AddGrenades(int amount)
    {
        grenades += amount;
    }

    // =====================================================================
    //  Elementos visuales
    // =====================================================================
    void DrawLine(Color c)
    {
        if (!showTrajectory)
        {
            line.positionCount = 0;
            return;
        }

        line.positionCount = pathPoints.Count;
        line.SetPositions(pathPoints.ToArray());
        line.startColor = new Color(c.r, c.g, c.b, 0.9f);
        line.endColor = new Color(c.r, c.g, c.b, 0.9f);
    }

    void UpdateEnemyMarks(float pulse)
    {
        if (!markEnemies)
        {
            HideEnemyMarks(0);
            return;
        }

        int count = Mathf.Min(enemiesInRange.Count, MaxEnemyMarks);

        for (int i = 0; i < count; i++)
        {
            var enemy = enemiesInRange[i];
            if (enemy == null) { enemyMarks[i].gameObject.SetActive(false); continue; }

            Vector3 pos = enemy.transform.position;
            if (Physics.Raycast(pos + Vector3.up * 1f, Vector3.down, out RaycastHit floor, 4f, aimMask,
                                QueryTriggerInteraction.Ignore))
                pos = floor.point;

            var t = enemyMarks[i];
            t.gameObject.SetActive(true);
            t.position = pos + Vector3.up * 0.05f;
            t.rotation = Quaternion.Euler(90f, 0f, 0f);
            float size = 1.6f * pulse;
            t.localScale = new Vector3(size, size, 1f);
        }

        enemyMarkMat.color = new Color(enemyColor.r, enemyColor.g, enemyColor.b, 0.95f);
        HideEnemyMarks(count);
    }

    void HideEnemyMarks(int from)
    {
        for (int i = from; i < enemyMarks.Count; i++)
            enemyMarks[i].gameObject.SetActive(false);
    }

    static Vector3 FlatUp(Vector3 normal)
    {
        return Mathf.Abs(Vector3.Dot(normal, Vector3.forward)) > 0.9f ? Vector3.up : Vector3.forward;
    }

    void BuildVisuals()
    {
        if (visualsRoot != null) return;

        EnsureTextures();
        visualsRoot = new GameObject("GrenadeAimVisuals");

        // Círculo del radio de explosión
        markerMat = new Material(Shader.Find("Sprites/Default")) { mainTexture = markerTex };
        markerTr = CreateQuad("Marker", markerMat);

        // Aros de los enemigos
        enemyMarkMat = new Material(Shader.Find("Sprites/Default")) { mainTexture = markerTex };
        for (int i = 0; i < MaxEnemyMarks; i++)
        {
            var t = CreateQuad("EnemyMark" + i, enemyMarkMat);
            t.gameObject.SetActive(false);
            enemyMarks.Add(t);
        }

        // Trayectoria punteada
        var lineGo = new GameObject("Trajectory");
        lineGo.transform.SetParent(visualsRoot.transform, false);
        line = lineGo.AddComponent<LineRenderer>();
        line.useWorldSpace = true;
        line.widthMultiplier = 0.07f;
        line.alignment = LineAlignment.View;
        line.textureMode = LineTextureMode.Tile;
        line.numCapVertices = 2;
        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        line.receiveShadows = false;
        lineMat = new Material(Shader.Find("Sprites/Default")) { mainTexture = dashTex };
        lineMat.mainTextureScale = new Vector2(2.5f, 1f);
        line.sharedMaterial = lineMat;

        visualsRoot.SetActive(false);
    }

    Transform CreateQuad(string name, Material mat)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
        go.name = name;
        var col = go.GetComponent<Collider>();
        col.enabled = false;
        Destroy(col);
        go.transform.SetParent(visualsRoot.transform, false);

        var r = go.GetComponent<MeshRenderer>();
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        r.sharedMaterial = mat;
        return go.transform;
    }

    // =====================================================================
    //  Texturas generadas por código
    // =====================================================================
    static void EnsureTextures()
    {
        if (markerTex != null && dashTex != null) return;

        // Círculo: relleno suave + aro marcado + punto central
        const int size = 256;
        markerTex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        markerTex.wrapMode = TextureWrapMode.Clamp;
        float half = size / 2f;
        Vector2 c = new Vector2(half - 0.5f, half - 0.5f);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), c) / half;

                float fill = (0.12f + 0.12f * Smooth(0.5f, 0.93f, d)) * (1f - Smooth(0.93f, 0.97f, d));
                float ring = Smooth(0.925f, 0.945f, d) * (1f - Smooth(0.985f, 1f, d));
                float dot = 1f - Smooth(0.025f, 0.04f, d);

                float a = Mathf.Clamp01(Mathf.Max(fill, Mathf.Max(ring, dot)));
                markerTex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        }
        markerTex.Apply();

        // Línea punteada
        dashTex = new Texture2D(32, 4, TextureFormat.RGBA32, false);
        dashTex.wrapMode = TextureWrapMode.Repeat;
        dashTex.filterMode = FilterMode.Point;
        for (int y = 0; y < 4; y++)
            for (int x = 0; x < 32; x++)
                dashTex.SetPixel(x, y, new Color(1f, 1f, 1f, x < 18 ? 1f : 0f));
        dashTex.Apply();
    }

    static float Smooth(float e0, float e1, float x)
    {
        float t = Mathf.Clamp01((x - e0) / (e1 - e0));
        return t * t * (3f - 2f * t);
    }
}