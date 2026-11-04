using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Cartel de "TUTORIAL COMPLETADO". Construye él solo toda la interfaz con un diseño ordenado:
/// marco doble dorado, icono de check, título, línea decorativa, subtítulo y aviso de continuar
/// con barra de cuenta atrás. Aparece con una animación escalonada.
///
/// Uso: pon este script en un objeto hijo del Canvas (puede estar desactivado y vacío).
/// No hay que crear textos ni imágenes a mano: los hijos que tuviera se retiran al empezar.
/// Continúa con ENTER / ESPACIO o automáticamente al acabar la cuenta atrás.
/// </summary>
public class TutorialCompletePanel : MonoBehaviour
{
    [Header("Textos")]
    [SerializeField] private string title = "¡TUTORIAL COMPLETADO!";
    [SerializeField] private string subtitle = "Estás listo para el Nivel 1";
    [SerializeField] private string continueText = "Pulsa <b>ENTER</b> para continuar";

    [Header("Estilo")]
    [SerializeField] private Color accentColor = new Color(0.949f, 0.714f, 0.196f);   // dorado #F2B632
    [SerializeField] private Color panelColor = new Color(0.055f, 0.086f, 0.129f);    // azul oscuro #0E1621
    [SerializeField] private Color backdropColor = new Color(0f, 0f, 0f, 0.65f);
    [SerializeField] private Color textColor = new Color(0.92f, 0.94f, 0.97f);
    [SerializeField] private Vector2 frameSize = new Vector2(900f, 470f);
    [SerializeField] private TMP_FontAsset titleFont;   // opcional
    [SerializeField] private TMP_FontAsset bodyFont;    // opcional

    [Header("Comportamiento")]
    [Tooltip("Si se deja vacío se busca en la escena")]
    [SerializeField] private NextLevelLoader loader;
    [SerializeField] private float minWaitBeforeInput = 0.5f;
    [SerializeField] private float autoContinueDelay = 10f;
    [SerializeField] private AudioClip showSound;
    [Tooltip("Retira los hijos que ya tuviera este objeto (el cartel antiguo hecho a mano)")]
    [SerializeField] private bool clearExistingChildren = true;

    // Elementos creados
    private bool built, shown, ready;
    private float readyAt, uiScale = 1f;
    private CanvasGroup rootGroup, iconGroup, titleGroup, subGroup, contGroup, barGroup;
    private RectTransform frame, content, icon, dividerLine, barFill;
    private TextMeshProUGUI titleLabel;
    private Sprite iconSprite;
    private Texture2D iconTexture;

    private const float TitleSpacing = 10f;

    private void Awake()
    {
        EnsureBuilt();
    }

    private void OnDestroy()
    {
        if (iconSprite != null) Destroy(iconSprite);
        if (iconTexture != null) Destroy(iconTexture);
    }

    // ───────── API ─────────
    public void Show()
    {
        if (shown) return;
        shown = true;

        EnsureBuilt();
        gameObject.SetActive(true);
        transform.SetAsLastSibling();   // por encima del resto del HUD
        StartCoroutine(Sequence());
    }

