using System.Collections;
using UnityEngine;

public class Enemy : MonoBehaviour
{
    [Header("Vida")]
    [SerializeField] private float maxHealth = 100f;

    [Header("Feedback")]
    [SerializeField] private Renderer targetRenderer;      // Si se deja vacío, usa el del propio objeto
    [SerializeField] private Color hitColor = Color.red;
    [SerializeField] private float flashDuration = 0.1f;
    [SerializeField] private ParticleSystem deathEffect;

    [Header("Pruebas")]
    [SerializeField] private bool respawn = true;          // Reaparece para probar sin parar
    [SerializeField] private float respawnTime = 3f;
    [SerializeField] private bool logDamage = true;

    private float currentHealth;
    private Color originalColor;
    private Collider col;
    private Coroutine flashRoutine;

    public bool IsDead => currentHealth <= 0f;

    private void Awake()
    {
        col = GetComponent<Collider>();
        if (targetRenderer == null) targetRenderer = GetComponentInChildren<Renderer>();
        if (targetRenderer != null) originalColor = targetRenderer.material.color;
        currentHealth = maxHealth;
    }

    public void TakeDamage(float amount)
    {
        if (IsDead) return;

        currentHealth = Mathf.Max(currentHealth - amount, 0f);

        if (logDamage)
            Debug.Log($"{name} recibe {amount} de daño. Vida: {currentHealth}/{maxHealth}");

        if (flashRoutine != null) StopCoroutine(flashRoutine);
        flashRoutine = StartCoroutine(FlashRoutine());

        if (IsDead) Die();
    }

    private IEnumerator FlashRoutine()
    {
        if (targetRenderer != null) targetRenderer.material.color = hitColor;
        yield return new WaitForSeconds(flashDuration);
        if (targetRenderer != null) targetRenderer.material.color = originalColor;
    }

    private void Die()
    {
        if (deathEffect)
        {
            ParticleSystem fx = Instantiate(deathEffect, transform.position, Quaternion.identity);
            Destroy(fx.gameObject, 3f);
        }

        if (respawn) StartCoroutine(RespawnRoutine());
        else Destroy(gameObject);
    }

    private IEnumerator RespawnRoutine()
    {
        // Se oculta en vez de destruirse, para que el script siga ejecutándose
        if (targetRenderer != null) targetRenderer.enabled = false;
        if (col != null) col.enabled = false;

        yield return new WaitForSeconds(respawnTime);

        currentHealth = maxHealth;
        if (targetRenderer != null)
        {
            targetRenderer.material.color = originalColor;
            targetRenderer.enabled = true;
        }
        if (col != null) col.enabled = true;
    }
}