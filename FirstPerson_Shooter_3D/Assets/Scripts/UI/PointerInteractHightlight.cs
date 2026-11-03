using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Resalta el puntero cuando apunta a objetos interactuables.
/// Compatible con Image y RawImage.
/// </summary>
public class PointerInteractHighlight : MonoBehaviour
{
    [Header("Efecto visual")]
    [SerializeField] private Color interactColor = new Color(1f, 0.835f, 0.31f);
    [SerializeField] private float interactScale = 1.35f;
    [SerializeField] private float smoothing = 18f;

    private Graphic graphic;
    private Color normalColor;
    private Vector3 normalScale;
    private float currentScale = 1f;

    private void Awake()
    {
        // Detecta automáticamente Image o RawImage.
        graphic = GetComponent<Graphic>();

        if (graphic == null)
        {
            Debug.LogError("PointerInteractHighlight necesita un componente Image o RawImage.", this);
            enabled = false;
            return;
        }

        normalColor = graphic.color;
        normalScale = transform.localScale;
    }

    private void OnDisable()
    {
        if (graphic != null)
            graphic.color = normalColor;

        transform.localScale = normalScale;
        currentScale = 1f;
    }

    private void Update()
    {
        if (graphic == null) return;

        bool on = IsAimingAtInteractable();

        float k = 1f - Mathf.Exp(-smoothing * Time.unscaledDeltaTime);

        graphic.color = Color.Lerp(
            graphic.color,
            on ? interactColor : normalColor,
            k
        );

        currentScale = Mathf.Lerp(
            currentScale,
            on ? interactScale : 1f,
            k
        );

        transform.localScale = normalScale * currentScale;
    }

    private static bool IsAimingAtInteractable()
    {
        if (!AimRaycaster.Cast(out RaycastHit hit))
            return false;

        Collider c = hit.collider;

        if (c.GetComponentInParent<TutorialTarget>() != null)
            return true;

        if (c.GetComponentInParent<GrenadePickup>() != null)
            return true;

        if (c.GetComponentInParent<HealthPickup>() != null)
            return true;

        // Las armas que ya están equipadas no cuentan.
        PickUpController weapon =
            c.GetComponentInParent<PickUpController>();

        return weapon != null && !weapon.equipped;
    }
}