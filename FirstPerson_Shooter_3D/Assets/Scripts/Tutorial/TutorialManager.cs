using System;
using UnityEngine;

/// <summary>
/// Director del tutorial sobre raíles. Cada elemento de "Steps" describe el waypoint
/// con el mismo índice en la lista Waypoints del RailManager.
/// </summary>
public class TutorialManager : MonoBehaviour
{
    public enum StepType
    {
        Pass, // solo de paso sigue al siguiente punto
        Shoot,         // se avanza al hacer un disparo
        ShootEnemies,  // aparecen enemigos (ZoneSpawner); el RailManager avanza al matarlos
        Heal,          // hay que disparar al botiquín (arrástralo en Target)
        Grenade,       // hay que coger la granada con [E]
        Finale         // combate final con granadas; al limpiar la zona termina el tutorial
    }

    [Serializable]
    public class Step
    {
        public string name;
        public StepType type;
        [Tooltip("Solo para Heal: el botiquín de esta parada")]
        public GameObject target;
        [Tooltip("OPCIONAL. Aviso propio de esta parada (si está vacío, se usa el de por defecto)")]
        [TextArea] public string message;
        [NonSerialized] public bool hadTarget;
    }

    [Header("Referencias")]
    [SerializeField] private TutorialCompletePanel completePanel;
    [Tooltip("OPCIONAL. Enemigos colocados a mano (se activan al llegar al paso Finale). " +
             "Si usas ZoneSpawner, déjalo vacío.")]
    [SerializeField] private GameObject[] tutorialEnemies;

    [Header("Pasos (el elemento N = waypoint N del RailManager)")]
    [SerializeField] private Step[] steps;

    [Header("Opciones")]
    [SerializeField] private bool grenadeOnlyFinale = true;

    [Header("Depuración")]
    [SerializeField] private bool debugLog = true;

    [Header("Mensajes por defecto")]
    [TextArea][SerializeField] private string shootMsg = "<b><color=#FFD54F>CLIC IZQUIERDO</color></b> para disparar";
    [TextArea][SerializeField] private string healPickupMsg = "Dispara a este objeto para <b><color=#5CFF7A>curarte</color></b>";
    [TextArea][SerializeField] private string grenadePickupMsg = "Pulsa <b><color=#FFD54F>[E]</color></b> para coger la granada";
    [TextArea]
    [SerializeField]
    private string grenadeControlsMsg =
        "Mantén <b><color=#FFD54F>[G]</color></b> para apuntar  ·  Suelta <b><color=#FFD54F>[G]</color></b> para lanzar\n" +
        "<b><color=#FFD54F>[X]</color></b> para cancelar el lanzamiento";
    [TextArea]
    [SerializeField]
    private string fallbackMsg =
        "¡Sin granadas! Remata a los enemigos con <b><color=#FFD54F>CLIC IZQUIERDO</color></b>";

    private bool weaponTaken, shootUnlocked, shotHere, healDone;
    private bool grenadeTaken, enemiesStarted, grenadeThrown, completed;
    private int lastGrenadeCount;
    private string shownMsg;
    private string lastStateLog;
    private RailManager rail;

    private void Awake()
    {
        lastGrenadeCount = GrenadeInventory.Count;

        foreach (GameObject e in tutorialEnemies)
            if (e != null) e.SetActive(false);

        if (steps != null)
            foreach (Step s in steps)
                s.hadTarget = s.target != null;

        RefreshGates();
    }

    private void Start()
    {
        rail = RailManager.Instance;
        if (rail == null)
        {
            Debug.LogError("[TutorialManager] No hay RailManager en la escena.", this);
            return;
        }

        rail.OnPathFinished += OnPathFinished;
        rail.OnStartedMoving += OnRailStartedMoving;
        rail.OnArrived += OnRailArrived;

        if (steps == null || steps.Length != rail.waypoints.Length)
            Debug.LogWarning($"[TutorialManager] Hay {(steps == null ? 0 : steps.Length)} pasos y " +
                             $"{rail.waypoints.Length} waypoints. Deben coincidir.", this);
    }

    private void OnEnable()
    {
        TutorialEvents.WeaponPickedUp += OnWeaponPickedUp;
        TutorialEvents.WeaponFired += OnWeaponFired;
        TutorialEvents.HealPickupShot += OnHealPickupShot;
        TutorialEvents.GrenadePickedUp += OnGrenadePickedUp;
        GrenadeInventory.OnChanged += OnGrenadeCountChanged;
    }

