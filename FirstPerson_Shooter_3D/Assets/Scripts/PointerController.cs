using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class PointerController : MonoBehaviour
{
    private RectTransform pointerRect;
    private Graphic[] graphics;
    private bool pointerShown;

    void Start()
    {
        pointerRect = GetComponent<RectTransform>();
        graphics = GetComponentsInChildren<Graphic>(true);

        // Ocultamos el cursor de Windows/Mac
        Cursor.visible = false;

        // Confinamos el cursor para que no pueda salir de la ventana del juego
        Cursor.lockState = CursorLockMode.Confined;

        // Forzamos el ratón físico al centro de la pantalla al iniciar
        if (Mouse.current != null)
        {
            Vector2 centerPosition = new Vector2(Screen.width / 2f, Screen.height / 2f);
            Mouse.current.WarpCursorPosition(centerPosition);
            pointerRect.position = centerPosition;
        }

        SetPointerVisible(PickUpController.slotFull);
    }

    void Update()
    {
        // El puntero solo se ve mientras hay un arma equipada
        if (PickUpController.slotFull != pointerShown)
            SetPointerVisible(PickUpController.slotFull);

        if (Mouse.current != null)
        {
            Vector2 mousePos = Mouse.current.position.ReadValue();
            mousePos.x = Mathf.Clamp(mousePos.x, 0, Screen.width);
            mousePos.y = Mathf.Clamp(mousePos.y, 0, Screen.height);
            pointerRect.position = mousePos;
        }
    }

    private void SetPointerVisible(bool visible)
    {
        pointerShown = visible;
        foreach (Graphic g in graphics) g.enabled = visible;
    }
}