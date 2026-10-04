using UnityEngine;

public class HealthPickup : MonoBehaviour
{
    [Tooltip("Cantidad de vida que restaura al jugador")]
    public float healAmount = 20f;

    [Tooltip("Efecto visual opcional (brillo/chispas verdes) al dispararle")]
    public GameObject collectEffect;

    // Esta función será llamada por nuestra pistola cuando le acierte el raycast
    public void OnShot()
    {
        if (PlayerHealth.Instance != null)
        {
            // Usamos el Singleton para curar al instante[cite: 3]
            PlayerHealth.Instance.Heal(healAmount);
        }

        if (collectEffect != null)
        {
            Instantiate(collectEffect, transform.position, Quaternion.identity);
        }

        Destroy(gameObject);
    }
}