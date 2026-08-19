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
    private float smoothedHighSpeedIntensity;
    private Vector3 lastObservedPosition;
    private bool hasObservedPosition;
    private float nextSpawnAt;
    private int patternPhase;

    private void Awake()
    {
        movement = GetComponent<PlayerMovement>();
        lastObservedPosition = transform.position;
    }

    private void Update()
    {
        if (movement == null)
            movement = GetComponent<PlayerMovement>();

        Vector3 observedVelocity = GetObservedPlanarVelocity();
        float observedSpeed = observedVelocity.magnitude;
        float responseRate = intensityResponse * maximumPresentationSpeed;
        smoothedPlanarSpeed = Mathf.MoveTowards(
            smoothedPlanarSpeed, observedSpeed, responseRate * Time.deltaTime);

        // The local camera streaks are the requested presentation; the world prefab produces oversized arcs.
        DestroyWorldWind();

        if (movement != null && movement.isOwner)
            UpdateOwnerCameraStreaks(observedSpeed, observedVelocity, responseRate);
        else
            DestroyCameraStreaks();
    }

    private void OnDisable()
    {
        DestroyWorldWind();
        DestroyCameraStreaks();
    }

    private Vector3 GetObservedPlanarVelocity()
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
        velocity.y = 0f;
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

        float highSpeedTarget = HighSpeedIntensity(
            actualPlanarSpeed,
            movement.maxSpeed,
            Mathf.Max(maximumPresentationSpeed, movement.maxSpeed + 0.1f));
        smoothedHighSpeedIntensity = Mathf.MoveTowards(
            smoothedHighSpeedIntensity, highSpeedTarget, response * Time.deltaTime);

        if (smoothedHighSpeedIntensity <= 0.001f)
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
            EmitRadialStreakPattern(smoothedHighSpeedIntensity, worldVelocity);
            nextSpawnAt = Time.time + Mathf.Lerp(0.16f, 0.028f, smoothedHighSpeedIntensity);
        }
    }

    private void EnsureCameraStreaks()
    {
        if (streaks != null)
            return;
        if (cameraStreaksPrefab == null)
            return;

        if (cameraController == null)
            cameraController = GetComponentInChildren<Cam>(true);
        if (cameraController == null || cameraController.cam == null)
            return;

        GameObject effect = Instantiate(cameraStreaksPrefab, cameraController.cam, false);
        effect.name = "LocalOuterSpeedStreaks";
        effect.transform.localPosition = new Vector3(0f, 0f, 1.3f);
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
        main.maxParticles = maximumStreaks;
        ParticleSystem.EmissionModule emission = streaks.emission;
        emission.enabled = false;
        ConfigureParticleMaterial(streaks, ref streakMaterial, "LocalWindStreakMaterial");
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
        particleRenderer.material = material;
        particleRenderer.trailMaterial = material;
    }

    private void EmitRadialStreakPattern(float intensity, Vector3 worldVelocity)
    {
        if (cameraController == null || cameraController.cam == null || streaks == null)
            return;

        Camera unityCamera = cameraController.cam.GetComponent<Camera>();
        if (unityCamera == null)
            return;

        float depth = 1.3f;
        float halfHeight = Mathf.Tan(unityCamera.fieldOfView * Mathf.Deg2Rad * 0.5f) * depth;
        float halfWidth = halfHeight * unityCamera.aspect;
        float phaseDegrees = patternPhase++ % 2 == 0 ? 0f : 45f;
        Vector3 cameraRelativeVelocity = cameraController.cam.InverseTransformDirection(worldVelocity);
        Vector3 screenTravel = new Vector3(cameraRelativeVelocity.x, cameraRelativeVelocity.y, 0f);
        if (screenTravel.sqrMagnitude > 0.0001f)
            screenTravel.Normalize();
        float forwardSign = cameraRelativeVelocity.z >= 0f ? 1f : -1f;
        int streakCount = Mathf.Clamp(Mathf.RoundToInt(Mathf.Lerp(2f, 8f, intensity)), 2, 8);

        for (int index = 0; index < streakCount; index++)
        {
            float angle = (phaseDegrees + index * (360f / streakCount)) * Mathf.Deg2Rad;
            Vector3 edgePosition = new Vector3(
                Mathf.Cos(angle) * halfWidth,
                Mathf.Sin(angle) * halfHeight,
                0f);
            Vector3 radial = edgePosition.normalized;
            Vector3 travel = (radial + screenTravel * 0.55f).normalized * forwardSign;
            ParticleSystem.EmitParams particle = new ParticleSystem.EmitParams
            {
                position = edgePosition * Mathf.Lerp(0.86f, 0.96f, intensity) + Vector3.forward * depth,
                velocity = travel * Mathf.Lerp(1.5f, 5.5f, intensity),
                startLifetime = Mathf.Lerp(0.08f, 0.15f, intensity),
                startSize = Mathf.Lerp(0.0015f, 0.004f, intensity),
                startColor = Color.white
            };
            streaks.Emit(particle, 1);
        }
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
        smoothedHighSpeedIntensity = 0f;
    }
}
