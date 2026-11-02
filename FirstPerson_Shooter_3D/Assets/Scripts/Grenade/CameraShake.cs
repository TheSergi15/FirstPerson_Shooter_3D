
using UnityEngine;

public class CameraShake : MonoBehaviour
{
    public static CameraShake Instance { get; private set; }

    [Header("Configuración del temblor")]
    public float maxOffset = 0.35f;
    public float frequency = 30f;
    public float recoverySpeed = 1.5f;

    private float trauma;

    private void Awake()
    {
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public void AddTrauma(float amount)
    {
        trauma = Mathf.Clamp01(trauma + amount);
    }

    // Calcula el desplazamiento, pero no mueve directamente la cámara.
    public Vector3 GetShakeOffset()
    {
        if (trauma <= 0f)
            return Vector3.zero;

        float power = trauma * trauma;
        float t = Time.time * frequency;

        Vector3 offset = new Vector3(
            Mathf.PerlinNoise(t, 0f) - 0.5f,
            Mathf.PerlinNoise(0f, t) - 0.5f,
            0f
        ) * 2f * maxOffset * power;

        trauma = Mathf.MoveTowards(
            trauma,
            0f,
            Time.deltaTime * recoverySpeed
        );

        return offset;
    }
}