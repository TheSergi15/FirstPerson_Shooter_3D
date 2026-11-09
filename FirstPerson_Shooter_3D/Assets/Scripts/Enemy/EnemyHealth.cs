using UnityEngine;
using UnityEngine.UI;

public class EnemyHealth : MonoBehaviour
{
    [Header("Atributos de Vida")]
    public float maxHealth = 30f;

    [Header("Barra de vida (se crea por código)")]
    public bool showHealthBar = true;
    [Tooltip("Si está activo, la barra solo aparece cuando el enemigo recibe su primer daño.")]
    public bool hideUntilDamaged = false;
    [Tooltip("Ancho de la barra en metros. Súbelo para élites o jefes.")]
    public float barWidth = 1f;
    [Tooltip("Alto de la barra en metros.")]
    public float barHeight = 0.12f;
    [Tooltip("Separación entre la cabeza del enemigo y la barra.")]
    public float barOffsetAboveHead = 0.25f;
    [Tooltip("Velocidad a la que baja la barra (fracción de vida por segundo).")]
    public float barSmoothSpeed = 3f;

    public Color fullColor = new Color(0.25f, 0.9f, 0.3f);
    public Color midColor = new Color(1f, 0.85f, 0.2f);
    public Color lowColor = new Color(0.95f, 0.2f, 0.2f);
    public Color backgroundColor = new Color(0f, 0f, 0f, 0.65f);

    [Header("Sonidos (ids de la SoundLibrary, vacío = sin sonido)")]
    public string hitSfx = "";
    public string deathSfx = "";

    private float currentHealth;
    private bool isDead;

    private Transform barRoot;
    private Image fillImage;
    private CanvasGroup barGroup;
    private Vector3 barOffset;
    private float shownRatio = 1f;
    private Camera cam;

    private static Sprite whiteSprite;

    public float CurrentHealth => currentHealth;
    public float MaxHealth => maxHealth;
    public bool IsDead => isDead;

    private void Awake()
    {
        currentHealth = maxHealth;
    }

    private void Start()
    {
        // Se crea en Start (no en Awake) para que el ZoneSpawner ya haya colocado al enemigo sobre el suelo
        if (showHealthBar) CreateHealthBar();
    }

    // Esta función pública será llamada por el arma cuando le acierte un disparo
    public void TakeDamage(float amount)
    {
        if (isDead) return;

        currentHealth -= amount;
        Debug.Log($"¡{gameObject.name} recibió {amount} de daño! Vida restante: {currentHealth}");

        if (barGroup != null) barGroup.alpha = 1f;

        if (currentHealth <= 0f)
        {
            Die();
        }
        else if (!string.IsNullOrEmpty(hitSfx))
        {
            AudioManager.SfxAt(hitSfx, transform.position);
        }
    }

    private void Die()
    {
        // Con escopeta o ráfaga, varios impactos pueden llegar en el mismo frame:
        // sin esta guarda, la baja se contaría dos veces en el RailManager.
        if (isDead) return;
        isDead = true;

        Debug.Log($"¡{gameObject.name} ha muerto!");

        // Avisamos al RailManager de esta baja
        if (RailManager.Instance != null)
        {
            RailManager.Instance.AddKill();
        }

        if (!string.IsNullOrEmpty(deathSfx))
            AudioManager.SfxAt(deathSfx, transform.position);

        // Eliminamos el objeto de la escena (la barra se elimina en OnDestroy)
        Destroy(gameObject);
    }

    private void OnDestroy()
    {
        if (barRoot != null) Destroy(barRoot.gameObject);
    }

    // ───────────── Barra de vida ─────────────

    private void LateUpdate()
    {
        if (barRoot == null) return;

        // La barra NO es hija del enemigo (así no hereda su escala ni su giro): lo sigue cada frame
        barRoot.position = transform.position + barOffset;

        if (cam == null) cam = Camera.main;
        if (cam != null) barRoot.rotation = cam.transform.rotation;   // siempre de cara a la cámara

        float target = Mathf.Clamp01(currentHealth / Mathf.Max(0.01f, maxHealth));
        shownRatio = Mathf.MoveTowards(shownRatio, target, barSmoothSpeed * Time.deltaTime);

        fillImage.fillAmount = shownRatio;
        fillImage.color = EvaluateColor(shownRatio);
    }

    private Color EvaluateColor(float ratio)
    {
        return ratio > 0.5f
            ? Color.Lerp(midColor, fullColor, (ratio - 0.5f) * 2f)
            : Color.Lerp(lowColor, midColor, ratio * 2f);
    }

    private void CreateHealthBar()
    {
        barOffset = new Vector3(0f, GetTopY() - transform.position.y + barOffsetAboveHead, 0f);

        GameObject root = new GameObject(name + "_HealthBar", typeof(RectTransform));
        barRoot = root.transform;
        barRoot.position = transform.position + barOffset;

        Canvas canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;

        barGroup = root.AddComponent<CanvasGroup>();
        barGroup.alpha = hideUntilDamaged ? 0f : 1f;
        barGroup.interactable = false;
        barGroup.blocksRaycasts = false;

        // El canvas mide 100 px de ancho y se escala para ocupar 'barWidth' metros
        const float pixelWidth = 100f;
        RectTransform rt = (RectTransform)barRoot;
        rt.sizeDelta = new Vector2(pixelWidth, pixelWidth * barHeight / Mathf.Max(0.01f, barWidth));
        rt.localScale = Vector3.one * (barWidth / pixelWidth);

        Image background = CreateImage("Background", rt, backgroundColor);
        Stretch(background.rectTransform, 0f);

        Image fill = CreateImage("Fill", background.rectTransform, fullColor);
        Stretch(fill.rectTransform, 2f);
        fill.type = Image.Type.Filled;
        fill.fillMethod = Image.FillMethod.Horizontal;
        fill.fillOrigin = (int)Image.OriginHorizontal.Left;
        fill.fillAmount = 1f;

        fillImage = fill;
        shownRatio = Mathf.Clamp01(currentHealth / Mathf.Max(0.01f, maxHealth));
    }

    private static Image CreateImage(string imageName, RectTransform parent, Color color)
    {
        GameObject go = new GameObject(imageName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);

        Image img = go.GetComponent<Image>();
        img.sprite = WhiteSprite;
        img.color = color;
        img.raycastTarget = false;
        return img;
    }

    private static void Stretch(RectTransform rt, float padding)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(padding, padding);
        rt.offsetMax = new Vector2(-padding, -padding);
    }

    // Sprite blanco generado por código (el modo "Filled" de Image necesita un sprite)
    private static Sprite WhiteSprite
    {
        get
        {
            if (whiteSprite == null)
            {
                Texture2D t = Texture2D.whiteTexture;
                whiteSprite = Sprite.Create(t, new Rect(0f, 0f, t.width, t.height), new Vector2(0.5f, 0.5f), 100f);
            }
            return whiteSprite;
        }
    }

    // Punto más alto del enemigo: colliders sólidos o, si no hay, sus mallas
    private float GetTopY()
    {
        float top = float.MinValue;
        bool found = false;

        foreach (Collider c in GetComponentsInChildren<Collider>())
        {
            if (c.isTrigger || !c.enabled) continue;
            top = Mathf.Max(top, c.bounds.max.y);
            found = true;
        }

        if (!found)
        {
            foreach (Renderer r in GetComponentsInChildren<Renderer>())
            {
                if (!(r is MeshRenderer) && !(r is SkinnedMeshRenderer)) continue;
                top = Mathf.Max(top, r.bounds.max.y);
                found = true;
            }
        }

        return found ? top : transform.position.y + 2f;
    }
}