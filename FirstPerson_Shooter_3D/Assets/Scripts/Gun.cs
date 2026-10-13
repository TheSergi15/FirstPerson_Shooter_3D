using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;

public class Gun : MonoBehaviour
{
    public enum FireMode { Projectile, Hitscan }

    [Header("Referencias")]
    [SerializeField] private Projectile projectilePrefab;   // Solo se usa en modo Projectile
    [SerializeField] private Transform firePoint;           // Punta del cañón
    [SerializeField] private Camera playerCamera;           // Si está vacía, usa Camera.main
    [SerializeField] private LayerMask aimMask = ~0;        // Quita aquí la capa del jugador

    [Header("Disparo")]
    [SerializeField] private FireMode fireMode = FireMode.Projectile;
    [SerializeField] private float projectileSpeed = 30f;
    [SerializeField] private float damage = 25f;
    [SerializeField] private float fireRate = 1.5f;         // Disparos por segundo
    [SerializeField] private bool automatic = false;        // Mantener pulsado para disparar
    [SerializeField, Range(0f, 0.1f)] private float spread = 0f;
    [SerializeField] private float aimDistance = 100f;      // Alcance del raycast / de la mira

    [Header("Munición")]
    [SerializeField] private int magazineSize = 6;
    [SerializeField] private int reserveAmmo = 24;
    [SerializeField] private float reloadTime = 2f;

    [Header("Efectos")]
    [SerializeField] private ParticleSystem muzzleFlash;
    [SerializeField] private GameObject impactEffectPrefab; // Solo en modo Hitscan
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip shootClip;
    [SerializeField] private AudioClip reloadClip;

    [Header("Interfaz de Usuario (UI)")]
    [Tooltip("Arrastra aquí el texto de TextMeshPro de tu Canvas")]
    [SerializeField] private TextMeshProUGUI ammoText;

    private int ammoInMag;
    private float nextFireTime;
    private bool triggerHeld;
    private bool isReloading;

    public int AmmoInMag => ammoInMag;
    public int ReserveAmmo => reserveAmmo;

    private void Awake()
    {
        ammoInMag = magazineSize;
        if (playerCamera == null) playerCamera = Camera.main;
    }

    private void OnEnable()
    {
        UpdateUI();
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
        UpdateUI();

        if (fireMode == FireMode.Projectile) ShootProjectile();
        else ShootHitscan();

        if (muzzleFlash) muzzleFlash.Play();
        if (audioSource && shootClip) audioSource.PlayOneShot(shootClip);
    }

    // ---------- MODO PROYECTIL ----------
    private void ShootProjectile()
    {
        if (projectilePrefab == null || firePoint == null) return;

        Ray ray = GetAimRay();

        // El proyectil sale del cañón pero apunta a donde mira la cámara
        Vector3 target = Physics.Raycast(ray, out RaycastHit hit, aimDistance,
                                         aimMask, QueryTriggerInteraction.Ignore)
            ? hit.point
            : ray.GetPoint(aimDistance);

        Vector3 dir = (target - firePoint.position).normalized;

        Projectile p = Instantiate(projectilePrefab, firePoint.position,
                                   Quaternion.LookRotation(dir));
        p.Launch(projectileSpeed, damage);
    }

    // ---------- MODO HITSCAN ----------
    private void ShootHitscan()
    {
        Ray ray = GetAimRay();

        // Sin QueryTriggerInteraction a propósito: así sigue detectando pickups con collider trigger
        if (!Physics.Raycast(ray, out RaycastHit hit, aimDistance, aimMask)) return;

        if (impactEffectPrefab != null)
        {
            GameObject fx = Instantiate(impactEffectPrefab, hit.point,
                                        Quaternion.LookRotation(hit.normal));
            Destroy(fx, 2f);
        }

        // Daño a enemigos
        if (hit.transform.TryGetComponent(out EnemyHealth enemy))
            enemy.TakeDamage(damage);

        // Curación
        if (hit.transform.TryGetComponent(out HealthPickup pickup))
            pickup.OnShot();
    }

    // Rayo desde el centro de la pantalla (la mira), con dispersión opcional
    private Ray GetAimRay()
    {
        Ray ray = playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));

        if (spread > 0f)
        {
            Vector3 dir = ray.direction
                        + playerCamera.transform.right * Random.Range(-spread, spread)
                        + playerCamera.transform.up * Random.Range(-spread, spread);
            ray.direction = dir.normalized;
        }
        return ray;
    }

    // ---------- RECARGA ----------
    public void Reload()
    {
        if (isReloading || ammoInMag == magazineSize || reserveAmmo <= 0) return;
        StartCoroutine(ReloadRoutine());
    }

    private IEnumerator ReloadRoutine()
    {
        isReloading = true;
        UpdateUI(); // Muestra "RECARGANDO..."
        if (audioSource && reloadClip) audioSource.PlayOneShot(reloadClip);

        yield return new WaitForSeconds(reloadTime);

        int taken = Mathf.Min(magazineSize - ammoInMag, reserveAmmo);
        ammoInMag += taken;
        reserveAmmo -= taken;
        isReloading = false;
        UpdateUI();
    }

    // ---------- UI ----------
    private void UpdateUI()
    {
        if (ammoText == null) return;
        ammoText.text = isReloading
            ? "RECARGANDO..."
            : $"BALAS: {ammoInMag} / {reserveAmmo}";
    }

    // Si se cambia de arma a mitad de recarga, se cancela
    private void OnDisable()
    {
        isReloading = false;
        triggerHeld = false;
    }
}