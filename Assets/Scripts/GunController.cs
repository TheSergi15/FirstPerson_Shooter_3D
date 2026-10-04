using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using TMPro; // Novedad: Importamos el espacio de nombres para TextMeshPro

public class GunController : MonoBehaviour
{
    [Header("Configuración del Arma")]
    public float damage = 10f;
    public float range = 100f;
    public LayerMask enemyLayer;

    [Header("Munición y Recarga")]
    public int maxAmmo = 6;
    private int currentAmmo;
    public float reloadTime = 1f;
    private bool isReloading = false;

    [Header("Efectos Visuales")]
    public GameObject impactEffectPrefab;

    [Header("Interfaz de Usuario (UI)")]
    [Tooltip("Arrastra aquí el texto de TextMeshPro de tu Canvas")]
    public TextMeshProUGUI ammoText;

    private Camera mainCamera;

    void Start()
    {
        mainCamera = Camera.main;
        currentAmmo = maxAmmo;
        UpdateUI(); // Actualizamos la UI al iniciar
    }

    void Update()
    {
        if (isReloading)
            return;

        // Recarga con clic derecho
        if (Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame)
        {
            if (currentAmmo < maxAmmo)
            {
                StartCoroutine(Reload());
                return;
            }
        }

        // Disparo con clic izquierdo
        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            if (currentAmmo > 0)
            {
                Shoot();
            }
        }
    }

    IEnumerator Reload()
    {
        isReloading = true;

        // Cambiamos el texto para avisar al jugador
        if (ammoText != null)
            ammoText.text = "RECARGANDO...";

        yield return new WaitForSeconds(reloadTime);

        currentAmmo = maxAmmo;
        isReloading = false;
        UpdateUI(); // Restauramos el contador visual
    }

    void Shoot()
    {
        currentAmmo--;
        UpdateUI();

        Ray ray = mainCamera.ScreenPointToRay(Mouse.current.position.ReadValue());
        RaycastHit hit;

        if (Physics.Raycast(ray, out hit, range, enemyLayer))
        {
            if (impactEffectPrefab != null)
            {
                GameObject impactGO = Instantiate(impactEffectPrefab, hit.point, Quaternion.LookRotation(hit.normal));
                Destroy(impactGO, 2f);
            }

            // Daño a enemigos[cite: 2]
            EnemyHealth target = hit.transform.GetComponent<EnemyHealth>();
            if (target != null)
            {
                target.TakeDamage(damage);
            }

            // --- NUEVA LÓGICA: Curación ---
            HealthPickup pickup = hit.transform.GetComponent<HealthPickup>();
            if (pickup != null)
            {
                pickup.OnShot();
            }
        }
    }

    // Nueva función centralizada para actualizar el texto
    void UpdateUI()
    {
        if (ammoText != null)
        {
            ammoText.text = "BALAS: " + currentAmmo + " / " + maxAmmo;
        }
    }
}