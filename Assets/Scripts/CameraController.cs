using UnityEngine;
using UnityEngine.InputSystem;

public class CameraController : MonoBehaviour
{
    [Header("Sensibilidad")]
    public float sensitivity = 0.5f;

    [Header("Límites de Rotación")]
    public float horizontalLimit = 90f;
    public float verticalLimit = 45f;

    [Header("Suavizado de Paneo")]
    [Tooltip("Tiempo en segundos que tarda en llegar al objetivo. (Más bajo = más rígido, Más alto = más flotante)")]
    public float smoothTime = 0.05f;

    // Guardamos a dónde QUEREMOS que mire la cámara
    private float targetXRotation = 0f;
    private float targetYRotation = 0f;

    // Guardamos hacia dónde ESTÁ MIRANDO realmente la cámara en este fotograma
    private float currentXRotation = 0f;
    private float currentYRotation = 0f;

    // Variables de referencia internas que SmoothDamp necesita para calcular la inercia
    private float xVelocity = 0f;
    private float yVelocity = 0f;

    void Start()
    {
        // Recuerda que quitamos el bloqueo del ratón de aquí porque ahora lo gestiona tu PointerController[cite: 3]
    }

    void Update()
    {
        if (Mouse.current != null)
        {
            Vector2 mouseDelta = Mouse.current.delta.ReadValue();

            // 1. Acumulamos el movimiento en nuestro OBJETIVO ideal
            targetYRotation += mouseDelta.x * sensitivity;
            targetXRotation -= mouseDelta.y * sensitivity;

            // Limitamos los grados del objetivo matemático (180º horizontales y 45º verticales)
            targetYRotation = Mathf.Clamp(targetYRotation, -horizontalLimit, horizontalLimit);
            targetXRotation = Mathf.Clamp(targetXRotation, -verticalLimit, verticalLimit);

            // 2. Interpolamos suavemente la rotación ACTUAL hacia el OBJETIVO usando SmoothDamp
            currentXRotation = Mathf.SmoothDamp(currentXRotation, targetXRotation, ref xVelocity, smoothTime);
            currentYRotation = Mathf.SmoothDamp(currentYRotation, targetYRotation, ref yVelocity, smoothTime);

            // 3. Aplicamos la rotación final y suavizada a la cámara real
            transform.localRotation = Quaternion.Euler(currentXRotation, currentYRotation, 0f);
        }
    }
}