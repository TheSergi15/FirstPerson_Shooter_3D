using UnityEngine;

public class HeadBobbing : MonoBehaviour
{
    [Header("Configuración de los Pasos")]
    [Tooltip("Velocidad a la que oscila la cámara (frecuencia)")]
    public float bobSpeed = 10f;
    [Tooltip("Qué tanto sube y baja la cámara (amplitud)")]
    public float bobAmount = 0.15f;

    private float defaultPosY = 0f;
    private float timer = 0f;
    private Vector3 lastPlayerPosition;

    [Tooltip("Arrastra aquí el objeto raíz de tu jugador (Player1)")]
    public Transform playerTransform;

    void Start()
    {
        // Guardamos la altura local original de la cámara
        defaultPosY = transform.localPosition.y;

        if (playerTransform != null)
        {
            lastPlayerPosition = playerTransform.position;
        }
    }

    void Update()
    {
        if (playerTransform == null) return;

        // Calculamos cuánto se ha movido el jugador desde el último fotograma
        float distanceMoved = Vector3.Distance(playerTransform.position, lastPlayerPosition);
        lastPlayerPosition = playerTransform.position;

        // Si hay movimiento (el RailManager nos está trasladando)
        if (distanceMoved > 0.001f)
        {
            // Avanzamos el temporizador y calculamos la altura de la cámara con una onda Seno
            timer += Time.deltaTime * bobSpeed;
            float newY = defaultPosY + Mathf.Sin(timer) * bobAmount;

            transform.localPosition = new Vector3(transform.localPosition.x, newY, transform.localPosition.z);
        }
        else
        {
            // Si nos detenemos, reiniciamos el ciclo y devolvemos la cámara suavemente al centro
            timer = 0f;
            float resetY = Mathf.Lerp(transform.localPosition.y, defaultPosY, Time.deltaTime * (bobSpeed / 2f));
            transform.localPosition = new Vector3(transform.localPosition.x, resetY, transform.localPosition.z);
        }
    }
}