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
            // Leemos la posición actual de tu puntero en la pantalla[cite: 1]
            Vector2 mousePos = Mouse.current.position.ReadValue();

            // Creamos un rayo desde la cámara pasando por el puntero
            Ray ray = mainCamera.ScreenPointToRay(mousePos);
            Vector3 targetPoint;
            RaycastHit hit;

            // Lanzamos el rayo para saber exactamente qué punto 3D estamos mirando[cite: 2]
            if (Physics.Raycast(ray, out hit, maxDistance, aimLayerMask))
            {
                targetPoint = hit.point; // El arma mirará al punto de impacto
            }
            else
            {
                // Si apuntamos al cielo o al vacío, mirará hacia el fondo del rayo
                targetPoint = ray.GetPoint(maxDistance);
            }

            // Calculamos la dirección matemática hacia ese objetivo
            Vector3 aimDirection = targetPoint - transform.position;

            // Calculamos la rotación necesaria
            Quaternion targetRotation = Quaternion.LookRotation(aimDirection);

            // Rotamos el arma de forma suave usando Slerp[cite: 1]
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * aimSpeed);
        }
    }
}