using UnityEngine;
using UnityEngine.UI;

// Ponlo en una Image que ocupe toda la pantalla (hija del Canvas).
// Hace un flash rojo al recibir daño, uno verde al curarse
// y un pulso rojo constante cuando queda poca vida.
[RequireComponent(typeof(Image))]
public class DamageFlash : MonoBehaviour
{
    [Header("Aspecto")]
    [Tooltip("Si la Image no tiene sprite, se genera una viñeta (bordes de color, centro transparente)")]
    public bool generateVignette = true;
    [Range(0f, 0.95f)] public float vignetteInnerRadius = 0.35f; // Más alto = centro más despejado
    public Color damageColor = new Color(0.85f, 0.05f, 0.05f, 1f);
    public Color healColor = new Color(0.2f, 0.9f, 0.3f, 1f);

    [Header("Flash de daño")]
    [Range(0f, 1f)] public float maxAlpha = 0.8f;
    public float flashInDuration = 0.06f;   // Lo rápido que aparece
    public float flashOutDuration = 0.5f;   // Lo que tarda en desvanecerse
    [Tooltip("Intensidad mínima aunque el daño sea pequeño")]
    [Range(0f, 1f)] public float minIntensity = 0.5f;
    [Tooltip("Porcentaje de vida máxima que quita un golpe para llegar a intensidad máxima (0.3 = 30%)")]
    [Range(0.05f, 1f)] public float fullIntensityAt = 0.3f;

    [Header("Flash de curación")]
    [Range(0f, 1f)] public float healAlpha = 0.35f;

    [Header("Vida baja")]
    [Tooltip("Por debajo de este porcentaje de vida empieza el pulso (0.3 = 30%)")]
    [Range(0.05f, 1f)] public float lowHealthThreshold = 0.3f;
    public Color lowHealthColor = new Color(0.6f, 0f, 0f, 1f);
    [Range(0f, 1f)] public float lowHealthMaxAlpha = 0.45f;
    public float pulseSpeed = 4f;

    private Image image;
    private Texture2D generatedTexture;
    private Sprite generatedSprite;

    private Color flashColor;
    private float flashAlpha;
    private float flashPeak;
    private bool attacking;

    private float healthPercent = 1f;
    private float pulseAlpha;

    void Awake()
    {
        image = GetComponent<Image>();
        image.raycastTarget = false; // Que no bloquee clics

        if (generateVignette && image.sprite == null)
        {
            image.sprite = CreateVignetteSprite();
        }

        flashColor = damageColor;
        image.color = Color.clear;
    }

    void OnDestroy()
    {
        if (generatedSprite != null) Destroy(generatedSprite);
        if (generatedTexture != null) Destroy(generatedTexture);
    }

    // ---------- API PÚBLICA ----------

    // damagePercent = daño recibido dividido entre la vida máxima (0 a 1)
    public void OnDamage(float damagePercent)
    {
        float intensity = Mathf.Lerp(minIntensity, 1f, Mathf.Clamp01(damagePercent / fullIntensityAt));
        Trigger(damageColor, intensity * maxAlpha);
    }

    public void OnHeal()
    {
        Trigger(healColor, healAlpha);
    }

    // healthPercent = vida actual dividida entre la vida máxima (0 a 1)
    public void SetHealthPercent(float percent)
    {
        healthPercent = Mathf.Clamp01(percent);
    }

    // ---------- INTERNO ----------

    void Trigger(Color color, float peakAlpha)
    {
        flashColor = color;
        flashPeak = Mathf.Max(flashAlpha, peakAlpha); // Un golpe nuevo no baja un flash que ya está fuerte
        attacking = true;
    }

    void Update()
    {
        // Tiempo sin escalar: el efecto sigue funcionando aunque el juego se pause al morir
        float dt = Time.unscaledDeltaTime;

        // 1) Flash: sube rápido hasta el pico y luego se desvanece
        if (attacking)
        {
            float riseSpeed = flashPeak / Mathf.Max(0.01f, flashInDuration);
            flashAlpha = Mathf.MoveTowards(flashAlpha, flashPeak, riseSpeed * dt);

            if (Mathf.Approximately(flashAlpha, flashPeak)) attacking = false;
        }
        else
        {
            float fallSpeed = Mathf.Max(flashPeak, 0.01f) / Mathf.Max(0.01f, flashOutDuration);
            flashAlpha = Mathf.MoveTowards(flashAlpha, 0f, fallSpeed * dt);
        }

        // 2) Pulso de vida baja: más fuerte cuanta menos vida queda
        float targetPulse = 0f;
        if (healthPercent <= lowHealthThreshold)
        {
            float severity = 1f - (healthPercent / lowHealthThreshold);
            float wave = Mathf.Sin(Time.unscaledTime * pulseSpeed) * 0.5f + 0.5f;
            targetPulse = lowHealthMaxAlpha * severity * Mathf.Lerp(0.4f, 1f, wave);
        }
        pulseAlpha = Mathf.Lerp(pulseAlpha, targetPulse, 10f * dt);

        // 3) Mezclamos ambos efectos en la misma Image
        float total = Mathf.Clamp01(flashAlpha + pulseAlpha);
        float flashWeight = flashAlpha / Mathf.Max(0.0001f, flashAlpha + pulseAlpha);

        Color final = Color.Lerp(lowHealthColor, flashColor, flashWeight);
        final.a = total;
        image.color = final;
    }

    // Viñeta: transparente en el centro y opaca hacia los bordes
    Sprite CreateVignetteSprite()
    {
        const int size = 128;

        generatedTexture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        generatedTexture.wrapMode = TextureWrapMode.Clamp;
        generatedTexture.filterMode = FilterMode.Bilinear;

        Color[] pixels = new Color[size * size];
        float inner = Mathf.Min(vignetteInnerRadius, 0.99f);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = (x / (size - 1f) - 0.5f) * 2f;
                float dy = (y / (size - 1f) - 0.5f) * 2f;
                float d = Mathf.Sqrt(dx * dx + dy * dy) / 1.41421356f; // 0 en el centro, 1 en las esquinas

                float a = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(inner, 1f, d));
                pixels[y * size + x] = new Color(1f, 1f, 1f, a);
            }
        }

        generatedTexture.SetPixels(pixels);
        generatedTexture.Apply();

        generatedSprite = Sprite.Create(generatedTexture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
        return generatedSprite;
    }
}

