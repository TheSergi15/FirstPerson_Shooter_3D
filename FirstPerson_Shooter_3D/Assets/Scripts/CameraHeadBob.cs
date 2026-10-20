using UnityEngine;

// Balanceo de cámara (head bob) mientras el jugador se desplaza por los raíles.
// Se añade a la CÁMARA. Detecta el movimiento del jugador por sí solo (mide cuánto se mueve
// su posición), así que no hace falta tocar el script de los raíles.
// Solo modifica la posición local de la cámara: no pisa la rotación del CameraController.
public class CameraHeadBob : MonoBehaviour
{
    [Header("Referencias")]
    [Tooltip("Objeto que se mueve por los raíles (el Player). Si está vacío, usa la raíz de la jerarquía de esta cámara.")]
    [SerializeField] private Transform target;

    [Header("Detección de movimiento")]
    [Tooltip("Por debajo de esta velocidad (m/s) no hay balanceo.")]
    [SerializeField] private float minSpeed = 0.3f;
    [Tooltip("A partir de esta velocidad (m/s) el balanceo es completo.")]
    [SerializeField] private float fullBobSpeed = 3f;
    [Tooltip("Más alto = el efecto empieza y termina más rápido.")]
    [SerializeField] private float speedSmoothing = 8f;
    [Tooltip("Ignora el movimiento en vertical (ascensores, rampas) para que no cuente como caminar.")]
    [SerializeField] private bool ignoreVerticalMovement = true;

    [Header("Ritmo de los pasos")]
    [Tooltip("Metros que recorre el jugador en cada paso. Más pequeño = pasos más rápidos.")]
    [SerializeField] private float stepLength = 1.6f;
    [Tooltip("Límite de pasos por segundo, para que a mucha velocidad no vibre.")]
    [SerializeField] private float maxStepsPerSecond = 3.2f;

    [Header("Amplitud (metros)")]
    [Tooltip("Subida y bajada de la cabeza en cada paso. Para caminar: ~0.025. Para correr: ~0.05.")]
    [SerializeField, Range(0f, 0.15f)] private float verticalAmplitude = 0.045f;
    [Tooltip("Balanceo de lado a lado (un ciclo completo cada dos pasos).")]
    [SerializeField, Range(0f, 0.15f)] private float horizontalAmplitude = 0.03f;

    private const float TwoPi = Mathf.PI * 2f;
    private const float TeleportSpeed = 50f;   // Más rápido que esto se considera un salto, no movimiento

    private Vector3 baseLocalPos;
    private Vector3 lastTargetPos;
    private float smoothedSpeed;
    private float phase;
    private bool offsetApplied;

    private void Start()
    {
        if (target == null) target = transform.root;

        baseLocalPos = transform.localPosition;
        lastTargetPos = target.position;
    }

    private void OnEnable()
    {
        // Evita un pico de velocidad falso al reactivar el script
        if (target != null) lastTargetPos = target.position;
    }

    private void OnDisable()
    {
        // Al desactivar el script, la cámara vuelve a su sitio
        if (offsetApplied)
        {
            transform.localPosition = baseLocalPos;
            offsetApplied = false;
        }
        smoothedSpeed = 0f;
    }

    // LateUpdate: así se mide el movimiento del jugador una vez que ya se ha movido en este frame
    private void LateUpdate()
    {
        float dt = Time.deltaTime;
        if (dt <= 0f || target == null) return;

        // Velocidad real del jugador (funciona con cualquier sistema de raíles)
        Vector3 pos = target.position;
        Vector3 delta = pos - lastTargetPos;
        lastTargetPos = pos;
        if (ignoreVerticalMovement) delta.y = 0f;

        float rawSpeed = delta.magnitude / dt;
        if (rawSpeed > TeleportSpeed) rawSpeed = smoothedSpeed;   // Teletransporte: se ignora

        // Suavizado independiente del framerate (también evita tirones si los raíles mueven en FixedUpdate)
        smoothedSpeed = Mathf.Lerp(smoothedSpeed, rawSpeed, 1f - Mathf.Exp(-speedSmoothing * dt));

        // 0 = parado, 1 = balanceo completo
        float intensity = Mathf.InverseLerp(minSpeed, fullBobSpeed, smoothedSpeed);

        if (intensity <= 0.001f)
        {
            if (offsetApplied)
            {
                // Termina el efecto: la cámara vuelve exactamente a su posición base
                transform.localPosition = baseLocalPos;
                offsetApplied = false;
            }
            else
            {
                // Parado: se actualiza la base por si otro script mueve la cámara (agacharse, etc.)
                baseLocalPos = transform.localPosition;
            }
            return;
        }

        // Los pasos dependen de la distancia recorrida: más rápido = pasos más rápidos
        float stepsPerSecond = Mathf.Min(smoothedSpeed / stepLength, maxStepsPerSecond);
        phase += stepsPerSecond * Mathf.PI * dt;   // Una vuelta completa = dos pasos (izquierdo y derecho)
        if (phase > TwoPi) phase -= TwoPi;

        // Recorrido en forma de ocho: sube y baja en cada paso, y se balancea de lado cada dos pasos
        Vector3 offset = new Vector3(
            Mathf.Cos(phase) * horizontalAmplitude,
            Mathf.Sin(phase * 2f) * verticalAmplitude,
            0f) * intensity;

        transform.localPosition = baseLocalPos + offset;
        offsetApplied = true;
    }
}
