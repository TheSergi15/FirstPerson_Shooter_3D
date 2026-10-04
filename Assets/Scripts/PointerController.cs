using UnityEngine;
using UnityEngine.InputSystem;

public class PointerController : MonoBehaviour
{
    private RectTransform pointerRect;

    void Start()
    {
        pointerRect = GetComponent<RectTransform>();

        // Ocultamos el cursor de Windows/Mac
        Cursor.visible = false;

        // Confinamos el cursor para que no pueda salir de la ventana del juego
        Cursor.lockState = CursorLockMode.Confined;

        // --- NUEVO: Forzamos el ratón físico al centro de la pantalla al iniciar ---
        if (Mouse.current != null)
        {
            Vector2 centerPosition = new Vector2(Screen.width / 2f, Screen.height / 2f);

            // Movemos el cursor del sistema operativo
            Mouse.current.WarpCursorPosition(centerPosition);

            // Actualizamos la posición visual de la imagen inmediatamente
            pointerRect.position = centerPosition;
        }
    }

    void Update()
    {
        if (Mouse.current != null)
        {
            // Leemos la posición actual del ratón en la pantalla
            Vector2 mousePos = Mouse.current.position.ReadValue();

            // Limitamos las coordenadas al tamaño exacto de la pantalla por seguridad
            mousePos.x = Mathf.Clamp(mousePos.x, 0, Screen.width);
            mousePos.y = Mathf.Clamp(mousePos.y, 0, Screen.height);

            // Movemos la imagen del Canvas a esa posición
            pointerRect.position = mousePos;
        }
    }
}