using UnityEngine;

public enum WeaponType { Shotgun, MachineGun }

public class WeaponPickup : MonoBehaviour
{
    [Header("Configuración del Arma")]
    public WeaponType weaponType = WeaponType.Shotgun;
    public int ammoAmount = 6;
    public float weaponDamage = 15f;

    [Header("Efectos")]
    public GameObject pickupEffect;

    // Se llama cuando el Raycast del jugador impacta este ítem
    public void OnShot(GunController playerGun)
    {
        if (playerGun != null)
        {
            playerGun.EquipSpecialWeapon(weaponType, ammoAmount, weaponDamage);
        }

        if (pickupEffect != null)
        {
            Instantiate(pickupEffect, transform.position, Quaternion.identity);
        }

        Destroy(gameObject);
    }
}