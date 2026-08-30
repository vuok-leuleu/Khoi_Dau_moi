using System.Collections;
using UnityEngine;

/// <summary>
/// Stylised dirt/rock burst for a fist hitting the ground.
/// Add this component to an empty prefab and call Play(position, normal).
/// The effect builds three lightweight particle systems at runtime:
/// rocks, dust and a flat expanding impact ring.
/// </summary>
[DisallowMultipleComponent]
public sealed class GroundPunchVFX : MonoBehaviour
{
    [Header("Playback")]
    [SerializeField] private bool playOnEnable;
    [SerializeField] private bool destroyAfterPlay = true;

    [Header("Debris")]
    [SerializeField, Min(1)] private int debrisCount = 28;
    [SerializeField, Min(0.1f)] private float debrisLifetime = 1.15f;
    [SerializeField, Min(0.1f)] private float debrisSpeed = 5.5f;
    [SerializeField, Min(0f)] private float debrisSize = 0.18f;
    [SerializeField] private Color debrisColor = new Color(0.34f, 0.19f, 0.09f, 1f);

    [Header("Dust")]
    [SerializeField, Min(1)] private int dustCount = 34;
    [SerializeField, Min(0.1f)] private float dustLifetime = 0.8f;
    [SerializeField] private Color dustColor = new Color(0.48f, 0.32f, 0.18f, 0.72f);

    [Header("Impact")]
    [SerializeField, Min(0.1f)] private float impactRadius = 1.8f;
    [SerializeField, Min(0.1f)] private float impactDuration = 0.42f;
    [SerializeField] private Color impactColor = new Color(0.65f, 0.38f, 0.16f, 0.85f);

    private ParticleSystem debris;
    private ParticleSystem dust;
    private ParticleSystem ring;
    private Mesh rockMesh;
    private Material rockMaterial;
    private Material dustMaterial;
    private Material ringMaterial;
    private Texture2D softCircleTexture;
    private Texture2D ringTexture;
    private Coroutine destroyRoutine;

    private void Awake() => BuildEffect();

    private void OnEnable()
    {
        if (playOnEnable)
            Play(transform.position, transform.up);
    }

    private void OnDestroy()
    {
        // These are created at runtime (not project assets), so release them
        // when one-shot instances are destroyed to avoid leaking per hit.
        if (rockMaterial != null) Destroy(rockMaterial);
        if (dustMaterial != null) Destroy(dustMaterial);
        if (ringMaterial != null) Destroy(ringMaterial);
        if (softCircleTexture != null) Destroy(softCircleTexture);
        if (ringTexture != null) Destroy(ringTexture);
        if (rockMesh != null) Destroy(rockMesh);
    }

    /// <summary>Plays the burst at the current transform position.</summary>
    public void Play() => Play(transform.position, transform.up);

    /// <summary>Plays the burst at a world position aligned to a ground normal.</summary>
    public void Play(Vector3 position, Vector3 normal)
    {
        BuildEffect();
        transform.SetPositionAndRotation(position, Quaternion.FromToRotation(Vector3.up, normal.sqrMagnitude > 0.001f ? normal.normalized : Vector3.up));

        if (destroyRoutine != null)
            StopCoroutine(destroyRoutine);

        debris.Clear();
        dust.Clear();
        ring.Clear();
        EmitDebris();
        EmitDust();
        EmitRing();

        if (destroyAfterPlay)
            destroyRoutine = StartCoroutine(DestroyAfter(Mathf.Max(debrisLifetime, dustLifetime, impactDuration) + 0.15f));
    }

    private void BuildEffect()
    {
        if (debris != null)
            return;

        rockMesh = CreateRockMesh();
        rockMaterial = CreateMaterial("Ground Punch Rocks", new Color(0.34f, 0.19f, 0.09f, 1f), false);
        softCircleTexture = CreateSoftCircleTexture(64);
        ringTexture = CreateRingTexture(128);
        dustMaterial = CreateMaterial("Ground Punch Dust", dustColor, true, softCircleTexture);
        ringMaterial = CreateMaterial("Ground Punch Ring", impactColor, true, ringTexture);

        debris = CreateParticleSystem("Debris", rockMaterial, ParticleSystemRenderMode.Mesh);
        ConfigureDebris(debris);
        dust = CreateParticleSystem("Dust", dustMaterial, ParticleSystemRenderMode.Billboard);
        ConfigureDust(dust);
        ring = CreateParticleSystem("Impact Ring", ringMaterial, ParticleSystemRenderMode.HorizontalBillboard);
        ConfigureRing(ring);
    }

