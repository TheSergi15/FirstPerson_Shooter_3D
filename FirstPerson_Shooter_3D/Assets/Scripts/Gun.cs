using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;

public class Gun : MonoBehaviour
{
    public enum BarrelAxis { Forward, Back, Right, Left, Up, Down }

    [Header("Referencias")]
    [SerializeField] private Camera playerCamera;           // Si está vacía, la rellena PickUpController o usa Camera.main
    [SerializeField] private LayerMask aimMask = ~0;        // Quita aquí la capa del jugador si tiene collider propio

    [Header("Controles (se leen directamente, ya no hace falta PlayerInput)")]
    [SerializeField] private bool reloadWithRightClick = true;
    [SerializeField] private Key reloadKey = Key.R;

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
    [SerializeField] private float maxAimAngle = 45f;       // Giro máximo respecto a la dirección frontal del Gun Container

    [Tooltip("Hacia qué eje LOCAL del arma sale el cañón. Selecciona el arma en la escena y mira la línea roja: debe salir por la boca del cañón.")]
    [SerializeField] private BarrelAxis barrelPointsTo = BarrelAxis.Forward;
    [Tooltip("Giro sobre el propio cañón. Pon 180 si el arma se ve boca abajo.")]
    [SerializeField] private float rollDegrees = 0f;

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
    [SerializeField] private TextMeshProUGUI ammoText;

    [Header("Depuración")]
    [SerializeField] private bool debugLog = true;          // Desactívalo cuando todo funcione

    private int ammoInMag;
    private float nextFireTime;
    private bool isReloading;
    private bool warnedNoCamera;
    private Transform owner;                                // El jugador (lo asigna PickUpController)

    private readonly RaycastHit[] hits = new RaycastHit[16];

    public int AmmoInMag => ammoInMag;
    public int ReserveAmmo => reserveAmmo;

    private Camera Cam
    {
        get
        {
            if (playerCamera == null) playerCamera = Camera.main;
            return playerCamera;
        }
    }

    private void Awake()
    {
        ammoInMag = magazineSize;
    }

    private void OnEnable()
    {
        UpdateUI();
    }

    // Lo llama PickUpController en Start
    public void SetOwner(Transform ownerTransform, Camera cam)
    {
        owner = ownerTransform;
        if (playerCamera == null && cam != null) playerCamera = cam;
    }

