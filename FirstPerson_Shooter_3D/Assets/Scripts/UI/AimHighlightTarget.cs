using UnityEngine;

/// <summary>
/// Hace brillar (con tu WeaponHighlight) cualquier objeto cuando el puntero le apunta.
/// Úsalo en el pickup de curación y en la granada: ponlo junto a un WeaponHighlight
/// en el mismo objeto. El objeto necesita un Collider (puede ser trigger).
/// </summary>
[RequireComponent(typeof(WeaponHighlight))]
public class AimHighlightTarget : MonoBehaviour
{
    [Tooltip("Distancia máxima desde la cámara para que brille")]
    [SerializeField] private float maxDistance = 10f;

    private WeaponHighlight highlight;

    private void Awake()
    {
        highlight = GetComponent<WeaponHighlight>();
    }

    private void OnDisable()
    {
        if (highlight != null) highlight.ResetNow();
    }

    private void Update()
    {
        bool aimed = AimRaycaster.Cast(out RaycastHit hit)
                     && hit.distance <= maxDistance
                     && hit.collider.GetComponentInParent<AimHighlightTarget>() == this;

        // Se llama cada fotograma para que el brillo suba y baje con fundido
        highlight.SetHighlighted(aimed);
    }
}
