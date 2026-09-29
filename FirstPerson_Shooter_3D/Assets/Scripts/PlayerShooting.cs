using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerShooting : MonoBehaviour
{
    [Header("Shooting")]
    public Camera playerCamera;
    public float damage = 25f;
    public float range = 100f;
    public float fireRate = 0.15f;          // seconds between shots
    public bool automatic = true;           // hold to keep firing
    public LayerMask hitLayers = ~0;        // all layers by default

    [Header("Effects (optional)")]
    public GameObject hitEffect;            // particle prefab, can be left empty

    private bool isFiring;
    private float nextFireTime;

    // Called from PlayerInput (Invoke Unity Events)
    public void OnFire(InputAction.CallbackContext context)
    {
        if (context.started)
        {
            isFiring = true;
            Shoot(); // first shot fires immediately
        }
        else if (context.canceled)
        {
            isFiring = false;
        }
    }

    private void Update()
    {
        if (isFiring && automatic)
            Shoot();
    }

    private void Shoot()
    {
        if (Time.time < nextFireTime) return;
        nextFireTime = Time.time + fireRate;

        // Ray from the center of the screen
        Ray ray = playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));

        if (Physics.Raycast(ray, out RaycastHit hit, range, hitLayers, QueryTriggerInteraction.Ignore))
        {
            Debug.DrawLine(ray.origin, hit.point, Color.red, 0.5f);

            Enemy enemy = hit.collider.GetComponentInParent<Enemy>();
            if (enemy != null)
                enemy.TakeDamage(damage);

            if (hitEffect != null)
            {
                GameObject effect = Instantiate(hitEffect, hit.point, Quaternion.LookRotation(hit.normal));
                Destroy(effect, 2f);
            }
        }
        else
        {
            Debug.DrawRay(ray.origin, ray.direction * range, Color.yellow, 0.5f);
        }
    }
}