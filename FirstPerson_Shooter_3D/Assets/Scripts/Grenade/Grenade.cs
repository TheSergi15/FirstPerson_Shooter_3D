using System.Collections.Generic;
using UnityEditor.Rendering.LookDev;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class Grenade : MonoBehaviour
{
    [Header("Mecha")]
    public float fuseTime = 3f;
    public bool explodeOnImpact = false;

    [Header("Explosión")]
    public float radius = 7f;
    public float maxDamage = 100f;
    [Tooltip("Daño según la distancia: eje X = 0 (centro) a 1 (borde del radio)")]
    public AnimationCurve damageFalloff = AnimationCurve.Linear(0f, 1f, 1f, 0f);
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

    Rigidbody rb;
    float timer;
    bool exploded;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
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

        // Ocultar la granada y quitar su collider para que no bloquee los raycasts
        foreach (var r in GetComponentsInChildren<Renderer>()) r.enabled = false;
        foreach (var c in GetComponentsInChildren<Collider>()) c.enabled = false;
        rb.isKinematic = true;

        var hits = Physics.OverlapSphere(pos, radius, affectedLayers, QueryTriggerInteraction.Ignore);
        var damaged = new HashSet<EnemyHealth>();
        var pushed = new HashSet<Rigidbody>();

        foreach (var col in hits)
        {
            if (!HasLineOfSight(pos, col)) continue;

            float dist = Vector3.Distance(pos, col.ClosestPoint(pos));
            float t = Mathf.Clamp01(dist / radius);

            // Daño a enemigos (una sola vez por enemigo aunque tenga varios colliders)
            var health = col.GetComponentInParent<EnemyHealth>();
            if (health != null && damaged.Add(health))
            {
                float damage = maxDamage * damageFalloff.Evaluate(t);
                health.TakeDamage(damage);
            }

            // Física: empuja objetos con Rigidbody
            var body = col.attachedRigidbody;
            if (body != null && !body.isKinematic && pushed.Add(body))
            {
                body.AddExplosionForce(explosionForce, pos, radius, upwardsModifier, ForceMode.Impulse);
            }
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

    bool HasLineOfSight(Vector3 origin, Collider target)
    {
        Vector3 from = origin + Vector3.up * 0.1f;
        Vector3 to = target.bounds.center;

        if (Physics.Linecast(from, to, out RaycastHit hit, blockingLayers, QueryTriggerInteraction.Ignore))
        {
            bool sameTarget = hit.collider == target ||
                              (hit.rigidbody != null && hit.rigidbody == target.attachedRigidbody);
            return sameTarget;
        }
        return true;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.4f, 0f, 0.35f);
        Gizmos.DrawWireSphere(transform.position, radius);
    }
}
