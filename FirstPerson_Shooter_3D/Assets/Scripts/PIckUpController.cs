using UnityEngine;
using UnityEngine.InputSystem;

public class PickUpController : MonoBehaviour
{
    public ProjectileGun gunScript;
    public Rigidbody rb;
    public BoxCollider coll;
    public Transform player, gunContainer, fpsCam;

    public float pickUpRange;
    public float dropForwardForce, dropUpwardForce;

    public bool equipped;
    public static bool slotFull;

    private void Start()
    {
        // Setup
        gunScript.enabled = equipped;
        rb.isKinematic = equipped;
        coll.isTrigger = equipped;

        if (equipped) slotFull = true;
    }

    private void Update()
    {
        if (Keyboard.current == null) return;

        // Comprueba si el jugador está en rango y la "E" se ha pulsado
        Vector3 distanceToPlayer = player.position - transform.position;
        if (!equipped && distanceToPlayer.magnitude <= pickUpRange
            && Keyboard.current.eKey.wasPressedThisFrame && !slotFull)
        {
            PickUp();
        }

        // Suelta el arma si está equipada y se pulsa la "Q"
        if (equipped && Keyboard.current.qKey.wasPressedThisFrame)
        {
            Drop();
        }
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

    private void Drop()
    {
        equipped = false;
        slotFull = false;

        transform.SetParent(null);

        rb.isKinematic = false;
        coll.isTrigger = false;

        // El arma hereda la velocidad del jugador (si tiene Rigidbody)
        if (player.TryGetComponent(out Rigidbody playerRb))
        {
            rb.linearVelocity = playerRb.linearVelocity;
        }

        // Fuerza de lanzamiento
        rb.AddForce(fpsCam.forward * dropForwardForce, ForceMode.Impulse);
        rb.AddForce(fpsCam.up * dropUpwardForce, ForceMode.Impulse);

        // Rotación aleatoria
        rb.AddTorque(new Vector3(
            Random.Range(-1f, 1f),
            Random.Range(-1f, 1f),
            Random.Range(-1f, 1f)) * 10f);

        gunScript.enabled = false;
    }
}