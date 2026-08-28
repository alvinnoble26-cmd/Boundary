using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class VoidAbility : MonoBehaviour, IAbility
{
    public const float CooldownSeconds = 45f;
    public const float DurationSeconds = 15f;
    public const float DarkTransitionSeconds = 3f;
    public const float ImmunitySeconds = DurationSeconds;
    public const float GravityRadius = 70f;
    public const float GravityAcceleration = 18f;
    public const float BlackHoleHeight = 12f;
    public const float BlackHoleSpawnDistance = 10f;
    public const float CasterSpeedMultiplier = 1.3f;
    public const float OpponentSpeedMultiplier = 0.7f;
    public const float SlashIntervalSeconds = 0.55f;
    public const float SlashLifetimeSeconds = 2.2f;

    private static readonly Color ColdWhite = new Color(7f, 8f, 10f, 1f);
    private static readonly Color VoidBlue = new Color(1.1f, 2.4f, 7f, 1f);
    private static readonly Color VoidViolet = new Color(4.5f, 1.2f, 7f, 1f);

    private readonly List<LightSnapshot> lights = new List<LightSnapshot>();
    private readonly List<SlashBurst> slashBursts = new List<SlashBurst>();
    private readonly RaycastHit[] splashSurfaceHits = new RaycastHit[32];
    private Coroutine presentation;
    private GameObject presentationRoot;
    private VolumeProfile volumeProfile;
    private Material brightMaterial;
    private Material darkMaterial;
    private LineRenderer[] rings;
    private LineRenderer[] tendrils;
    private PlayerMovement modifiedLocalMovement;
    private Transform enemyHighlight;
    private BoundaryPlayerState highlightedOpponent;
    private readonly List<GameObject> enemyOutlineObjects = new List<GameObject>();
    private Material enemyOutlineMaterial;
    private int speedModifierId;
    private AmbientMode previousAmbientMode;
    private Color previousAmbientLight;
    private Color previousAmbientSky;
    private Color previousAmbientEquator;
    private Color previousAmbientGround;
    private bool previousFog;
    private Color previousFogColor;
    private float previousFogDensity;
    private bool renderStateCaptured;

    private struct LightSnapshot
    {
        public Light light;
        public float intensity;
    }

    private sealed class SlashBurst
    {
        public GameObject root;
        public LineRenderer[] lines;
        public Light light;
        public float startedAt;
    }

    public AbilityId Id => AbilityId.Void;
    public float CooldownDuration => CooldownSeconds;
    public void Activate() { }

    public static bool CanActivate(float casterHealth, float opponentHealth)
    {
        return BoundaryPlayerState.HasVoidHealthAdvantage(casterHealth, opponentHealth);
    }

    public static bool CanActivateForMode(bool practiceMode, bool hasOpponent,
        float casterHealth, float opponentHealth)
    {
        return practiceMode || (hasOpponent && CanActivate(casterHealth, opponentHealth));
    }

    public static bool ShouldShowEnemyHighlight(bool presentationOwnedByCaster, bool hasOpponent)
    {
        return presentationOwnedByCaster && hasOpponent;
    }

    public static float GravityFalloff(float distance)
    {
        return Mathf.Clamp01(1f - Mathf.Max(0f, distance) / GravityRadius);
    }

    public static Vector3 GravityVelocityChange(Vector3 delta, float elapsed)
    {
        float distance = delta.magnitude;
        if (distance <= 0.05f || distance >= GravityRadius || elapsed <= 0f)
            return Vector3.zero;
        return delta.normalized * GravityAcceleration * GravityFalloff(distance) * elapsed;
    }

    public static Vector3 GetBlackHoleGroundPosition(Vector3 casterPosition, Vector3 aimDirection)
    {
        Vector3 flatDirection = Vector3.ProjectOnPlane(aimDirection, Vector3.up);
        if (flatDirection.sqrMagnitude < 0.0001f)
            flatDirection = Vector3.forward;
        return casterPosition + flatDirection.normalized * BlackHoleSpawnDistance;
    }

    public void BeginPresentation(Vector3 groundPosition, int seed, bool showEnemyHighlight,
        float elapsed = 0f)
    {
        if (Application.isBatchMode)
            return;

        StopPresentation(false);
        presentation = StartCoroutine(PlayPresentation(groundPosition, seed,
            showEnemyHighlight, Mathf.Max(0f, elapsed)));
    }

    private IEnumerator PlayPresentation(Vector3 groundPosition, int seed,
        bool showEnemyHighlight, float elapsedAtStart)
    {
        presentationRoot = new GameObject("Void Domain Presentation");
        Vector3 blackHolePosition = groundPosition + Vector3.up * BlackHoleHeight;
        CreateDomainVolume(presentationRoot.transform);
        CaptureAndDarkenWorld();
        CreateBlackHole(presentationRoot.transform, blackHolePosition);
        if (showEnemyHighlight)
            CreateEnemyHighlight(presentationRoot.transform);
        ApplyLocalSpeedModifier();

        SfxManager.PlayVoidStart();
        SfxManager.StartVoidLoop();
        RequestLocalFeedback();

        System.Random random = new System.Random(seed);
        float startedAt = Time.unscaledTime - Mathf.Min(elapsedAtStart, DurationSeconds);
        float nextSlashAt = elapsedAtStart >= DarkTransitionSeconds
            ? Time.unscaledTime + SlashIntervalSeconds
            : startedAt + DarkTransitionSeconds;
        while (Time.unscaledTime - startedAt < DurationSeconds)
        {
            float elapsed = Time.unscaledTime - startedAt;
            UpdateLighting(elapsed);
            UpdateBlackHole(elapsed);
            UpdateEnemyHighlight();
            if (Time.unscaledTime >= nextSlashAt)
            {
                SpawnSlashBurst(ResolveArenaCenter(groundPosition), random);
                nextSlashAt += SlashIntervalSeconds;
            }
            UpdateSlashBursts();
            yield return null;
        }

        SfxManager.StopVoidLoop();
        SfxManager.PlayVoidEnd();
        StopPresentation(true);
    }

    private void CreateDomainVolume(Transform parent)
    {
        Volume volume = parent.gameObject.AddComponent<Volume>();
        volume.isGlobal = true;
        volume.priority = 900f;
        volume.weight = 1f;
        volumeProfile = ScriptableObject.CreateInstance<VolumeProfile>();
        volume.profile = volumeProfile;

        ColorAdjustments color = volumeProfile.Add<ColorAdjustments>(true);
        color.postExposure.Override(-2.5f);
        color.contrast.Override(32f);
        color.saturation.Override(-82f);
        color.colorFilter.Override(new Color(0.3f, 0.36f, 0.52f, 1f));
        Vignette vignette = volumeProfile.Add<Vignette>(true);
        vignette.color.Override(Color.black);
        vignette.intensity.Override(0.52f);
        vignette.smoothness.Override(0.82f);
        Bloom bloom = volumeProfile.Add<Bloom>(true);
        bloom.intensity.Override(1.2f);
        bloom.threshold.Override(0.35f);
    }

    private void CaptureAndDarkenWorld()
    {
        previousAmbientMode = RenderSettings.ambientMode;
        previousAmbientLight = RenderSettings.ambientLight;
        previousAmbientSky = RenderSettings.ambientSkyColor;
        previousAmbientEquator = RenderSettings.ambientEquatorColor;
        previousAmbientGround = RenderSettings.ambientGroundColor;
        previousFog = RenderSettings.fog;
        previousFogColor = RenderSettings.fogColor;
        previousFogDensity = RenderSettings.fogDensity;
        renderStateCaptured = true;

        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.006f, 0.008f, 0.018f);
        RenderSettings.fog = true;
        RenderSettings.fogColor = new Color(0.004f, 0.006f, 0.015f);
        RenderSettings.fogDensity = Mathf.Max(previousFogDensity, 0.012f);
        BoundaryHazard.SetDarknessGlowForAll(true);
        BoundaryArenaPresentation.Instance?.SetVoidWallGlow(true);

        lights.Clear();
        Light[] sceneLights = FindObjectsByType<Light>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        foreach (Light sceneLight in sceneLights)
        {
            if (sceneLight == null || sceneLight.transform.IsChildOf(presentationRoot.transform))
                continue;
            lights.Add(new LightSnapshot { light = sceneLight, intensity = sceneLight.intensity });
            sceneLight.intensity *= 0.04f;
        }
    }

    private void UpdateLighting(float elapsed)
    {
        float darkness01 = Mathf.Clamp01(elapsed / DarkTransitionSeconds);
        float lightFactor = elapsed < DarkTransitionSeconds
            ? Mathf.Lerp(0.04f, 0.015f, darkness01)
            : Mathf.Lerp(0.34f, 0.68f, Mathf.Clamp01((elapsed - DarkTransitionSeconds) / 0.55f));
        for (int index = 0; index < lights.Count; index++)
        {
            LightSnapshot snapshot = lights[index];
            if (snapshot.light != null)
                snapshot.light.intensity = snapshot.intensity * lightFactor;
        }
    }

    private void CreateBlackHole(Transform parent, Vector3 position)
    {
        Transform blackHole = new GameObject("Void Fantasy Black Hole").transform;
        blackHole.SetParent(parent, false);
        blackHole.position = position;
        brightMaterial = CreateMaterial(Color.white, true);
        darkMaterial = CreateMaterial(new Color(0.003f, 0.002f, 0.008f, 1f), false);

        GameObject core = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        core.name = "Void Event Horizon";
        core.transform.SetParent(blackHole, false);
        core.transform.localScale = Vector3.one * 8.2f;
        Destroy(core.GetComponent<Collider>());
        core.GetComponent<Renderer>().sharedMaterial = darkMaterial;

        rings = new LineRenderer[7];
        for (int index = 0; index < rings.Length; index++)
        {
            GameObject ringObject = new GameObject("Void Lensing Ring", typeof(LineRenderer));
            ringObject.transform.SetParent(blackHole, false);
            LineRenderer ring = ringObject.GetComponent<LineRenderer>();
            ring.useWorldSpace = false;
            ring.loop = true;
            ring.positionCount = 96;
            ring.widthMultiplier = 0.22f + index * 0.09f;
            ring.numCornerVertices = 3;
            ring.sharedMaterial = brightMaterial;
            Color color = index % 3 == 0 ? ColdWhite : index % 3 == 1 ? VoidBlue : VoidViolet;
            ring.startColor = ring.endColor = color;
            rings[index] = ring;
        }

        tendrils = new LineRenderer[9];
        for (int index = 0; index < tendrils.Length; index++)
        {
            GameObject tendrilObject = new GameObject("Void Gravity Tendril", typeof(LineRenderer));
            tendrilObject.transform.SetParent(blackHole, false);
            LineRenderer tendril = tendrilObject.GetComponent<LineRenderer>();
            tendril.useWorldSpace = false;
            tendril.positionCount = 18;
            tendril.widthMultiplier = 0.055f;
            tendril.sharedMaterial = brightMaterial;
            tendril.startColor = new Color(0.7f, 0.78f, 1f, 0.72f);
            tendril.endColor = new Color(0.12f, 0.02f, 0.2f, 0f);
            tendrils[index] = tendril;
        }

        CreateBlackParticles(blackHole, darkMaterial);
        CreateHaloLight(blackHole);
        UpdateBlackHole(0f);
    }

    private void CreateEnemyHighlight(Transform parent)
    {
        BoundaryPlayerState caster = GetComponent<BoundaryPlayerState>();
        if (caster == null || !BoundaryPlayerState.TryGetOpponent(caster, out highlightedOpponent) ||
            highlightedOpponent == null)
            return;

        enemyHighlight = new GameObject("Void Enemy Highlight").transform;
        enemyHighlight.SetParent(parent, false);
        ApplyEnemyOutline(highlightedOpponent.transform);
        UpdateEnemyHighlight();
    }

    private void ApplyEnemyOutline(Transform opponentRoot)
    {
        RestoreEnemyOutline();
        Shader shader = Shader.Find("Boundary/Void Enemy Outline");
        if (shader == null)
            return;
        enemyOutlineMaterial = new Material(shader) { name = "Void Enemy White Outline" };
        enemyOutlineMaterial.SetColor("_OutlineColor", Color.white);
        enemyOutlineMaterial.SetFloat("_OutlineWidth", 0.045f);

        Renderer[] renderers = opponentRoot.GetComponentsInChildren<Renderer>(true);
        foreach (Renderer targetRenderer in renderers)
        {
            if (targetRenderer == null || targetRenderer is ParticleSystemRenderer ||
                targetRenderer.GetComponentInParent<Canvas>() != null)
                continue;

            GameObject outlineObject = new GameObject(targetRenderer.name + " Void White Outline");
            Transform sourceTransform = targetRenderer.transform;
            outlineObject.transform.SetParent(sourceTransform.parent, false);
            outlineObject.transform.localPosition = sourceTransform.localPosition;
            outlineObject.transform.localRotation = sourceTransform.localRotation;
            outlineObject.transform.localScale = sourceTransform.localScale;

            Renderer outlineRenderer = null;
            if (targetRenderer is SkinnedMeshRenderer sourceSkin)
            {
                SkinnedMeshRenderer skin = outlineObject.AddComponent<SkinnedMeshRenderer>();
                skin.sharedMesh = sourceSkin.sharedMesh;
                skin.bones = sourceSkin.bones;
                skin.rootBone = sourceSkin.rootBone;
                skin.localBounds = sourceSkin.localBounds;
                skin.updateWhenOffscreen = true;
                outlineRenderer = skin;
            }
            else if (targetRenderer is MeshRenderer &&
                     targetRenderer.GetComponent<MeshFilter>() is MeshFilter sourceFilter)
            {
                outlineObject.AddComponent<MeshFilter>().sharedMesh = sourceFilter.sharedMesh;
                outlineRenderer = outlineObject.AddComponent<MeshRenderer>();
            }

            if (outlineRenderer == null)
            {
                Destroy(outlineObject);
                continue;
            }

            int materialCount = Mathf.Max(1, targetRenderer.sharedMaterials.Length);
            Material[] outlineMaterials = new Material[materialCount];
            for (int index = 0; index < outlineMaterials.Length; index++)
                outlineMaterials[index] = enemyOutlineMaterial;
            outlineRenderer.sharedMaterials = outlineMaterials;
            outlineRenderer.shadowCastingMode = ShadowCastingMode.Off;
            outlineRenderer.receiveShadows = false;
            outlineRenderer.lightProbeUsage = LightProbeUsage.Off;
            outlineRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            enemyOutlineObjects.Add(outlineObject);
        }
    }

    private void RestoreEnemyOutline()
    {
        foreach (GameObject outlineObject in enemyOutlineObjects)
            if (outlineObject != null)
                Destroy(outlineObject);
        enemyOutlineObjects.Clear();
        if (enemyOutlineMaterial != null)
            Destroy(enemyOutlineMaterial);
        enemyOutlineMaterial = null;
    }

    private void UpdateEnemyHighlight()
    {
        if (enemyHighlight == null || highlightedOpponent == null)
            return;

        enemyHighlight.position = highlightedOpponent.transform.position + Vector3.up * 1.15f;
    }

    private static void CreateBlackParticles(Transform parent, Material material)
    {
        GameObject particleObject = new GameObject("Orbiting Black Matter", typeof(ParticleSystem));
        particleObject.transform.SetParent(parent, false);
        ParticleSystem particles = particleObject.GetComponent<ParticleSystem>();
        ParticleSystem.MainModule main = particles.main;
        main.loop = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(2.2f, 4.5f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(-5.5f, -2.2f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.18f, 0.75f);
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(0f, 0f, 0f, 0.94f), new Color(0.08f, 0.025f, 0.12f, 0.98f));
        main.maxParticles = 240;
        ParticleSystem.EmissionModule emission = particles.emission;
        emission.rateOverTime = 58f;
        ParticleSystem.ShapeModule shape = particles.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 14f;
        shape.radiusThickness = 0.85f;
        ParticleSystemRenderer renderer = particles.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = material;
        particles.Play();
    }

    private static void CreateHaloLight(Transform parent)
    {
        GameObject lightObject = new GameObject("Void Photon Halo", typeof(Light));
        lightObject.transform.SetParent(parent, false);
        Light light = lightObject.GetComponent<Light>();
        light.type = LightType.Point;
        light.color = new Color(0.52f, 0.63f, 1f);
        light.range = 165f;
        light.intensity = 140f;
        light.shadows = LightShadows.None;
    }

    private void UpdateBlackHole(float elapsed)
    {
        if (rings == null)
            return;
        float reveal = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / 0.7f));
        for (int ringIndex = 0; ringIndex < rings.Length; ringIndex++)
        {
            LineRenderer ring = rings[ringIndex];
            if (ring == null)
                continue;
            float radius = (4.6f + ringIndex * 0.68f) * reveal;
            float phase = Time.unscaledTime * (0.7f + ringIndex * 0.16f) * (ringIndex % 2 == 0 ? 1f : -1f);
            for (int point = 0; point < ring.positionCount; point++)
            {
                float angle = point / (float)ring.positionCount * Mathf.PI * 2f + phase;
                float turbulence = Mathf.Sin(angle * (3f + ringIndex % 3) + Time.unscaledTime * 2.1f) * 0.24f;
                ring.SetPosition(point, new Vector3(
                    Mathf.Cos(angle) * (radius + turbulence),
                    Mathf.Sin(angle * 2f + ringIndex) * (0.22f + ringIndex * 0.055f),
                    Mathf.Sin(angle) * (radius + turbulence) * 0.58f));
            }
        }

        for (int tendrilIndex = 0; tendrils != null && tendrilIndex < tendrils.Length; tendrilIndex++)
        {
            LineRenderer tendril = tendrils[tendrilIndex];
            float baseAngle = tendrilIndex / (float)tendrils.Length * Mathf.PI * 2f + Time.unscaledTime * 0.22f;
            for (int point = 0; point < tendril.positionCount; point++)
            {
                float t = point / (float)(tendril.positionCount - 1);
                float radius = Mathf.Lerp(4.2f, 16f, t);
                float angle = baseAngle + t * 1.8f + Mathf.Sin(Time.unscaledTime * 1.7f + t * 7f) * 0.14f;
                tendril.SetPosition(point, new Vector3(Mathf.Cos(angle) * radius,
                    Mathf.Sin(t * Mathf.PI) * 2.2f - t * 1.4f,
                    Mathf.Sin(angle) * radius * 0.62f));
            }
        }
    }

    private void SpawnSlashBurst(Vector3 arenaCenter, System.Random random)
    {
        float playableRadius = BoundaryArenaPresentation.Instance != null
            ? Mathf.Max(18f, BoundaryArenaPresentation.Instance.AuthoredPlayableRadius * 0.82f)
            : 26f;
        ResolveSplashSurface(arenaCenter, playableRadius, random,
            out Vector3 position, out Vector3 surfaceNormal);
        GameObject root = new GameObject("Void White Slash Splash");
        root.transform.SetParent(presentationRoot.transform, false);
        root.transform.SetPositionAndRotation(position + surfaceNormal * 0.14f,
            Quaternion.LookRotation(surfaceNormal, ResolveSurfaceUp(surfaceNormal)) *
            Quaternion.Euler(0f, 0f, (float)random.NextDouble() * 360f));

        Light slashLight = root.AddComponent<Light>();
        slashLight.type = LightType.Point;
        slashLight.color = new Color(0.78f, 0.88f, 1f);
        slashLight.range = 36f;
        slashLight.intensity = 32f;
        slashLight.shadows = LightShadows.None;

        int style = random.Next(0, 4);
        int count = 5 + random.Next(0, 8);
        LineRenderer[] lines = new LineRenderer[count];
        for (int index = 0; index < count; index++)
        {
            GameObject lineObject = new GameObject("White Ink Slash", typeof(LineRenderer));
            lineObject.transform.SetParent(root.transform, false);
            LineRenderer line = lineObject.GetComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.positionCount = 7;
            line.widthCurve = new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(0.18f, 1f),
                new Keyframe(0.72f, 0.65f), new Keyframe(1f, 0f));
            line.widthMultiplier = 0.38f + (float)random.NextDouble() * 1.05f;
            line.sharedMaterial = brightMaterial;
            line.startColor = line.endColor = ColdWhite;
            float length = 8f + (float)random.NextDouble() * 24f;
            float verticalOffset = (index - count * 0.5f) *
                (0.5f + (float)random.NextDouble() * 1.15f);
            float lineAngle = style == 1
                ? index / (float)count * Mathf.PI * 2f + (float)random.NextDouble() * 0.35f
                : ((float)random.NextDouble() - 0.5f) * (style == 0 ? 0.5f : 2.5f);
            Vector2 direction = new Vector2(Mathf.Cos(lineAngle), Mathf.Sin(lineAngle));
            Vector2 tangent = new Vector2(-direction.y, direction.x);
            Vector2 origin = style == 1
                ? Vector2.zero
                : tangent * verticalOffset + new Vector2(
                    ((float)random.NextDouble() - 0.5f) * 4f,
                    ((float)random.NextDouble() - 0.5f) * 4f);
            for (int point = 0; point < line.positionCount; point++)
            {
                float t = point / (float)(line.positionCount - 1);
                float centeredT = style == 1 ? t : t - 0.5f;
                float curve = Mathf.Sin(t * Mathf.PI) *
                    ((float)random.NextDouble() * (style == 2 ? 5f : 2f));
                float jitter = ((float)random.NextDouble() - 0.5f) *
                    (style == 3 ? 2.2f : 0.7f);
                Vector2 point2 = origin + direction * (centeredT * length) +
                    tangent * (curve + jitter);
                line.SetPosition(point, new Vector3(point2.x, point2.y,
                    Mathf.Sin(t * Mathf.PI) * 0.035f));
            }
            lines[index] = line;
        }

        slashBursts.Add(new SlashBurst
        {
            root = root,
            lines = lines,
            light = slashLight,
            startedAt = Time.unscaledTime
        });
        SfxManager.PlayVoidSlash();
    }

    private void ResolveSplashSurface(Vector3 arenaCenter, float playableRadius,
        System.Random random, out Vector3 position, out Vector3 normal)
    {
        Camera localCamera = Camera.main;
        if (localCamera != null)
        {
            Ray visibleSurfaceRay = localCamera.ViewportPointToRay(new Vector3(
                0.12f + (float)random.NextDouble() * 0.76f,
                0.10f + (float)random.NextDouble() * 0.58f,
                0f));
            if (TryFindSplashSurface(visibleSurfaceRay.origin, visibleSurfaceRay.direction,
                    120f, out position, out normal))
                return;

            Vector3 cameraForward = Vector3.ProjectOnPlane(localCamera.transform.forward, Vector3.up);
            if (cameraForward.sqrMagnitude < 0.01f)
                cameraForward = Vector3.forward;
            cameraForward.Normalize();
            Vector3 cameraRight = Vector3.Cross(Vector3.up, cameraForward);
            Vector3 visibleFloorSample = localCamera.transform.position +
                cameraForward * (7f + (float)random.NextDouble() * 20f) +
                cameraRight * (-9f + (float)random.NextDouble() * 18f);
            if (TryFindSplashSurface(visibleFloorSample + Vector3.up * 35f, Vector3.down,
                    80f, out position, out normal))
                return;
        }

        bool requestWall = random.NextDouble() < 0.42;
        float angle = (float)random.NextDouble() * Mathf.PI * 2f;
        Vector3 radial = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
        if (requestWall)
        {
            Vector3 origin = arenaCenter + Vector3.up *
                (2f + (float)random.NextDouble() * 11f);
            if (TryFindSplashSurface(origin, radial, playableRadius * 1.5f,
                    out position, out normal))
                return;
        }

        float radius = Mathf.Sqrt((float)random.NextDouble()) * playableRadius;
        Vector3 floorSample = arenaCenter + radial * radius;
        if (TryFindSplashSurface(floorSample + Vector3.up * 55f, Vector3.down, 110f,
                out position, out normal))
            return;

        position = floorSample + Vector3.up * 0.1f;
        normal = Vector3.up;
    }

    private bool TryFindSplashSurface(Vector3 origin, Vector3 direction, float distance,
        out Vector3 position, out Vector3 normal)
    {
        int hitCount = Physics.RaycastNonAlloc(origin, direction, splashSurfaceHits,
            distance, ~0, QueryTriggerInteraction.Ignore);
        float nearestDistance = float.MaxValue;
        RaycastHit nearest = default;
        bool found = false;
        for (int index = 0; index < hitCount; index++)
        {
            RaycastHit hit = splashSurfaceHits[index];
            Collider collider = hit.collider;
            if (collider == null || hit.distance >= nearestDistance ||
                collider.GetComponentInParent<PlayerMovement>() != null ||
                collider.GetComponentInParent<BoundaryHazard>() != null ||
                collider.GetComponentInParent<NetworkProjectilePhysics>() != null)
                continue;
            nearest = hit;
            nearestDistance = hit.distance;
            found = true;
        }

        position = found ? nearest.point : Vector3.zero;
        normal = found && nearest.normal.sqrMagnitude > 0.001f
            ? nearest.normal.normalized
            : Vector3.up;
        return found;
    }

    private static Vector3 ResolveSurfaceUp(Vector3 normal)
    {
        return Mathf.Abs(Vector3.Dot(normal, Vector3.up)) > 0.92f
            ? Vector3.forward
            : Vector3.up;
    }

    private void UpdateSlashBursts()
    {
        for (int index = slashBursts.Count - 1; index >= 0; index--)
        {
            SlashBurst burst = slashBursts[index];
            float age = Time.unscaledTime - burst.startedAt;
            if (age >= SlashLifetimeSeconds || burst.root == null)
            {
                if (burst.root != null)
                    Destroy(burst.root);
                slashBursts.RemoveAt(index);
                continue;
            }
            float scale = Mathf.SmoothStep(0.15f, 1f, Mathf.Clamp01(age / 0.16f));
            burst.root.transform.localScale = Vector3.one * scale;
            float alpha = 1f - Mathf.Clamp01((age - 0.72f) /
                (SlashLifetimeSeconds - 0.72f));
            if (burst.light != null)
                burst.light.intensity = 32f * alpha * Mathf.Clamp01(age / 0.08f);
            foreach (LineRenderer line in burst.lines)
            {
                if (line != null)
                    line.startColor = line.endColor = new Color(
                        ColdWhite.r, ColdWhite.g, ColdWhite.b, alpha);
            }
        }
    }

    private void ApplyLocalSpeedModifier()
    {
        PlayerMovement[] players = FindObjectsByType<PlayerMovement>(FindObjectsSortMode.None);
        foreach (PlayerMovement player in players)
        {
            if (!player.isOwner)
                continue;
            modifiedLocalMovement = player;
            speedModifierId = GetInstanceID();
            bool caster = player.transform.root == transform.root;
            player.SetExternalSpeedMultiplier(speedModifierId,
                caster ? CasterSpeedMultiplier : OpponentSpeedMultiplier);
            break;
        }
    }

    private static Vector3 ResolveArenaCenter(Vector3 fallback)
    {
        BoundaryMatchController match = BoundaryMatchController.Instance;
        return match != null ? match.ArenaCenter : fallback;
    }

    private static Material CreateMaterial(Color color, bool bright)
    {
        Shader shader = Shader.Find(bright ? "Sprites/Default" : "Universal Render Pipeline/Unlit");
        if (shader == null)
            shader = Shader.Find("Sprites/Default");
        Material material = new Material(shader) { name = bright ? "Void Luminous Material" : "Void Black Material" };
        material.SetColor("_Color", color);
        material.SetColor("_BaseColor", color);
        return material;
    }

    private static void RequestLocalFeedback()
    {
        Camera mainCamera = Camera.main;
        Cam localCameraController = mainCamera != null
            ? mainCamera.GetComponentInParent<Cam>()
            : null;

        if (localCameraController == null || !localCameraController.isOwner)
        {
            Cam[] cameraControllers = FindObjectsByType<Cam>(FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);
            for (int index = 0; index < cameraControllers.Length; index++)
            {
                if (!cameraControllers[index].isOwner)
                    continue;

                localCameraController = cameraControllers[index];
                break;
            }
        }

        localCameraController?.RequestVoidShake();
#if UNITY_IOS || UNITY_ANDROID
        Handheld.Vibrate();
#endif
    }

    private void StopPresentation(bool completed)
    {
        if (!completed && presentation != null)
            StopCoroutine(presentation);
        presentation = null;
        SfxManager.StopVoidLoop();

        if (modifiedLocalMovement != null)
            modifiedLocalMovement.RemoveExternalSpeedMultiplier(speedModifierId);
        modifiedLocalMovement = null;
        RestoreEnemyOutline();
        enemyHighlight = null;
        highlightedOpponent = null;

        for (int index = 0; index < lights.Count; index++)
        {
            LightSnapshot snapshot = lights[index];
            if (snapshot.light != null)
                snapshot.light.intensity = snapshot.intensity;
        }
        lights.Clear();

        if (renderStateCaptured)
        {
            RenderSettings.ambientMode = previousAmbientMode;
            RenderSettings.ambientLight = previousAmbientLight;
            RenderSettings.ambientSkyColor = previousAmbientSky;
            RenderSettings.ambientEquatorColor = previousAmbientEquator;
            RenderSettings.ambientGroundColor = previousAmbientGround;
            RenderSettings.fog = previousFog;
            RenderSettings.fogColor = previousFogColor;
            RenderSettings.fogDensity = previousFogDensity;
            renderStateCaptured = false;
        }
        BoundaryHazard.SetDarknessGlowForAll(false);
        BoundaryArenaPresentation.Instance?.SetVoidWallGlow(false);

        slashBursts.Clear();
        if (presentationRoot != null)
            Destroy(presentationRoot);
        presentationRoot = null;
        if (volumeProfile != null)
            Destroy(volumeProfile);
        volumeProfile = null;
        if (brightMaterial != null)
            Destroy(brightMaterial);
        if (darkMaterial != null)
            Destroy(darkMaterial);
        brightMaterial = null;
        darkMaterial = null;
        rings = null;
        tendrils = null;
    }

    private void OnDisable()
    {
        StopPresentation(false);
    }
}
