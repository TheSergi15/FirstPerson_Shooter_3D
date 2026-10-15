
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
    public Vector3 equippedRotation = Vector3.zero;   // Grados (Euler)

    [Header("Lanzar")]
    public Key throwKey = Key.Q;
    public float dropForwardForce = 12f;
    public float dropUpwardForce = 2f;

    [Header("Depuración")]
    public bool debugLog = true;          // Desactívalo cuando todo funcione

    public bool equipped;

    // Propiedad en vez de campo para ver en la consola quién cambia el slot
    // (otros scripts pueden seguir usando PickUpController.slotFull igual que antes)
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
    private bool warnedNoKeyboard;
    private bool quitting;

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

        // El arma lanzada no debe chocar con ningún collider del jugador (raíz e hijos)
        foreach (Collider playerCol in player.GetComponentsInChildren<Collider>())
        {
            if (playerCol != coll)
                Physics.IgnoreCollision(coll, playerCol);
        }

        lastPlayerPos = player.position;

        // Setup
        gunScript.enabled = equipped;
        rb.isKinematic = equipped;
        coll.isTrigger = equipped;

        if (equipped) slotFull = true;

        if (debugLog)
            Debug.Log($"[PickUp] {name} listo -> equipped={equipped}, slotFull={slotFull}, rango={pickUpRange}, jugador='{player.name}', tecla={pickUpKey}", this);
    }

    private void OnDisable()
    {
        // La traza de este aviso en la consola muestra quién ha desactivado el script
        if (debugLog && !quitting)
            Debug.LogWarning($"[PickUp] {name}: PickUpController DESACTIVADO (mira la traza para ver quién)", this);
    }

    private void OnApplicationQuit() => quitting = true;

    private void OnDestroy()
    {
        // Si se destruye el arma estando equipada, libera el slot
        if (equipped && !quitting) slotFull = false;
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

        if (equipped || slotFull || Time.time < nextPickUpTime)
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

    private void PickUp()
    {
        equipped = true;
        slotFull = true;

        // Hacer el arma hija del contenedor y colocarla en la posición/rotación configurada
        transform.SetParent(gunContainer);
        transform.localPosition = equippedPosition;
        transform.localRotation = Quaternion.Euler(equippedRotation);
        transform.localScale = Vector3.one;

        rb.isKinematic = true;
        coll.isTrigger = true;

        gunScript.enabled = true;

        if (debugLog) Debug.Log($"[PickUp] {name}: RECOGIDA", this);
    }

    // Suelta y lanza el arma
    public void Drop()
    {
        equipped = false;
        slotFull = false;
        nextPickUpTime = Time.time + pickUpCooldown;

        transform.SetParent(null);

        rb.isKinematic = false;
        coll.isTrigger = false;

        // Hereda la velocidad del jugador para no quedarse atrás en los raíles
        // (en Unity anteriores a Unity 6, usa rb.velocity)
        rb.linearVelocity = playerVelocity;

        // Fuerza de lanzamiento
        rb.AddForce(GetThrowDirection() * dropForwardForce, ForceMode.Impulse);
        rb.AddForce(fpsCam.up * dropUpwardForce, ForceMode.Impulse);

        // Rotación aleatoria
        rb.AddTorque(new Vector3(
            Random.Range(-1f, 1f),
            Random.Range(-1f, 1f),
            Random.Range(-1f, 1f)) * 10f);

        gunScript.enabled = false;

        if (debugLog) Debug.Log($"[PickUp] {name}: LANZADA", this);
    }

    // Hacia donde apunta el cursor; si no hay cámara o ratón, hacia delante
    private Vector3 GetThrowDirection()
    {
        if (fpsCam.TryGetComponent(out Camera cam) && Mouse.current != null)
            return cam.ScreenPointToRay(Mouse.current.position.ReadValue()).direction;
        return fpsCam.forward;
    }

    // Selecciona el arma en la escena para ver el rango (esfera amarilla)
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, pickUpRange);
    }
}
