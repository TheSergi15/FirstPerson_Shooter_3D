using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(AudioSource))]
public class LaserGun : MonoBehaviour
{
    public enum BarrelAxis { Forward, Back, Right, Left, Up, Down }

    [Header("Referencias")]
    [SerializeField] private Camera playerCamera;
    [SerializeField] private LayerMask aimMask = ~0;
    [SerializeField] private LineRenderer lineRenderer; // Componente para dibujar el rayo láser visualmente

    [Header("Batería / Energía del Láser")]
    [SerializeField] private float maxEnergy = 100f;
    [SerializeField] private float energyConsumptionRate = 35f; // Energía gastada por segundo al disparar
    [SerializeField] private float energyRechargeRate = 25f;    // Energía recuperada por segundo al no disparar
    [SerializeField] private float rechargeDelay = 1f;          // Segundos de espera antes de empezar a recargar
    [SerializeField] private float overheatPenaltyTime = 2f;     // Tiempo bloqueado si la batería llega a 0 (sobrecalentamiento)

    [Header("Daño y Láser")]
    [SerializeField] private float damagePerSecond = 60f;
    [SerializeField] private float aimDistance = 100f;
    [SerializeField] private bool aimWithCursor = true;
    [SerializeField, Range(0f, 0.1f)] private float spread = 0f;

    [Header("Apuntado del arma")]
    [SerializeField] private bool aimWeaponAtCursor = true;
    [SerializeField] private float aimSpeed = 15f;
    [SerializeField] private float maxAimAngle = 45f;
    [SerializeField] private BarrelAxis barrelPointsTo = BarrelAxis.Forward;
    [SerializeField] private float rollDegrees = 0f;

    [Header("Efectos y Sonido")]
    [SerializeField] private ParticleSystem impactSparkEffect;
    [SerializeField] private AudioClip laserLoopClip;
    private AudioSource audioSource;

    [Header("Depuración")]
    [SerializeField] private bool debugLog = true;

    // Estado interno
    private float currentEnergy;
    private bool isOverheated;
    private float lastFireTime;
    private Transform owner;
    private bool isFiring;

    private readonly RaycastHit[] hits = new RaycastHit[16];

    // Propiedades públicas para interfaces de usuario (HUD de energía)
    public float CurrentEnergy => currentEnergy;
    public float MaxEnergy => maxEnergy;
    public bool IsOverheated => isOverheated;
    public float EnergyProgress => currentEnergy / maxEnergy;

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
        currentEnergy = maxEnergy;
        audioSource = GetComponent<AudioSource>();
        if (audioSource != null)
        {
            audioSource.loop = true;
            audioSource.clip = laserLoopClip;
        }

