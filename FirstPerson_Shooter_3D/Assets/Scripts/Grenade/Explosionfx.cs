using UnityEngine;

/// <summary>
/// Explosión procedural: destello, bola de fuego, humo turbulento, chispas con estela,
/// ascuas, escombros, onda expansiva, polvo rasante y marca de quemadura en el suelo.
/// No necesita assets ni prefabs.
/// </summary>
public class ExplosionFX : MonoBehaviour
{
    enum Mode { Main, Scorch }
    Mode mode = Mode.Main;

    // ---------- Recursos compartidos ----------
    static Texture2D softTex, smokeTex, hardTex, ringTex, scorchTex;
    static Material alphaMat, smokeMat, debrisMat, additiveMat, ringMat;

    // ---------- Luz ----------
    const float LightDuration = 0.7f;
    const float LightIntensity = 10f;
    static readonly Color LightHot = new Color(1f, 0.85f, 0.55f);
    static readonly Color LightCool = new Color(1f, 0.3f, 0.08f);
    Light flash;
    float flashTime;

    // ---------- Marca de quemadura ----------
    Material scorchMat;
    float scorchAge;
    const float ScorchLife = 14f;
    const float ScorchAlpha = 0.7f;

    // =====================================================================
    //  API
    // =====================================================================
    public static void Spawn(Vector3 position, float radius)
    {
        EnsureAssets();
        float s = Mathf.Max(0.3f, radius / 7f);

        // ¿Explota cerca del suelo? Entonces la explosión se dirige hacia arriba
        bool grounded = Physics.Raycast(position + Vector3.up * 0.3f, Vector3.down, out RaycastHit hit,
                                        1.5f, ~0, QueryTriggerInteraction.Ignore);
        Vector3 groundPoint = grounded ? hit.point : position;
        Vector3 normal = grounded ? hit.normal : Vector3.up;

        var root = new GameObject("ExplosionFX");
        root.transform.position = position;
        var fx = root.AddComponent<ExplosionFX>();
        fx.Build(radius, s, grounded, groundPoint, normal);
        Destroy(root, 8f);
    }

    void Build(float radius, float s, bool grounded, Vector3 groundPoint, Vector3 normal)
    {
        BuildLight(radius);
        BuildCoreFlash(s);
        BuildFireball(s, grounded);
        BuildSmoke(s, grounded);
        BuildSparks(s);
        BuildEmbers(s, grounded);
        BuildDebris(s, grounded);

        if (grounded)
        {
            BuildShockwave(s, groundPoint);
            BuildDust(s, groundPoint);
            CreateScorch(s, groundPoint, normal);
        }
    }

    // =====================================================================
    //  Update (luz y marca de quemadura)
    // =====================================================================
    void Update()
    {
        if (mode == Mode.Scorch) { UpdateScorch(); return; }
        if (flash == null) return;

        flashTime += Time.deltaTime;
        float t = flashTime / LightDuration;
        if (t >= 1f)
        {
            Destroy(flash.gameObject);
            flash = null;
            return;
        }

        float falloff = (1f - t) * (1f - t);
        float flicker = 1f + 0.2f * Mathf.Sin(flashTime * 70f);
        flash.intensity = LightIntensity * falloff * flicker;
        flash.color = Color.Lerp(LightHot, LightCool, t);
    }

    void UpdateScorch()
    {
        scorchAge += Time.deltaTime;
        if (scorchAge >= ScorchLife) { Destroy(gameObject); return; }

        float fadeStart = ScorchLife * 0.6f;
        if (scorchAge > fadeStart && scorchMat != null)
        {
            Color c = scorchMat.color;
            c.a = ScorchAlpha * (1f - (scorchAge - fadeStart) / (ScorchLife - fadeStart));
            scorchMat.color = c;
        }
    }

    void OnDestroy()
    {
        if (scorchMat != null) Destroy(scorchMat);
    }

    // =====================================================================
    //  Luz
    // =====================================================================
    void BuildLight(float radius)
    {
        var go = new GameObject("Flash");
        go.transform.SetParent(transform, false);
        go.transform.localPosition = Vector3.up * 0.6f;

        flash = go.AddComponent<Light>();
        flash.type = LightType.Point;
        flash.color = LightHot;
        flash.range = radius * 2.2f;
        flash.intensity = LightIntensity;
        flash.shadows = LightShadows.None;
    }

