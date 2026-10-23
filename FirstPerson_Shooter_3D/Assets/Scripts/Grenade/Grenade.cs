using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class Grenade : MonoBehaviour
{
    [Header("Mecha")]
    public float fuseTime = 3f;
    public bool explodeOnImpact = false;

    [Header("Explosión")]
    public float radius = 7f;

    [Header("Daño a enemigos")]
    [Tooltip("Dentro de este radio el enemigo muere de un solo golpe")]
    public float killRadius = 3f;
    public float killDamage = 100f;
    [Tooltip("Daño justo al salir del radio de muerte")]
    public float maxDamage = 100f;
    [Tooltip("Daño en el borde del radio de explosión")]
    public float minDamage = 25f;

    [Header("Física")]
    [Tooltip("Impulso aplicado a los Rigidbody cercanos")]
    public float explosionForce = 15f;
    public float upwardsModifier = 0.5f;
    public LayerMask affectedLayers = ~0;
    [Tooltip("Capas que bloquean la explosión (paredes, suelo...)")]
    public LayerMask blockingLayers = ~0;

    [Header("Efectos")]
    [Tooltip("Opcional: si lo dejas vacío se genera la explosión por código (ExplosionFX)")]
    public GameObject customExplosionPrefab;
    public AudioClip explosionSound;
    public AudioClip bounceSound;
    public float shakeIntensity = 0.6f;
    public float shakeRange = 25f;

    [Header("Debug")]
    [Tooltip("Muestra en la consola a quién ha dañado la granada")]
    public bool debugLog = true;

    Rigidbody rb;
    float timer;
    bool exploded;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
    }

    void OnValidate()
    {
        killRadius = Mathf.Min(killRadius, radius);
    }

    public void Launch(Vector3 velocity)
    {
        rb.AddForce(velocity, ForceMode.VelocityChange);
        rb.AddTorque(Random.insideUnitSphere * 6f, ForceMode.VelocityChange);
    }

    void Update()
    {
        timer += Time.deltaTime;
        if (timer >= fuseTime) Explode();
    }

    void OnCollisionEnter(Collision collision)
    {
        if (explodeOnImpact) { Explode(); return; }

        if (bounceSound != null && collision.relativeVelocity.magnitude > 2f)
            AudioSource.PlayClipAtPoint(bounceSound, transform.position, 0.6f);
    }

    void Explode()
    {
        if (exploded) return;
        exploded = true;

        Vector3 pos = transform.position;

        // Ocultar la granada y quitar sus colliders para que no bloqueen los raycasts
        foreach (var r in GetComponentsInChildren<Renderer>()) r.enabled = false;
        foreach (var c in GetComponentsInChildren<Collider>()) c.enabled = false;
        rb.isKinematic = true;

        var hits = Physics.OverlapSphere(pos, radius, affectedLayers, QueryTriggerInteraction.Ignore);
        var enemies = new Dictionary<EnemyHealth, float>();   // enemigo -> distancia mínima
        var pushed = new HashSet<Rigidbody>();

        foreach (var col in hits)
        {
            EnemyHealth health = FindHealth(col);
            if (!HasLineOfSight(pos, col, health, blockingLayers)) continue;

            float dist = Vector3.Distance(pos, ClosestPointSafe(col, pos));

            // Nos quedamos con el collider más cercano de cada enemigo
            if (health != null)
            {
                if (!enemies.TryGetValue(health, out float best) || dist < best)
                    enemies[health] = dist;
            }

            // Física: empuja objetos con Rigidbody
            var body = col.attachedRigidbody;
            if (body != null && !body.isKinematic && pushed.Add(body))
                body.AddExplosionForce(explosionForce, pos, radius, upwardsModifier, ForceMode.Impulse);
        }

        if (debugLog && enemies.Count == 0)
            Debug.Log($"[Granada] Ningún enemigo afectado ({hits.Length} colliders dentro del radio)");

        // Aplicar daño
        foreach (var pair in enemies)
        {
            if (pair.Key == null) continue;

            float damage = CalculateDamage(pair.Value);
            if (debugLog)
                Debug.Log($"[Granada] {pair.Key.name}: distancia {pair.Value:F1} -> daño {damage:F0}");

            pair.Key.TakeDamage(damage);
        }

        // Efectos visuales
        if (customExplosionPrefab != null)
            Destroy(Instantiate(customExplosionPrefab, pos, Quaternion.identity), 6f);
        else
            ExplosionFX.Spawn(pos, radius);

        // Sonido
        if (explosionSound != null)
            AudioSource.PlayClipAtPoint(explosionSound, pos, 1f);

        // Temblor de cámara según la distancia
        if (CameraShake.Instance != null)
        {
            float camDist = Vector3.Distance(CameraShake.Instance.transform.position, pos);
            float k = 1f - Mathf.Clamp01(camDist / shakeRange);
            if (k > 0f) CameraShake.Instance.AddTrauma(shakeIntensity * k);
        }

        Destroy(gameObject);
    }

    float CalculateDamage(float dist)
    {
        if (dist <= killRadius) return killDamage;
        float t = Mathf.InverseLerp(killRadius, radius, dist);
        return Mathf.Lerp(maxDamage, minDamage, t);
    }

    // ---------- Lo usa el sistema de apuntado (GrenadeThrower) ----------
    /// <summary>
    /// Simula qué afectaría una explosión en 'pos' sin hacer daño: devuelve los enemigos
    /// alcanzados y cuántas estructuras hay dentro del radio.
    /// </summary>
    public static void Scan(Vector3 pos, float radius, LayerMask affected, LayerMask blocking,
                            LayerMask structures, Collider landing, Transform ignoreRoot,
                            List<EnemyHealth> enemiesOut, out int structureCount)
    {
        enemiesOut.Clear();
        structureCount = 0;

        var hits = Physics.OverlapSphere(pos, radius, affected, QueryTriggerInteraction.Ignore);
        foreach (var col in hits)
        {
            if (ignoreRoot != null && col.transform.IsChildOf(ignoreRoot)) continue;

            EnemyHealth h = FindHealth(col);
            if (!HasLineOfSight(pos, col, h, blocking)) continue;

            if (h != null)
            {
                if (!enemiesOut.Contains(h)) enemiesOut.Add(h);
                continue;
            }

            bool isStructure;
            if (structures.value != 0)
            {
                // El usuario ha indicado qué capas son estructuras
                isStructure = (structures.value & (1 << col.gameObject.layer)) != 0;
            }
            else
            {
                // Sin capas definidas: es estructura todo lo que sobresale del suelo
                isStructure = col != landing &&
                              !(col is TerrainCollider) &&
                              col.bounds.max.y > pos.y + 0.5f;
            }

            if (isStructure) structureCount++;
        }
    }

    static EnemyHealth FindHealth(Collider col)
    {
        var h = col.GetComponentInParent<EnemyHealth>();
        if (h == null && col.attachedRigidbody != null)
            h = col.attachedRigidbody.GetComponentInChildren<EnemyHealth>();
        if (h == null)
            h = col.GetComponentInChildren<EnemyHealth>();
        return h;
    }

    // ClosestPoint falla con MeshCollider no convexos, CharacterController, etc.
    static Vector3 ClosestPointSafe(Collider col, Vector3 point)
    {
        if (col is BoxCollider || col is SphereCollider || col is CapsuleCollider)
            return col.ClosestPoint(point);
        if (col is MeshCollider mc && mc.convex)
            return col.ClosestPoint(point);
        return col.bounds.ClosestPoint(point);
    }

    // Comprueba varios puntos del objetivo (centro, cabeza, pies) para que
    // el suelo o el propio cuerpo del enemigo no bloqueen la explosión por error.
    static bool HasLineOfSight(Vector3 origin, Collider target, EnemyHealth owner, LayerMask blocking)
    {
        Vector3 from = origin + Vector3.up * 0.15f;
        Bounds b = target.bounds;

        Vector3[] points =
        {
            b.center,
            new Vector3(b.center.x, b.max.y - 0.1f, b.center.z),
            new Vector3(b.center.x, b.min.y + 0.1f, b.center.z)
        };

        foreach (var p in points)
            if (IsClear(from, p, target, owner, blocking)) return true;

        return false;
    }

    static bool IsClear(Vector3 from, Vector3 to, Collider target, EnemyHealth owner, LayerMask blocking)
    {
        Vector3 dir = to - from;
        float dist = dir.magnitude;
        if (dist < 0.01f) return true;

        var hits = Physics.RaycastAll(from, dir / dist, dist, blocking, QueryTriggerInteraction.Ignore);
        foreach (var h in hits)
        {
            if (IsSameTarget(h.collider, target, owner)) continue;
            return false;
        }
        return true;
    }

    static bool IsSameTarget(Collider c, Collider target, EnemyHealth owner)
    {
        if (c == target) return true;
        if (owner != null && c.transform.IsChildOf(owner.transform)) return true;
        if (target.attachedRigidbody != null && c.attachedRigidbody == target.attachedRigidbody) return true;
        return false;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.4f, 0f, 0.35f);
        Gizmos.DrawWireSphere(transform.position, radius);
        Gizmos.color = new Color(1f, 0f, 0f, 0.5f);
        Gizmos.DrawWireSphere(transform.position, killRadius);
    }
}
