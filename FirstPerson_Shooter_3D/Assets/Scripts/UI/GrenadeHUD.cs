using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Icono de granada, aparte del inventario de armas. Solo se ve cuando tienes granadas.
/// Se coloca POR CÓDIGO justo encima del inventario de armas: mide lo que ocupa el inventario
/// (solo los elementos visibles) y pone el icono sobre él, con la separación indicada.
/// Se reajusta solo si el inventario cambia de tamaño o cambias la resolución / el Canvas Scaler.
/// </summary>
public class GrenadeHUD : MonoBehaviour
{
    public enum HAlign { Left, Center, Right }

    [Header("Referencias")]
    [Tooltip("Panel con el icono de la granada (cualquier objeto del mismo Canvas)")]
    [SerializeField] private GameObject slot;
    [Tooltip("Objeto que hace el efecto 'pop' al coger una granada (normalmente el mismo slot)")]
    [SerializeField] private RectTransform popTarget;
    [Tooltip("Opcional: muestra 'x2' si hay más de una")]
    [SerializeField] private TMP_Text countText;

    [Header("Colocación automática")]
    [Tooltip("Panel que contiene los slots del inventario de armas. El icono se coloca encima de él")]
    [SerializeField] private RectTransform weaponInventoryRoot;
    [Tooltip("Con qué borde del inventario se alinea el icono")]
    [SerializeField] private HAlign align = HAlign.Left;
    [Tooltip("Separación entre el inventario y el icono (en unidades del Canvas)")]
    [SerializeField] private float gap = 24f;
    [SerializeField] private Vector2 slotSize = new Vector2(90f, 90f);
    [Tooltip("Solo si no hay inventario asignado: margen desde la esquina inferior izquierda")]
    [SerializeField] private Vector2 fallbackMargin = new Vector2(40f, 40f);

    // Para el depurador
    public GameObject Slot => slot;
    public bool IsConfigured => slot != null && slotGroup != null;

    private CanvasGroup slotGroup;
    private RectTransform slotRect;
    private Canvas rootCanvas;
    private int last;

    private readonly Vector3[] corners = new Vector3[4];
    private readonly List<Graphic> graphicBuffer = new List<Graphic>();

    private void Awake()
    {
        if (slot == null)
        {
            Debug.LogError("[GrenadeHUD] Falta asignar 'Slot' en el Inspector: sin él no se ve el icono.", this);
            return;
        }

        slot.SetActive(true);   // se controla con CanvasGroup, no con SetActive

        slotRect = slot.transform as RectTransform;
        if (slotRect == null)
        {
            Debug.LogError("[GrenadeHUD] El Slot debe ser un objeto de UI (con RectTransform).", this);
            return;
        }

        // Si el padre tiene un Layout Group, que no mueva este icono
        LayoutElement le = slot.GetComponent<LayoutElement>();
        if (le == null) le = slot.AddComponent<LayoutElement>();
        le.ignoreLayout = true;

        slotGroup = slot.GetComponent<CanvasGroup>();
        if (slotGroup == null) slotGroup = slot.AddComponent<CanvasGroup>();
        slotGroup.interactable = false;
        slotGroup.blocksRaycasts = false;
        slotGroup.alpha = 0f;

        if (popTarget == null) popTarget = slotRect;

        Canvas c = slotRect.GetComponentInParent<Canvas>();
        rootCanvas = c != null ? c.rootCanvas : null;
    }

    private void OnEnable()
    {
        last = GrenadeInventory.Count;
        GrenadeInventory.OnChanged += Refresh;
        Refresh(GrenadeInventory.Count);
        PlaceSlot();
    }

    private void OnDisable()
    {
        GrenadeInventory.OnChanged -= Refresh;
    }

    // Después de que los layouts del inventario hayan calculado su tamaño
    private void LateUpdate()
    {
        PlaceSlot();
    }

    private void Refresh(int count)
    {
        if (slotGroup == null) return;

        bool show = count > 0;
        slotGroup.alpha = show ? 1f : 0f;
        if (countText != null) countText.text = count > 1 ? "x" + count : "";

        if (show && count > last && popTarget != null && isActiveAndEnabled)
        {
            StopAllCoroutines();
            StartCoroutine(Pop());
        }
        last = count;
    }

    private IEnumerator Pop()
    {
        const float duration = 0.3f;
        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.SmoothStep(0f, 1f, t / duration);
            popTarget.localScale = Vector3.one * Mathf.Lerp(1.5f, 1f, k);
            yield return null;
        }
        popTarget.localScale = Vector3.one;
    }

    // ───────── Colocación ─────────
    private void PlaceSlot()
    {
        if (slotRect == null) return;

        if (weaponInventoryRoot != null && TryGetInventoryEdges(out float left, out float right, out float top))
        {
            // Posición en el mundo: el icono apoya su borde inferior sobre el borde superior del inventario
            float scale = rootCanvas != null ? rootCanvas.transform.lossyScale.y : 1f;

            float x;
            Vector2 pivot;
            switch (align)
            {
                case HAlign.Center: x = (left + right) * 0.5f; pivot = new Vector2(0.5f, 0f); break;
                case HAlign.Right: x = right; pivot = new Vector2(1f, 0f); break;
                default: x = left; pivot = new Vector2(0f, 0f); break;
            }

            slotRect.anchorMin = slotRect.anchorMax = new Vector2(0.5f, 0.5f);
            slotRect.pivot = pivot;
            if (slotSize.x > 0f && slotSize.y > 0f) slotRect.sizeDelta = slotSize;

            Vector3 p = slotRect.position;
            slotRect.position = new Vector3(x, top + gap * scale, p.z);
        }
        else
        {
            // Sin inventario asignado: esquina inferior izquierda
            slotRect.anchorMin = slotRect.anchorMax = Vector2.zero;
            slotRect.pivot = Vector2.zero;
            if (slotSize.x > 0f && slotSize.y > 0f) slotRect.sizeDelta = slotSize;
            slotRect.anchoredPosition = fallbackMargin;
        }
    }

    /// <summary>Bordes (en el mundo) de lo que se ve del inventario de armas.</summary>
    private bool TryGetInventoryEdges(out float left, out float right, out float top)
    {
        left = float.MaxValue;
        right = float.MinValue;
        top = float.MinValue;
        bool any = false;

        // Solo cuentan los elementos visibles (imágenes y textos), no contenedores vacíos
        weaponInventoryRoot.GetComponentsInChildren(false, graphicBuffer);
        foreach (Graphic g in graphicBuffer)
        {
            if (!g.enabled || g.color.a < 0.01f) continue;
            if (g.rectTransform.IsChildOf(slotRect)) continue;   // el propio icono de granada no cuenta

            Rect r = g.rectTransform.rect;
            if (r.width <= 0.01f || r.height <= 0.01f) continue;

            g.rectTransform.GetWorldCorners(corners);
            for (int i = 0; i < 4; i++)
            {
                left = Mathf.Min(left, corners[i].x);
                right = Mathf.Max(right, corners[i].x);
                top = Mathf.Max(top, corners[i].y);
            }
            any = true;
        }

        if (!any)
        {
            // Si no hay nada visible dentro, se usa el propio rectángulo del panel
            weaponInventoryRoot.GetWorldCorners(corners);
            for (int i = 0; i < 4; i++)
            {
                left = Mathf.Min(left, corners[i].x);
                right = Mathf.Max(right, corners[i].x);
                top = Mathf.Max(top, corners[i].y);
            }
        }
        return true;
    }
}