    private void OnDisable()
    {
        TutorialEvents.WeaponPickedUp -= OnWeaponPickedUp;
        TutorialEvents.WeaponFired -= OnWeaponFired;
        TutorialEvents.HealPickupShot -= OnHealPickupShot;
        TutorialEvents.GrenadePickedUp -= OnGrenadePickedUp;
        GrenadeInventory.OnChanged -= OnGrenadeCountChanged;
    }

    private void OnDestroy()
    {
        if (rail != null)
        {
            rail.OnPathFinished -= OnPathFinished;
            rail.OnStartedMoving -= OnRailStartedMoving;
            rail.OnArrived -= OnRailArrived;
        }
        TutorialGate.ResetAll();
    }

    // ───────── Utilidades ─────────
    private Step CurrentStep
    {
        get
        {
            if (rail == null || steps == null) return null;
            int i = rail.CurrentStopIndex;
            return (i >= 0 && i < steps.Length) ? steps[i] : null;
        }
    }

    /// <summary>True si el vagón está parado en una parada de ese tipo.</summary>
    private bool AtStep(StepType type)
    {
        if (rail == null || rail.IsMoving) return false;
        Step s = CurrentStep;
        return s != null && s.type == type;
    }

    // ───────── Eventos ─────────
    private void OnWeaponPickedUp()
    {
        weaponTaken = true;
        if (debugLog) Debug.Log("[Tutorial] Arma recogida.", this);
        RefreshGates();
    }

    private void OnWeaponFired()
    {
        if (TutorialGate.CanShoot && AtStep(StepType.Shoot)) shotHere = true;
        if (TutorialGate.CanShoot && AtStep(StepType.ShootEnemies)) shotHere = true;
    }

    // Opcional: si algún script lo lanza, también cuenta
    private void OnHealPickupShot()
    {
        if (TutorialGate.CanShoot && AtStep(StepType.Heal)) healDone = true;
    }

    private void OnGrenadePickedUp()
    {
        grenadeTaken = true;
        if (debugLog) Debug.Log("[Tutorial] Granada recogida.", this);
    }

    private void OnGrenadeCountChanged(int count)
    {
        if (enemiesStarted && count < lastGrenadeCount) grenadeThrown = true;
        lastGrenadeCount = count;
        RefreshGates();
    }

    private void OnRailStartedMoving(int waypointIndex)
    {
        shownMsg = null;
        if (debugLog) Debug.Log($"[Tutorial] El rail se mueve al waypoint {waypointIndex}.", this);
    }

    private void OnRailArrived(int waypointIndex)
    {
        shownMsg = null;
        shotHere = false;
        healDone = false;
        if (debugLog) Debug.Log($"[Tutorial] Llegada al waypoint {waypointIndex}.", this);
    }

    private void OnPathFinished()
    {
        if (debugLog) Debug.Log($"[Tutorial] Ruta terminada (enemiesStarted = {enemiesStarted}).", this);
        if (enemiesStarted) Complete();
    }

    private bool FallbackShooting => enemiesStarted && !GrenadeInventory.HasGrenade;

    private void RefreshGates()
    {
        bool grenadeOnly = grenadeOnlyFinale && enemiesStarted && !FallbackShooting;
        TutorialGate.CanShoot = weaponTaken && shootUnlocked && !grenadeOnly;
        TutorialGate.CanThrowGrenade = enemiesStarted;
    }

    // ───────── Bucle principal ─────────
    private void Update()
    {
        if (completed) return;

        UpdateProgress();
        UpdateHint();
        LogState();

        if (enemiesStarted && AllManualEnemiesDead()) Complete();
    }

    private void LogState()
    {
        if (!debugLog) return;

        Step s = CurrentStep;
        string state = $"parada {(rail != null ? rail.CurrentStopIndex : -99)} " +
                       $"({(s != null ? s.type.ToString() : "-")}) | " +
                       $"moviéndose: {(rail != null && rail.IsMoving)} | CanShoot: {TutorialGate.CanShoot} | " +
                       $"arma: {weaponTaken} | disparo: {shotHere} | granada: {grenadeTaken} | " +
                       $"enemiesStarted: {enemiesStarted} | tiene granadas: {GrenadeInventory.HasGrenade}";

        if (state == lastStateLog) return;
        lastStateLog = state;
        Debug.Log("[Tutorial] " + state, this);
    }

