using UnityEngine;

/// <summary>
/// Genera por código una explosión realista: destello de luz, bola de fuego,
/// humo que sube y chispas que rebotan. No necesita assets ni prefabs.
/// </summary>
public class ExplosionFX : MonoBehaviour
{
    static Texture2D softTex;
    static Material particleMat;

    const float FlashDuration = 0.3f;
    const float FlashIntensity = 8f;

    Light flash;
    float flashTime;

    public static void Spawn(Vector3 position, float radius)
    {
        var root = new GameObject("ExplosionFX");
        root.transform.position = position;
        var fx = root.AddComponent<ExplosionFX>();
        fx.Build(radius);
        Destroy(root, 6f);
    }

    void Update()
    {
        if (flash == null) return;

        flashTime += Time.deltaTime;
        float t = flashTime / FlashDuration;
        if (t >= 1f)
        {
            Destroy(flash.gameObject);
            flash = null;
            return;
        }
        float k = 1f - t;
        flash.intensity = FlashIntensity * k * k;
    }

    void Build(float radius)
    {
        float s = radius / 7f; // escala respecto a un radio de 7
        Material m = GetMaterial();

        BuildFlash(radius);
        BuildFireball(m, s);
        BuildSmoke(m, s);
        BuildSparks(m, s);
    }

    // ---------- Material / textura ----------
    static Material GetMaterial()
    {
        if (particleMat != null) return particleMat;

        if (softTex == null)
        {
            const int size = 64;
            softTex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            softTex.wrapMode = TextureWrapMode.Clamp;
            Vector2 c = new Vector2(size / 2f - 0.5f, size / 2f - 0.5f);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x, y), c) / (size / 2f);
                    float a = Mathf.Clamp01(1f - d);
                    a = a * a * (3f - 2f * a); // smoothstep
                    softTex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            }
            softTex.Apply();
        }

        // Sprites/Default siempre está incluido en la build y funciona en Built-in y URP
        particleMat = new Material(Shader.Find("Sprites/Default"));
        particleMat.mainTexture = softTex;
        return particleMat;
    }

    ParticleSystem NewSystem(string name, Material m)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        go.GetComponent<ParticleSystemRenderer>().sharedMaterial = m;
        return ps;
    }

    // ---------- Luz ----------
    void BuildFlash(float radius)
    {
        var go = new GameObject("Flash");
        go.transform.SetParent(transform, false);
        go.transform.localPosition = Vector3.up * 0.5f;

        flash = go.AddComponent<Light>();
        flash.type = LightType.Point;
        flash.color = new Color(1f, 0.65f, 0.3f);
        flash.range = radius * 2f;
        flash.intensity = FlashIntensity;
        flash.shadows = LightShadows.None;
    }

    // ---------- Bola de fuego ----------
    void BuildFireball(Material m, float s)
    {
        var ps = NewSystem("Fireball", m);

        var main = ps.main;
        main.loop = false;
        main.duration = 0.5f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.8f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(2f * s, 6f * s);
        main.startSize = new ParticleSystem.MinMaxCurve(1.2f * s, 2.8f * s);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.gravityModifier = -0.1f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 100;

        var emission = ps.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, 22f) });

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.3f * s;

        var col = ps.colorOverLifetime;
        col.enabled = true;
        var g = new Gradient();
        g.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(1f, 0.95f, 0.6f), 0f),
                new GradientColorKey(new Color(1f, 0.45f, 0.08f), 0.3f),
                new GradientColorKey(new Color(0.12f, 0.1f, 0.1f), 1f)
            },
            new[]
            {
                new GradientAlphaKey(1f, 0f),
                new GradientAlphaKey(0.85f, 0.4f),
                new GradientAlphaKey(0f, 1f)
            });
        col.color = new ParticleSystem.MinMaxGradient(g);

        var size = ps.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 0.4f, 1f, 1.2f));

        ps.Play();
    }

    // ---------- Humo ----------
    void BuildSmoke(Material m, float s)
    {
        var ps = NewSystem("Smoke", m);

        var main = ps.main;
        main.loop = false;
        main.duration = 0.5f;
        main.startDelay = 0.05f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(2f, 4f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.5f * s, 2f * s);
        main.startSize = new ParticleSystem.MinMaxCurve(1.5f * s, 3.2f * s);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startColor = new Color(0.25f, 0.25f, 0.25f, 0.6f);
        main.gravityModifier = -0.04f; // sube lentamente
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 60;

        var emission = ps.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, 18f) });

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.6f * s;

        var col = ps.colorOverLifetime;
        col.enabled = true;
        var g = new Gradient();
        g.SetKeys(
            new[]
            {
                new GradientColorKey(Color.white, 0f),
                new GradientColorKey(Color.white, 1f)
            },
            new[]
            {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(1f, 0.12f),
                new GradientAlphaKey(0f, 1f)
            });
        col.color = new ParticleSystem.MinMaxGradient(g);

        var size = ps.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 0.5f, 1f, 1.5f));

        var rot = ps.rotationOverLifetime;
        rot.enabled = true;
        rot.z = new ParticleSystem.MinMaxCurve(-0.4f, 0.4f);

        ps.Play();
    }

    // ---------- Chispas / metralla ----------
    void BuildSparks(Material m, float s)
    {
        var ps = NewSystem("Sparks", m);

        var main = ps.main;
        main.loop = false;
        main.duration = 0.5f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 1.2f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(8f * s, 20f * s);
        main.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.14f);
        main.startColor = new Color(1f, 0.8f, 0.35f, 1f);
        main.gravityModifier = 1.2f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 80;

        var emission = ps.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, 45f) });

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.2f;

        var col = ps.colorOverLifetime;
        col.enabled = true;
        var g = new Gradient();
        g.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(1f, 0.9f, 0.5f), 0f),
                new GradientColorKey(new Color(1f, 0.35f, 0.05f), 1f)
            },
            new[]
            {
                new GradientAlphaKey(1f, 0f),
                new GradientAlphaKey(1f, 0.6f),
                new GradientAlphaKey(0f, 1f)
            });
        col.color = new ParticleSystem.MinMaxGradient(g);

        var collision = ps.collision;
        collision.enabled = true;
        collision.type = ParticleSystemCollisionType.World;
        collision.bounce = 0.35f;
        collision.dampen = 0.3f;
        collision.lifetimeLoss = 0.1f;

        var r = ps.GetComponent<ParticleSystemRenderer>();
        r.renderMode = ParticleSystemRenderMode.Stretch;
        r.lengthScale = 2f;
        r.velocityScale = 0.05f;

        ps.Play();
    }
}
