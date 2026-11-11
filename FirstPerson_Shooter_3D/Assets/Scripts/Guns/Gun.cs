using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class Gun : MonoBehaviour
{
    public enum BarrelAxis { Forward, Back, Right, Left, Up, Down }

    [Header("Datos del arma (opcional)")]
    [Tooltip("Si se asigna, el tipo de disparo, la cadencia, el daño, el alcance, la munición y la recarga salen de este asset " +
             "y se ignoran los valores sueltos de abajo. Si se deja vacío, el arma funciona como antes.")]
    [SerializeField] private WeaponData data;

    [Header("Tutorial")]
    [Tooltip("Nombre de la escena del tutorial. SOLO en esa escena se aplican el bloqueo de disparo y los avisos del tutorial.")]
    [SerializeField] private string tutorialSceneName = "Tutorial";

    [Header("Referencias")]
    [SerializeField] private Camera playerCamera;           // Si está vacía, la rellena PickUpController o usa Camera.main
    [SerializeField] private LayerMask aimMask = ~0;        // Quita aquí la capa del jugador si tiene collider propio

    [Header("Controles (se leen directamente, ya no hace falta PlayerInput)")]
    [SerializeField] private bool reloadWithRightClick = true;
    [SerializeField] private Key reloadKey = Key.R;

    [Header("Disparo (hitscan) - valores sueltos, solo se usan si NO hay WeaponData")]
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

    [Header("Munición - valores sueltos, solo se usan si NO hay WeaponData")]
    [SerializeField] private int magazineSize = 6;
    [SerializeField] private int reserveAmmo = 24;
    [SerializeField] private float reloadTime = 2f;

    [Header("Efectos")]
    [SerializeField] private ParticleSystem muzzleFlash;
    [SerializeField] private GameObject impactEffectPrefab;
    [Tooltip("Si 'Shoot Clip' / 'Reload Clip' están asignados se usan ellos; si no, suenan los ids del WeaponData a través del AudioManager.")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip shootClip;
    [SerializeField] private AudioClip reloadClip;

    [Header("Depuración")]
    [SerializeField] private bool debugLog = true;          // Desactívalo cuando todo funcione

    private int ammoInMag;
    private float nextFireTime;
    private bool isReloading;
    private float reloadStartTime;
    private bool warnedNoCamera;
    private bool inTutorial;
    private Transform owner;                                // El jugador (lo asigna PickUpController)
    private WeaponFireControl fire;                         // Solo existe si hay WeaponData

    private readonly RaycastHit[] hits = new RaycastHit[16];

    // Valores efectivos: del WeaponData si existe, si no los sueltos
    private float Damage => data != null ? data.damage : damage;
    private float Range => data != null ? data.range : aimDistance;
    private float ReloadTime => data != null ? data.reloadTime : reloadTime;

    // Datos que lee el HUD de munición y el inventario
    public int AmmoInMag => ammoInMag;
    public int ReserveAmmo => reserveAmmo;
    public int MagazineSize => data != null ? data.magazineSize : magazineSize;
    public bool IsReloading => isReloading;
    public float ReloadProgress => isReloading
        ? Mathf.Clamp01((Time.time - reloadStartTime) / Mathf.Max(0.01f, ReloadTime))
        : 0f;

    public WeaponData Data => data;
    public Sprite Icon => data != null ? data.icon : null;
    public string DisplayName => data != null ? data.displayName : name;

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
        inTutorial = !string.IsNullOrEmpty(tutorialSceneName)
                  && SceneManager.GetActiveScene().name == tutorialSceneName;

        ammoInMag = MagazineSize;

        if (data != null)
        {
            reserveAmmo = data.startReserveAmmo;

            fire = GetComponent<WeaponFireControl>();
            if (fire == null) fire = gameObject.AddComponent<WeaponFireControl>();

            fire.data = data;
            fire.ShootCallback = ShootOnce;
            fire.enabled = enabled;
        }
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
            if (fire != null)
            {
                // Con WeaponData: el WeaponFireControl decide cuándo sale cada disparo
                if (mouse.leftButton.wasPressedThisFrame) fire.PressTrigger();
                if (mouse.leftButton.wasReleasedThisFrame) fire.ReleaseTrigger();
            }
            else
            {
                // Sin WeaponData: comportamiento de siempre
                bool shoot = automatic ? mouse.leftButton.isPressed : mouse.leftButton.wasPressedThisFrame;
                if (shoot) TryShootLegacy();
            }

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
    private void TryShootLegacy()
    {
        if (Time.time < nextFireTime) return;
        if (ShootOnce()) nextFireTime = Time.time + 1f / Mathf.Max(0.05f, fireRate);
    }

    /// <summary>
    /// Un disparo completo (puede ser varios proyectiles). Devuelve false si no se pudo disparar.
    /// Lo llama WeaponFireControl (con WeaponData) o TryShootLegacy (sin WeaponData).
    /// </summary>
    private bool ShootOnce()
    {
        if (inTutorial && !TutorialGate.CanShoot) return false;   // bloqueado hasta que el tutorial lo permita

        if (isReloading) return false;

        if (Cam == null)
        {
            if (!warnedNoCamera)
            {
                warnedNoCamera = true;
                Debug.LogError($"[Gun] {name}: no hay cámara. Asigna 'Player Camera' o etiqueta tu cámara como MainCamera.", this);
            }
            return false;
        }

        if (ammoInMag <= 0)
        {
            if (reserveAmmo > 0) Reload();
            else if (data != null) PlaySound(null, data.emptySfx);   // clic de arma vacía
            return false;
        }

        Shoot();
        return true;
    }

    private void Shoot()
    {
        ammoInMag--;
        if (inTutorial) TutorialEvents.NotifyWeaponFired();

        int pellets = data != null ? Mathf.Max(1, data.pellets) : 1;
        HealthPickup lastPickup = null;   // evita curar varias veces con un mismo disparo de escopeta

        for (int i = 0; i < pellets; i++)
        {
            Ray ray = GetShotRay();

            if (!RaycastIgnoringSelf(ray, out RaycastHit hit)) continue;

            if (debugLog && i == 0)
                Debug.Log($"[Gun] Disparo -> impacto en '{hit.collider.name}' a {hit.distance:F1} m", this);

            if (impactEffectPrefab != null)
            {
                GameObject fx = Instantiate(impactEffectPrefab, hit.point,
                                            Quaternion.LookRotation(hit.normal));
                Destroy(fx, 2f);
            }

            // Daño a enemigos (también si el collider está en un hijo del enemigo)
            EnemyHealth enemy = hit.collider.GetComponentInParent<EnemyHealth>();
            if (enemy != null) enemy.TakeDamage(Damage);

            // Curación
            HealthPickup pickup = hit.collider.GetComponentInParent<HealthPickup>();
            if (pickup != null && pickup != lastPickup)
            {
                lastPickup = pickup;
                pickup.OnShot();
                if (inTutorial) TutorialEvents.NotifyHealPickupShot();
            }
        }

        if (muzzleFlash) muzzleFlash.Play();
        PlaySound(shootClip, data != null ? data.shootSfx : null);
    }

    // Prioridad: clip suelto asignado en el arma; si no, el id del WeaponData en el AudioManager
    private void PlaySound(AudioClip clip, string sfxId)
    {
        if (audioSource != null && clip != null)
            audioSource.PlayOneShot(clip);
        else if (data != null && !string.IsNullOrEmpty(sfxId))
            AudioManager.Sfx(sfxId);
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

    // Rayo de UN proyectil: el del puntero más la dispersión
    private Ray GetShotRay()
    {
        Ray ray = GetCursorRay();

        if (data != null)
        {
            // Dispersión en cono (grados) definida en el WeaponData
            if (fire != null) ray.direction = fire.GetShotDirection(ray.direction);
        }
        else if (spread > 0f)
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
        int count = Physics.RaycastNonAlloc(ray, hits, Range, aimMask, QueryTriggerInteraction.Collide);
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
            : ray.GetPoint(Range);

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
            case BarrelAxis.Back: zToBarrel = Quaternion.Euler(0f, 180f, 0f); break;
            case BarrelAxis.Right: zToBarrel = Quaternion.Euler(0f, 90f, 0f); break;
            case BarrelAxis.Left: zToBarrel = Quaternion.Euler(0f, -90f, 0f); break;
            case BarrelAxis.Up: zToBarrel = Quaternion.Euler(-90f, 0f, 0f); break;
            case BarrelAxis.Down: zToBarrel = Quaternion.Euler(90f, 0f, 0f); break;
            default: zToBarrel = Quaternion.identity; break;
        }
        return Quaternion.Inverse(zToBarrel);
    }

    // ---------- RECARGA ----------
    public void Reload()
    {
        if (isReloading || ammoInMag == MagazineSize || reserveAmmo <= 0) return;

        // Evita el error "Coroutine couldn't be started because the game object is inactive"
        if (!isActiveAndEnabled) return;

        StartCoroutine(ReloadRoutine());
    }

    private IEnumerator ReloadRoutine()
    {
        isReloading = true;
        reloadStartTime = Time.time;
        PlaySound(reloadClip, data != null ? data.reloadSfx : null);

        yield return new WaitForSeconds(ReloadTime);

        int taken = Mathf.Min(MagazineSize - ammoInMag, reserveAmmo);
        ammoInMag += taken;
        reserveAmmo -= taken;
        isReloading = false;
    }

    private void OnEnable()
    {
        if (fire != null) fire.enabled = true;
    }

    // Si se suelta el arma a mitad de recarga, se cancela de verdad
    // (desactivar solo el componente NO detiene las corrutinas, por eso StopAllCoroutines).
    // El control de disparo también se apaga para que no salga ningún disparo suelto.
    private void OnDisable()
    {
        StopAllCoroutines();
        isReloading = false;

        if (fire != null) fire.enabled = false;
    }
}