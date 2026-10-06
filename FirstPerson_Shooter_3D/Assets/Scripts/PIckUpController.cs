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

        //Hacer que el Rigidbody no sea kinematic y BoxCollider trigger
        rb.isKinematic = false;
        coll.isTrigger = false;

        //Deshabilita  script
        gunScript.enabled = false;
    }

}
