using UnityEngine;

public class Projectile : MonoBehaviour
{
    [SerializeField] private ParticleSystem hitEffect;   // Explosión, humo...
    [SerializeField] private float lifeTime = 5f;
    [SerializeField] private float explosionRadius = 0f; // 0 = daño directo

    private float damage;

    public void Launch(float speed, float dmg)
    {
        damage = dmg;
        GetComponent<Rigidbody>().linearVelocity = transform.forward * speed; // Unity 6
        Destroy(gameObject, lifeTime);
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (explosionRadius > 0f)
        {
            foreach (Collider col in Physics.OverlapSphere(transform.position, explosionRadius))
                if (col.TryGetComponent(out Enemy e)) e.TakeDamage(damage);
        }
        else if (collision.collider.TryGetComponent(out Enemy enemy))
        {
            enemy.TakeDamage(damage);
        }

        if (hitEffect)
        {
            ParticleSystem fx = Instantiate(hitEffect, transform.position, Quaternion.identity);
            Destroy(fx.gameObject, 3f);
        }
        Destroy(gameObject);
    }
}