        if (lineRenderer != null)
        {
            lineRenderer.enabled = false;
        }
    }

    // Llamado por PickUpController al equipar el arma
    public void SetOwner(Transform ownerTransform, Camera cam)
    {
        owner = ownerTransform;
        if (playerCamera == null && cam != null) playerCamera = cam;
    }

    private void Update()
    {
        Mouse mouse = Mouse.current;
        bool triggerHeld = mouse != null && mouse.leftButton.isPressed;

        if (triggerHeld && !isOverheated)
        {
            TryFireLaser();
        }
        else
        {
            StopLaser();
        }
    }

    private void LateUpdate()
    {
        if (aimWeaponAtCursor) AimWeaponAtCursor();
    }

    // ---------- MECÁNICA DE DISPARO LÁSER ----------
    private void TryFireLaser()
    {
        if (!TutorialGate.CanShoot)
        {
            StopLaser();
            return;
        }

        if (Cam == null) return;

        if (currentEnergy <= 0f)
        {
            StartCoroutine(OverheatRoutine());
            StopLaser();
            return;
        }

        isFiring = true;
        lastFireTime = Time.time;
        currentEnergy = Mathf.Max(0f, currentEnergy - energyConsumptionRate * Time.deltaTime);

        if (currentEnergy <= 0f)
        {
            StartCoroutine(OverheatRoutine());
            StopLaser();
            return;
        }

        // Reproducir sonido continuo de láser
        if (audioSource != null && !audioSource.isPlaying && laserLoopClip != null)
        {
            audioSource.Play();
        }

        // Raycast continuo del láser
        Ray ray = GetShotRay();
        if (RaycastIgnoringSelf(ray, out RaycastHit hit))
        {
            Vector3 hitPoint = hit.point;

            // Daño progresivo a enemigos por frame/segundo
            EnemyHealth enemy = hit.collider.GetComponentInParent<EnemyHealth>();
            if (enemy != null)
            {
                enemy.TakeDamage(damagePerSecond * Time.deltaTime);
            }

            // Soporte para botiquines interactivos del tutorial
            HealthPickup pickup = hit.collider.GetComponentInParent<HealthPickup>();
            if (pickup != null)
            {
                pickup.OnShot();
                TutorialEvents.NotifyHealPickupShot();
            }

            // Notificar al tutorial que se está disparando
            TutorialEvents.NotifyWeaponFired();

            // Gestionar partículas de impacto en el punto de colisión
            if (impactSparkEffect != null)
            {
                if (!impactSparkEffect.isPlaying) impactSparkEffect.Play();
                impactSparkEffect.transform.position = hitPoint;
            }

            DrawLaser(transform.position, hitPoint);
        }
        else
        {
            Vector3 endPoint = ray.GetPoint(aimDistance);
            DrawLaser(transform.position, endPoint);
            if (impactSparkEffect != null && impactSparkEffect.isPlaying)
                impactSparkEffect.Stop();
        }
    }

    private void StopLaser()
    {
        if (!isFiring) return;
        isFiring = false;

        if (lineRenderer != null) lineRenderer.enabled = false;
        if (impactSparkEffect != null && impactSparkEffect.isPlaying) impactSparkEffect.Stop();

        if (audioSource != null && audioSource.isPlaying)
        {
            audioSource.Stop();
        }

        // Recarga automática pasiva si no se está disparando y pasó el tiempo de espera
        if (!isOverheated && currentEnergy < maxEnergy && Time.time >= lastFireTime + rechargeDelay)
        {
            currentEnergy = Mathf.Min(maxEnergy, currentEnergy + energyRechargeRate * Time.deltaTime);
        }
    }

    private IEnumerator OverheatRoutine()
    {
        isOverheated = true;
        if (debugLog) Debug.Log("[LaserGun] ¡Batería agotada / Sobrecalentamiento! Recargando...", this);
        yield return new WaitForSeconds(overheatPenaltyTime);
        currentEnergy = maxEnergy;
        isOverheated = false;
    }

    private void DrawLaser(Vector3 start, Vector3 end)
    {
        if (lineRenderer == null) return;
        lineRenderer.enabled = true;
        lineRenderer.positionCount = 2;
        lineRenderer.SetPosition(0, start);
        lineRenderer.SetPosition(1, end);
    }

    private Vector2 GetScreenPoint()
    {
        if (aimWithCursor && Mouse.current != null)
            return Mouse.current.position.ReadValue();

        return new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
    }

    private Ray GetCursorRay()
    {
        return Cam.ScreenPointToRay(GetScreenPoint());
    }

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

        Vector3 localDir = parent.InverseTransformDirection(dir);
        localDir = Vector3.RotateTowards(Vector3.forward, localDir, maxAimAngle * Mathf.Deg2Rad, 0f);
        Vector3 worldDir = parent.TransformDirection(localDir);

        Quaternion desired = Quaternion.LookRotation(worldDir, parent.up)
                            * Quaternion.Euler(0f, 0f, rollDegrees)
                            * BarrelCorrection();

        float t = 1f - Mathf.Exp(-aimSpeed * Time.deltaTime);
        transform.rotation = Quaternion.Slerp(transform.rotation, desired, t);
    }

    private Quaternion BarrelCorrection()
    {
        Quaternion zToBarrel;
        switch (barrelPointsTo)
        {
            case BarrelAxis.Back: zToBarrel = Quaternion.Euler(0f, 180f, 0f); break;
            case BarrelAxis.Right: zToBarrel = Quaternion.Euler(0f, 90f, 0f); break;
            case BarrelAxis.Left: zToBarrel = Quaternion.Euler(0f, -90f, 0f); break;
            case BarrelAxis.Up: zToBarrel = Quaternion.Euler(-90f, 0f, 0f); break;
            case BarrelAxis.Down: zToBarrel = Quaternion.Euler(0f, 0f, 0f); break;
            default: zToBarrel = Quaternion.identity; break;
        }
        return Quaternion.Inverse(zToBarrel);
    }

    private void OnDisable()
    {
        StopAllCoroutines();
        StopLaser();
        isOverheated = false;
        currentEnergy = maxEnergy;
    }
}