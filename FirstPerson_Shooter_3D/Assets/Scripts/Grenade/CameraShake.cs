using UnityEngine;

/// <summary>Ponlo en la Main Camera. Sacude solo la posición local (no toca la rotación del MouseLook).</summary>
public class CameraShake : MonoBehaviour
{
    public static CameraShake Instance { get; private set; }

    public float maxOffset = 0.35f;
    public float frequency = 30f;
    public float recoverySpeed = 1.5f;

    float trauma;
    Vector3 baseLocalPos;

    void Awake()
    {
        Instance = this;
        baseLocalPos = transform.localPosition;
    }

    public void AddTrauma(float amount)
    {
        trauma = Mathf.Clamp01(trauma + amount);
    }

    void LateUpdate()
    {
        if (trauma <= 0f)
        {
            transform.localPosition = baseLocalPos;
            return;
        }

        float power = trauma * trauma;
        float t = Time.time * frequency;
        Vector3 offset = new Vector3(
            Mathf.PerlinNoise(t, 0f) - 0.5f,
            Mathf.PerlinNoise(0f, t) - 0.5f,
            0f) * 2f * maxOffset * power;

        transform.localPosition = baseLocalPos + offset;
        trauma = Mathf.MoveTowards(trauma, 0f, Time.deltaTime * recoverySpeed);
    }
}