    // ───────── Construcción de la interfaz ─────────
    private void EnsureBuilt()
    {
        if (built) return;
        built = true;

        if (clearExistingChildren)
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                GameObject child = transform.GetChild(i).gameObject;
                child.SetActive(false);
                Destroy(child);
            }
        }

        // Raíz: ocupa toda la pantalla, con fondo oscuro
        RectTransform root = (RectTransform)transform;
        root.anchorMin = Vector2.zero;
        root.anchorMax = Vector2.one;
        root.offsetMin = Vector2.zero;
        root.offsetMax = Vector2.zero;

        Image backdrop = GetComponent<Image>();
        if (backdrop == null) backdrop = gameObject.AddComponent<Image>();
        backdrop.color = backdropColor;
        backdrop.raycastTarget = false;

        rootGroup = GetComponent<CanvasGroup>();
        if (rootGroup == null) rootGroup = gameObject.AddComponent<CanvasGroup>();
        rootGroup.alpha = 0f;
        rootGroup.interactable = false;
        rootGroup.blocksRaycasts = false;

        // Escala: si el Canvas no escala con la pantalla, se adapta a la altura (referencia 1080)
        Canvas canvas = GetComponentInParent<Canvas>();
        CanvasScaler scaler = canvas != null ? canvas.GetComponent<CanvasScaler>() : null;
        bool needsOwnScale = scaler == null || scaler.uiScaleMode == CanvasScaler.ScaleMode.ConstantPixelSize;
        uiScale = needsOwnScale ? Mathf.Clamp(Screen.height / 1080f, 0.5f, 3f) : 1f;

        // Marco doble: borde dorado -> relleno -> línea fina interior -> relleno
        frame = NewRect("Frame", transform);
        frame.anchorMin = frame.anchorMax = new Vector2(0.5f, 0.5f);
        frame.sizeDelta = frameSize;
        AddImage(frame, accentColor);

        RectTransform fill = NewRect("Fill", frame);
        Stretch(fill, 4f);
        AddImage(fill, panelColor);

        RectTransform innerLine = NewRect("InnerLine", fill);
        Stretch(innerLine, 14f);
        AddImage(innerLine, new Color(accentColor.r, accentColor.g, accentColor.b, 0.35f));

        RectTransform innerFill = NewRect("InnerFill", innerLine);
        Stretch(innerFill, 1.5f);
        AddImage(innerFill, panelColor);

        // Contenido: columna centrada con espaciado fijo
        content = NewRect("Content", innerFill);
        Stretch(content, 0f);

        VerticalLayoutGroup layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(40, 40, 34, 26);
        layout.spacing = 14f;
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;

        float innerWidth = frameSize.x - 2f * (4f + 14f + 1.5f) - layout.padding.left - layout.padding.right;

        // 1) Icono de check
        icon = NewRect("Icon", content);
        Image iconImage = AddImage(icon, accentColor);
        iconImage.sprite = BuildCheckSprite();
        iconImage.preserveAspect = true;
        AddLayout(icon.gameObject, 110f, 110f);
        iconGroup = AddGroup(icon.gameObject);

        // 2) Título
        RectTransform titleRect = NewRect("Title", content);
        titleLabel = AddText(titleRect, title, 58f, accentColor, FontStyles.Bold, titleFont);
        titleLabel.characterSpacing = TitleSpacing;
        titleLabel.textWrappingMode = TextWrappingModes.NoWrap;
        titleLabel.enableAutoSizing = true;
        titleLabel.fontSizeMin = 30f;
        titleLabel.fontSizeMax = 58f;
        AddLayout(titleRect.gameObject, innerWidth, 84f);
        titleGroup = AddGroup(titleRect.gameObject);

        // 3) Línea decorativa (se dibuja de dentro hacia fuera)
        RectTransform divider = NewRect("Divider", content);
        AddLayout(divider.gameObject, 560f, 3f);
        dividerLine = NewRect("Line", divider);
        Stretch(dividerLine, 0f);
        AddImage(dividerLine, accentColor);
        dividerLine.localScale = new Vector3(0f, 1f, 1f);

        // 4) Subtítulo
        RectTransform subRect = NewRect("Subtitle", content);
        TextMeshProUGUI sub = AddText(subRect, subtitle, 30f, textColor, FontStyles.Normal, bodyFont);
        sub.enableAutoSizing = true;
        sub.fontSizeMin = 18f;
        sub.fontSizeMax = 30f;
        AddLayout(subRect.gameObject, innerWidth, 50f);
        subGroup = AddGroup(subRect.gameObject);

        // Espacio flexible: empuja el pie hacia abajo
        RectTransform spacer = NewRect("Spacer", content);
        LayoutElement spacerLE = spacer.gameObject.AddComponent<LayoutElement>();
        spacerLE.flexibleHeight = 1f;

        // 5) Aviso de continuar + barra de cuenta atrás
        RectTransform contRect = NewRect("Continue", content);
        TextMeshProUGUI cont = AddText(contRect, continueText, 24f,
            new Color(textColor.r, textColor.g, textColor.b, 0.9f), FontStyles.Normal, bodyFont);
        cont.richText = true;
        AddLayout(contRect.gameObject, innerWidth, 34f);
        contGroup = AddGroup(contRect.gameObject);

        RectTransform bar = NewRect("Bar", content);
        AddImage(bar, new Color(1f, 1f, 1f, 0.12f));
        AddLayout(bar.gameObject, 360f, 3f);
        barGroup = AddGroup(bar.gameObject);

        barFill = NewRect("Fill", bar);
        Stretch(barFill, 0f);
        barFill.pivot = new Vector2(0f, 0.5f);   // se vacía hacia la izquierda
        AddImage(barFill, new Color(accentColor.r, accentColor.g, accentColor.b, 0.85f));

        frame.localScale = Vector3.one * uiScale;
    }

    // ───────── Animación de entrada ─────────
    private IEnumerator Sequence()
    {
        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(content);

        if (showSound != null && Camera.main != null)
            AudioSource.PlayClipAtPoint(showSound, Camera.main.transform.position);

        // Fondo y marco
        yield return Tween(0.45f, k =>
        {
            k = Smooth(k);
            rootGroup.alpha = k;
            frame.localScale = Vector3.one * uiScale * Mathf.Lerp(0.9f, 1f, k);
        });

        // Icono con pequeño rebote
        yield return Tween(0.5f, k =>
        {
            iconGroup.alpha = Mathf.Clamp01(k * 2.5f);
            icon.localScale = Vector3.one * Mathf.LerpUnclamped(0.3f, 1f, OutBack(k));
        });

        // Título: las letras se juntan mientras aparece
        yield return Tween(0.6f, k =>
        {
            float e = Smooth(k);
            titleGroup.alpha = e;
            titleLabel.characterSpacing = Mathf.Lerp(45f, TitleSpacing, e);
        });

        // Línea decorativa
        yield return Tween(0.4f, k => dividerLine.localScale = new Vector3(Smooth(k), 1f, 1f));

        // Subtítulo
        yield return Tween(0.45f, k => subGroup.alpha = Smooth(k));

        // Aviso de continuar
        yield return Tween(0.4f, k =>
        {
            float e = Smooth(k);
            contGroup.alpha = e;
            barGroup.alpha = e;
        });

        ready = true;
        readyAt = Time.unscaledTime;
    }

    private void Update()
    {
        if (!ready) return;

        float elapsed = Time.unscaledTime - readyAt;
        float remaining = autoContinueDelay - elapsed;

        // Pulso suave del aviso y barra que se vacía
        contGroup.alpha = 0.6f + 0.4f * (0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 3f));
        barFill.localScale = new Vector3(Mathf.Clamp01(remaining / autoContinueDelay), 1f, 1f);

        Keyboard kb = Keyboard.current;
        bool pressed = kb != null &&
            (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame);

        if ((pressed && elapsed >= minWaitBeforeInput) || remaining <= 0f)
        {
            ready = false;
            if (loader == null) loader = FindFirstObjectByType<NextLevelLoader>();
            if (loader != null) loader.LoadNext();
            else Debug.LogError("[TutorialCompletePanel] No hay NextLevelLoader en la escena.", this);
        }
    }

    // ───────── Utilidades de animación ─────────
    private static IEnumerator Tween(float duration, Action<float> apply)
    {
        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            apply(Mathf.Clamp01(t / duration));
            yield return null;
        }
        apply(1f);
    }

    private static float Smooth(float k) => k * k * (3f - 2f * k);

    private static float OutBack(float k)
    {
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;
        float x = k - 1f;
        return 1f + c3 * x * x * x + c1 * x * x;
    }

    // ───────── Utilidades de construcción ─────────
    private static RectTransform NewRect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    private static void Stretch(RectTransform r, float inset)
    {
        r.anchorMin = Vector2.zero;
        r.anchorMax = Vector2.one;
        r.offsetMin = new Vector2(inset, inset);
        r.offsetMax = new Vector2(-inset, -inset);
    }

    private static Image AddImage(RectTransform r, Color color)
    {
        Image img = r.gameObject.AddComponent<Image>();
        img.color = color;
        img.raycastTarget = false;
        return img;
    }

    private static void AddLayout(GameObject go, float width, float height)
    {
        LayoutElement le = go.AddComponent<LayoutElement>();
        le.preferredWidth = width;
        le.preferredHeight = height;
        le.flexibleWidth = 0f;
        le.flexibleHeight = 0f;
    }

    private static CanvasGroup AddGroup(GameObject go)
    {
        CanvasGroup g = go.AddComponent<CanvasGroup>();
        g.alpha = 0f;
        g.interactable = false;
        g.blocksRaycasts = false;
        return g;
    }

    private static TextMeshProUGUI AddText(RectTransform r, string text, float size, Color color,
                                           FontStyles style, TMP_FontAsset font)
    {
        TextMeshProUGUI t = r.gameObject.AddComponent<TextMeshProUGUI>();
        t.text = text;
        t.fontSize = size;
        t.color = color;
        t.fontStyle = style;
        t.alignment = TextAlignmentOptions.Center;
        t.raycastTarget = false;
        if (font != null) t.font = font;
        return t;
    }

    // ───────── Icono generado por código (círculo con check) ─────────
    private Sprite BuildCheckSprite()
    {
        const int size = 256;
        iconTexture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        iconTexture.wrapMode = TextureWrapMode.Clamp;
        iconTexture.filterMode = FilterMode.Bilinear;

        Vector2 p1 = new Vector2(0.29f, 0.52f);
        Vector2 p2 = new Vector2(0.43f, 0.36f);
        Vector2 p3 = new Vector2(0.72f, 0.68f);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                Vector2 p = new Vector2((x + 0.5f) / size, (y + 0.5f) / size);
                float d = Vector2.Distance(p, new Vector2(0.5f, 0.5f));

                float ring = Smooth(Mathf.InverseLerp(0.44f, 0.455f, d)) * (1f - Smooth(Mathf.InverseLerp(0.495f, 0.5f, d)));
                float fill = 0.12f * (1f - Smooth(Mathf.InverseLerp(0.44f, 0.455f, d)));

                float cd = Mathf.Min(SegmentDistance(p, p1, p2), SegmentDistance(p, p2, p3));
                float check = 1f - Smooth(Mathf.InverseLerp(0.03f, 0.04f, cd));

                float a = Mathf.Clamp01(Mathf.Max(ring, Mathf.Max(fill, check)));
                iconTexture.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        }
        iconTexture.Apply();

        iconSprite = Sprite.Create(iconTexture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        return iconSprite;
    }

    private static float SegmentDistance(Vector2 p, Vector2 a, Vector2 b)
    {
        Vector2 ab = b - a;
        float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
        return Vector2.Distance(p, a + ab * t);
    }
}