using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;

public class Gun : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField] private Camera playerCamera;           // Si está vacía, usa Camera.main
    [SerializeField] private LayerMask aimMask = ~0;        // Quita aquí la capa del jugador

    [Header("Disparo (hitscan)")]
    [SerializeField] private float damage = 25f;
    [SerializeField] private float fireRate = 1.5f;         // Disparos por segundo
    [SerializeField] private bool automatic = false;        // Mantener pulsado para disparar
    [SerializeField, Range(0f, 0.1f)] private float spread = 0f;
    [SerializeField] private float aimDistance = 100f;      // Alcance del disparo
    [SerializeField] private bool aimWithCursor = true;     // Apunta con el puntero; si no, con el centro de la pantalla

    [Header("Apuntado del arma")]
    [SerializeField] private bool aimWeaponAtCursor = true;
    [SerializeField] private float aimSpeed = 15f;          // Más alto = sigue al puntero más rápido
    [SerializeField] private float maxAimAngle = 45f;       // Giro máximo respecto a su posición base

    [Header("Munición")]
    [SerializeField] private int magazineSize = 6;
    [SerializeField] private int reserveAmmo = 24;
    [SerializeField] private float reloadTime = 2f;

    [Header("Efectos")]
    [SerializeField] private ParticleSystem muzzleFlash;
    [SerializeField] private GameObject impactEffectPrefab;
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

    private readonly RaycastHit[] hits = new RaycastHit[16];

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

    // Va en LateUpdate para que ninguna animación pise el giro del arma
    private void LateUpdate()
    {
        if (aimWeaponAtCursor) AimWeaponAtCursor();
    }

    // ---------- INPUT (eventos del PlayerInput, Invoke Unity Events) ----------
    public void OnFire(InputAction.CallbackContext ctx)
    {
        if (!enabled) return;   // El arma no equipada no dispara

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
        if (!enabled) return;
        if (ctx.performed) Reload();
    }

    // ---------- DISPARO ----------
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

        Ray ray = GetAimRay();

        if (RaycastIgnoringSelf(ray, out RaycastHit hit))
        {
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

        if (muzzleFlash) muzzleFlash.Play();
        if (audioSource && shootClip) audioSource.PlayOneShot(shootClip);
    }

    // Rayo desde el puntero (o el centro de la pantalla), sin dispersión
    private Ray GetCursorRay()
    {
        Vector2 screenPoint = (aimWithCursor && Mouse.current != null)
            ? Mouse.current.position.ReadValue()
            : new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);

        return playerCamera.ScreenPointToRay(screenPoint);
    }

    // Rayo del disparo: el del puntero más la dispersión opcional
    private Ray GetAimRay()
    {
        Ray ray = GetCursorRay();

        if (spread > 0f)
        {
            Vector3 dir = ray.direction
                        + playerCamera.transform.right * Random.Range(-spread, spread)
                        + playerCamera.transform.up * Random.Range(-spread, spread);
            ray.direction = dir.normalized;
        }
        return ray;
    }

    // Impacto más cercano que no sea el propio arma
    // (el collider del arma equipada puede quedar delante del puntero y "comerse" el disparo)
    private bool RaycastIgnoringSelf(Ray ray, out RaycastHit result)
    {
        int count = Physics.RaycastNonAlloc(ray, hits, aimDistance, aimMask);
        float closest = float.MaxValue;
        bool found = false;
        result = default;

        for (int i = 0; i < count; i++)
        {
            if (hits[i].transform.IsChildOf(transform)) continue;

            if (hits[i].distance < closest)
            {
                closest = hits[i].distance;
                result = hits[i];
                found = true;
            }
        }
        return found;
    }

    // ---------- APUNTADO DEL ARMA ----------
    // El arma gira hacia el mismo punto al que llega el disparo
    private void AimWeaponAtCursor()
    {
        if (playerCamera == null || transform.parent == null) return;

        Ray ray = GetCursorRay();
        Vector3 target = RaycastIgnoringSelf(ray, out RaycastHit hit)
            ? hit.point
            : ray.GetPoint(aimDistance);

        // Dirección desde el arma hasta ese punto, en el espacio del contenedor
        Vector3 toTarget = target - transform.position;
        if (toTarget.sqrMagnitude < 0.0001f) return;

        Vector3 localDir = transform.parent.InverseTransformDirection(toTarget.normalized);
        Quaternion targetRot = Quaternion.LookRotation(localDir);

        // Limita cuánto puede girar respecto a su posición base
        targetRot = Quaternion.RotateTowards(Quaternion.identity, targetRot, maxAimAngle);

        transform.localRotation = Quaternion.Slerp(
            transform.localRotation, targetRot, aimSpeed * Time.deltaTime);
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

    // Si se suelta el arma a mitad de recarga, se cancela
    private void OnDisable()
    {
        isReloading = false;
        triggerHeld = false;
    }
}