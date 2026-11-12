using System.Collections;
using UnityEngine;

/// <summary>
/// Reacciones de un enemigo 2D sin animaciones propias de daño ni muerte:
/// parpadeo rojo al recibir un impacto y caída con fundido al morir.
/// </summary>
public class EnemyFeedback2D : MonoBehaviour
{
    [Tooltip("El SpriteRenderer del hijo Visual (si está vacío, lo busca solo)")]
    [SerializeField] private SpriteRenderer sprite;
    [SerializeField] private Color hitColor = new Color(1f, 0.3f, 0.3f);
    [SerializeField] private float hitFlashTime = 0.1f;

    [Header("Muerte")]
    [SerializeField] private float deathTime = 0.5f;
    [SerializeField] private float deathFallDistance = 0.5f;

    private Color baseColor;
    private Coroutine flashRoutine;
    private bool dying;

    private void Awake()
    {
        if (sprite == null) sprite = GetComponentInChildren<SpriteRenderer>();
        baseColor = sprite.color;
    }

    /// <summary>Parpadeo rojo corto al recibir daño.</summary>
    public void PlayHit()
    {
        if (dying) return;
        if (flashRoutine != null) StopCoroutine(flashRoutine);
        flashRoutine = StartCoroutine(Flash());
    }

    /// <summary>Inicia la muerte y devuelve cuánto dura, para destruir el objeto después.</summary>
    public float PlayDeath()
    {
        if (dying) return deathTime;
        dying = true;
        if (flashRoutine != null) StopCoroutine(flashRoutine);
        StartCoroutine(Death());
        return deathTime;
    }

    private IEnumerator Flash()
    {
        sprite.color = hitColor;
        yield return new WaitForSeconds(hitFlashTime);
        sprite.color = baseColor;
        flashRoutine = null;
    }

    private IEnumerator Death()
    {
        Transform t = sprite.transform;
        Vector3 startPos = t.localPosition;
        Vector3 startScale = t.localScale;

        float time = 0f;
        while (time < deathTime)
        {
            time += Time.deltaTime;
            float k = Mathf.Clamp01(time / deathTime);

            t.localPosition = startPos + Vector3.down * deathFallDistance * k;
            t.localScale = Vector3.Lerp(startScale, startScale * 0.6f, k);

            Color c = hitColor;
            c.a = 1f - k;
            sprite.color = c;

            yield return null;
        }
    }
}