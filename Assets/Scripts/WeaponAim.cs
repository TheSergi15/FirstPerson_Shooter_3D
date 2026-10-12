using UnityEngine;
using UnityEngine.InputSystem; // Utilizamos el input moderno

public class WeaponAim : MonoBehaviour
{
    [Header("Configuración de Apuntado")]
    [Tooltip("Arrastra aquí tu Main Camera")]
    public Camera mainCamera;

    [Tooltip("Velocidad a la que el arma sigue al puntero")]
    public float aimSpeed = 15f;

    [Tooltip("Distancia máxima a la que apunta si no hay paredes")]
    public float maxDistance = 100f;

    [Tooltip("Las capas contra las que puede chocar el puntero (Ej: Enemigo, Suelo, Paredes)")]
    public LayerMask aimLayerMask;

    void Start()
    {
        if (mainCamera == null)
        {
            mainCamera = Camera.main;
        }
    }

    void Update()
    {
        if (Mouse.current != null)
        {
            Vector2 mousePos = Mouse.current.position.ReadValue();
            Ray ray = mainCamera.ScreenPointToRay(mousePos);
            Vector3 targetPoint;
            RaycastHit hit;

            if (Physics.Raycast(ray, out hit, maxDistance, aimLayerMask))
            {
                targetPoint = hit.point;
                // Muestra en consola el culpable del impacto prematuro
                Debug.Log("El arma está chocando contra: " + hit.collider.gameObject.name);
                // Dibuja una línea roja en la pestaña Scene para ver el trayecto
                Debug.DrawLine(ray.origin, hit.point, Color.red);
            }
            else
            {
                targetPoint = ray.GetPoint(maxDistance);
            }

            Vector3 aimDirection = targetPoint - transform.position;
            Quaternion targetRotation = Quaternion.LookRotation(aimDirection);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * aimSpeed);
        }
    }
}