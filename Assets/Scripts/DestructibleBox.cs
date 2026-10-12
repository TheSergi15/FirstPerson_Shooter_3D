using UnityEngine;

public class DestructibleBox : MonoBehaviour
{
    [Header("Recompensa")]
    [Tooltip("Prefab del arma que soltará la caja al romperse")]
    public GameObject weaponPickupPrefab;

    [Header("Efectos")]
    public GameObject breakEffectPrefab;

    public void BreakBox()
    {
        // 1. Instanciamos el arma en la posición de la caja
        if (weaponPickupPrefab != null)
        {
            Instantiate(weaponPickupPrefab, transform.position, Quaternion.identity);
        }

        // 2. Efecto visual de destrucción (madera rota, partículas)
        if (breakEffectPrefab != null)
        {
            Instantiate(breakEffectPrefab, transform.position, Quaternion.identity);
        }

        // 3. Destruimos la caja
        Destroy(gameObject);
    }
}