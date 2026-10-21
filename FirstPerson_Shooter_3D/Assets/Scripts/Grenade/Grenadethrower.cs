using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Lanza granadas hacia donde apunta el ratón (o hacia delante de la cámara si desactivas useMouseAim).
/// Ponlo en el jugador. Usa el Input System nuevo.
/// </summary>
public class GrenadeThrower : MonoBehaviour
{
    [Header("Referencias")]
    public Grenade grenadePrefab;
    [Tooltip("Desde dónde sale la granada (mano, arma, cámara...). Si está vacío usa este objeto")]
    public Transform throwPoint;
    [Tooltip("Si está vacío usa Camera.main")]
    public Camera aimCamera;

    [Header("Lanzamiento")]
    public Key throwKey = Key.G;
    public float throwSpeed = 15f;
    public float upwardBoost = 2.5f;
    public float cooldown = 1f;
    public int grenades = 5;

    [Header("Puntería")]
    public bool useMouseAim = true;
    [Tooltip("Excluye aquí la capa del jugador para que el rayo no choque con él")]
    public LayerMask aimMask = ~0;

    float nextThrowTime;

    void Update()
    {
        if (Keyboard.current == null) return;

        if (Keyboard.current[throwKey].wasPressedThisFrame &&
            Time.time >= nextThrowTime &&
            grenades > 0)
        {
            Throw();
        }
    }

    void Throw()
    {
        Camera cam = aimCamera != null ? aimCamera : Camera.main;
        if (cam == null || grenadePrefab == null) return;

        Vector3 origin = throwPoint != null ? throwPoint.position : transform.position;
        Vector3 target;

        if (useMouseAim && Mouse.current != null)
        {
            Ray ray = cam.ScreenPointToRay(Mouse.current.position.ReadValue());
            target = Physics.Raycast(ray, out RaycastHit hit, 100f, aimMask, QueryTriggerInteraction.Ignore)
                ? hit.point
                : ray.GetPoint(30f);
        }
        else
        {
            target = cam.transform.position + cam.transform.forward * 30f;
        }

        Vector3 dir = (target - origin).normalized;

        Grenade g = Instantiate(grenadePrefab, origin + dir * 0.5f, Quaternion.identity);
        g.Launch(dir * throwSpeed + Vector3.up * upwardBoost);

        grenades--;
        nextThrowTime = Time.time + cooldown;
    }

    /// <summary>Útil para pickups de munición de granadas.</summary>
    public void AddGrenades(int amount)
    {
        grenades += amount;
    }
}
