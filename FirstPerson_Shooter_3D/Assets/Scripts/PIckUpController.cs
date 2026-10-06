using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class PIckUpController : MonoBehaviour
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
        //Setup
        if (!equipped)
        {
            gunScript.enabled = false;
            rb.isKinematic = false;
            coll.isTrigger = false;
        }
        if (equipped)
        {
            gunScript.enabled = true;
            rb.isKinematic = true;
            coll.isTrigger = true;
            slotFull = true;
        }
    }


    private void Update()
    {
        //Comprueba si el jugador está en el rango de alcance y la "E" esta presionada
        Vector3 distanceToPlayer = player.position - transform.position;
        if (!equipped && distanceToPlayer.magnitude <= pickUpRange && Input.GetKeyDown(KeyCode.E) && !slotFull) PickUp();

        //Suelta el arma si está equipada y la "Q" está presionada
        if (equipped && Input.GetKeyDown(KeyCode.Q)) Drop();
    }


    private void PickUp()
    {
        equipped = true;
        slotFull = true;

        //Hacer el arma un hijo de la cámara y moverlo a la posición inicial por defecto 
        transform.SetParent(gunContainer);
        transform.localPosition = Vector3.zero;
        transform.localRotation = Quaternion.Euler(Vector3.zero);
        transform.localScale = Vector3.one;

        //Hacer que el Rigidbody sea kinematic y BoxCollider trigger
        rb.isKinematic = true;
        coll.isTrigger = true;

        //Habilita  script
        gunScript.enabled = true;

    }


    private void Drop()
    {
        equipped = false;
        slotFull = false;

        //Set Parent to null
        transform.SetParent(null);

        //Hacer que el Rigidbody no sea kinematic y BoxCollider trigger
        rb.isKinematic = false;
        coll.isTrigger = false;

        //Arma recoge el momento del player 
        rb.linearVelocity = player.GetComponent<Rigidbody>().linearVelocity;

        //Aplica Fuerza
        rb.AddForce(fpsCam.forward * dropForwardForce, ForceMode.Impulse);
        rb.AddForce(fpsCam.up * dropUpwardForce, ForceMode.Impulse);
        //Add random rotation
        float random = Random.Range(-1f, 1f);
        rb.AddTorque(new Vector3(random, random, random) * 10);



        //Deshabilita  script
        gunScript.enabled = false;
    }

}
