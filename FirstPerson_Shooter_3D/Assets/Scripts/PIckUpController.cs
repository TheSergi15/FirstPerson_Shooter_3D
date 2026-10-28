using UnityEngine;
using UnityEngine.InputSystem;

public class PickUpController : MonoBehaviour
{
    public Gun gunScript;
    public Rigidbody rb;
    public BoxCollider coll;
    public Transform player, gunContainer, fpsCam;

    [Header("Recoger")]
    public Key pickUpKey = Key.E;
    public float pickUpRange = 3f;
    public float pickUpCooldown = 1.5f;   // Tras lanzarla, tarda en poder recogerse otra vez

    [Header("Posición al equipar (relativa al Gun Container)")]
    public Vector3 equippedPosition = Vector3.zero;
    public Vector3 equippedRotation = Vector3.zero;   // Solo vale para el primer instante: luego el Gun apunta al puntero

    [Header("Lanzar")]
    public Key throwKey = Key.Q;
    public float dropForwardForce = 12f;
    public float dropUpwardForce = 2f;

    [Header("Avisos en pantalla")]
    public WeaponControlsGuide guide;   // Opcional: si se deja vacío se busca en la escena
    public string pickUpMessage = "Pulsa {0} para agarrar el arma";

    // NUEVO (inventario)
    [Header("Inventario de armas")]
    public WeaponSwitcher switcher;     // Opcional: si se deja vacío se busca en la escena. Sin switcher, solo se puede llevar un arma

    [Header("Depuración")]
    public bool debugLog = true;          // Desactívalo cuando todo funcione

    public bool equipped;

    // Propiedad en vez de campo para ver en la consola quién cambia el slot
    // (otros scripts pueden seguir usando PickUpController.slotFull igual que antes)
    // Con inventario significa "el jugador lleva algún arma en la mano"
    private static bool _slotFull;
    public static bool slotFull
    {
        get => _slotFull;
        set
        {
            if (_slotFull != value)
                Debug.Log($"[PickUp] slotFull: {_slotFull} -> {value}");
            _slotFull = value;
        }
    }

    private float nextPickUpTime;
    private Vector3 lastPlayerPos;
    private Vector3 playerVelocity;
    private Vector3 baseScale;
    private RigidbodyInterpolation originalInterpolation;
    private Camera cam;
    private bool warnedNoKeyboard;
    private bool quitting;
    private bool promptShown;

    // NUEVO (inventario): ranura de esta arma en el WeaponSwitcher (-1 = no usa inventario)
    private int slotIndex = -1;
    private bool UsesInventory => switcher != null && slotIndex >= 0;

    // Reinicia el slot al entrar en Play, aunque el Domain Reload esté desactivado
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => _slotFull = false;

    private void Start()
    {
        string missing = "";
        if (!gunScript) missing += "gunScript ";
        if (!rb) missing += "rb ";
        if (!coll) missing += "coll ";
        if (!player) missing += "player ";
        if (!gunContainer) missing += "gunContainer ";
        if (!fpsCam) missing += "fpsCam ";

        if (missing != "")
        {
            Debug.LogError($"{name}: faltan referencias en PickUpController: {missing}", this);
            enabled = false;
            return;
        }

        if (pickUpRange <= 0f)
            Debug.LogWarning($"{name}: pickUpRange es 0, nunca se podrá recoger.", this);

        // Si el contenedor no cuelga del jugador, el arma no seguirá al jugador cuando se mueva por los raíles
        if (!gunContainer.IsChildOf(player))
            Debug.LogWarning($"[PickUp] '{gunContainer.name}' (Gun Container) NO es hijo de '{player.name}' (player). " +
                             "Si los raíles mueven al jugador, el arma se quedará atrás. " +
                             "Arrastra el Gun Container dentro del objeto que se mueve por los raíles.", this);

        // Busca la guía de controles si no se ha asignado a mano
        if (!guide) guide = FindFirstObjectByType<WeaponControlsGuide>();

        // NUEVO (inventario): busca el WeaponSwitcher y la ranura que ocupa esta arma
        if (!switcher) switcher = FindFirstObjectByType<WeaponSwitcher>();
        if (switcher)
        {
            slotIndex = switcher.IndexOf(gameObject);
            if (slotIndex < 0)
            {
                Debug.LogWarning($"[PickUp] {name} no está en la lista Weapons del WeaponSwitcher: se usa el modo de un solo arma (slotFull).", this);
                switcher = null;
            }
        }

        // Escala original del arma (para no deformarla al equiparla)
        baseScale = transform.localScale;

        // Interpolación original del Rigidbody (se desactiva mientras el arma está equipada)
        originalInterpolation = rb.interpolation;

        // Cámara que usan el disparo, el apuntado y el lanzamiento
        cam = fpsCam.GetComponent<Camera>();
        if (cam == null) cam = fpsCam.GetComponentInChildren<Camera>();
        if (cam == null) cam = Camera.main;
        if (cam == null)
            Debug.LogError($"{name}: no se encuentra ninguna cámara. Asigna 'fpsCam' a la cámara o etiquétala como MainCamera.", this);

        // El Gun necesita saber quién es el dueño y qué cámara usar
        gunScript.SetOwner(player, cam);

        // El arma lanzada no debe chocar con ningún collider del jugador (raíz e hijos)
        foreach (Collider playerCol in player.GetComponentsInChildren<Collider>())
        {
            if (playerCol != coll)
                Physics.IgnoreCollision(coll, playerCol);
        }

        lastPlayerPos = player.position;

        // Si el arma empieza equipada pero no cuelga del contenedor, la colgamos aquí
        // (si no, no seguiría al jugador ni apuntaría al puntero)
        if (equipped && transform.parent != gunContainer)
            AttachToContainer();

        // Setup
        gunScript.enabled = equipped;
        rb.isKinematic = equipped;
        coll.isTrigger = equipped;

        if (equipped)
        {
            rb.interpolation = RigidbodyInterpolation.None;
            slotFull = true;
        }

        if (debugLog)
            Debug.Log($"[PickUp] {name} listo -> equipped={equipped}, slotFull={slotFull}, rango={pickUpRange}, jugador='{player.name}', tecla={pickUpKey}, inventario={(UsesInventory ? "ranura " + slotIndex : "no")}", this);
    }

