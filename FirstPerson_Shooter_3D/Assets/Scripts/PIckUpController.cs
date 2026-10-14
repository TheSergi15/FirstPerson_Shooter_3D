using UnityEngine;
using UnityEngine.InputSystem;

public class PickUpController : MonoBehaviour
{
    public Gun gunScript;
    public Rigidbody rb;
    public BoxCollider coll;
    public Transform player, gunContainer, fpsCam;

    [Header("Recoger")]
    public float pickUpRange = 3f;
    public float pickUpCooldown = 1.5f;   // Tras lanzarla, tarda en poder recogerse otra vez

    [Header("Lanzar")]
    public Key throwKey = Key.Q;
    public float dropForwardForce = 12f;
    public float dropUpwardForce = 2f;

    public bool equipped;
    public static bool slotFull;

    private float nextPickUpTime;
    private Vector3 lastPlayerPos;
    private Vector3 playerVelocity;

    // Reinicia el slot al entrar en Play, aunque el Domain Reload esté desactivado
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => slotFull = false;

    private void Start()
    {
        if (!gunScript || !rb || !coll || !player || !gunContainer || !fpsCam)
        {
            Debug.LogError($"{name}: faltan referencias en PickUpController.", this);
            enabled = false;
            return;
        }

        if (pickUpRange <= 0f)
            Debug.LogWarning($"{name}: pickUpRange es 0, nunca se podrá recoger.", this);

        // El arma lanzada no debe chocar con el collider del jugador
        if (player.TryGetComponent(out Collider playerCol))
            Physics.IgnoreCollision(coll, playerCol);

        lastPlayerPos = player.position;

        // Setup
        gunScript.enabled = equipped;
        rb.isKinematic = equipped;
        coll.isTrigger = equipped;

        if (equipped) slotFull = true;
    }

    private void Update()
    {
        // Velocidad del jugador, calculada a mano (por los raíles puede no tener Rigidbody)
        if (Time.deltaTime > 0f)
            playerVelocity = (player.position - lastPlayerPos) / Time.deltaTime;
        lastPlayerPos = player.position;

        // Lanzar el arma
        if (equipped && Keyboard.current != null && Keyboard.current[throwKey].wasPressedThisFrame)
        {
            Drop();
            return;
        }

        if (equipped || slotFull || Time.time < nextPickUpTime) return;

        // Se equipa sola cuando el jugador pasa dentro del rango
        float sqrDistance = (player.position - transform.position).sqrMagnitude;
        if (sqrDistance <= pickUpRange * pickUpRange)
            PickUp();
    }

    private void PickUp()
    {
        equipped = true;
        slotFull = true;

        // Hacer el arma hija del contenedor y colocarla en su posición por defecto
        transform.SetParent(gunContainer);
        transform.localPosition = Vector3.zero;
        transform.localRotation = Quaternion.identity;
        transform.localScale = Vector3.one;

        rb.isKinematic = true;
        coll.isTrigger = true;

        gunScript.enabled = true;
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