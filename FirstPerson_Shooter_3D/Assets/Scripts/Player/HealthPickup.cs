
using UnityEngine;

public class HealthPickup : MonoBehaviour
{
    [Header("Curación")]
    [Tooltip("Cantidad de vida que restaura al jugador")]
    public float healAmount = 20f;

    [Header("Efecto visual")]
    [Tooltip("Prefab opcional. Si está vacío, se genera el efecto por código.")]
    public GameObject collectEffect;

    [Tooltip("Altura desde donde aparecen las partículas")]
    public float effectHeight = 0.5f;

    public void OnShot()
    {
        if (PlayerHealth.Instance != null)
            PlayerHealth.Instance.Heal(healAmount);

        

        SpawnCollectEffect();
        Destroy(gameObject);
    }

    private void SpawnCollectEffect()
    {
        // Si existe un prefab personalizado, utilizarlo.
        if (collectEffect != null)
        {
            Instantiate(
                collectEffect,
                transform.position,
                Quaternion.identity
            );

            return;
        }

        // Si no hay prefab, generar partículas por código.
        CreateProceduralEffect();
    }

    private void CreateProceduralEffect()
    {
        GameObject effectObject = new GameObject("HealthPickupEffect");

        effectObject.transform.position =
            transform.position + Vector3.up * effectHeight;

        ParticleSystem particles =
            effectObject.AddComponent<ParticleSystem>();

        // CONFIGURACIÓN PRINCIPAL
        var main = particles.main;

        main.duration = 0.3f;
        main.loop = false;
        main.playOnAwake = false;

        main.startLifetime = new ParticleSystem.MinMaxCurve(0.4f, 0.75f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(1.5f, 3f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.07f, 0.14f);

        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(0.2f, 1f, 0.3f),
            new Color(0.85f, 1f, 0.8f)
        );

        main.gravityModifier = 0.1f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 40;
        main.stopAction = ParticleSystemStopAction.Destroy;

        // EMISIÓN
        var emission = particles.emission;

        emission.enabled = true;
        emission.rateOverTime = 0f;

        emission.SetBursts(new ParticleSystem.Burst[]
        {
            new ParticleSystem.Burst(0f, (short)28)
        });

        // FORMA DE EMISIÓN
        var shape = particles.shape;

        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.15f;
        shape.radiusThickness = 0.2f;

        // COLOR Y DESVANECIMIENTO
        var color = particles.colorOverLifetime;

        color.enabled = true;

        Gradient gradient = new Gradient();

        gradient.SetKeys(
            new GradientColorKey[]
            {
                new GradientColorKey(Color.green, 0f),
                new GradientColorKey(Color.white, 1f)
            },
            new GradientAlphaKey[]
            {
                new GradientAlphaKey(1f, 0f),
                new GradientAlphaKey(0f, 1f)
            }
        );

        color.color = new ParticleSystem.MinMaxGradient(gradient);

        // TAMAÑO PROGRESIVO
        var size = particles.sizeOverLifetime;

        size.enabled = true;

        AnimationCurve sizeCurve = new AnimationCurve(
            new Keyframe(0f, 1f),
            new Keyframe(0.7f, 0.8f),
            new Keyframe(1f, 0f)
        );

        size.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

        // MATERIAL COMPATIBLE
        ParticleSystemRenderer particleRenderer =
            effectObject.GetComponent<ParticleSystemRenderer>();

        particleRenderer.renderMode = ParticleSystemRenderMode.Billboard;

        Shader particleShader =
            Shader.Find("Universal Render Pipeline/Particles/Unlit");

        if (particleShader == null)
            particleShader = Shader.Find("Particles/Standard Unlit");

        if (particleShader == null)
            particleShader = Shader.Find("Sprites/Default");

        if (particleShader != null)
        {
            Material particleMaterial = new Material(particleShader);

            particleRenderer.material = particleMaterial;

            Destroy(particleMaterial, 2f);
        }

        // INICIAR EFECTO
        particles.Play();
    }
}