    private void OnDisable()
    {
        // Si este arma estaba mostrando el aviso, lo quita
        if (!quitting) HidePrompt();

        // La traza de este aviso en la consola muestra quién ha desactivado el script
        // NUEVO (inventario): si el GameObject está inactivo es que se ha guardado con el WeaponSwitcher; no es un fallo
        if (debugLog && !quitting && gameObject.activeInHierarchy)
            Debug.LogWarning($"[PickUp] {name}: PickUpController DESACTIVADO (mira la traza para ver quién)", this);
    }

    private void OnApplicationQuit() => quitting = true;

    private void OnDestroy()
    {
        // Si se destruye el arma estando equipada, libera el slot
        if (equipped && !quitting)
        {
            if (UsesInventory)
            {
                switcher.Lock(slotIndex);
                slotFull = switcher.HasWeapon;
            }
            else
            {
                slotFull = false;
            }
        }
    }

    // La traza muestra quién cambia el padre del arma (también salta con PickUp y Drop)
    private void OnTransformParentChanged()
    {
        if (debugLog)
            Debug.Log($"[PickUp] {name}: cambio de padre -> {(transform.parent ? transform.parent.name : "ninguno")}", this);
    }

    private void Update()
    {
        // Velocidad del jugador, calculada a mano (por los raíles puede no tener Rigidbody)
        if (Time.deltaTime > 0f)
            playerVelocity = (player.position - lastPlayerPos) / Time.deltaTime;
        lastPlayerPos = player.position;

        // Aviso "Pulsa E para agarrar el arma"
        UpdatePickUpPrompt();

        if (Keyboard.current == null)
        {
            if (debugLog && !warnedNoKeyboard)
            {
                warnedNoKeyboard = true;
                Debug.LogWarning("[PickUp] Keyboard.current es null: revisa Project Settings > Player > Active Input Handling (debe ser Input System Package o Both).", this);
            }
            return;
        }

        // Lanzar el arma
        if (equipped && Keyboard.current[throwKey].wasPressedThisFrame)
        {
            Drop();
            return;
        }

        bool pressed = Keyboard.current[pickUpKey].wasPressedThisFrame;

        // NUEVO (inventario): CanBePickedUp() sustituye a "equipped || slotFull"
        if (!CanBePickedUp() || Time.time < nextPickUpTime)
        {
            if (pressed && debugLog)
                Debug.Log($"[PickUp] {name}: no se puede recoger -> equipped={equipped}, slotFull={slotFull}, cooldown restante={Mathf.Max(0f, nextPickUpTime - Time.time):F2}s", this);
            return;
        }

        if (!pressed) return;

        // Se equipa al pulsar la tecla si el jugador está dentro del rango
        float distance = Vector3.Distance(player.position, transform.position);
        if (distance > pickUpRange)
        {
            if (debugLog)
                Debug.Log($"[PickUp] {name}: fuera de rango -> distancia {distance:F2} m, rango {pickUpRange} m (jugador: '{player.name}')", this);
            return;
        }

        PickUp();
    }