    private ParticleSystem CreateParticleSystem(string objectName, Material material, ParticleSystemRenderMode renderMode)
    {
        var child = new GameObject(objectName);
        child.transform.SetParent(transform, false);
        var ps = child.AddComponent<ParticleSystem>();
        var renderer = child.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = renderMode;
        renderer.material = material;
        if (renderMode == ParticleSystemRenderMode.Mesh)
            renderer.mesh = rockMesh;
        return ps;
    }

    private static void ConfigureCommon(ParticleSystem ps, float lifetime)
    {
        var main = ps.main;
        main.loop = false;
        main.playOnAwake = false;
        main.duration = lifetime;
        main.startLifetime = lifetime;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.maxParticles = 256;
        var emission = ps.emission;
        emission.enabled = false;
    }

    private void ConfigureDebris(ParticleSystem ps)
    {
        ConfigureCommon(ps, debrisLifetime);
        var main = ps.main;
        main.startSize3D = true;
        main.startSizeX = new ParticleSystem.MinMaxCurve(0.65f, 1.35f);
        main.startSizeY = new ParticleSystem.MinMaxCurve(0.55f, 1.2f);
        main.startSizeZ = new ParticleSystem.MinMaxCurve(0.65f, 1.35f);
        main.startRotation3D = true;
        main.startRotationX = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startRotationY = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startRotationZ = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.gravityModifier = 1.25f;
        main.startColor = debrisColor;
        var rotation = ps.rotationOverLifetime;
        rotation.enabled = true;
        rotation.separateAxes = true;
        rotation.x = new ParticleSystem.MinMaxCurve(-2.5f, 2.5f);
        rotation.y = new ParticleSystem.MinMaxCurve(-2.5f, 2.5f);
        rotation.z = new ParticleSystem.MinMaxCurve(-2.5f, 2.5f);
    }

