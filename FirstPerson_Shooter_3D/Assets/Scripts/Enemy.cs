using System.Collections;
using UnityEngine;

public class Enemy : MonoBehaviour
{
    [Header("Health")]
    public float maxHealth = 100f;

    [Header("Hit Feedback")]
    public Color hitColor = Color.red;
    public float flashDuration = 0.08f;

    private float currentHealth;
    private Renderer enemyRenderer;
    private Color originalColor;

    private void Awake()
    {
        currentHealth = maxHealth;
        enemyRenderer = GetComponentInChildren<Renderer>();

        if (enemyRenderer != null)
            originalColor = enemyRenderer.material.color;
    }

    public void TakeDamage(float amount)
    {
        currentHealth -= amount;
        Debug.Log($"{name} took {amount} damage. Health: {currentHealth}/{maxHealth}");

        if (enemyRenderer != null)
        {
            StopAllCoroutines();
            StartCoroutine(FlashRoutine());
        }

        if (currentHealth <= 0f)
            Die();
    }

    private IEnumerator FlashRoutine()
    {
        enemyRenderer.material.color = hitColor;
        yield return new WaitForSeconds(flashDuration);
        enemyRenderer.material.color = originalColor;
    }

    private void Die()
    {
        Debug.Log($"{name} died");
        Destroy(gameObject);
    }
}