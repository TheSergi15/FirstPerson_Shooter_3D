using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Barra de inventario de armas en pantalla. Se construye por código: no hace falta crear
/// prefabs ni ranuras a mano. Cada arma recogida aparece con su imagen, su nombre y la tecla
/// que la selecciona; la equipada se resalta. Solo muestra las armas que llevas encima.
/// </summary>
public class WeaponInventoryUI : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField] private WeaponSwitcher switcher;   // Si se deja vacío se busca en la escena

    [Header("Posición y tamaño")]
    [SerializeField] private Vector2 anchoredPosition = new Vector2(24f, 24f);  // Desde la esquina inferior izquierda
    [SerializeField] private Vector2 slotSize = new Vector2(160f, 100f);
    [SerializeField] private float spacing = 14f;

    [Header("Estilo")]
    [SerializeField] private Color slotColor = new Color(0.07f, 0.08f, 0.10f, 0.70f);
    [SerializeField] private Color selectedSlotColor = new Color(0.16f, 0.14f, 0.07f, 0.90f);
    [SerializeField] private Color frameColor = new Color(1f, 0.78f, 0.2f, 1f);
    [SerializeField] private Color textColor = Color.white;
    [SerializeField, Range(0.4f, 1f)] private float unselectedBrightness = 0.65f;
    [SerializeField] private bool showLockedSlots = false;   // Muestra también las armas aún no recogidas (apagadas)

    [Header("Animación")]
    [SerializeField] private float selectedScale = 1.15f;
    [SerializeField] private float animSpeed = 14f;
    [SerializeField] private float popDuration = 0.45f;      // Entrada de un arma recién recogida
    [SerializeField] private float punchAmount = 0.12f;      // Rebote al equipar

    private const float FrameThickness = 4f;

    private class Slot
    {
        public RectTransform root;
        public Image frame, background, icon;
        public TextMeshProUGUI nameLabel, keyLabel;
        public bool selected, unlocked;
        public float selectedT, punch;
        public float popTime = 1f;   // 0 -> 1: animación de entrada
    }

    private readonly List<Slot> slots = new List<Slot>();
    private GameObject ownedCanvas;   // Solo si no había Canvas y hemos creado uno

    // ---------- CICLO DE VIDA ----------
    private void OnEnable()
    {
        if (switcher == null) return;
        Subscribe();
    }

    private void OnDisable()
    {
        if (switcher == null) return;
        Unsubscribe();
    }

    private void Start()
    {
        if (switcher == null)
        {
            switcher = FindFirstObjectByType<WeaponSwitcher>();
            if (switcher == null)
            {
                Debug.LogError("[WeaponInventoryUI] No hay ningún WeaponSwitcher en la escena.", this);
                enabled = false;
                return;
            }
            Subscribe();
        }

        Build();
        Refresh();
    }

    private void OnDestroy()
    {
        if (ownedCanvas != null) Destroy(ownedCanvas);
    }

    private void Subscribe()
    {
        Unsubscribe();   // evita suscribirse dos veces
        switcher.OnWeaponChanged += HandleWeaponChanged;
        switcher.OnWeaponUnlocked += HandleWeaponUnlocked;
        switcher.OnSlotsChanged += Refresh;
    }

    private void Unsubscribe()
    {
        switcher.OnWeaponChanged -= HandleWeaponChanged;
        switcher.OnWeaponUnlocked -= HandleWeaponUnlocked;
        switcher.OnSlotsChanged -= Refresh;
    }

    // ---------- EVENTOS ----------
    private void HandleWeaponChanged(int index, WeaponSwitcher.WeaponEntry entry)
    {
        if (index >= 0 && index < slots.Count) slots[index].punch = punchAmount;
        Refresh();
    }

    private void HandleWeaponUnlocked(int index)
    {
        if (index >= 0 && index < slots.Count) slots[index].popTime = 0f;   // arranca la animación de entrada
    }

    // ---------- CONSTRUCCIÓN ----------
    private void Build()
    {
        Transform canvasParent = GetCanvasTransform();

        var rootGO = new GameObject("WeaponInventory", typeof(RectTransform),
                                    typeof(HorizontalLayoutGroup), typeof(ContentSizeFitter));
        rootGO.transform.SetParent(canvasParent, false);

        var rootRT = (RectTransform)rootGO.transform;
        rootRT.anchorMin = rootRT.anchorMax = new Vector2(0f, 0f);     // esquina inferior izquierda
        rootRT.pivot = new Vector2(0f, 0f);
        rootRT.anchoredPosition = anchoredPosition;

        var layout = rootGO.GetComponent<HorizontalLayoutGroup>();
        layout.spacing = spacing;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;

        var fitter = rootGO.GetComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var weapons = switcher.Weapons;
        for (int i = 0; i < weapons.Count; i++)
            slots.Add(CreateSlot(rootRT, weapons[i], i));
    }

    // Usa el Canvas en el que esté este objeto; si no hay ninguno, crea uno propio
    private Transform GetCanvasTransform()
    {
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas != null) return canvas.rootCanvas.transform;

        ownedCanvas = new GameObject("WeaponInventoryCanvas", typeof(Canvas), typeof(CanvasScaler));
        var c = ownedCanvas.GetComponent<Canvas>();
        c.renderMode = RenderMode.ScreenSpaceOverlay;

        var scaler = ownedCanvas.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        return ownedCanvas.transform;
    }

    private Slot CreateSlot(Transform parent, WeaponSwitcher.WeaponEntry entry, int index)
    {
        var slot = new Slot();

        var rootGO = new GameObject($"Slot_{index + 1}_{entry.displayName}", typeof(RectTransform), typeof(LayoutElement));
        rootGO.transform.SetParent(parent, false);
        slot.root = (RectTransform)rootGO.transform;

        var le = rootGO.GetComponent<LayoutElement>();
        le.preferredWidth = slotSize.x;
        le.preferredHeight = slotSize.y;

        // Marco que se ilumina en el arma equipada
        slot.frame = CreateImage(slot.root, "Frame", frameColor);
        Stretch(slot.frame.rectTransform, new Vector2(-FrameThickness, -FrameThickness), new Vector2(FrameThickness, FrameThickness));

        // Fondo
        slot.background = CreateImage(slot.root, "Background", slotColor);
        Stretch(slot.background.rectTransform, Vector2.zero, Vector2.zero);

        // Imagen del arma
        slot.icon = CreateImage(slot.root, "Icon", Color.white);
        slot.icon.preserveAspect = true;
        slot.icon.sprite = entry.icon;
        slot.icon.enabled = entry.icon != null;
        Stretch(slot.icon.rectTransform, new Vector2(12f, 28f), new Vector2(-12f, -12f));

        // Nombre
        slot.nameLabel = CreateLabel(slot.root, "Name", textColor);
        slot.nameLabel.text = entry.displayName;
        slot.nameLabel.alignment = TextAlignmentOptions.Center;
        slot.nameLabel.fontStyle = FontStyles.Bold;
        slot.nameLabel.enableAutoSizing = true;

        var nameRT = slot.nameLabel.rectTransform;
        if (entry.icon != null)
        {
            // Nombre pequeño abajo
            nameRT.anchorMin = new Vector2(0f, 0f);
            nameRT.anchorMax = new Vector2(1f, 0f);
            nameRT.pivot = new Vector2(0.5f, 0f);
            nameRT.sizeDelta = new Vector2(-12f, 24f);
            nameRT.anchoredPosition = new Vector2(0f, 4f);
            slot.nameLabel.fontSizeMin = 10f;
            slot.nameLabel.fontSizeMax = 18f;
        }
        else
        {
            // Sin imagen: el nombre ocupa el centro de la ranura
            Stretch(nameRT, new Vector2(8f, 8f), new Vector2(-8f, -8f));
            slot.nameLabel.fontSizeMin = 12f;
            slot.nameLabel.fontSizeMax = 30f;
        }

        // Tecla que selecciona el arma (el número de ranura)
        var badge = CreateImage(slot.root, "KeyBadge", new Color(0f, 0f, 0f, 0.65f));
        var badgeRT = badge.rectTransform;
        badgeRT.anchorMin = badgeRT.anchorMax = new Vector2(0f, 1f);
        badgeRT.pivot = new Vector2(0f, 1f);
        badgeRT.sizeDelta = new Vector2(28f, 28f);
        badgeRT.anchoredPosition = new Vector2(6f, -6f);

        slot.keyLabel = CreateLabel(badgeRT, "KeyLabel", textColor);
        slot.keyLabel.text = (index + 1).ToString();
        slot.keyLabel.alignment = TextAlignmentOptions.Center;
        slot.keyLabel.fontStyle = FontStyles.Bold;
        slot.keyLabel.fontSize = 20f;
        Stretch(slot.keyLabel.rectTransform, Vector2.zero, Vector2.zero);

        rootGO.SetActive(false);   // Refresh() decide si se ve
        return slot;
    }

    private static Image CreateImage(Transform parent, string name, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>();
        img.color = color;
        img.raycastTarget = false;
        return img;
    }

    private static TextMeshProUGUI CreateLabel(Transform parent, string name, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        var t = go.GetComponent<TextMeshProUGUI>();
        t.color = color;
        t.raycastTarget = false;
        t.overflowMode = TextOverflowModes.Ellipsis;
        return t;
    }

    private static void Stretch(RectTransform rt, Vector2 offsetMin, Vector2 offsetMax)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = offsetMin;
        rt.offsetMax = offsetMax;
    }

    // ---------- ACTUALIZACIÓN ----------
    private void Refresh()
    {
        if (slots.Count == 0) return;   // aún no se ha construido (el switcher avisa antes del Start)

        var weapons = switcher.Weapons;
        for (int i = 0; i < slots.Count && i < weapons.Count; i++)
        {
            var slot = slots[i];
            bool unlocked = weapons[i].unlocked && weapons[i].weapon != null;

            slot.unlocked = unlocked;
            slot.selected = i == switcher.CurrentIndex;
            slot.root.gameObject.SetActive(unlocked || showLockedSlots);
        }
    }

    private void Update()
    {
        float dt = Time.unscaledDeltaTime;          // sigue animándose aunque el juego esté en pausa
        float k = 1f - Mathf.Exp(-animSpeed * dt);  // suavizado independiente del framerate

        foreach (var s in slots)
        {
            if (!s.root.gameObject.activeSelf) continue;

            s.selectedT = Mathf.Lerp(s.selectedT, s.selected ? 1f : 0f, k);
            s.punch = Mathf.Lerp(s.punch, 0f, k);
            if (s.popTime < 1f) s.popTime = Mathf.Min(1f, s.popTime + dt / Mathf.Max(0.01f, popDuration));

            float scale = Mathf.Lerp(1f, selectedScale, s.selectedT) * EaseOutBack(s.popTime) * (1f + s.punch);
            s.root.localScale = Vector3.one * scale;

            s.background.color = Color.Lerp(slotColor, selectedSlotColor, s.selectedT);

            Color f = frameColor;
            f.a = frameColor.a * s.selectedT;
            s.frame.color = f;

            float b = Mathf.Lerp(unselectedBrightness, 1f, s.selectedT);
            float alpha = s.unlocked ? 1f : 0.25f;
            s.icon.color = new Color(b, b, b, alpha);
            s.nameLabel.color = new Color(textColor.r * b, textColor.g * b, textColor.b * b, textColor.a * alpha);
        }
    }

    // Entrada con ligero rebote: 0 -> 1 pasándose un poco de 1
    private static float EaseOutBack(float t)
    {
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;
        float u = t - 1f;
        return 1f + c3 * u * u * u + c1 * u * u;
    }
}
