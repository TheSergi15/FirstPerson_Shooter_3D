using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class PlayerHealth : MonoBehaviour
{
    public static PlayerHealth Instance;

    [Header("Salud")]
    public float maxHealth = 100f;
    private float currentHealth;

    [Header("Interfaz de Usuario (UI)")]
    public TextMeshProUGUI healthText;

    [Header("Efectos de Daño y Curación en UI")]
    public Image damageFlash;
    public float flashSpeed = 5f;
    public Color flashColor = new Color(1f, 0f, 0f, 0.5f); // Rojo para daño

    // --- NUEVAS VARIABLES DE CURACIÓN ---
    public Color healColor = new Color(0f, 1f, 0f, 0.5f);  // Verde para curación
    private bool isHealed;
    // ------------------------------------

    private bool isDamaged;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    void Start()
    {
        currentHealth = maxHealth;
        if (damageFlash != null) damageFlash.color = Color.clear;

        UpdateHealthUI();
    }

    void Update()
    {
        // Priorizamos mostrar el daño visualmente
        if (isDamaged && damageFlash != null)
        {
            damageFlash.color = flashColor;
        }
        // Si no hay daño pero sí curación, pintamos de verde
        else if (isHealed && damageFlash != null)
        {
            damageFlash.color = healColor;
        }
        // Desvanecemos el color (sea rojo o verde) hacia transparente[cite: 2]
        else if (damageFlash != null)
        {
            damageFlash.color = Color.Lerp(damageFlash.color, Color.clear, flashSpeed * Time.deltaTime);
        }

        // Reseteamos ambos estados
        isDamaged = false;
        isHealed = false;
    }

    public void TakeDamage(float amount)
    {
        currentHealth -= amount;
        isDamaged = true; // Activa el flash rojo[cite: 2]

        UpdateHealthUI();

        if (currentHealth <= 0)
        {
            currentHealth = 0;
            UpdateHealthUI();
            Die();
        }
    }

    // --- FUNCIÓN DE CURACIÓN ACTUALIZADA ---
    public void Heal(float amount)
    {
        currentHealth += amount;

        if (currentHealth > maxHealth)
        {
            currentHealth = maxHealth;
        }

        isHealed = true; // Activa el flash verde
        UpdateHealthUI();
        Debug.Log("¡Vida recuperada! Salud actual: " + currentHealth);
    }

    void UpdateHealthUI()
    {
        if (healthText != null)
        {
            healthText.text = "VIDA: " + currentHealth;
        }
    }

    void Die()
    {
        Debug.Log("¡HAS MUERTO! Fin del juego.");
    }
}