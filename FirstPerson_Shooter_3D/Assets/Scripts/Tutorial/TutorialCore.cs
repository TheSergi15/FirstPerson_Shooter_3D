using System;
using UnityEngine;
using UnityEngine.InputSystem;

// ─────────────────────────────────────────────────────────────
// Clases estáticas de apoyo (un solo archivo, no van en objetos)
// ─────────────────────────────────────────────────────────────

public enum TutorialTargetType { Weapon, HealPickup, Grenade }
public enum TutorialZoneId { Shooting, Enemies }

/// <summary>Bloqueos de controles. Fuera del tutorial todo está permitido.</summary>
public static class TutorialGate
{
    public static bool CanShoot = true;
    public static bool CanThrowGrenade = true;

    public static void ResetAll()
    {
        CanShoot = true;
        CanThrowGrenade = true;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Init() => ResetAll();
}

/// <summary>Eventos que lanzan Gun, PickUpController, etc. y escucha el TutorialManager.</summary>
public static class TutorialEvents
{
    public static event Action WeaponPickedUp;
    public static event Action WeaponFired;
    public static event Action GrenadePickedUp;
    public static event Action<TutorialZoneId> ZoneEntered;

    public static void NotifyWeaponPickedUp() => WeaponPickedUp?.Invoke();
    public static void NotifyWeaponFired() => WeaponFired?.Invoke();
    public static void NotifyGrenadePickedUp() => GrenadePickedUp?.Invoke();
    public static void NotifyZoneEntered(TutorialZoneId id) => ZoneEntered?.Invoke(id);
}

/// <summary>Inventario de granadas. Empieza en 0: la granada NO va equipada de base.</summary>
public static class GrenadeInventory
{
    public static int Count { get; private set; }
    public static bool HasGrenade => Count > 0;
    public static event Action<int> OnChanged;

    public static void Add(int amount = 1)
    {
        Count += amount;
        OnChanged?.Invoke(Count);
    }

    /// <summary>Gasta una granada. Devuelve false si no había ninguna.</summary>
    public static bool Consume()
    {
        if (Count <= 0) return false;
        Count--;
        OnChanged?.Invoke(Count);
        return true;
    }

    public static void Reset()
    {
        Count = 0;
        OnChanged?.Invoke(Count);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Init() => Count = 0;
}

/// <summary>
/// Raycast desde la cámara hasta la posición del ratón (donde está tu puntero).
/// Cachea el resultado por frame para que varios scripts lo usen sin coste extra.
/// </summary>
public static class AimRaycaster
{
    private const float MaxDistance = 40f;
    private static readonly RaycastHit[] buffer = new RaycastHit[24];
    private static int cachedFrame = -1;
    private static bool cachedResult;
    private static RaycastHit cachedHit;

    public static bool Cast(out RaycastHit hit)
    {
        if (cachedFrame == Time.frameCount)
        {
            hit = cachedHit;
            return cachedResult;
        }

        cachedFrame = Time.frameCount;
        cachedResult = false;
        cachedHit = default;

        Camera cam = Camera.main;
        if (cam != null && Mouse.current != null)
        {
            Ray ray = cam.ScreenPointToRay(Mouse.current.position.ReadValue());
            int n = Physics.RaycastNonAlloc(ray, buffer, MaxDistance,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide);

            float best = float.MaxValue;
            for (int i = 0; i < n; i++)
            {
                Collider c = buffer[i].collider;
                // Ignorar al propio jugador (y el arma que lleva en la mano)
                if (c.GetComponentInParent<CharacterController>() != null) continue;
                // Ignorar triggers que no sean objetivos (zonas del tutorial, etc.)
                if (c.isTrigger &&
                    c.GetComponentInParent<TutorialTarget>() == null &&
                    c.GetComponentInParent<GrenadePickup>() == null &&
                    c.GetComponentInParent<AimHighlightTarget>() == null) continue;

                if (buffer[i].distance < best)
                {
                    best = buffer[i].distance;
                    cachedHit = buffer[i];
                    cachedResult = true;
                }
            }
        }

        hit = cachedHit;
        return cachedResult;
    }
}
