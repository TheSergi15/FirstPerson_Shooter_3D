using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class Projectile : MonoBehaviour
{
    [SerializeField] private ParticleSystem hitEffect;   // Explosión, humo...
    [SerializeField] private float lifeTime = 5f;
    [SerializeField] private float explosionRadius = 0f; // 0 = daño directo

    private float damage;
    private bool hasHit;

    public void Launch(float speed, float dmg)
    {
        damage = dmg;
        GetComponent<Rigidbody>().linearVelocity = transform.forward * speed; // Unity 6
        Destroy(gameObject, lifeTime);
    }

    private void OnCollisionEnter(Collision collision)
    {
        // Destroy se ejecuta al final del frame, así que evitamos procesar dos impactos
        if (hasHit) return;
        hasHit = true;

        // Evita aplicar el efecto dos veces al mismo objetivo si tiene varios colliders
        var alreadyHit = new HashSet<Component>();

        if (explosionRadius > 0f)
        {
            foreach (Collider col in Physics.OverlapSphere(transform.position, explosionRadius))
                ApplyHit(col, alreadyHit);
        }
        else
        {
            ApplyHit(collision.collider, alreadyHit);
        }

        if (hitEffect)
        {
            ParticleSystem fx = Instantiate(hitEffect, transform.position, Quaternion.identity);
            Destroy(fx.gameObject, 3f);
        }
        Destroy(gameObject);
    }

    private void ApplyHit(Collider col, HashSet<Component> alreadyHit)
    {
        // Daño a enemigos
        if (col.TryGetComponent(out EnemyHealth enemy) && alreadyHit.Add(enemy))
            enemy.TakeDamage(damage);

        // Curación
        if (col.TryGetComponent(out HealthPickup pickup) && alreadyHit.Add(pickup))
            pickup.OnShot();
    }
}