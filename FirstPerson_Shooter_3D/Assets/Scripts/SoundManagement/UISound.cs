using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Sonido de hover y clic para un elemento de UI (botón, toggle, slider...).
/// </summary>
public class UISound : MonoBehaviour, IPointerEnterHandler, IPointerClickHandler
{
    public string hoverId = "ui_hover";
    public string clickId = "ui_click";

    private Selectable selectable;

    private void Awake()
    {
        selectable = GetComponent<Selectable>();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (CanPlay()) AudioManager.Sfx(hoverId);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (CanPlay()) AudioManager.Sfx(clickId);
    }

    private bool CanPlay()
    {
        return selectable == null || selectable.IsInteractable();
    }
}