    // =====================================================================
    //  Núcleo blanco (destello inicial muy breve)
    // =====================================================================
    void BuildCoreFlash(float s)
    {
        var ps = NewSystem("Core", additiveMat, transform.position);

        var main = ps.main;
        main.loop = false;
        main.duration = 0.2f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.1f, 0.2f);
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(4f * s, 6.5f * s);
        main.startColor = new Color(1f, 0.95f, 0.75f, 1f);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 6;

        Burst(ps, 3f);

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.2f * s;

        var col = ps.colorOverLifetime;
        col.enabled = true;
        col.color = Grad(
            new[] { CK(Color.white, 0f), CK(new Color(1f, 0.8f, 0.4f), 1f) },
            new[] { AK(1f, 0f), AK(0f, 1f) });

        var size = ps.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 0.6f, 1f, 1.3f));

        ps.Play();
    }

    // =====================================================================
    //  Bola de fuego
    // =====================================================================
    void BuildFireball(float s, bool grounded)
    {
        var ps = NewSystem("Fireball", additiveMat, transform.position);

        var main = ps.main;
        main.loop = false;
        main.duration = 0.6f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.45f, 1f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(3f * s, 9f * s);
        main.startSize = new ParticleSystem.MinMaxCurve(1.6f * s, 3.4f * s);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startColor = new Color(1f, 0.55f, 0.15f, 0.85f);
        main.gravityModifier = -0.15f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 120;

        Burst(ps, 26f);
        SetupBlastShape(ps, grounded, 0.3f * s);
        Decelerate(ps, 1.2f, 0.12f);

        var col = ps.colorOverLifetime;
        col.enabled = true;
        col.color = Grad(
            new[]
            {
                CK(new Color(1f, 0.95f, 0.7f), 0f),
                CK(new Color(1f, 0.5f, 0.1f), 0.25f),
                CK(new Color(0.35f, 0.08f, 0.02f), 1f)
            },
            new[] { AK(0.9f, 0f), AK(0.8f, 0.4f), AK(0f, 1f) });

        var size = ps.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 0.4f, 1f, 1.25f));

        var rot = ps.rotationOverLifetime;
        rot.enabled = true;
        rot.z = new ParticleSystem.MinMaxCurve(-0.6f, 0.6f);

        ps.Play();
    }

    // =====================================================================
    //  Humo turbulento
    // =====================================================================
    void BuildSmoke(float s, bool grounded)
    {
        var ps = NewSystem("Smoke", smokeMat, transform.position);

        var main = ps.main;
        main.loop = false;
        main.duration = 0.6f;
        main.startDelay = 0.08f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(2.5f, 4.5f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(1.5f * s, 5f * s);
        main.startSize = new ParticleSystem.MinMaxCurve(2.2f * s, 4.2f * s);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(0.1f, 0.1f, 0.1f, 0.6f), new Color(0.3f, 0.28f, 0.26f, 0.6f));
        main.gravityModifier = -0.03f; // sube despacio
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 60;

        Burst(ps, 18f);
        SetupBlastShape(ps, grounded, 0.6f * s);
        Decelerate(ps, 1.5f, 0.08f);
        AddNoise(ps, 0.9f, 0.35f);

        var col = ps.colorOverLifetime;
        col.enabled = true;
        col.color = Grad(
            new[]
            {
                CK(new Color(0.35f, 0.3f, 0.25f), 0f),
                CK(new Color(0.55f, 0.55f, 0.55f), 1f)
            },
            new[] { AK(0f, 0f), AK(1f, 0.1f), AK(0.6f, 0.6f), AK(0f, 1f) });

        var size = ps.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 0.4f, 1f, 1.5f));

        var rot = ps.rotationOverLifetime;
        rot.enabled = true;
        rot.z = new ParticleSystem.MinMaxCurve(-0.35f, 0.35f);

        ps.Play();
    }

    // =====================================================================
    //  Chispas con estela y rebote
    // =====================================================================
    void BuildSparks(float s)
    {
        var ps = NewSystem("Sparks", additiveMat, transform.position);

        var main = ps.main;
        main.loop = false;
        main.duration = 0.5f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.7f, 1.5f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(10f * s, 26f * s);
        main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.16f);
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(1f, 0.85f, 0.4f, 1f), new Color(1f, 0.5f, 0.1f, 1f));
        main.gravityModifier = 1.5f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 90;

        Burst(ps, 60f);

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.2f;

        var col = ps.colorOverLifetime;
        col.enabled = true;
        col.color = Grad(
            new[] { CK(new Color(1f, 0.9f, 0.5f), 0f), CK(new Color(1f, 0.3f, 0.05f), 1f) },
            new[] { AK(1f, 0f), AK(1f, 0.6f), AK(0f, 1f) });

        var collision = ps.collision;
        collision.enabled = true;
        collision.type = ParticleSystemCollisionType.World;
        collision.bounce = 0.35f;
        collision.dampen = 0.3f;
        collision.lifetimeLoss = 0.1f;

        // Estela de cada chispa
        var trails = ps.trails;
        trails.enabled = true;
        trails.mode = ParticleSystemTrailMode.PerParticle;
        trails.ratio = 1f;
        trails.lifetime = 0.3f;
        trails.minVertexDistance = 0.08f;
        trails.worldSpace = true;
        trails.dieWithParticles = true;
        trails.inheritParticleColor = true;
        trails.widthOverTrail = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0f));
        trails.colorOverTrail = Grad(
            new[] { CK(Color.white, 0f), CK(Color.white, 1f) },
            new[] { AK(1f, 0f), AK(0f, 1f) });

        ps.GetComponent<ParticleSystemRenderer>().trailMaterial = additiveMat;

        ps.Play();
    }

    // =====================================================================
    //  Ascuas flotando
    // =====================================================================
    void BuildEmbers(float s, bool grounded)
    {
        var ps = NewSystem("Embers", additiveMat, transform.position);

        var main = ps.main;
        main.loop = false;
        main.duration = 0.6f;
        main.startDelay = 0.15f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(2f, 4f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(1.5f * s, 5f * s);
        main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.1f);
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(1f, 0.6f, 0.2f, 1f), new Color(1f, 0.35f, 0.08f, 1f));
        main.gravityModifier = -0.08f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 40;

        Burst(ps, 28f);
        SetupBlastShape(ps, grounded, 0.4f * s);
        Decelerate(ps, 1f, 0.06f);
        AddNoise(ps, 1.6f, 0.8f);

        var col = ps.colorOverLifetime;
        col.enabled = true;
        col.color = Grad(
            new[] { CK(new Color(1f, 0.8f, 0.4f), 0f), CK(new Color(1f, 0.25f, 0.05f), 1f) },
            new[] { AK(0f, 0f), AK(1f, 0.1f), AK(0.8f, 0.6f), AK(0f, 1f) });

        ps.Play();
    }

    // =====================================================================
    //  Escombros
    // =====================================================================
    void BuildDebris(float s, bool grounded)
    {
        var ps = NewSystem("Debris", debrisMat, transform.position);

        var main = ps.main;
        main.loop = false;
        main.duration = 0.5f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(1.5f, 3f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(6f * s, 16f * s);
        main.startSize = new ParticleSystem.MinMaxCurve(0.07f, 0.22f);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(0.18f, 0.15f, 0.12f, 1f), new Color(0.35f, 0.3f, 0.25f, 1f));
        main.gravityModifier = 2.5f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 40;

        Burst(ps, 26f);
        SetupBlastShape(ps, grounded, 0.25f * s);

        var rot = ps.rotationOverLifetime;
        rot.enabled = true;
        rot.z = new ParticleSystem.MinMaxCurve(-6f, 6f);

        var col = ps.colorOverLifetime;
        col.enabled = true;
        col.color = Grad(
            new[] { CK(Color.white, 0f), CK(Color.white, 1f) },
            new[] { AK(1f, 0f), AK(1f, 0.8f), AK(0f, 1f) });

        var collision = ps.collision;
        collision.enabled = true;
        collision.type = ParticleSystemCollisionType.World;
        collision.bounce = 0.3f;
        collision.dampen = 0.4f;
        collision.lifetimeLoss = 0.05f;

        ps.Play();
    }

    // =====================================================================
    //  Onda expansiva en el suelo (anillo)
    // =====================================================================
    void BuildShockwave(float s, Vector3 groundPoint)
    {
        var ps = NewSystem("Shockwave", ringMat, groundPoint + Vector3.up * 0.08f);

        var main = ps.main;
        main.loop = false;
        main.duration = 0.2f;
        main.startLifetime = 0.5f;
        main.startSpeed = 0f;
        main.startSize = 15f * s;     // diámetro final del anillo
        main.startColor = new Color(1f, 0.85f, 0.6f, 0.9f);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 2;

        Burst(ps, 1f);

        var shape = ps.shape;
        shape.enabled = false;

        var size = ps.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 0.05f, 1f, 1f));

        var col = ps.colorOverLifetime;
        col.enabled = true;
        col.color = Grad(
            new[] { CK(Color.white, 0f), CK(new Color(1f, 0.6f, 0.3f), 1f) },
            new[] { AK(1f, 0f), AK(0f, 1f) });

        ps.GetComponent<ParticleSystemRenderer>().renderMode = ParticleSystemRenderMode.HorizontalBillboard;

        ps.Play();
    }

    // =====================================================================
    //  Polvo rasante que sale disparado por el suelo
    // =====================================================================
    void BuildDust(float s, Vector3 groundPoint)
    {
        var ps = NewSystem("Dust", smokeMat, groundPoint + Vector3.up * 0.15f);
        ps.transform.rotation = Quaternion.Euler(-90f, 0f, 0f); // el círculo queda horizontal

        var main = ps.main;
        main.loop = false;
        main.duration = 0.4f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(1.3f, 2.4f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(6f * s, 12f * s);
        main.startSize = new ParticleSystem.MinMaxCurve(1.2f * s, 2.4f * s);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(0.45f, 0.38f, 0.3f, 0.55f), new Color(0.55f, 0.5f, 0.45f, 0.55f));
        main.gravityModifier = -0.02f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 40;

        Burst(ps, 26f);

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = 0.4f * s;

        Decelerate(ps, 1.5f, 0.12f);
        AddNoise(ps, 0.5f, 0.4f);

        var col = ps.colorOverLifetime;
        col.enabled = true;
        col.color = Grad(
            new[] { CK(Color.white, 0f), CK(Color.white, 1f) },
            new[] { AK(0f, 0f), AK(1f, 0.1f), AK(0f, 1f) });

        var size = ps.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 0.5f, 1f, 1.5f));

        ps.Play();
    }

    // =====================================================================
    //  Marca de quemadura en el suelo
    // =====================================================================
    void CreateScorch(float s, Vector3 point, Vector3 normal)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
        go.name = "Scorch";
        Destroy(go.GetComponent<Collider>());

        Vector3 up = Mathf.Abs(Vector3.Dot(normal, Vector3.forward)) > 0.9f ? Vector3.up : Vector3.forward;
        go.transform.position = point + normal * 0.03f;
        go.transform.rotation = Quaternion.LookRotation(-normal, up);
        go.transform.Rotate(0f, 0f, Random.Range(0f, 360f), Space.Self);
        float d = 4.5f * s;
        go.transform.localScale = new Vector3(d, d, 1f);

        var rend = go.GetComponent<MeshRenderer>();
        rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        rend.receiveShadows = false;

        var mat = new Material(Shader.Find("Sprites/Default"));
        mat.mainTexture = scorchTex;
        mat.color = new Color(0f, 0f, 0f, ScorchAlpha);
        rend.sharedMaterial = mat;

        var fx = go.AddComponent<ExplosionFX>();
        fx.mode = Mode.Scorch;
        fx.scorchMat = mat;
    }

    // =====================================================================
    //  Utilidades de partículas
    // =====================================================================
    ParticleSystem NewSystem(string name, Material m, Vector3 worldPos)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        go.transform.position = worldPos;

        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        go.GetComponent<ParticleSystemRenderer>().sharedMaterial = m;
        return ps;
    }

    static void Burst(ParticleSystem ps, float count)
    {
        var e = ps.emission;
        e.rateOverTime = 0f;
        e.SetBursts(new[] { new ParticleSystem.Burst(0f, count) });
    }

    // Si explota en el suelo, la explosión sale hacia arriba (hemisferio); en el aire, en esfera
    static void SetupBlastShape(ParticleSystem ps, bool grounded, float radius)
    {
        var shape = ps.shape;
        shape.shapeType = grounded ? ParticleSystemShapeType.Hemisphere : ParticleSystemShapeType.Sphere;
        shape.radius = radius;
        if (grounded) ps.transform.rotation = Quaternion.Euler(-90f, 0f, 0f); // +Z local -> +Y mundo
    }

    // Las partículas salen rápido y frenan enseguida (como una onda de presión)
    static void Decelerate(ParticleSystem ps, float limit, float dampen)
    {
        var lim = ps.limitVelocityOverLifetime;
        lim.enabled = true;
        lim.limit = limit;
        lim.dampen = dampen;
    }

    static void AddNoise(ParticleSystem ps, float strength, float frequency)
    {
        var n = ps.noise;
        n.enabled = true;
        n.strength = strength;
        n.frequency = frequency;
        n.scrollSpeed = 0.2f;
        n.damping = true;
        n.quality = ParticleSystemNoiseQuality.Medium;
    }

    static GradientColorKey CK(Color c, float t) { return new GradientColorKey(c, t); }
    static GradientAlphaKey AK(float a, float t) { return new GradientAlphaKey(a, t); }

    static ParticleSystem.MinMaxGradient Grad(GradientColorKey[] colors, GradientAlphaKey[] alphas)
    {
        var g = new Gradient();
        g.SetKeys(colors, alphas);
        return new ParticleSystem.MinMaxGradient(g);
    }

    // =====================================================================
    //  Texturas y materiales (se generan una sola vez)
    // =====================================================================
    static void EnsureAssets()
    {
        if (alphaMat != null && additiveMat != null && smokeMat != null &&
            debrisMat != null && ringMat != null && scorchTex != null) return;

        softTex = MakeTexture(64, (x, y, d) => Smooth(1f, 0f, d));
        smokeTex = MakeTexture(128, (x, y, d) =>
        {
            float n = Mathf.PerlinNoise(x * 0.09f + 3.1f, y * 0.09f + 7.7f);
            return Smooth(1f, 0.15f, d) * (0.55f + 0.45f * n);
        });
        hardTex = MakeTexture(32, (x, y, d) => 1f - Smooth(0.7f, 1f, d));
        ringTex = MakeTexture(128, (x, y, d) => Smooth(0.6f, 0.82f, d) * (1f - Smooth(0.84f, 1f, d)));
        scorchTex = MakeTexture(128, (x, y, d) =>
        {
            float n = Mathf.PerlinNoise(x * 0.1f + 11f, y * 0.1f + 5f);
            return (1f - Smooth(0.25f, 1f, d)) * (0.6f + 0.4f * n);
        });

        Shader alpha = Shader.Find("Sprites/Default"); // siempre incluido en la build
        Shader additive = Shader.Find("Legacy Shaders/Particles/Additive");
        if (additive == null) additive = Shader.Find("Mobile/Particles/Additive");
        if (additive == null) additive = alpha;      // plan B: mezcla normal

        alphaMat = new Material(alpha) { mainTexture = softTex };
        smokeMat = new Material(alpha) { mainTexture = smokeTex };
        debrisMat = new Material(alpha) { mainTexture = hardTex };
        additiveMat = new Material(additive) { mainTexture = softTex };
        ringMat = new Material(additive) { mainTexture = ringTex };
    }

    static float Smooth(float edge0, float edge1, float x)
    {
        float t = Mathf.Clamp01((x - edge0) / (edge1 - edge0));
        return t * t * (3f - 2f * t);
    }

    static Texture2D MakeTexture(int size, System.Func<int, int, float, float> alphaFunc)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;

        float half = size / 2f;
        Vector2 c = new Vector2(half - 0.5f, half - 0.5f);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), c) / half;
                float a = Mathf.Clamp01(alphaFunc(x, y, d));
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        }
        tex.Apply();
        return tex;
    }
}