    private void UpdateProgress()
    {
        if (rail == null || rail.IsMoving) return;

        // Aún en el punto de inicio: esperando el arma
        if (rail.CurrentStopIndex < 0)
        {
            if (weaponTaken) Advance();
            return;
        }

        Step s = CurrentStep;
        if (s == null) return;

        // Desde la primera parada de disparo en adelante el arma queda desbloqueada
        if (!shootUnlocked && s.type != StepType.Finale)
        {
            shootUnlocked = true;
            RefreshGates();
        }

        switch (s.type)
        {
            case StepType.Pass:
                Advance();
                break;

            case StepType.Shoot:
                if (shotHere) Advance();
                break;

            case StepType.ShootEnemies:
                // El RailManager avanza solo al matar a los enemigos
                break;

            case StepType.Heal:
                // El botiquín se destruye al dispararlo: eso cuenta como curación
                if (healDone || (s.hadTarget && s.target == null)) Advance();
                break;

            case StepType.Grenade:
                if (grenadeTaken || GrenadeInventory.HasGrenade) Advance();
                break;

            case StepType.Finale:
                if (!enemiesStarted)
                {
                    enemiesStarted = true;
                    foreach (GameObject e in tutorialEnemies)
                        if (e != null) e.SetActive(true);
                    RefreshGates();
                }
                break;
        }
    }

    private void Advance()
    {
        if (debugLog) Debug.Log($"[Tutorial] Avanzando desde la parada {rail.CurrentStopIndex}.", this);
        rail.GoToNextWaypoint();
    }

    // ───────── Avisos ─────────
    private void UpdateHint()
    {
        if (GrenadeThrower.IsAiming)
        {
            shownMsg = null;
            return;
        }

        string msg = (rail != null && rail.IsMoving) ? null : (GetAimMessage() ?? GetPersistentMessage());
        if (msg == shownMsg) return;
        shownMsg = msg;

        if (ControlsHintUI.Instance == null) return;
        if (msg != null) ControlsHintUI.Instance.Show(msg, 0f, true);
        else ControlsHintUI.Instance.Hide();
    }

    private string GetAimMessage()
    {
        if (!AimRaycaster.Cast(out RaycastHit hit)) return null;

        TutorialTarget target = hit.collider.GetComponentInParent<TutorialTarget>();
        if (target == null || hit.distance > target.maxAimDistance) return null;

        Step s = CurrentStep;

        switch (target.type)
        {
            case TutorialTargetType.Weapon:
                return null; // lo muestra PickUpController
            case TutorialTargetType.HealPickup:
                return (TutorialGate.CanShoot && AtStep(StepType.Heal))
                    ? (string.IsNullOrEmpty(s.message) ? healPickupMsg : s.message) : null;
            case TutorialTargetType.Grenade:
                return (!grenadeTaken && AtStep(StepType.Grenade))
                    ? (string.IsNullOrEmpty(s.message) ? grenadePickupMsg : s.message) : null;
        }
        return null;
    }

    private string GetPersistentMessage()
    {
        Step s = CurrentStep;
        if (s == null || rail == null || rail.IsMoving) return null;

        switch (s.type)
        {
            case StepType.Shoot:
            case StepType.ShootEnemies:
                if (TutorialGate.CanShoot && !shotHere)
                    return string.IsNullOrEmpty(s.message) ? shootMsg : s.message;
                return null;

            case StepType.Finale:
                if (!enemiesStarted) return null;
                if (!grenadeThrown && GrenadeInventory.HasGrenade) return grenadeControlsMsg;
                if (grenadeOnlyFinale && FallbackShooting) return fallbackMsg;
                return null;
        }
        return null;
    }

    private bool AllManualEnemiesDead()
    {
        if (tutorialEnemies == null || tutorialEnemies.Length == 0) return false;
        foreach (GameObject e in tutorialEnemies)
            if (e != null && e.activeInHierarchy) return false;
        return true;
    }

    private void Complete()
    {
        if (completed) return;
        completed = true;
        if (shownMsg != null && ControlsHintUI.Instance != null) ControlsHintUI.Instance.Hide();
        shownMsg = null;
        if (completePanel != null) completePanel.Show();
    }
}