    // Red de seguridad: si algo despega el arma del contenedor mientras está equipada, se vuelve a colgar
    private void LateUpdate()
    {
        if (equipped && transform.parent != gunContainer)
        {
            if (debugLog)
                Debug.LogWarning($"[PickUp] {name}: el arma equipada ya no era hija de '{gunContainer.name}'; se vuelve a colgar.", this);
            AttachToContainer();
        }
    }

    // NUEVO (inventario): con inventario se pueden recoger varias armas; sin él, solo una (slotFull)
    private bool CanBePickedUp()
    {
        if (equipped) return false;
        return UsesInventory || !slotFull;
    }

    // Muestra el aviso solo cuando realmente se puede recoger
    private void UpdatePickUpPrompt()
    {
        bool canPickUp = CanBePickedUp() && Time.time >= nextPickUpTime
                         && Vector3.Distance(player.position, transform.position) <= pickUpRange;

        if (canPickUp && !promptShown) ShowPrompt();
        else if (!canPickUp && promptShown) HidePrompt();
    }

    private void ShowPrompt()
    {
        if (ControlsHintUI.Instance == null) return;

        string text = string.Format(pickUpMessage, ControlsHintUI.Key(pickUpKey.ToString()));
        ControlsHintUI.Instance.Show(text, 0f, true);   // 0 = se queda hasta ocultarlo
        promptShown = true;
    }

    private void HidePrompt()
    {
        if (!promptShown) return;
        promptShown = false;

        if (ControlsHintUI.Instance != null)
            ControlsHintUI.Instance.Hide();
    }

    // Cuelga el arma del contenedor y la coloca en la posición/rotación configurada
    private void AttachToContainer()
    {
        transform.SetParent(gunContainer, false);
        transform.localPosition = equippedPosition;
        transform.localRotation = Quaternion.Euler(equippedRotation);
        transform.localScale = baseScale;
    }

    private void PickUp()
    {
        equipped = true;
        slotFull = true;

        AttachToContainer();

        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        rb.isKinematic = true;
        coll.isTrigger = true;

        // Un Rigidbody con interpolación, hijo de algo que se mueve, se va quedando atrás
        rb.interpolation = RigidbodyInterpolation.None;

        gunScript.enabled = true;

        // NUEVO (inventario): la mete en el inventario y la equipa (guarda la que llevaba en la mano)
        if (UsesInventory) switcher.Unlock(slotIndex, true);

        // Quita el aviso de recoger y enseña el resto de controles
        HidePrompt();
        if (guide) guide.PlayControls();

        if (debugLog) Debug.Log($"[PickUp] {name}: RECOGIDA", this);
    }

    // Suelta y lanza el arma
    public void Drop()
    {
        equipped = false;
        nextPickUpTime = Time.time + pickUpCooldown;

        // Sin inventario, el slot se libera ya; con inventario se decide al final
        if (!UsesInventory) slotFull = false;

        // Corta la guía de controles si seguía en marcha
        if (guide) guide.StopGuide();

        // Primero se desactiva el Gun: cancela recarga y deja de apuntar
        gunScript.enabled = false;

        transform.SetParent(null);

        rb.isKinematic = false;
        coll.isTrigger = false;
        rb.interpolation = originalInterpolation;

        // Hereda la velocidad del jugador para no quedarse atrás en los raíles
        // (en Unity anteriores a Unity 6, usa rb.velocity)
        rb.linearVelocity = playerVelocity;

        // Fuerza de lanzamiento
        Vector3 up = fpsCam != null ? fpsCam.up : Vector3.up;
        rb.AddForce(GetThrowDirection() * dropForwardForce, ForceMode.Impulse);
        rb.AddForce(up * dropUpwardForce, ForceMode.Impulse);

        // Rotación aleatoria
        rb.AddTorque(new Vector3(
            Random.Range(-1f, 1f),
            Random.Range(-1f, 1f),
            Random.Range(-1f, 1f)) * 10f);

        // NUEVO (inventario): la saca del inventario y el WeaponSwitcher equipa otra si hay
        if (UsesInventory)
        {
            switcher.Lock(slotIndex);
            slotFull = switcher.HasWeapon;
        }

        if (debugLog) Debug.Log($"[PickUp] {name}: LANZADA", this);
    }

    // Hacia donde apunta el cursor; si no hay cámara o ratón, hacia delante
    private Vector3 GetThrowDirection()
    {
        if (cam != null && Mouse.current != null)
            return cam.ScreenPointToRay(Mouse.current.position.ReadValue()).direction;
        return fpsCam.forward;
    }
}
