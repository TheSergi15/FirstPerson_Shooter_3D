using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Muestra la munición del arma equipada con iconos, sin texto:
///  - una bala por cada hueco del cargador (llena = queda, apagada = gastada),
///  - un icono de cargador por cada recarga que te queda en reserva,
///  - una barra fina que se llena mientras recargas.
/// Se construye por código. Si el arma no tiene iconos propios en el WeaponSwitcher,
/// usa una bala y un cargador genéricos generados al vuelo.
/// </summary>
public class AmmoIconsUI : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField] private WeaponSwitcher switcher;   // Si se deja vacío se busca en la escena

    [Header("Posición")]
    [SerializeField] private Vector2 anchoredPosition = new Vector2(-40f, 40f);   // Desde la esquina inferior derecha

    [Header("Balas del cargador")]
    [SerializeField] private Vector2 bulletSize = new Vector2(12f, 32f);
    [SerializeField] private float bulletSpacing = 4f;
    [SerializeField] private int maxBulletsPerRow = 15;   // Si el cargador es mayor, se parte en varias filas

    [Header("Cargadores de reserva")]
    [SerializeField] private Vector2 magazineIconSize = new Vector2(24f, 44f);
    [SerializeField] private float magazineSpacing = 6f;
    [SerializeField] private int maxMagazineIcons = 6;    // Si hay más, se enseña un icono con "x N"

    [Header("Colores")]
    [SerializeField] private Color filledColor = new Color(0.96f, 0.78f, 0.35f, 1f);
    [SerializeField] private Color emptyColor = new Color(1f, 1f, 1f, 0.18f);
    [SerializeField] private Color lowAmmoColor = new Color(1f, 0.3f, 0.2f, 1f);
    [SerializeField] private Color magazineColor = new Color(0.85f, 0.88f, 0.92f, 0.95f);
    [SerializeField] private Color reloadBarColor = new Color(1f, 0.78f, 0.2f, 1f);
    [SerializeField, Range(0f, 1f)] private float lowAmmoThreshold = 0.25f;   // Fracción del cargador que se considera "poca munición"

    [Header("Barra de recarga")]
    [SerializeField] private float reloadBarHeight = 4f;

    private class MagIcon
    {
        public GameObject go;
        public Image back, fill;
    }

    private readonly List<Image> bullets = new List<Image>();
    private readonly List<MagIcon> magazines = new List<MagIcon>();

    private Gun gun;
    private Sprite bulletSprite, magazineSprite;
    private static Sprite defaultBulletSprite, defaultMagazineSprite;

    private GameObject ownedCanvas;   // Solo si no había Canvas y hemos creado uno
    private RectTransform root, bulletGrid, reloadFill;
    private GridLayoutGroup grid;
    private LayoutElement reloadBarLayout;
    private CanvasGroup reloadBarGroup;
    private RectTransform magRow;
    private TextMeshProUGUI overflowLabel;
    private bool built;

    // ---------- CICLO DE VIDA ----------
    private void OnEnable()
    {
        if (switcher != null) Subscribe();
    }

    private void OnDisable()
    {
        if (switcher != null) switcher.OnWeaponChanged -= HandleWeaponChanged;
    }

    private void Start()
    {
        if (switcher == null)
        {
            switcher = FindFirstObjectByType<WeaponSwitcher>();
            if (switcher == null)
            {
                Debug.LogError("[AmmoIconsUI] No hay ningún WeaponSwitcher en la escena.", this);
                enabled = false;
                return;
            }
            Subscribe();
        }

        Build();
        BindGun(switcher.CurrentIndex);
    }

    private void OnDestroy()
    {
        if (ownedCanvas != null) Destroy(ownedCanvas);
    }

    private void Subscribe()
    {
        switcher.OnWeaponChanged -= HandleWeaponChanged;   // evita suscribirse dos veces
        switcher.OnWeaponChanged += HandleWeaponChanged;
    }

    private void HandleWeaponChanged(int index, WeaponSwitcher.WeaponEntry entry)
    {
        if (built) BindGun(index);
    }

    // ---------- ARMA ACTUAL ----------
    private void BindGun(int index)
    {
        gun = null;

        if (index >= 0 && index < switcher.Weapons.Count)
        {
            var entry = switcher.Weapons[index];
            if (entry.weapon != null) gun = entry.weapon.GetComponentInChildren<Gun>(true);

            bulletSprite = entry.ammoIcon != null ? entry.ammoIcon : GetDefaultBulletSprite();
            magazineSprite = entry.magazineIcon != null ? entry.magazineIcon : GetDefaultMagazineSprite();
        }

        if (gun == null) return;

        RebuildBullets(gun.MagazineSize);
        foreach (var m in magazines)
        {
            m.back.sprite = magazineSprite;
            m.fill.sprite = magazineSprite;
        }
    }

    // ---------- CONSTRUCCIÓN ----------
    private void Build()
    {
        var rootGO = new GameObject("AmmoIcons", typeof(RectTransform),
                                    typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        rootGO.transform.SetParent(GetCanvasTransform(), false);

        root = (RectTransform)rootGO.transform;
        root.anchorMin = root.anchorMax = new Vector2(1f, 0f);   // abajo a la derecha
        root.pivot = new Vector2(1f, 0f);
        root.anchoredPosition = anchoredPosition;

        var vlg = rootGO.GetComponent<VerticalLayoutGroup>();
        vlg.spacing = 8f;
        vlg.childAlignment = TextAnchor.LowerRight;
        vlg.childControlWidth = true;
        vlg.childControlHeight = true;
        vlg.childForceExpandWidth = false;
        vlg.childForceExpandHeight = false;

        var fitter = rootGO.GetComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        // 1) Balas del cargador
        var gridGO = new GameObject("Bullets", typeof(RectTransform), typeof(GridLayoutGroup));
        gridGO.transform.SetParent(root, false);
        bulletGrid = (RectTransform)gridGO.transform;
        grid = gridGO.GetComponent<GridLayoutGroup>();
        grid.cellSize = bulletSize;
        grid.spacing = new Vector2(bulletSpacing, bulletSpacing);
        grid.startCorner = GridLayoutGroup.Corner.UpperLeft;
        grid.startAxis = GridLayoutGroup.Axis.Horizontal;
        grid.childAlignment = TextAnchor.UpperLeft;
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = 1;

        // 2) Barra de recarga (ocupa siempre su sitio; solo cambia su transparencia)
        var barGO = new GameObject("ReloadBar", typeof(RectTransform), typeof(LayoutElement),
                                   typeof(CanvasGroup), typeof(Image));
        barGO.transform.SetParent(root, false);
        barGO.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.15f);
        barGO.GetComponent<Image>().raycastTarget = false;
        reloadBarLayout = barGO.GetComponent<LayoutElement>();
        reloadBarLayout.preferredHeight = reloadBarHeight;
        reloadBarLayout.preferredWidth = 100f;
        reloadBarGroup = barGO.GetComponent<CanvasGroup>();
        reloadBarGroup.alpha = 0f;
        reloadBarGroup.interactable = false;
        reloadBarGroup.blocksRaycasts = false;

        var fill = CreateImage(barGO.transform, "Fill", reloadBarColor);
        reloadFill = fill.rectTransform;
        reloadFill.anchorMin = Vector2.zero;
        reloadFill.anchorMax = new Vector2(0f, 1f);
        reloadFill.offsetMin = reloadFill.offsetMax = Vector2.zero;

        // 3) Cargadores de reserva
        var rowGO = new GameObject("Magazines", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        rowGO.transform.SetParent(root, false);
        magRow = (RectTransform)rowGO.transform;
        var hlg = rowGO.GetComponent<HorizontalLayoutGroup>();
        hlg.spacing = magazineSpacing;
        hlg.childAlignment = TextAnchor.MiddleRight;
        hlg.childControlWidth = true;
        hlg.childControlHeight = true;
        hlg.childForceExpandWidth = false;
        hlg.childForceExpandHeight = false;

        // Etiqueta "x N" para cuando hay más cargadores de los que caben como icono
        var labelGO = new GameObject("Overflow", typeof(RectTransform), typeof(TextMeshProUGUI), typeof(LayoutElement));
        labelGO.transform.SetParent(magRow, false);
        overflowLabel = labelGO.GetComponent<TextMeshProUGUI>();
        overflowLabel.fontSize = 26f;
        overflowLabel.fontStyle = FontStyles.Bold;
        overflowLabel.alignment = TextAlignmentOptions.MidlineLeft;
        overflowLabel.color = magazineColor;
        overflowLabel.raycastTarget = false;
        var labelLE = labelGO.GetComponent<LayoutElement>();
        labelLE.preferredWidth = 56f;
        labelLE.preferredHeight = magazineIconSize.y;
        labelGO.SetActive(false);

        built = true;
    }

    // Usa el Canvas en el que esté este objeto; si no hay ninguno, crea uno propio
    private Transform GetCanvasTransform()
    {
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas != null) return canvas.rootCanvas.transform;

        ownedCanvas = new GameObject("AmmoIconsCanvas", typeof(Canvas), typeof(CanvasScaler));
        ownedCanvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;

        var scaler = ownedCanvas.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        return ownedCanvas.transform;
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

    // Ajusta el número de balas (y el ancho de la barra de recarga) al tamaño del cargador
    private void RebuildBullets(int count)
    {
        count = Mathf.Max(0, count);

        int columns = Mathf.Max(1, Mathf.Min(count, maxBulletsPerRow));
        grid.constraintCount = columns;
        reloadBarLayout.preferredWidth = columns * bulletSize.x + (columns - 1) * bulletSpacing;

        while (bullets.Count < count)
            bullets.Add(CreateImage(bulletGrid, $"Bullet_{bullets.Count + 1}", filledColor));

        while (bullets.Count > count)
        {
            Image last = bullets[bullets.Count - 1];
            bullets.RemoveAt(bullets.Count - 1);
            last.gameObject.SetActive(false);   // desactivar ya para que el layout no lo cuente
            Destroy(last.gameObject);
        }

        foreach (Image b in bullets)
        {
            b.sprite = bulletSprite;
            b.preserveAspect = true;
        }
    }

    private void EnsureMagazineIcons(int count)
    {
        while (magazines.Count < count)
        {
            var go = new GameObject($"Magazine_{magazines.Count + 1}", typeof(RectTransform), typeof(LayoutElement));
            go.transform.SetParent(magRow, false);

            var le = go.GetComponent<LayoutElement>();
            le.preferredWidth = magazineIconSize.x;
            le.preferredHeight = magazineIconSize.y;

            var m = new MagIcon { go = go };

            // Fondo apagado (se ve en la parte vacía de un cargador a medias)
            m.back = CreateImage(go.transform, "Back", emptyColor);
            Stretch(m.back.rectTransform);
            m.back.sprite = magazineSprite;
            m.back.preserveAspect = true;

            // Relleno: se recorta desde arriba según las balas que queden en ese cargador
            m.fill = CreateImage(go.transform, "Fill", magazineColor);
            Stretch(m.fill.rectTransform);
            m.fill.sprite = magazineSprite;
            m.fill.preserveAspect = true;
            m.fill.type = Image.Type.Filled;
            m.fill.fillMethod = Image.FillMethod.Vertical;
            m.fill.fillOrigin = (int)Image.OriginVertical.Bottom;
            m.fill.fillAmount = 1f;

            magazines.Add(m);
        }

        overflowLabel.transform.SetAsLastSibling();   // la etiqueta "x N" siempre al final
    }

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    // ---------- ACTUALIZACIÓN ----------
    private void Update()
    {
        if (!built) return;

        if (gun == null)
        {
            if (root.gameObject.activeSelf) root.gameObject.SetActive(false);
            return;
        }
        if (!root.gameObject.activeSelf) root.gameObject.SetActive(true);

        int size = gun.MagazineSize;
        if (bullets.Count != size) RebuildBullets(size);   // por si cambia el tamaño en caliente

        int inMag = gun.AmmoInMag;
        int reserve = gun.ReserveAmmo;
        bool reloading = gun.IsReloading;
        float progress = gun.ReloadProgress;

        // --- Balas: mientras recargas se van "llenando" hasta el cargador nuevo ---
        int filled = inMag;
        if (reloading)
        {
            int target = Mathf.Min(size, inMag + reserve);
            filled = Mathf.RoundToInt(Mathf.Lerp(inMag, target, progress));
        }

        bool outOfAmmo = inMag == 0 && reserve == 0;
        bool low = !reloading && inMag <= Mathf.Max(1, Mathf.CeilToInt(size * lowAmmoThreshold));

        Color on = low ? lowAmmoColor : filledColor;
        Color off = emptyColor;
        if (outOfAmmo)
        {
            // Sin munición: los huecos parpadean en rojo
            float blink = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 6f);
            off = new Color(lowAmmoColor.r, lowAmmoColor.g, lowAmmoColor.b, Mathf.Lerp(0.15f, 0.6f, blink));
        }

        for (int i = 0; i < bullets.Count; i++)
            bullets[i].color = i < filled ? on : off;

        // --- Cargadores de reserva ---
        int needed = size > 0 ? Mathf.CeilToInt(reserve / (float)size) : 0;
        bool overflow = needed > maxMagazineIcons;
        int shown = overflow ? 1 : needed;

        EnsureMagazineIcons(shown);

        for (int k = 0; k < magazines.Count; k++)
        {
            bool active = k < shown;
            magazines[k].go.SetActive(active);
            if (!active) continue;

            // El último cargador puede estar a medias
            float fraction = 1f;
            if (!overflow && k == shown - 1 && size > 0)
            {
                int rest = reserve % size;
                if (rest != 0) fraction = rest / (float)size;
            }
            magazines[k].fill.fillAmount = fraction;
        }

        overflowLabel.gameObject.SetActive(overflow);
        if (overflow) overflowLabel.text = "x" + needed;

        // --- Barra de recarga ---
        reloadBarGroup.alpha = reloading ? 1f : 0f;
        if (reloading) reloadFill.anchorMax = new Vector2(progress, 1f);
    }

    // ---------- ICONOS GENÉRICOS (se usan si el arma no tiene los suyos) ----------
    private static Sprite GetDefaultBulletSprite()
    {
        if (defaultBulletSprite != null) return defaultBulletSprite;

        const int w = 24, h = 64;
        var tex = NewTexture(w, h);
        var px = new Color32[w * h];
        float cx = (w - 1) * 0.5f;

        for (int y = 0; y < h; y++)
        {
            float half, shade = 1f;
            if (y < 40)
            {
                half = 9.5f;                               // casquillo
                if (y < 5) { half = 10.5f; shade = 0.85f; }  // base
                else if (y < 8) shade = 0.7f;                // ranura
            }
            else
            {
                float t = (y - 40f) / (h - 1f - 40f);      // punta redondeada
                half = 9.5f * Mathf.Sqrt(Mathf.Max(0f, 1f - t * t));
                shade = 0.78f;
            }

            for (int x = 0; x < w; x++)
            {
                float a = Mathf.Clamp01(half - Mathf.Abs(x - cx) + 0.5f);
                byte g = (byte)(255 * shade);
                px[y * w + x] = new Color32(g, g, g, (byte)(255 * a));
            }
        }

        defaultBulletSprite = FinishSprite(tex, px, w, h);
        return defaultBulletSprite;
    }

    private static Sprite GetDefaultMagazineSprite()
    {
        if (defaultMagazineSprite != null) return defaultMagazineSprite;

        const int w = 28, h = 56;
        var tex = NewTexture(w, h);
        var px = new Color32[w * h];

        for (int y = 0; y < h; y++)
        {
            float t = y / (h - 1f);                         // 0 abajo, 1 arriba
            float cx = 9f + 10f * (1f - t) * (1f - t);      // cargador curvo
            float half = 7.5f;
            float shade = 1f;
            if (y > h - 6) { half = 6.5f; shade = 0.8f; }   // labio superior
            if (y % 9 == 0) shade = 0.75f;                  // nervios

            for (int x = 0; x < w; x++)
            {
                float a = Mathf.Clamp01(half - Mathf.Abs(x - cx) + 0.5f);
                byte g = (byte)(255 * shade);
                px[y * w + x] = new Color32(g, g, g, (byte)(255 * a));
            }
        }

        defaultMagazineSprite = FinishSprite(tex, px, w, h);
        return defaultMagazineSprite;
    }

    private static Texture2D NewTexture(int w, int h)
    {
        return new Texture2D(w, h, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };
    }

    private static Sprite FinishSprite(Texture2D tex, Color32[] pixels, int w, int h)
    {
        tex.SetPixels32(pixels);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f);
    }
}
