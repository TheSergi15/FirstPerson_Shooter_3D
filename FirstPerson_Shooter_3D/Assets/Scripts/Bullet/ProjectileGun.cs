using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

public class ProjectileGun : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField] private Projectile projectilePrefab;
    [SerializeField] private Transform firePoint;        // Punta del cañón
    [SerializeField] private Camera playerCamera;
    [SerializeField] private LayerMask aimMask = ~0;     // Quita aquí la capa del jugador

    [Header("Disparo")]
    [SerializeField] private float projectileSpeed = 30f;
    [SerializeField] private float damage = 25f;
    [SerializeField] private float fireRate = 1.5f;      // Disparos por segundo
    [SerializeField] private bool automatic = false;     // Mantener pulsado para disparar
    [SerializeField, Range(0f, 0.1f)] private float spread = 0f;
    [SerializeField] private float aimDistance = 100f;

    [Header("Munición")]
    [SerializeField] private int magazineSize = 6;
    [SerializeField] private int reserveAmmo = 24;
    [SerializeField] private float reloadTime = 2f;

    [Header("Efectos")]
    [SerializeField] private ParticleSystem muzzleFlash;
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip shootClip;
    [SerializeField] private AudioClip reloadClip;

    private int ammoInMag;
    private float nextFireTime;
    private bool triggerHeld;
    private bool isReloading;

    public int AmmoInMag => ammoInMag;
    public int ReserveAmmo => reserveAmmo;

    private void Start()
    {
        ammoInMag = magazineSize;
    }

    private void Update()
    {
        if (triggerHeld && automatic) TryShoot();
    }

    // Eventos del PlayerInput (Invoke Unity Events)
    public void OnFire(InputAction.CallbackContext ctx)
    {
        if (ctx.started)
        {
            triggerHeld = true;
            if (!automatic) TryShoot();
        }
        else if (ctx.canceled)
        {
            triggerHeld = false;
        }
    }

    public void OnReload(InputAction.CallbackContext ctx)
    {
        if (ctx.performed) Reload();
    }

    private void TryShoot()
    {
        if (isReloading || Time.time < nextFireTime) return;

        if (ammoInMag <= 0)
        {
            Reload();
            return;
        }

        nextFireTime = Time.time + 1f / fireRate;
        Shoot();
    }

    private void Shoot()
    {
        ammoInMag--;

        Projectile p = Instantiate(projectilePrefab, firePoint.position,
                                   Quaternion.LookRotation(GetShootDirection()));
        p.Launch(projectileSpeed, damage);

        if (muzzleFlash) muzzleFlash.Play();
        if (audioSource && shootClip) audioSource.PlayOneShot(shootClip);
    }

    // El proyectil sale del cañón pero apunta al centro de la pantalla (mira)
    private Vector3 GetShootDirection()
    {
        Ray ray = playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));

        Vector3 target = Physics.Raycast(ray, out RaycastHit hit, aimDistance,
                                         aimMask, QueryTriggerInteraction.Ignore)
            ? hit.point
            : ray.GetPoint(aimDistance);

        Vector3 dir = (target - firePoint.position).normalized;

        if (spread > 0f)
        {
            dir += playerCamera.transform.right * Random.Range(-spread, spread)
                 + playerCamera.transform.up * Random.Range(-spread, spread);
            dir.Normalize();
        }
        return dir;
    }

    public void Reload()
    {
        if (isReloading || ammoInMag == magazineSize || reserveAmmo <= 0) return;
        StartCoroutine(ReloadRoutine());
    }

    private IEnumerator ReloadRoutine()
    {
        isReloading = true;
        if (audioSource && reloadClip) audioSource.PlayOneShot(reloadClip);

        yield return new WaitForSeconds(reloadTime);

        int taken = Mathf.Min(magazineSize - ammoInMag, reserveAmmo);
        ammoInMag += taken;
        reserveAmmo -= taken;
        isReloading = false;
    }

    // Si se cambia de arma a mitad de recarga, se cancela
    private void OnDisable()
    {
        isReloading = false;
        triggerHeld = false;
    }
}