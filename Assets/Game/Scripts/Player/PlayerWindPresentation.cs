using UnityEngine;

// Presents observed player motion without adding networked particle objects or RPCs.
public sealed class PlayerWindPresentation : MonoBehaviour
{
    [Header("Sources")]
    [SerializeField] private GameObject worldWindPrefab;
    [SerializeField] private GameObject cameraStreaksPrefab;

    [Header("Speed Response")]
    [SerializeField, Min(0f)] private float startSpeed = 1.2f;
    [SerializeField, Min(0.1f)] private float maximumPresentationSpeed = 24f;
    [SerializeField, Min(0.01f)] private float intensityResponse = 7f;
    [SerializeField, Range(4, 64)] private int maximumStreaks = 32;

    private PlayerMovement movement;
    private Cam cameraController;
    private ParticleSystem worldWind;
    private ParticleSystem streaks;
    private Material worldWindMaterial;
    private Material streakMaterial;
    private float smoothedPlanarSpeed;
    private float smoothedCameraWindIntensity;
    private Vector3 lastObservedPosition;
    private bool hasObservedPosition;
    private float nextSpawnAt;
    private int perimeterSequence;
    private float abilityWindUntil;
    private float abilityWindIntensity;

    public void TriggerAbilityWind(float intensity, float duration)
    {
        if (intensity <= 0f || duration <= 0f)
            return;
        abilityWindIntensity = Mathf.Max(abilityWindIntensity, Mathf.Clamp01(intensity));
        abilityWindUntil = Mathf.Max(abilityWindUntil, Time.time + duration);
    }

    private void Awake()
    {
        movement = GetComponent<PlayerMovement>();
        lastObservedPosition = transform.position;
    }

    private void Update()
    {
        if (movement == null)
            movement = GetComponent<PlayerMovement>();

        Vector3 observedVelocity = GetObservedVelocity();
        float observedSpeed = observedVelocity.magnitude;
        float responseRate = intensityResponse * maximumPresentationSpeed;
        smoothedPlanarSpeed = Mathf.MoveTowards(
            smoothedPlanarSpeed, observedSpeed, responseRate * Time.deltaTime);

        // The local camera streaks are the requested presentation; the world prefab produces oversized arcs.
        DestroyWorldWind();

        if (movement != null && movement.isOwner)
            UpdateOwnerCameraStreaks(smoothedPlanarSpeed, observedVelocity, responseRate);
        else
            DestroyCameraStreaks();
    }

    private void OnDisable()
    {
        DestroyWorldWind();
        DestroyCameraStreaks();
    }

    private Vector3 GetObservedVelocity()
    {
        Vector3 velocity;
        if (movement != null && movement.isOwner && movement.rb != null)
        {
            velocity = movement.rb.linearVelocity;
        }
        else if (hasObservedPosition)
        {
            float deltaTime = Mathf.Max(Time.deltaTime, 0.0001f);
            velocity = (transform.position - lastObservedPosition) / deltaTime;
        }
        else
        {
            velocity = Vector3.zero;
            hasObservedPosition = true;
        }

        lastObservedPosition = transform.position;
        hasObservedPosition = true;
        return velocity;
    }

    private void UpdateWorldWind(float planarSpeed, Vector3 planarVelocity)
    {
        float normalSpeed = movement != null ? movement.maxSpeed : maximumPresentationSpeed;
        float intensity = WorldWindIntensity(planarSpeed, startSpeed, normalSpeed);
        if (intensity <= 0.001f)
        {
            if (worldWind != null)
                worldWind.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            return;
        }

        EnsureWorldWind();
        if (worldWind == null)
            return;

        ParticleSystem.EmissionModule emission = worldWind.emission;
        emission.enabled = true;
        emission.rateOverTimeMultiplier = Mathf.Lerp(0.35f, 1.25f, intensity);
        if (!worldWind.isPlaying)
            worldWind.Play();

        if (planarVelocity.sqrMagnitude > 0.0001f)
            worldWind.transform.rotation = Quaternion.LookRotation(planarVelocity, Vector3.up);
    }

