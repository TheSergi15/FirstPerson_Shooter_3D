using UnityEngine;

public class EnemyHealth : MonoBehaviour
{
    [Header("Atributos de Vida")]
    public float maxHealth = 30f; // Con nuestra arma que hace 10 de daño, aguantará 3 tiros
    private float currentHealth;

    void Start()
    {
        currentHealth = maxHealth;
    }

    // Esta función pública será llamada por nuestra pistola cuando le acierte un raycast
    public void TakeDamage(float amount)
    {
        currentHealth -= amount;
        Debug.Log($"¡{gameObject.name} recibió {amount} de daño! Vida restante: {currentHealth}");

        // Aquí más adelante podrás añadir efectos visuales, partículas de sangre o sonido

        if (currentHealth <= 0f)
        {
            Die();
        }
    }

    void Die()
    {
        Debug.Log($"¡{gameObject.name} ha muerto!");

        // --- NUEVO: Avisamos al RailManager de esta baja ---
        if (RailManager.Instance != null)
        {
            RailManager.Instance.AddKill();
        }

        // Eliminamos el objeto de la escena
        Destroy(gameObject);
    }
}