    private void ConfigureDust(ParticleSystem ps)
    {
        ConfigureCommon(ps, dustLifetime);
        var main = ps.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(dustLifetime * 0.65f, dustLifetime * 1.35f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.45f, 1.15f);
        main.startColor = dustColor;
        var size = ps.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
            new Keyframe(0f, 0.35f), new Keyframe(0.2f, 1f), new Keyframe(1f, 1.35f)));
        var color = ps.colorOverLifetime;
        color.enabled = true;
        color.color = new Gradient
        {
            colorKeys = new[] { new GradientColorKey(dustColor, 0f), new GradientColorKey(dustColor * 1.15f, 0.45f) },
            alphaKeys = new[] { new GradientAlphaKey(dustColor.a, 0f), new GradientAlphaKey(dustColor.a * 0.65f, 0.55f), new GradientAlphaKey(0f, 1f) }
        };
    }

    private void ConfigureRing(ParticleSystem ps)
    {
        ConfigureCommon(ps, impactDuration);
        var main = ps.main;
        main.startLifetime = impactDuration;
        main.startSize = 0.25f;
        main.startColor = impactColor;
        var size = ps.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.15f), new Keyframe(0.7f, 1f), new Keyframe(1f, 1.12f)));
        var color = ps.colorOverLifetime;
        color.enabled = true;
        color.color = new Gradient
        {
            colorKeys = new[] { new GradientColorKey(impactColor, 0f), new GradientColorKey(impactColor, 0.5f) },
            alphaKeys = new[] { new GradientAlphaKey(impactColor.a, 0f), new GradientAlphaKey(impactColor.a * 0.4f, 0.55f), new GradientAlphaKey(0f, 1f) }
        };
    }

    private void EmitDebris()
    {
        for (int i = 0; i < debrisCount; i++)
        {
            float angle = Random.Range(0f, Mathf.PI * 2f);
            Vector3 radial = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
            Vector3 velocity = radial * Random.Range(debrisSpeed * 0.55f, debrisSpeed) + Vector3.up * Random.Range(debrisSpeed * 0.7f, debrisSpeed * 1.35f);
            Vector2 offset = Random.insideUnitCircle * 0.22f;
            var emit = new ParticleSystem.EmitParams
            {
                position = new Vector3(offset.x, 0f, offset.y),
                velocity = velocity,
                startSize = debrisSize * Random.Range(0.65f, 1.4f),
                startLifetime = Random.Range(debrisLifetime * 0.7f, debrisLifetime * 1.2f),
                startColor = Color.Lerp(debrisColor * 0.7f, debrisColor * 1.25f, Random.value),
                rotation3D = new Vector3(Random.value, Random.value, Random.value) * Mathf.PI * 2f
            };
            debris.Emit(emit, 1);
        }
        debris.Play();
    }

    private void EmitDust()
    {
        for (int i = 0; i < dustCount; i++)
        {
            float angle = Random.Range(0f, Mathf.PI * 2f);
            Vector3 radial = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
            Vector3 velocity = radial * Random.Range(0.8f, 2.5f) + Vector3.up * Random.Range(0.45f, 1.7f);
            Vector2 offset = Random.insideUnitCircle * 0.3f;
            var emit = new ParticleSystem.EmitParams
            {
                position = new Vector3(offset.x, 0f, offset.y),
                velocity = velocity,
                startSize = Random.Range(0.45f, 1.0f),
                startLifetime = Random.Range(dustLifetime * 0.6f, dustLifetime * 1.25f),
                startColor = Color.Lerp(dustColor * 0.75f, dustColor * 1.15f, Random.value)
            };
            dust.Emit(emit, 1);
        }
        dust.Play();
    }

    private void EmitRing()
    {
        var emit = new ParticleSystem.EmitParams { startSize = impactRadius * 2f, startLifetime = impactDuration, startColor = impactColor };
        ring.Emit(emit, 1);
        ring.Play();
    }

    private IEnumerator DestroyAfter(float seconds)
    {
        yield return new WaitForSeconds(seconds);
        if (this != null)
            Destroy(gameObject);
    }

    private static Material CreateMaterial(string materialName, Color color, bool transparent, Texture2D texture = null)
    {
        Shader shader = Shader.Find(transparent ? "Universal Render Pipeline/Particles/Unlit" : "Universal Render Pipeline/Lit");
        if (shader == null)
            shader = Shader.Find(transparent ? "Particles/Standard Unlit" : "Standard");
        var material = new Material(shader) { name = materialName };
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
        if (material.HasProperty("_Color")) material.SetColor("_Color", color);
        if (texture != null && material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", texture);
        if (texture != null && material.HasProperty("_MainTex")) material.SetTexture("_MainTex", texture);
        if (transparent && material.HasProperty("_Surface")) material.SetFloat("_Surface", 1f);
        return material;
    }

    private static Mesh CreateRockMesh()
    {
        var temp = GameObject.CreatePrimitive(PrimitiveType.Cube);
        var mesh = Object.Instantiate(temp.GetComponent<MeshFilter>().sharedMesh);
        Object.Destroy(temp);
        return mesh;
    }

    private static Texture2D CreateSoftCircleTexture(int size)
    {
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "GroundPunch_SoftCircle" };
        for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
        {
            float d = Vector2.Distance(new Vector2(x, y), Vector2.one * (size - 1) * 0.5f) / (size * 0.5f);
            texture.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(1f - d) * Mathf.Clamp01(1.2f - d)));
        }
        texture.Apply();
        return texture;
    }

    private static Texture2D CreateRingTexture(int size)
    {
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "GroundPunch_Ring" };
        for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
        {
            float d = Vector2.Distance(new Vector2(x, y), Vector2.one * (size - 1) * 0.5f) / (size * 0.5f);
            float alpha = Mathf.Clamp01(1f - Mathf.Abs(d - 0.72f) / 0.12f) * Mathf.Clamp01(1f - d);
            texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
        }
        texture.Apply();
        return texture;
    }
}