    private void EnsureWorldWind()
    {
        if (worldWind != null || worldWindPrefab == null)
            return;

        GameObject effect = Instantiate(worldWindPrefab, transform);
        effect.name = "PlayerWorldWind";
        effect.transform.localPosition = Vector3.zero;
        effect.transform.localRotation = Quaternion.identity;
        worldWind = effect.GetComponent<ParticleSystem>();
        if (worldWind == null)
            worldWind = effect.GetComponentInChildren<ParticleSystem>();

        if (worldWind != null)
        {
            ConfigureParticleMaterial(worldWind, ref worldWindMaterial, "LocalWorldWindMaterial");
            worldWind.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
    }

    private void UpdateOwnerCameraStreaks(float actualPlanarSpeed, Vector3 worldVelocity, float response)
    {
        if (movement == null)
            return;

        float windTarget = CameraWindIntensity(
            actualPlanarSpeed,
            startSpeed,
            Mathf.Max(maximumPresentationSpeed, startSpeed + 0.1f));
        if (Time.time < abilityWindUntil)
            windTarget = Mathf.Max(windTarget, abilityWindIntensity);
        else
            abilityWindIntensity = 0f;
        smoothedCameraWindIntensity = Mathf.MoveTowards(
            smoothedCameraWindIntensity, windTarget, response * Time.deltaTime);

        if (smoothedCameraWindIntensity <= 0.001f)
        {
            if (streaks != null)
                streaks.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            return;
        }

        EnsureCameraStreaks();
        if (streaks == null)
            return;

        if (Time.time >= nextSpawnAt)
        {
            if (!streaks.isPlaying)
                streaks.Play();
            int streakCount = smoothedCameraWindIntensity >= 0.78f ? 3 :
                smoothedCameraWindIntensity >= 0.42f ? 2 : 1;
            for (int index = 0; index < streakCount; index++)
                EmitPerimeterStreak(smoothedCameraWindIntensity, worldVelocity);
            nextSpawnAt = Time.time + Mathf.Lerp(0.11f, 0.014f, smoothedCameraWindIntensity);
        }
    }

    private void EnsureCameraStreaks()
    {
        if (streaks != null)
            return;
        if (cameraController == null)
            cameraController = GetComponentInChildren<Cam>(true);
        if (cameraController == null || cameraController.cam == null)
            return;

        GameObject effect = cameraStreaksPrefab != null
            ? Instantiate(cameraStreaksPrefab, cameraController.cam, false)
            : new GameObject("Runtime Straight Wind Streaks", typeof(ParticleSystem));
        if (effect.transform.parent == null)
            effect.transform.SetParent(cameraController.cam, false);
        effect.name = "LocalOuterSpeedStreaks";
        // Particle positions already include their camera depth. Offsetting the root as well
        // doubles that depth and visually pulls viewport-edge particles toward the center.
        effect.transform.localPosition = Vector3.zero;
        effect.transform.localRotation = Quaternion.identity;
        effect.transform.localScale = Vector3.one;
        streaks = effect.GetComponent<ParticleSystem>();
        if (streaks == null)
            streaks = effect.GetComponentInChildren<ParticleSystem>();
        if (streaks == null)
            return;

        ParticleSystem.MainModule main = streaks.main;
        main.loop = false;
        main.playOnAwake = false;
        // The amplified tier can emit three streaks per pulse; retain enough
        // live particles for the effect to become denser instead of clipping.
        main.maxParticles = Mathf.Max(maximumStreaks, 64);
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        ParticleSystem.EmissionModule emission = streaks.emission;
        emission.enabled = false;
        ConfigureParticleMaterial(streaks, ref streakMaterial, "LocalWindStreakMaterial");
        ParticleSystemRenderer streakRenderer = streaks.GetComponent<ParticleSystemRenderer>();
        if (streakRenderer != null)
        {
            streakRenderer.renderMode = ParticleSystemRenderMode.Stretch;
            streakRenderer.velocityScale = 0.08f;
            streakRenderer.lengthScale = 9f;
            streakRenderer.cameraVelocityScale = 0f;
        }
        streaks.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        streaks.Play();
    }

    private static void ConfigureParticleMaterial(
        ParticleSystem particleSystem,
        ref Material material,
        string materialName)
    {
        ParticleSystemRenderer particleRenderer = particleSystem.GetComponent<ParticleSystemRenderer>();
        Shader particleShader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (particleRenderer == null || particleShader == null)
            return;

        material = new Material(particleShader)
        {
            name = materialName
        };
        material.SetColor("_BaseColor", Color.white);
        Texture2D lineTexture = CreateStraightLineTexture();
        if (material.HasProperty("_BaseMap"))
            material.SetTexture("_BaseMap", lineTexture);
        if (material.HasProperty("_MainTex"))
            material.SetTexture("_MainTex", lineTexture);
        particleRenderer.material = material;
        particleRenderer.trailMaterial = material;
    }

    private static Texture2D CreateStraightLineTexture()
    {
        const int width = 4;
        const int height = 32;
        Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
        {
            name = "Straight Wind Line",
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };
        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
        {
            float along = y / (float)(height - 1);
            float edge = x == 0 || x == width - 1 ? 0.45f : 1f;
            float alpha = Mathf.Sin(along * Mathf.PI) * edge;
            texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
        }
        texture.Apply(false, true);
        return texture;
    }

    private void EmitPerimeterStreak(float intensity, Vector3 worldVelocity)
    {
        if (cameraController == null || cameraController.cam == null || streaks == null)
            return;

        Camera unityCamera = cameraController.cam.GetComponent<Camera>();
        if (unityCamera == null)
            return;

        float depth = 1.3f;
        Vector3 cameraRelativeVelocity = cameraController.cam.InverseTransformDirection(worldVelocity);
        float inset = Mathf.Lerp(0.035f, 0.012f, intensity);
        Vector2 viewportPosition = PerimeterViewportPosition(perimeterSequence++, inset);
        Vector3 worldPosition = unityCamera.ViewportToWorldPoint(new Vector3(
            viewportPosition.x, viewportPosition.y, depth));
        Vector3 localPosition = streaks.transform.InverseTransformPoint(worldPosition);
        Vector2 radial = (viewportPosition - new Vector2(0.5f, 0.5f)).normalized;
        Vector2 screenDirection = ScreenFlowDirection(cameraRelativeVelocity, radial);
        float forwardAmount = cameraRelativeVelocity.sqrMagnitude > 0.0001f
            ? cameraRelativeVelocity.normalized.z
            : 0f;
        // Air moves opposite the player: forward travel brings streaks toward the
        // camera and expands them outward; backward travel recedes and contracts.
        float depthTravel = -forwardAmount * Mathf.Lerp(0.35f, 1.4f, intensity);
        Vector3 travel = new Vector3(screenDirection.x, screenDirection.y, depthTravel).normalized;
        float sizeVariation = 0.82f + Mathf.Repeat(perimeterSequence * 0.381966f, 1f) * 0.36f;
        ParticleSystem.EmitParams particle = new ParticleSystem.EmitParams
        {
            position = localPosition,
            velocity = travel * Mathf.Lerp(1.8f, 9.2f, intensity),
            startLifetime = Mathf.Lerp(0.14f, 0.25f, intensity),
            startSize = Mathf.Lerp(0.0025f, 0.0062f, intensity) * sizeVariation,
            startColor = Color.Lerp(new Color(1f, 1f, 1f, 0.46f), Color.white, intensity)
        };
        streaks.Emit(particle, 1);
    }

    public static Vector2 PerimeterViewportPosition(int sequence, float inset)
    {
        inset = Mathf.Clamp(inset, 0f, 0.49f);
        int positiveSequence = Mathf.Max(0, sequence);
        int side = positiveSequence % 4;
        int lap = positiveSequence / 4;
        float edgeT = Mathf.Lerp(inset, 1f - inset,
            Mathf.Repeat((lap + 0.5f) * 0.61803398875f, 1f));
        switch (side)
        {
            case 0: return new Vector2(inset, edgeT);
            case 1: return new Vector2(edgeT, 1f - inset);
            case 2: return new Vector2(1f - inset, 1f - edgeT);
            default: return new Vector2(1f - edgeT, inset);
        }
    }

    public static Vector2 ScreenFlowDirection(Vector3 cameraRelativeVelocity, Vector2 radial)
    {
        if (radial.sqrMagnitude < 0.0001f)
            radial = Vector2.up;
        radial.Normalize();

        if (cameraRelativeVelocity.sqrMagnitude < 0.0001f)
            return radial;

        Vector3 localDirection = cameraRelativeVelocity.normalized;
        Vector2 oppositeLateralMotion = new Vector2(-localDirection.x, -localDirection.y);
        float forwardAmount = localDirection.z;
        Vector2 direction = radial * forwardAmount +
            oppositeLateralMotion * Mathf.Lerp(1f, 0.38f, Mathf.Abs(forwardAmount));
        return direction.sqrMagnitude > 0.0001f ? direction.normalized : radial;
    }

    public static float WorldWindIntensity(float planarSpeed, float startThreshold, float normalSpeed)
    {
        if (normalSpeed <= startThreshold || planarSpeed <= startThreshold)
            return 0f;
        return Mathf.Clamp01((planarSpeed - startThreshold) / (normalSpeed - startThreshold));
    }

    public static float HighSpeedIntensity(float planarSpeed, float normalSpeed, float maximumSpeed)
    {
        if (maximumSpeed <= normalSpeed || planarSpeed <= normalSpeed)
            return 0f;
        return Mathf.Clamp01((planarSpeed - normalSpeed) / (maximumSpeed - normalSpeed));
    }

    public static float CameraWindIntensity(float planarSpeed, float startThreshold, float maximumSpeed)
    {
        if (maximumSpeed <= startThreshold || planarSpeed <= startThreshold)
            return 0f;
        float normalizedSpeed = Mathf.Clamp01(
            (planarSpeed - startThreshold) / (maximumSpeed - startThreshold));
        // Normal locomotion should be atmospheric rather than dominant.
        // Dash/Grapple use TriggerAbilityWind and intentionally override this.
        return Mathf.Lerp(0.055f, 0.52f, Mathf.Pow(normalizedSpeed, 1.25f));
    }

    private void DestroyWorldWind()
    {
        if (worldWind == null)
            return;
        Destroy(worldWind.gameObject);
        worldWind = null;
        if (worldWindMaterial != null)
        {
            Destroy(worldWindMaterial);
            worldWindMaterial = null;
        }
    }

    private void DestroyCameraStreaks()
    {
        if (streaks == null)
            return;
        Destroy(streaks.gameObject);
        streaks = null;
        if (streakMaterial != null)
        {
            Destroy(streakMaterial);
            streakMaterial = null;
        }
        nextSpawnAt = 0f;
        perimeterSequence = 0;
        smoothedCameraWindIntensity = 0f;
    }
}
