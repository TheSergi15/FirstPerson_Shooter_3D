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
    [Tooltip("Escribe en la consola por qué no se recoge (desmárcalo cuando todo funcione)")]
    [SerializeField] private bool debugLog = true;

    private void Update()
    {
        if (Keyboard.current == null || !Keyboard.current.eKey.wasPressedThisFrame) return;

        if (!AimRaycaster.Cast(out RaycastHit hit))
        {
            Log("E pulsada, pero el rayo del puntero no golpea nada. ¿La cámara tiene el tag MainCamera?");
            return;
        }

        if (hit.collider.GetComponentInParent<GrenadePickup>() != this)
        {
            Log($"E pulsada, pero el puntero apunta a '{hit.collider.name}' y no a la granada. ¿Tiene Collider?");
            return;
        }

        if (hit.distance > pickupDistance)
        {
            Log($"Demasiado lejos: {hit.distance:F1} m (máximo {pickupDistance} m).");
            return;
        }

        GrenadeInventory.Add(amount);
        TutorialEvents.NotifyGrenadePickedUp();
        Log($"Granada recogida. Total en el inventario: {GrenadeInventory.Count}");
        Destroy(gameObject);
    }

    private void Log(string msg)
    {
        if (debugLog) Debug.Log("[GrenadePickup] " + msg, this);
    }
}