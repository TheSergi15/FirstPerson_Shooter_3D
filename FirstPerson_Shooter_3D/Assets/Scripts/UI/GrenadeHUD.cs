using System.Collections;
using TMPro;
using UnityEngine;

/// <summary>
/// Icono de granada, aparte del inventario de armas. Solo aparece cuando tienes granadas.
/// Pon ESTE script en un objeto que siempre esté activo (p. ej. el Canvas), NO en el slot.
/// </summary>
public class GrenadeHUD : MonoBehaviour
{
    [SerializeField] private GameObject slot;          // Panel con el icono (empieza oculto)
    [SerializeField] private RectTransform popTarget;  // Normalmente el mismo slot (efecto de "pop")
    [SerializeField] private TMP_Text countText;       // Opcional: muestra "x2" si hay más de una

    private int last;

    private void OnEnable()
    {
        last = GrenadeInventory.Count;
        GrenadeInventory.OnChanged += Refresh;
        Refresh(GrenadeInventory.Count);
    }

    private void OnDisable()
    {
        GrenadeInventory.OnChanged -= Refresh;
    }

    private void Refresh(int count)
    {
        bool show = count > 0;
        slot.SetActive(show);
        if (countText != null) countText.text = count > 1 ? "x" + count : "";

        if (show && count > last && popTarget != null)
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
}
