using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;

public class GunController : MonoBehaviour
{
    [Header("Configuración de Pistola Base")]
    public float pistolDamage = 10f;
    public int pistolMaxAmmo = 6;
    public float pistolReloadTime = 1f;

    [Header("Arma Activa")]
    public WeaponType currentWeaponType;
    public string currentWeaponName = "Pistola";
    public bool isSpecialWeapon = false;
    private float currentDamage;
    private int currentAmmo;
    private int maxAmmo;
    private float reloadTime;

    [Header("Propiedades Escopeta")]
    public int shotgunPellets = 5;       // Número de perdigones
    public float shotgunSpread = 0.05f;   // Dispersión del disparo

    [Header("Propiedades Machine Gun (Automática)")]
    [Tooltip("Disparos por segundo para la ametralladora")]
    public float machineGunFireRate = 10f;
    private float nextTimeToFire = 0f;

    [Header("Detección")]
    public float range = 100f;
    public LayerMask hitLayers; // Asigna capas de Enemigos, Cajas y Pickups

    [Header("Efectos y UI")]
    public GameObject impactEffectPrefab;
    public TextMeshProUGUI ammoText;

    private Camera mainCamera;
    private bool isReloading = false;

    void Start()
    {
        mainCamera = Camera.main;
        SetDefaultPistol();
    }

    void Update()
    {
        if (isReloading) return;

        // Recarga manual solo para la pistola base con clic derecho
        if (!isSpecialWeapon && Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame)
        {
            if (currentAmmo < maxAmmo)
            {
                StartCoroutine(Reload());
                return;
            }
        }

        // --- GESTIÓN DE DISPARO ---
        if (Mouse.current != null)
        {
            // MODO AUTOMÁTICO: Solo cuando tenemos equipada la Machine Gun
            if (isSpecialWeapon && currentWeaponType == WeaponType.MachineGun)
            {
                // isPressed detecta si mantienes el clic izquierdo presionado
                if (Mouse.current.leftButton.isPressed && Time.time >= nextTimeToFire)
                {
                    nextTimeToFire = Time.time + (1f / machineGunFireRate);

                    if (currentAmmo > 0)
                    {
                        Shoot();
                    }
                }
            }
            // MODO SEMIAUTOMÁTICO: Pistola estándar y Escopeta (un disparo por clic)
            else
            {
                if (Mouse.current.leftButton.wasPressedThisFrame)
                {
                    if (currentAmmo > 0)
                    {
                        Shoot();
                    }
                    else if (!isSpecialWeapon)
                    {
                        Debug.Log("¡Arma vacía! Recarga.");
                    }
                }
            }
        }
    }

    void Shoot()
    {
        currentAmmo--;
        UpdateUI();

        if (isSpecialWeapon && currentWeaponType == WeaponType.Shotgun)
        {
            // Disparo de escopeta con perdigones dispersos
            for (int i = 0; i < shotgunPellets; i++)
            {
                FireRaycast(true);
            }
        }
        else
        {
            // Disparo único y preciso (Pistola o Machine Gun)
            FireRaycast(false);
        }

        // Si se agota la munición de cualquier arma especial, vuelve a la pistola base
        if (isSpecialWeapon && currentAmmo <= 0)
        {
            Debug.Log("Munición especial agotada. Cambiando a Pistola.");
            SetDefaultPistol();
        }
    }

    void FireRaycast(bool useSpread)
    {
        Vector2 mousePos = Mouse.current.position.ReadValue();
        Ray ray = mainCamera.ScreenPointToRay(mousePos);

        if (useSpread)
        {
            Vector3 spread = mainCamera.transform.right * Random.Range(-shotgunSpread, shotgunSpread) +
                             mainCamera.transform.up * Random.Range(-shotgunSpread, shotgunSpread);
            ray.direction += spread;
        }

        RaycastHit hit;
        if (Physics.Raycast(ray, out hit, range, hitLayers))
        {
            // Efecto visual de impacto
            if (impactEffectPrefab != null)
            {
                GameObject impactGO = Instantiate(impactEffectPrefab, hit.point, Quaternion.LookRotation(hit.normal));
                Destroy(impactGO, 2f);
            }

            // 1. Golpe a Caja destructible
            DestructibleBox box = hit.transform.GetComponent<DestructibleBox>();
            if (box != null)
            {
                box.BreakBox();
                return;
            }

            // 2. Golpe a Ítem de Arma
            WeaponPickup pickup = hit.transform.GetComponent<WeaponPickup>();
            if (pickup != null)
            {
                pickup.OnShot(this);
                return;
            }

            // 3. Golpe a Botiquín
            HealthPickup heal = hit.transform.GetComponent<HealthPickup>();
            if (heal != null)
            {
                heal.OnShot();
                return;
            }

            // 4. Daño a Enemigo
            EnemyHealth enemy = hit.transform.GetComponent<EnemyHealth>();
            if (enemy != null)
            {
                enemy.TakeDamage(currentDamage);
            }
        }
    }

    public void EquipSpecialWeapon(WeaponType type, int ammo, float damage)
    {
        isSpecialWeapon = true;
        currentWeaponType = type;
        currentAmmo = ammo;
        maxAmmo = ammo;
        currentDamage = damage;

        if (type == WeaponType.Shotgun)
        {
            currentWeaponName = "Escopeta";
        }
        else if (type == WeaponType.MachineGun)
        {
            currentWeaponName = "Ametralladora";
        }

        UpdateUI();
        Debug.Log($"¡Arma equipada: {currentWeaponName}! Balas: {currentAmmo}");
    }

    void SetDefaultPistol()
    {
        isSpecialWeapon = false;
        currentWeaponName = "Pistola";
        currentDamage = pistolDamage;
        maxAmmo = pistolMaxAmmo;
        currentAmmo = maxAmmo;
        reloadTime = pistolReloadTime;
        UpdateUI();
    }

    IEnumerator Reload()
    {
        isReloading = true;
        if (ammoText != null) ammoText.text = "RECARGANDO...";

        yield return new WaitForSeconds(reloadTime);

        currentAmmo = maxAmmo;
        isReloading = false;
        UpdateUI();
    }

    void UpdateUI()
    {
        if (ammoText != null)
        {
            ammoText.text = $"{currentWeaponName}: {currentAmmo} / {maxAmmo}";
        }
    }
}