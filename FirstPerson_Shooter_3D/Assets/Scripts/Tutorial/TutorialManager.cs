using UnityEngine;

/// <summary>
/// Director del tutorial sobre raíles. Va llevando al jugador por los waypoints del RailManager
/// según lo que haga, y decide qué aviso se ve y qué controles están desbloqueados:
///  Inicio       -> sin arma, el jugador no avanza. Apunta al arma: PickUpController avisa de [E].
///  Coger arma   -> avanza al waypoint 0. Al llegar se desbloquea el disparo ("Clic izquierdo").
///  Disparo      -> aparecen 2 enemigos en el waypoint 0; al matarlos avanza al waypoint 1
///                  (pickup de curación: "Dispara a este objeto para curarte").
///  Curación     -> avanza al waypoint 2 (granada: "Pulsa [E] para coger la granada").
///  Coger granada-> avanza al waypoint 3 (ZoneSpawner crea 5 enemigos que hay que matar con la granada;
///                  el arma se bloquea hasta gastar las granadas; se muestran los controles
///                  de la granada y se desbloquea el lanzamiento).
///  Matar a todos-> fin de la ruta -> cartel de tutorial completado -> siguiente nivel.
/// </summary>
public class TutorialManager : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField] private TutorialCompletePanel completePanel;
    [Tooltip("OPCIONAL. Enemigos colocados a mano (se activan al llegar al último waypoint). " +
             "Si usas ZoneSpawner, déjalo vacío.")]
    [SerializeField] private GameObject[] tutorialEnemies;

    [Header("Opciones")]
    [Tooltip("Marcado: en el waypoint 0 aparecen enemigos (ZoneSpawner) y la parada se supera MATÁNDOLOS " +
             "(RailStop = Kill Enemies). Desmarcado: se avanza al hacer el primer disparo.")]
    [SerializeField] private bool shootStopHasEnemies = true;
    [Tooltip("Marcado: en el último combate el arma queda bloqueada para obligar a usar la granada. " +
             "Si se gastan todas las granadas y quedan enemigos, el disparo se vuelve a permitir (evita atascos).")]
    [SerializeField] private bool grenadeOnlyFinale = true;

    [Header("Mensajes (admite rich text de TextMeshPro)")]
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

    // Etapas: 0 espera arma | 1 en waypoint 0 (disparo) | 2 en waypoint 1 (curación)
    //         3 en waypoint 2 (granada) | 4 en waypoint 3 (enemigos)
    private int stage;

    private bool weaponTaken, shootReady, hasShot, healShot;
    private bool grenadeTaken, enemiesStarted, grenadeThrown, completed;
    private int lastGrenadeCount;
    private string shownMsg; // último mensaje que ha mostrado el tutorial
    private RailManager rail;

    private void Awake()
    {
        lastGrenadeCount = GrenadeInventory.Count;

        foreach (GameObject e in tutorialEnemies)
            if (e != null) e.SetActive(false);

        RefreshGates();
    }

    private void Start()
    {
        rail = RailManager.Instance;
        if (rail == null)
            Debug.LogError("[TutorialManager] No hay RailManager en la escena.", this);
        else
        {
            rail.OnPathFinished += OnPathFinished;
            rail.OnStartedMoving += OnRailStartedMoving;
        }
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
        }
        TutorialGate.ResetAll(); // al salir del tutorial todo vuelve a estar permitido
    }

    // ───────── Eventos ─────────
    private void OnWeaponPickedUp() { weaponTaken = true; RefreshGates(); }
    private void OnWeaponFired() { if (TutorialGate.CanShoot) hasShot = true; }
    private void OnHealPickupShot() { if (TutorialGate.CanShoot) healShot = true; }
    private void OnGrenadePickedUp() { grenadeTaken = true; }

    private void OnGrenadeCountChanged(int count)
    {
        if (enemiesStarted && count < lastGrenadeCount) grenadeThrown = true;
        lastGrenadeCount = count;
        RefreshGates();
    }

    // La etapa sigue siempre al RailManager: ir al waypoint N = etapa N+1.
    // Así funciona también cuando el propio rail avanza por matar enemigos.
    private void OnRailStartedMoving(int waypointIndex)
    {
        stage = waypointIndex + 1;
    }

    private void OnPathFinished()
    {
        if (enemiesStarted) Complete(); // última zona limpia = tutorial completado
    }

    // Granadas gastadas con enemigos vivos: se permite disparar para no quedarse atascado
    private bool FallbackShooting => grenadeThrown && !GrenadeInventory.HasGrenade;

    private void RefreshGates()
    {
        bool grenadeOnly = grenadeOnlyFinale && enemiesStarted && !FallbackShooting;
        TutorialGate.CanShoot = weaponTaken && shootReady && !grenadeOnly;
        TutorialGate.CanThrowGrenade = enemiesStarted;
    }

    // ───────── Bucle principal ─────────
    private void Update()
    {
        if (completed) return;

        UpdateProgress();
        UpdateHint();

        if (enemiesStarted && AllManualEnemiesDead()) Complete();
    }

    /// <summary>Mueve al jugador por los waypoints según lo que haya hecho.</summary>
    private void UpdateProgress()
    {
        if (rail == null || rail.IsMoving) return; // solo se decide cuando está parado

        switch (stage)
        {
            case 0:
                if (weaponTaken) Advance();            // -> waypoint 0
                break;

            case 1:                                    // parado en el waypoint 0
                if (!shootReady) { shootReady = true; RefreshGates(); }
                if (hasShot && !shootStopHasEnemies) Advance(); // -> waypoint 1
                // con enemigos: el RailManager avanza solo al matarlos (RailStop = Kill Enemies)
                break;

            case 2:                                    // parado en el waypoint 1 (curación)
                if (healShot) Advance();               // -> waypoint 2
                break;

            case 3:                                    // parado en el waypoint 2 (granada)
                if (grenadeTaken) Advance();           // -> waypoint 3
                break;

            case 4:                                    // parado en el waypoint 3 (enemigos)
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
        rail.GoToNextWaypoint(); // la etapa la actualiza OnRailStartedMoving
    }

    // ───────── Avisos ─────────
    private void UpdateHint()
    {
        // Mientras apuntas la granada, el aviso lo gestiona el propio GrenadeThrower
        if (GrenadeThrower.IsAiming)
        {
            shownMsg = null;
            return;
        }

        string msg = GetAimMessage() ?? GetPersistentMessage();
        if (msg == shownMsg) return; // evita relanzar la corrutina del fundido cada frame
        shownMsg = msg;

        if (ControlsHintUI.Instance == null) return;
        if (msg != null) ControlsHintUI.Instance.Show(msg, 0f, true); // 0 = se queda hasta Hide()
        else ControlsHintUI.Instance.Hide();
    }

    /// <summary>Avisos que salen solo mientras el puntero apunta al objeto.</summary>
    private string GetAimMessage()
    {
        if (!AimRaycaster.Cast(out RaycastHit hit)) return null;

        TutorialTarget target = hit.collider.GetComponentInParent<TutorialTarget>();
        if (target == null || hit.distance > target.maxAimDistance) return null;

        switch (target.type)
        {
            case TutorialTargetType.Weapon:
                return null; // el aviso de recoger el arma ya lo muestra PickUpController
            case TutorialTargetType.HealPickup:
                return TutorialGate.CanShoot ? healPickupMsg : null;
            case TutorialTargetType.Grenade:
                return grenadeTaken ? null : grenadePickupMsg;
        }
        return null;
    }

    /// <summary>Avisos que se mantienen hasta que haces la acción.</summary>
    private string GetPersistentMessage()
    {
        if (TutorialGate.CanShoot && !hasShot) return shootMsg;
        if (enemiesStarted && !grenadeThrown && GrenadeInventory.HasGrenade) return grenadeControlsMsg;
        if (enemiesStarted && grenadeOnlyFinale && FallbackShooting) return fallbackMsg;
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