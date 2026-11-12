using UnityEngine;

/// <summary>Hace que el sprite mire siempre a la cámara.</summary>
public class Billboard : MonoBehaviour
{
    [Tooltip("Marcado: solo gira en el eje Y (el sprite se queda vertical, ideal para goblins). " +
             "Desmarcado: copia la rotación completa de la cámara (ideal para murciélagos).")]
    [SerializeField] private bool onlyYAxis = true;

    private Transform cam;

    private void Start()
    {
        if (Camera.main != null) cam = Camera.main.transform;
    }

    private void LateUpdate()
    {
        if (cam == null) return;

        transform.rotation = onlyYAxis
            ? Quaternion.Euler(0f, cam.eulerAngles.y, 0f)
            : cam.rotation;
    }
}