    private void Update()
    {
        Mouse mouse = Mouse.current;
        if (mouse != null)
        {
            bool fire = automatic ? mouse.leftButton.isPressed : mouse.leftButton.wasPressedThisFrame;
            if (fire) TryShoot();

            if (reloadWithRightClick && mouse.rightButton.wasPressedThisFrame) Reload();
        }

        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && reloadKey != Key.None && keyboard[reloadKey].wasPressedThisFrame)
            Reload();
    }

    // Va en LateUpdate para que ninguna animación pise el giro del arma
    private void LateUpdate()
    {
        if (aimWeaponAtCursor) AimWeaponAtCursor();
    }

    // ---------- DISPARO ----------
    private void TryShoot()
    {
        if (isReloading || Time.time < nextFireTime) return;

        if (Cam == null)
        {
            if (!warnedNoCamera)
            {
                warnedNoCamera = true;
                Debug.LogError($"[Gun] {name}: no hay cámara. Asigna 'Player Camera' o etiqueta tu cámara como MainCamera.", this);
            }
            return;
        }

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

        Ray ray = GetShotRay();

        if (RaycastIgnoringSelf(ray, out RaycastHit hit))
        {
           
            if (debugLog) Debug.Log($"[Gun] Disparo -> impacto en '{hit.collider.name}' a {hit.distance:F1} m", this);

            if (impactEffectPrefab != null)
            {
                GameObject fx = Instantiate(impactEffectPrefab, hit.point,
                                            Quaternion.LookRotation(hit.normal));
                Destroy(fx, 2f);
            }

            // Daño a enemigos (también si el collider está en un hijo del enemigo)
            EnemyHealth enemy = hit.collider.GetComponentInParent<EnemyHealth>();
            if (enemy != null) enemy.TakeDamage(damage);

            // Curación
            HealthPickup pickup = hit.collider.GetComponentInParent<HealthPickup>();
            if (pickup != null) pickup.OnShot();
        }
        else
        {
           
           
        }

        if (muzzleFlash) muzzleFlash.Play();
        if (audioSource && shootClip) audioSource.PlayOneShot(shootClip);
    }

    private Vector2 GetScreenPoint()
    {
        if (aimWithCursor && Mouse.current != null)
            return Mouse.current.position.ReadValue();

        return new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
    }

    // Rayo desde el puntero (o el centro de la pantalla), sin dispersión
    private Ray GetCursorRay()
    {
        return Cam.ScreenPointToRay(GetScreenPoint());
    }

    // Rayo del disparo: el del puntero más la dispersión opcional
    private Ray GetShotRay()
    {
        Ray ray = GetCursorRay();

        if (spread > 0f)
        {
            Vector3 dir = ray.direction
                        + Cam.transform.right * Random.Range(-spread, spread)
                        + Cam.transform.up * Random.Range(-spread, spread);
            ray.direction = dir.normalized;
        }
        return ray;
    }

    // Un collider se ignora si es el propio arma, un trigger "de escenario" o el propio jugador.
    // Los enemigos y los pickups de vida SIEMPRE son impactables, aunque usen trigger.
    private bool IsIgnored(Collider c)
    {
        if (c.transform.IsChildOf(transform)) return true;

        bool shootable = c.GetComponentInParent<EnemyHealth>() != null
                      || c.GetComponentInParent<HealthPickup>() != null;
        if (shootable) return false;

        if (c.isTrigger) return true;
        if (owner != null && c.transform.IsChildOf(owner)) return true;

        return false;
    }

    // Impacto más cercano que no sea ignorado
    private bool RaycastIgnoringSelf(Ray ray, out RaycastHit result)
    {
        int count = Physics.RaycastNonAlloc(ray, hits, aimDistance, aimMask, QueryTriggerInteraction.Collide);
        float closest = float.MaxValue;
        bool found = false;
        result = default;

        for (int i = 0; i < count; i++)
        {
            if (IsIgnored(hits[i].collider)) continue;

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
    // El arma gira para que su CAÑÓN apunte al mismo punto al que llega el disparo
    private void AimWeaponAtCursor()
    {
        Transform parent = transform.parent;
        if (Cam == null || parent == null) return;

        Ray ray = GetCursorRay();
        Vector3 target = RaycastIgnoringSelf(ray, out RaycastHit hit)
            ? hit.point
            : ray.GetPoint(aimDistance);

        Vector3 dir = target - transform.position;
        if (dir.sqrMagnitude < 0.0001f) return;
        dir.Normalize();

        // Límite de giro respecto a la dirección frontal del contenedor
        Vector3 localDir = parent.InverseTransformDirection(dir);
        localDir = Vector3.RotateTowards(Vector3.forward, localDir, maxAimAngle * Mathf.Deg2Rad, 0f);
        Vector3 worldDir = parent.TransformDirection(localDir);

        Quaternion desired = Quaternion.LookRotation(worldDir, parent.up)
                           * Quaternion.Euler(0f, 0f, rollDegrees)
                           * BarrelCorrection();

        float t = 1f - Mathf.Exp(-aimSpeed * Time.deltaTime);   // suavizado independiente del framerate
        transform.rotation = Quaternion.Slerp(transform.rotation, desired, t);
    }

    // Compensa el eje del modelo: lleva el eje del cañón al +Z que usa LookRotation
    private Quaternion BarrelCorrection()
    {
        Quaternion zToBarrel;
        switch (barrelPointsTo)
        {
            case BarrelAxis.Back:  zToBarrel = Quaternion.Euler(0f, 180f, 0f); break;
            case BarrelAxis.Right: zToBarrel = Quaternion.Euler(0f, 90f, 0f);  break;
            case BarrelAxis.Left:  zToBarrel = Quaternion.Euler(0f, -90f, 0f); break;
            case BarrelAxis.Up:    zToBarrel = Quaternion.Euler(-90f, 0f, 0f); break;
            case BarrelAxis.Down:  zToBarrel = Quaternion.Euler(90f, 0f, 0f);  break;
            default:               zToBarrel = Quaternion.identity;            break;
        }
        return Quaternion.Inverse(zToBarrel);
    }

   


    // ---------- RECARGA ----------
    public void Reload()
    {
        if (isReloading || ammoInMag == magazineSize || reserveAmmo <= 0) return;

        // Evita el error "Coroutine couldn't be started because the game object is inactive"
        if (!isActiveAndEnabled) return;

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

    // Si se suelta el arma a mitad de recarga, se cancela de verdad
    // (desactivar solo el componente NO detiene las corrutinas, por eso StopAllCoroutines)
    private void OnDisable()
    {
        StopAllCoroutines();
        isReloading = false;
    }
}