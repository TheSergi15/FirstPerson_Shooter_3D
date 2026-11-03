using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Granada recogible del suelo: apunta con el puntero y pulsa E.
/// Necesita un Collider. Sirve igual en el tutorial que en el resto de niveles.
/// </summary>
public class GrenadePickup : MonoBehaviour
{
    [SerializeField] private int amount = 1;
    [SerializeField] private float pickupDistance = 10f;

    private void Update()
    {
        if (Keyboard.current == null || !Keyboard.current.eKey.wasPressedThisFrame) return;
        if (!AimRaycaster.Cast(out RaycastHit hit)) return;
        if (hit.distance > pickupDistance) return;
        if (hit.collider.GetComponentInParent<GrenadePickup>() != this) return;

        GrenadeInventory.Add(amount);
        TutorialEvents.NotifyGrenadePickedUp();
        Destroy(gameObject);
    }
}
