using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;

public class CameraController : MonoBehaviour
{
    [Header("Sensibilidad")]
    public float sensitivity = 0.5f;

    [Header("Límites de Rotación")]
    [Tooltip("90 a la izquierda y 90 a la derecha suman los 180º")]
    public float horizontalLimit = 90f;

    [Tooltip("Límite hacia arriba")]
    [FormerlySerializedAs("verticalLimit")]   // Conserva el valor que ya tenías en el Inspector
    [Range(0f, 90f)] public float verticalLimitUp = 45f;

    [Tooltip("Límite hacia abajo (90 = mirar recto al suelo)")]
    [Range(0f, 90f)] public float verticalLimitDown = 80f;

    private float xRotation = 0f;
    private float yRotation = 0f;

    void Start()
    {
        // Bloqueamos y ocultamos el cursor para que no se salga de la pantalla al jugar
        // Cursor.lockState = CursorLockMode.Locked;
    }

    void Update()
    {
        // Verificamos el ratón usando el nuevo Input System
        if (Mouse.current != null)
        {
            // Obtenemos cuánto se movió el ratón en este fotograma
            Vector2 mouseDelta = Mouse.current.delta.ReadValue();

            // Sumamos el movimiento a nuestros ejes
            yRotation += mouseDelta.x * sensitivity;
            xRotation -= mouseDelta.y * sensitivity; // Se resta para que el eje vertical no esté invertido

            // Limitamos la rotación matemática usando Clamp
            // (en X negativo = arriba, positivo = abajo)
            yRotation = Mathf.Clamp(yRotation, -horizontalLimit, horizontalLimit);
            xRotation = Mathf.Clamp(xRotation, -verticalLimitUp, verticalLimitDown);

            // Aplicamos la rotación calculada a la cámara
            transform.localRotation = Quaternion.Euler(xRotation, yRotation, 0f);
        }
    }
}
