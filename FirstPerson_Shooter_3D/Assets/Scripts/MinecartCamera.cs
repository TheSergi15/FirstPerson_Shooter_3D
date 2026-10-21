
using UnityEngine;

public class MinecartCamera : MonoBehaviour
{
    [Header("Referencias")]
    [Tooltip("Objeto que se desplaza por los raíles.")]
    [SerializeField] private Transform target;

    [Header("Detección del movimiento")]
    [SerializeField] private float minSpeed = 0.15f;
    [SerializeField] private float fullSpeed = 3f;
    [SerializeField] private float speedSmoothing = 4f;
    [SerializeField] private bool ignoreVerticalMovement = true;

    [Header("Traqueteo de la vagoneta")]
    [Tooltip("Intensidad de las vibraciones mientras avanza.")]
    [Range(0f, 0.1f)]
    [SerializeField] private float vibrationAmplitude = 0.012f;

    [Tooltip("Velocidad de las vibraciones mecánicas.")]
    [SerializeField] private float vibrationFrequency = 18f;

    [Header("Golpes en los raíles")]
    [Tooltip("Frecuencia aproximada de los golpes.")]
    [SerializeField] private float railHitFrequency = 2.2f;

    [Tooltip("Fuerza de los golpes al pasar por las juntas.")]
    [Range(0f, 0.2f)]
    [SerializeField] private float railHitStrength = 0.045f;

    [Header("Balanceo lateral")]
    [Range(0f, 0.15f)]
    [SerializeField] private float lateralAmplitude = 0.035f;

    [SerializeField] private float lateralFrequency = 1.8f;

    [Header("Avance con dificultades")]
    [Tooltip("Intensidad de los tirones al avanzar.")]
    [Range(0f, 0.15f)]
    [SerializeField] private float struggleAmplitude = 0.025f;

    [Tooltip("Frecuencia de los tirones.")]
    [SerializeField] private float struggleFrequency = 2.5f;

    [Header("Frenada")]
    [Tooltip("Desplazamiento de la cámara al detenerse.")]
    [Range(0f, 0.2f)]
    [SerializeField] private float brakingAmplitude = 0.07f;

    [Tooltip("Velocidad de recuperación después de frenar.")]
    [SerializeField] private float brakingRecovery = 3f;

    [Header("Vagoneta parada")]
    [Range(0f, 0.05f)]
    [SerializeField] private float idleVibration = 0.003f;

    [Header("Suavizado")]
    [SerializeField] private float positionSmoothing = 12f;

    private Vector3 baseLocalPos;
    private Vector3 lastTargetPos;

    private float smoothedSpeed;
    private float previousSpeed;
    private float elapsed;
    private float railTimer;
    private float railInterval;
    private float brakingOffset;

    private Vector3 currentOffset;
    private Vector3 railImpulse;

    private void Start()
    {
        if (target == null)
            target = transform.root;

        baseLocalPos = transform.localPosition;

        if (target != null)
            lastTargetPos = target.position;

        railInterval = 1f / Mathf.Max(railHitFrequency, 0.1f);
        railTimer = railInterval;
    }

    private void OnEnable()
    {
        if (target != null)
            lastTargetPos = target.position;
    }

    private void OnDisable()
    {
        transform.localPosition = baseLocalPos;
        currentOffset = Vector3.zero;
        railImpulse = Vector3.zero;
        brakingOffset = 0f;
    }

    private void LateUpdate()
    {
        float dt = Time.deltaTime;

        if (dt <= 0f || target == null)
            return;

        elapsed += dt;

        // Medir el movimiento del jugador.
        Vector3 position = target.position;
        Vector3 delta = position - lastTargetPos;
        lastTargetPos = position;

        if (ignoreVerticalMovement)
            delta.y = 0f;

        float rawSpeed = delta.magnitude / dt;

        // Ignorar teletransportes.
        if (rawSpeed > 50f)
            rawSpeed = smoothedSpeed;

        previousSpeed = smoothedSpeed;

        smoothedSpeed = Mathf.Lerp(
            smoothedSpeed,
            rawSpeed,
            1f - Mathf.Exp(-speedSmoothing * dt)
        );

        float intensity = Mathf.InverseLerp(
            minSpeed,
            fullSpeed,
            smoothedSpeed
        );

        // Detectar una frenada.
        float deceleration = (previousSpeed - smoothedSpeed) / dt;

        if (deceleration > 0.8f)
        {
            brakingOffset -= Mathf.Clamp(
                deceleration * brakingAmplitude * 0.12f,
                0f,
                brakingAmplitude
            );
        }

        brakingOffset = Mathf.Lerp(
            brakingOffset,
            0f,
            1f - Mathf.Exp(-brakingRecovery * dt)
        );

        // Vibración mecánica irregular.
        float vibrationX =
            (Mathf.PerlinNoise(elapsed * vibrationFrequency, 0.3f) - 0.5f) * 2f;

        float vibrationY =
            (Mathf.PerlinNoise(0.7f, elapsed * vibrationFrequency) - 0.5f) * 2f;

        Vector3 vibration = new Vector3(
            vibrationX,
            vibrationY,
            0f
        ) * vibrationAmplitude * intensity;

        // Balanceo lateral lento, como una vagoneta inestable.
        float lateral = Mathf.Sin(
            elapsed * lateralFrequency * Mathf.PI * 2f
        ) * lateralAmplitude * intensity;

        // Tirones que simulan un avance dificultoso.
        float struggle = Mathf.Sin(
            elapsed * struggleFrequency * Mathf.PI * 2f
        );

        struggle *= Mathf.Sin(
            elapsed * 0.8f * Mathf.PI * 2f
        );

        Vector3 struggleOffset = new Vector3(
            0f,
            struggle * struggleAmplitude * intensity,
            0f
        );

        // Golpes al pasar por juntas de los raíles.
        railTimer += dt;

        if (intensity > 0.1f && railTimer >= railInterval)
        {
            railTimer = 0f;

            // Variar ligeramente el intervalo para evitar un ritmo artificial.
            railInterval = (1f / Mathf.Max(railHitFrequency, 0.1f))
                * Random.Range(0.8f, 1.2f);

            railImpulse += new Vector3(
                Random.Range(-railHitStrength, railHitStrength),
                railHitStrength,
                0f
            ) * intensity;
        }

        railImpulse = Vector3.Lerp(
            railImpulse,
            Vector3.zero,
            1f - Mathf.Exp(-14f * dt)
        );

        // Pequeña vibración residual cuando la vagoneta está parada.
        Vector3 idleOffset = Vector3.zero;

        if (intensity < 0.1f)
        {
            idleOffset = new Vector3(
                Mathf.Sin(elapsed * 7f) * idleVibration,
                Mathf.Sin(elapsed * 9f) * idleVibration,
                0f
            );
        }

        // Combinar todos los efectos.
        Vector3 targetOffset =
            vibration +
            new Vector3(lateral, 0f, 0f) +
            struggleOffset +
            railImpulse +
            new Vector3(0f, brakingOffset, 0f) +
            idleOffset;

        currentOffset = Vector3.Lerp(
            currentOffset,
            targetOffset,
            1f - Mathf.Exp(-positionSmoothing * dt)
        );

        transform.localPosition = baseLocalPos + currentOffset;
    }
}
