using UnityEngine;
using UnityEngine.Rendering;

public class HealthPickup : MonoBehaviour
{
    [Header("Curación")]
    [Tooltip("Cantidad de vida que restaura al jugador")]
    public float healAmount = 20f;

    [Header("Efecto visual")]
    [Tooltip("Prefab opcional. Si está vacío, se genera el efecto por código.")]
    public GameObject collectEffect;

    [Tooltip("Segundos tras los que se destruye el efecto del prefab.")]
    [Min(0.1f)] public float collectEffectLifetime = 3f;

    [Tooltip("Altura desde donde aparecen las partículas")]
    public float effectHeight = 0.5f;

    private bool collected;

    public void OnShot()
    {
        // Evita curar dos veces si llegan dos disparos en el mismo fotograma.
        if (collected) return;
        collected = true;

        // Curar al jugador.
        if (PlayerHealth.Instance != null)
        {
            PlayerHealth.Instance.Heal(healAmount);
        }

        // Crear el efecto visual.
        SpawnCollectEffect();

        // Destruir el botiquín.
        Destroy(gameObject);
    }

    private void SpawnCollectEffect()
    {
        // Si existe un prefab personalizado, utilizarlo.
        if (collectEffect != null)
        {
            GameObject effect = Instantiate(
                collectEffect,
                transform.position,
                Quaternion.identity
            );

            Destroy(effect, collectEffectLifetime);
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

        // AddComponent arranca el sistema solo con valores por defecto.
        // Hay que pararlo antes de configurar la duración, o Unity da error.
        particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

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

        Material particleMaterial = CreateParticleMaterial();

        if (particleMaterial != null)
        {
            particleRenderer.material = particleMaterial;
            Destroy(particleMaterial, 2f);
        }

        // INICIAR EFECTO
        particles.Play();

        // Seguridad: si el stopAction no llegara a destruirlo, se limpia por tiempo.
        Destroy(effectObject, 2f);
    }

    private Material CreateParticleMaterial()
    {
        // URP: el material creado por código es opaco por defecto,
        // así que hay que pasarlo a transparente para que se vea el desvanecimiento.
        Shader urp = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (urp != null)
        {
            Material m = new Material(urp);
            m.SetFloat("_Surface", 1f);   // 1 = Transparent
            m.SetFloat("_Blend", 0f);     // Alpha
            m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_ZWrite", 0f);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.SetOverrideTag("RenderType", "Transparent");
            m.renderQueue = (int)RenderQueue.Transparent;
            return m;
        }

        // Pipeline Built-in
        Shader builtIn = Shader.Find("Particles/Standard Unlit");
        if (builtIn == null) builtIn = Shader.Find("Sprites/Default");

        return builtIn != null ? new Material(builtIn) : null;
    }
}