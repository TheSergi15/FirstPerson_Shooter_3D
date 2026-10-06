using UnityEngine;
using UnityEngine.InputSystem; // Utilizamos el input moderno de tu proyecto

public class WeaponSway : MonoBehaviour
{
    [Header("Ajustes de Rotación (Sway)")]
    public float swayMultiplier = 1.5f;
    public float smoothStep = 8f;
    public float maxSwayAmount = 4f;

    private Quaternion initialRotation;

    void Start()
    {
        // Guardamos la rotación original del arma frente a la cámara
        initialRotation = transform.localRotation;
    }

    void Update()
    {
        float mouseX = 0f;
        float mouseY = 0f;

        // Leemos el movimiento del ratón
        if (Mouse.current != null)
        {
            Vector2 mouseDelta = Mouse.current.delta.ReadValue();
            mouseX = mouseDelta.x * swayMultiplier;
            mouseY = mouseDelta.y * swayMultiplier;
        }

        // Limitamos los valores para que el arma no gire de forma extrema
        mouseX = Mathf.Clamp(mouseX, -maxSwayAmount, maxSwayAmount);
        mouseY = Mathf.Clamp(mouseY, -maxSwayAmount, maxSwayAmount);

        // Calculamos la rotación inversa: si miras a la derecha, el arma rota a la izquierda
        Quaternion targetRotation = Quaternion.Euler(
            initialRotation.eulerAngles.x + mouseY,
            initialRotation.eulerAngles.y - mouseX,
            initialRotation.eulerAngles.z
        );

        // Aplicamos una interpolación suave (Slerp) para que regrese a su centro
        transform.localRotation = Quaternion.Slerp(transform.localRotation, targetRotation, Time.deltaTime * smoothStep);
    }
}