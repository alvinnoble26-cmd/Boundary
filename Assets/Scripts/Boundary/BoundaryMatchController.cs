using System;
using System.Collections.Generic;
using PurrNet;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class BoundaryMatchController : NetworkBehaviour
{
    public static BoundaryMatchController Instance { get; private set; }
    public const int ArenaMassPopulation = 20;
    public const int ArenaMassInnerSurvivors = 5;
    public const float HazardSizeMultiplier = 1.6f;
    public const float EventHazardSizeMultiplier = 1.5f;
    public const float ArenaMassCubeScale = 2.8f * HazardSizeMultiplier;
    public const float ArenaMassBlackHoleScale = 1.75f * HazardSizeMultiplier;

    [Header("Phase timing")]
    [SerializeField, Min(10f)] private float outerRingDuration = 60f;
    [SerializeField, Min(15f)] private float middleRingDuration = 50f;
    [SerializeField, Range(3f, 12f)] private float transitionDuration = 7f;

    [Header("Arena")]
    [SerializeField] private float arenaFloorY = -0.9f;
    [SerializeField, Min(8f)] private float outerRadius = 106f;
    [SerializeField, Min(6f)] private float middleRadius = 68f;
    [SerializeField, Min(4f)] private float innerRadius = 38f;
    [SerializeField, Min(3f)] private float minimumInnerRadius = 38f;
    [SerializeField, Min(0f)] private float innerShrinkPerSecond;
    [SerializeField] private float outerPlatformSurfaceY;
    [SerializeField] private float middlePlatformSurfaceY = 2f;
    [SerializeField] private float innerPlatformSurfaceY = 4.25f;

    [Header("Singularity pull")]
    [SerializeField, Min(0f)] private float outerPull = 0.325f;
    [SerializeField, Min(0f)] private float middlePull = 1.05f;
    [SerializeField, Min(0f)] private float innerPull = 2.75f;
    [SerializeField, Min(0f)] private float innerPullGrowthPerSecond;

    [Header("Boundary event")]
    [SerializeField, Range(1f, 10f)] private float disasterRevealDelay = 5f;
    [SerializeField, Range(2f, 8f)] private float disasterWarningDuration = 3f;
    [SerializeField, Range(15f, 25f)] private float disasterDuration = 20f;

    [Header("Shared object pull")]
    [SerializeField, Min(20f)] private float objectPullRadius = 126f;
    [SerializeField, Min(8)] private int overlapCapacity = 256;

    private readonly SyncVar<BoundaryPhase> phase = new(BoundaryPhase.Waiting, ownerAuth: false);
    private readonly SyncVar<BoundaryTransition> transition = new(BoundaryTransition.None, ownerAuth: false);
    private readonly SyncVar<BoundaryDisaster> disaster = new(BoundaryDisaster.None, ownerAuth: false);
    private readonly SyncVar<BoundaryDisasterStage> disasterStage = new(BoundaryDisasterStage.None, ownerAuth: false);
    private readonly SyncVar<uint> phaseStartTick = new(0u, ownerAuth: false);
    private readonly SyncVar<uint> transitionStartTick = new(0u, ownerAuth: false);
    private readonly SyncVar<uint> disasterRevealTick = new(0u, ownerAuth: false);
    private readonly SyncVar<uint> disasterActiveTick = new(0u, ownerAuth: false);
    private readonly SyncVar<uint> disasterEndTick = new(0u, ownerAuth: false);
    private readonly SyncVar<int> disasterSeed = new(0, ownerAuth: false);
    private readonly SyncVar<float> ringRadius = new(106f, 0.1f, ownerAuth: false);
    private readonly SyncVar<float> pullStrength = new(0.65f, 0.05f, ownerAuth: false);

    private Collider[] overlapBuffer;
    private readonly HashSet<Rigidbody> uniqueBodies = new HashSet<Rigidbody>();
    private readonly Dictionary<int, int> platformContactCounts = new Dictionary<int, int>();
    private System.Random disasterRandom;
    private BoundaryDisaster previousDisaster;
    private bool roundStarted;
    private bool disasterChosen;
    private bool disasterBegan;
    private bool unstableMassPulsed;
    private bool tornadoStarted;
    private bool arenaMassesSpawned;
    private float nextArenaMassRetryAt;
    private int disasterWave;
    private uint nextWaveTick;
    private uint lastContinuousSyncTick;
    private GameObject hazardPrefab;

    public BoundaryPhase Phase => phase.value;
    public BoundaryTransition Transition => transition.value;
    public BoundaryDisaster Disaster => disaster.value;
    public BoundaryDisasterStage DisasterStage => disasterStage.value;
    public float RingRadius => ringRadius.value;
    public float OuterRadius => outerRadius;
    public float MiddleRadius => middleRadius;
    public float InnerRadius => innerRadius;
    public float OuterPlatformSurfaceY => outerPlatformSurfaceY;
    public float MiddlePlatformSurfaceY => middlePlatformSurfaceY;
    public float InnerPlatformSurfaceY => innerPlatformSurfaceY;
    public float BasePullStrength => pullStrength.value;
    public float ArenaFloorY => arenaFloorY;
    public Vector3 SingularityPosition => transform.position;
    public Vector3 ArenaCenter => new Vector3(transform.position.x, arenaFloorY, transform.position.z);
    public int DisasterSeed => disasterSeed.value;
    public bool IsDisasterActive => disasterStage.value == BoundaryDisasterStage.Active;

    public float PlatformSurfaceYAtRadius(float horizontalDistance)
    {
        if (horizontalDistance <= innerRadius)
            return innerPlatformSurfaceY;
        if (horizontalDistance <= middleRadius)
            return middlePlatformSurfaceY;
        return outerPlatformSurfaceY;
    }

    public double NetworkTime
    {
        get
        {
            NetworkManager manager = NetworkManager.main;
            if (manager == null || manager.tickModule == null)
                return Time.unscaledTimeAsDouble;

            return manager.tickModule.syncedPreciseTick / manager.tickModule.tickRate;
        }
    }

    public float PhaseElapsed => SecondsSince(phaseStartTick.value);
    public float TransitionElapsed => transition.value == BoundaryTransition.None
        ? 0f
        : SecondsSince(transitionStartTick.value);
    public float TransitionRemaining => transition.value == BoundaryTransition.None
        ? 0f
        : Mathf.Max(0f, transitionDuration - TransitionElapsed);
    public float TransitionProgress => transition.value == BoundaryTransition.None
        ? 0f
        : Mathf.Clamp01(TransitionElapsed / Mathf.Max(0.1f, transitionDuration));
    public float DisasterElapsed => disasterStage.value == BoundaryDisasterStage.Active
        ? SecondsSince(disasterActiveTick.value)
        : 0f;
    public float DisasterTimeRemaining => disasterStage.value == BoundaryDisasterStage.Warning
        ? Mathf.Max(0f, SecondsBetween(CurrentTick, disasterActiveTick.value))
        : disasterStage.value == BoundaryDisasterStage.Active
            ? Mathf.Max(0f, SecondsBetween(CurrentTick, disasterEndTick.value))
            : 0f;

    public float PhaseTimeRemaining
    {
        get
        {
            if (transition.value != BoundaryTransition.None)
                return TransitionRemaining;

            switch (phase.value)
            {
                case BoundaryPhase.OuterRing:
                    return Mathf.Max(0f, outerRingDuration - PhaseElapsed);
                case BoundaryPhase.MiddleRing:
                    return Mathf.Max(0f, middleRingDuration - PhaseElapsed);
                default:
                    return 0f;
            }
        }
    }

    public float GravitySurgePulse
    {
        get
        {
            if (!IsDisasterActive || disaster.value != BoundaryDisaster.GravitySurge)
                return 0f;
            return BoundaryMath.RhythmicPulse(DisasterElapsed, 3.6f, 0.9f, 1.35f);
        }
    }

    public float EffectivePullStrength => pullStrength.value *
        (1f + GravitySurgePulse * 1.85f * BoundaryMath.DisasterPower(BoundaryDisaster.GravitySurge));

    public float GravityDominance => Mathf.InverseLerp(3.5f, Mathf.Max(5f, innerPull), EffectivePullStrength);

    public float CurrentDirection => 1f;

    public float FracturePulse
    {
        get
        {
            if (!IsDisasterActive || disaster.value != BoundaryDisaster.FractureLines)
                return 0f;
            return BoundaryMath.RhythmicPulse(DisasterElapsed, 3f, 0.9f, 1.15f);
        }
    }

    public float FogAmount
    {
        get
        {
            if (!IsDisasterActive || disaster.value != BoundaryDisaster.DarkMatterFog)
                return 0f;
            return BoundaryMath.EaseInOut(Mathf.Min(DisasterElapsed, 1.6f) / 1.6f);
        }
    }

    private uint CurrentTick
    {
        get
        {
            NetworkManager manager = NetworkManager.main;
            return manager != null && manager.tickModule != null
                ? manager.tickModule.syncedTick
                : (uint)Mathf.Max(0, Mathf.RoundToInt(Time.unscaledTime * 20f));
        }
    }

    private void Awake()
    {
        Instance = this;
        overlapBuffer = new Collider[Mathf.Max(16, overlapCapacity)];
        hazardPrefab = Resources.Load<GameObject>("Boundary/BoundaryHazard");
    }

    protected override void OnSpawned()
    {
        Instance = this;
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();
        if (Instance == this)
            Instance = null;
    }

    private void FixedUpdate()
    {
        if (!isServer)
            return;

        if (!roundStarted)
        {
            int requiredPlayers = GameManager.I != null && GameManager.I.IsPracticeMode ? 1 : 2;
            int loadedPlayers = FindObjectsByType<PlayerMovement>(FindObjectsInactive.Exclude, FindObjectsSortMode.None).Length;
            NetworkManager manager = NetworkManager.main;
            int connectedPlayers = manager != null ? manager.playerCount : 0;
            if (Mathf.Max(loadedPlayers, connectedPlayers) < requiredPlayers)
                return;

            roundStarted = true;
            Debug.Log($"[Boundary] Starting round with {connectedPlayers} connected and {loadedPlayers} loaded player(s).");
            BeginPhase(BoundaryPhase.OuterRing);
            return;
        }

        if (!arenaMassesSpawned && Time.unscaledTime >= nextArenaMassRetryAt)
        {
            nextArenaMassRetryAt = Time.unscaledTime + 0.5f;
            SpawnArenaMassPopulation();
        }

        TickMatchState();
        TickDisaster();
        TickTornado();
        PullSharedObjects();
        SyncContinuousState();
    }

    private void TickMatchState()
    {
        if (transition.value != BoundaryTransition.None)
        {
            if (TransitionElapsed >= transitionDuration)
            {
                if (transition.value == BoundaryTransition.ClosingOuterRing)
                    BeginPhase(BoundaryPhase.MiddleRing);
                else
                    BeginPhase(BoundaryPhase.InnerRing);
            }
            return;
        }

        switch (phase.value)
        {
            case BoundaryPhase.OuterRing:
                if (PhaseElapsed >= outerRingDuration)
                    BeginTransition(BoundaryTransition.ClosingOuterRing);
                break;
            case BoundaryPhase.MiddleRing:
                if (PhaseElapsed >= middleRingDuration)
                    BeginTransition(BoundaryTransition.ClosingMiddleRing);
                break;
        }
    }

    private void BeginPhase(BoundaryPhase newPhase)
    {
        phase.value = newPhase;
        phaseStartTick.value = CurrentTick;
        transition.value = BoundaryTransition.None;
        transitionStartTick.value = 0u;

        switch (newPhase)
        {
            case BoundaryPhase.OuterRing:
                ringRadius.value = outerRadius;
                pullStrength.value = outerPull;
                ResetDisaster();
                SpawnArenaMassPopulation();
                break;
            case BoundaryPhase.MiddleRing:
                ringRadius.value = middleRadius;
                pullStrength.value = middlePull;
                ResetDisaster();
                break;
            case BoundaryPhase.InnerRing:
                ringRadius.value = innerRadius;
                pullStrength.value = innerPull;
                ResetDisaster();
                break;
        }
    }

    private void BeginTransition(BoundaryTransition nextTransition)
    {
        transition.value = nextTransition;
        transitionStartTick.value = CurrentTick;
        if (disasterStage.value == BoundaryDisasterStage.Active ||
            disasterStage.value == BoundaryDisasterStage.Warning)
        {
            disasterStage.value = BoundaryDisasterStage.Recovery;
        }
    }

    private void ResetDisaster()
    {
        disaster.value = BoundaryDisaster.None;
        disasterStage.value = BoundaryDisasterStage.None;
        disasterRevealTick.value = 0u;
        disasterActiveTick.value = 0u;
        disasterEndTick.value = 0u;
        disasterChosen = false;
        disasterBegan = false;
        unstableMassPulsed = false;
        disasterWave = 0;
        nextWaveTick = 0u;
    }

    private void TickDisaster()
    {
        if (phase.value != BoundaryPhase.MiddleRing || transition.value != BoundaryTransition.None)
            return;

        if (!disasterChosen && PhaseElapsed >= disasterRevealDelay)
        {
            disasterChosen = true;
            disaster.value = PickDisaster();
            disasterSeed.value = BoundaryMath.StableHash(Environment.TickCount, (int)CurrentTick);
            disasterRandom = new System.Random(disasterSeed.value);
            disasterRevealTick.value = CurrentTick;
            disasterActiveTick.value = CurrentTick + SecondsToTicks(disasterWarningDuration);
            disasterEndTick.value = disasterActiveTick.value + SecondsToTicks(disasterDuration);
            disasterStage.value = BoundaryDisasterStage.Warning;
            return;
        }

        if (!disasterChosen)
            return;

        if (!disasterBegan && TickReached(disasterActiveTick.value))
        {
            disasterBegan = true;
            disasterStage.value = BoundaryDisasterStage.Active;
            nextWaveTick = CurrentTick;
        }

        if (!disasterBegan)
            return;

        if (disasterStage.value == BoundaryDisasterStage.Active && TickReached(disasterEndTick.value))
        {
            disasterStage.value = BoundaryDisasterStage.Recovery;
            return;
        }

        if (disasterStage.value != BoundaryDisasterStage.Active)
            return;

        RunDisasterServer();
    }

    private BoundaryDisaster PickDisaster()
    {
        Array values = Enum.GetValues(typeof(BoundaryDisaster));
        BoundaryDisaster selected;
        do
        {
            selected = (BoundaryDisaster)UnityEngine.Random.Range(1, values.Length);
        } while (selected == previousDisaster && values.Length > 2);

        previousDisaster = selected;
        return selected;
    }

    private void RunDisasterServer()
    {
        if (!TickReached(nextWaveTick))
            return;

        switch (disaster.value)
        {
            case BoundaryDisaster.BlackRain:
                SpawnRainWave(6);
                ScheduleWave(3.8f, 5);
                break;
            case BoundaryDisaster.CubeStorm:
                SpawnDebrisWave(BoundaryHazardKind.Cube, disasterWave == 0 ? 14 : 7, 1.25f);
                ScheduleWave(5f, 4);
                break;
            case BoundaryDisaster.OrbitalStrike:
                SpawnOrbitWave(9, false);
                ScheduleWave(7f, 3);
                break;
            case BoundaryDisaster.MeteorBreak:
                SpawnDebrisWave(BoundaryHazardKind.Meteor, 5, 2.8f);
                ScheduleWave(4.8f, 4);
                break;
            case BoundaryDisaster.UnstableMass:
                if (disasterWave == 0)
                {
                    SpawnDebrisWave(BoundaryHazardKind.Cube, 12, 1.5f);
                    disasterWave++;
                    nextWaveTick = CurrentTick + SecondsToTicks(4.5f);
                }
                else if (!unstableMassPulsed)
                {
                    unstableMassPulsed = true;
                    PulseEveryMass();
                    nextWaveTick = uint.MaxValue;
                }
                break;
            default:
                nextWaveTick = uint.MaxValue;
                break;
        }
    }

    private void ScheduleWave(float delay, int maximumWaves)
    {
        disasterWave++;
        nextWaveTick = disasterWave >= maximumWaves
            ? uint.MaxValue
            : CurrentTick + SecondsToTicks(delay);
    }

    private void SpawnRainWave(int count)
    {
        for (int i = 0; i < count; i++)
        {
            Vector2 point = RandomPointInRing(0.25f, 0.88f);
            Vector3 landing = new Vector3(
                ArenaCenter.x + point.x,
                PlatformSurfaceYAtRadius(point.magnitude) + 1.2f,
                ArenaCenter.z + point.y);
            Vector3 spawn = new Vector3(landing.x, SingularityPosition.y - 3f, landing.z);
            SpawnHazard(BoundaryHazardKind.BlackRainSingularity, spawn, Vector3.zero, landing,
                12f, 0, 1.35f);
        }
    }

    private void SpawnDebrisWave(BoundaryHazardKind kind, int count, float scale)
    {
        for (int i = 0; i < count; i++)
        {
            Vector2 point = RandomPointInRing(0.15f, 0.90f);
            Vector3 spawn = new Vector3(
                ArenaCenter.x + point.x,
                PlatformSurfaceYAtRadius(point.magnitude) + 20f + NextFloat(0f, 10f),
                ArenaCenter.z + point.y);
            Vector3 velocity = new Vector3(NextFloat(-3f, 3f), NextFloat(-6f, -2f), NextFloat(-3f, 3f));
            float size = scale * NextFloat(0.75f, 1.25f);
            SpawnHazard(kind, spawn, velocity, ArenaCenter, 24f, 0, size);
        }
    }

    private void SpawnOrbitWave(int count, bool tornado)
    {
        for (int i = 0; i < count; i++)
        {
            int lane = i % 3;
            float laneRadius = lane == 0 ? Mathf.Max(5f, ringRadius.value * 0.58f) :
                lane == 1 ? Mathf.Max(8f, ringRadius.value * 0.82f) : Mathf.Max(10f, ringRadius.value * 1.05f);
            float angle = (Mathf.PI * 2f * i / count) + NextFloat(-0.18f, 0.18f);
            float height = lane == 0 ? 1.4f : lane == 1 ? 6f : 12f;
            Vector3 spawn = new Vector3(
                ArenaCenter.x + Mathf.Cos(angle) * laneRadius,
                PlatformSurfaceYAtRadius(laneRadius) + height,
                ArenaCenter.z + Mathf.Sin(angle) * laneRadius);
            Vector3 tangent = new Vector3(-Mathf.Sin(angle), 0f, Mathf.Cos(angle));
            SpawnHazard(
                tornado ? BoundaryHazardKind.TornadoDebris : BoundaryHazardKind.OrbitalDebris,
                spawn,
                tangent * (tornado ? 11f : 18f),
                ArenaCenter,
                tornado ? 55f : 30f,
                lane,
                lane == 0 ? 1.3f : 1f);
        }
    }

    private void TickTornado()
    {
        if (phase.value != BoundaryPhase.InnerRing)
            return;

        if (!tornadoStarted)
        {
            tornadoStarted = true;
            disasterRandom = new System.Random(BoundaryMath.StableHash(Environment.TickCount, (int)CurrentTick));
            SpawnOrbitWave(15, true);
            nextWaveTick = CurrentTick + SecondsToTicks(10f);
            return;
        }

        if (TickReached(nextWaveTick))
        {
            SpawnOrbitWave(3, true);
            nextWaveTick = CurrentTick + SecondsToTicks(10f);
        }
    }

    private void SpawnHazard(
        BoundaryHazardKind kind,
        Vector3 position,
        Vector3 velocity,
        Vector3 target,
        float lifetime,
        int variant,
        float scale)
    {
        if (hazardPrefab == null)
        {
            Debug.LogError("[Boundary] Resources/Boundary/BoundaryHazard prefab is missing.");
            return;
        }

        GameObject instance = Instantiate(hazardPrefab, position, Quaternion.identity);
        instance.transform.localScale = Vector3.one * ScaleEventBoundaryHazard(scale);
        BoundaryHazard hazard = instance.GetComponent<BoundaryHazard>();
        if (hazard == null)
        {
            Destroy(instance);
            return;
        }

        hazard.ServerConfigure(kind, CurrentTick, lifetime, variant, target, velocity);
        NetworkIdentity.Spawn(instance, hazardPrefab);
    }

    private void SpawnArenaMassPopulation()
    {
        if (arenaMassesSpawned)
            return;

        if (hazardPrefab == null)
            hazardPrefab = Resources.Load<GameObject>("Boundary/BoundaryHazard");
        if (hazardPrefab == null)
        {
            Debug.LogError("[Boundary] Cannot spawn arena masses: Resources/Boundary/BoundaryHazard is missing.");
            return;
        }

        HashSet<int> spawnedVariants = new HashSet<int>();
        BoundaryHazard[] existingHazards = FindObjectsByType<BoundaryHazard>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None);
        foreach (BoundaryHazard existingHazard in existingHazards)
        {
            if (existingHazard != null && existingHazard.IsArenaMass)
                spawnedVariants.Add(existingHazard.Variant);
        }

        for (int i = 0; i < ArenaMassPopulation; i++)
        {
            if (spawnedVariants.Contains(i))
                continue;

            bool sphere = i >= ArenaMassPopulation / 2;
            bool survivesInner = i == 0 || i == 4 || i == 10 || i == 14 || i == 18;
            float angle = i * 2.399963f + 0.31f;
            float radius = 24f + (i % 5) * 15.2f + (i / 5) * 1.7f;
            float scale = sphere ? ArenaMassBlackHoleScale : ArenaMassCubeScale;
            float groundClearance = sphere ? scale * 1.65f : scale * 0.5f;
            Vector3 position = new Vector3(
                ArenaCenter.x + Mathf.Cos(angle) * radius,
                PlatformSurfaceYAtRadius(radius) + groundClearance,
                ArenaCenter.z + Mathf.Sin(angle) * radius);

            GameObject instance = Instantiate(hazardPrefab, position, Quaternion.identity);
            instance.name = sphere ? $"Arena Black Hole {i - 9:00}" : $"Arena Mass Cube {i + 1:00}";
            instance.transform.localScale = Vector3.one * scale;
            BoundaryHazard hazard = instance.GetComponent<BoundaryHazard>();
            if (hazard == null)
            {
                Destroy(instance);
                continue;
            }

            hazard.ServerConfigureArenaMass(
                sphere ? BoundaryHazardKind.ArenaBlackHole : BoundaryHazardKind.Cube,
                CurrentTick,
                i,
                survivesInner,
                position);
            try
            {
                NetworkIdentity.Spawn(instance, hazardPrefab);
                spawnedVariants.Add(i);
            }
            catch (Exception exception)
            {
                Debug.LogError($"[Boundary] Failed to network-spawn arena mass {i}: {exception.Message}");
                Destroy(instance);
            }
        }

        arenaMassesSpawned = spawnedVariants.Count >= ArenaMassPopulation;
        if (arenaMassesSpawned)
            Debug.Log($"[Boundary] Spawned all {ArenaMassPopulation} arena cubes and black holes.");
        else
            Debug.LogWarning($"[Boundary] Spawned {spawnedVariants.Count}/{ArenaMassPopulation} arena masses; retrying.");
    }

    public void ServerRegisterPlatformContact(int platformIndex)
    {
        if (!isServer || platformIndex < 0)
            return;

        platformContactCounts.TryGetValue(platformIndex, out int current);
        int next = Mathf.Min(3, current + 1);
        if (next == current)
            return;

        platformContactCounts[platformIndex] = next;
        ApplyPlatformContact(platformIndex, next);
    }

    [ObserversRpc(runLocally: true)]
    private void ApplyPlatformContact(int platformIndex, int hitCount)
    {
        BoundaryArenaPresentation.Instance?.ApplyBlackHoleContact(platformIndex, hitCount);
    }

    private void PulseEveryMass()
    {
        BoundaryHazard[] hazards = FindObjectsByType<BoundaryHazard>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        foreach (BoundaryHazard hazard in hazards)
        {
            if (hazard != null && hazard.Kind == BoundaryHazardKind.Cube)
                hazard.ServerPulse((BoundaryMath.StableHash(disasterSeed.value, hazard.GetInstanceID()) & 1) == 0);
        }

        NetworkArenaCubePhysics[] arenaCubes = FindObjectsByType<NetworkArenaCubePhysics>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        foreach (NetworkArenaCubePhysics cube in arenaCubes)
        {
            if (cube == null || !cube.TryGetComponent(out Rigidbody body) || body.isKinematic)
                continue;
            bool outward = (BoundaryMath.StableHash(disasterSeed.value, cube.GetInstanceID()) & 1) == 0;
            Vector3 direction = body.position - ArenaCenter;
            direction.y = 0.35f;
            if (!outward) direction = -direction;
            body.AddForce(direction.normalized *
                (16f * BoundaryMath.DisasterPower(BoundaryDisaster.UnstableMass)),
                ForceMode.VelocityChange);
        }
    }

    private void PullSharedObjects()
    {
        if (phase.value == BoundaryPhase.Waiting || EffectivePullStrength <= 0f)
            return;

        int count = Physics.OverlapSphereNonAlloc(
            SingularityPosition,
            objectPullRadius,
            overlapBuffer,
            ~0,
            QueryTriggerInteraction.Ignore);

        uniqueBodies.Clear();
        for (int i = 0; i < count; i++)
        {
            Rigidbody body = overlapBuffer[i] != null ? overlapBuffer[i].attachedRigidbody : null;
            if (body == null || body.isKinematic || !uniqueBodies.Add(body))
                continue;
            if (body.GetComponentInParent<PlayerMovement>() != null)
                continue;

            BoundaryHazard boundaryHazard = body.GetComponent<BoundaryHazard>();
            bool supportedObject = boundaryHazard != null ||
                                   body.GetComponent<NetworkArenaCubePhysics>() != null ||
                                   body.GetComponent<NetworkProjectilePhysics>() != null;
            if (!supportedObject)
                continue;

            Vector3 toSingularity = SingularityPosition - body.position;
            if (toSingularity.sqrMagnitude < 0.01f)
                continue;

            float massResistance = 1f / Mathf.Sqrt(Mathf.Max(0.5f, body.mass));
            float altitude = Mathf.InverseLerp(arenaFloorY, SingularityPosition.y, body.position.y);
            float acceleration = EffectivePullStrength * Mathf.Lerp(0.45f, 1.35f, altitude) * massResistance;
            if (boundaryHazard != null && boundaryHazard.IsArenaMass)
            {
                // Player abilities own the short tactical interaction window.
                // The overhead singularity resumes only a light environmental
                // influence after the pulse has visibly moved the mass.
                acceleration *= boundaryHazard.AbilityInfluenceActive ? 0f : 0.18f;
            }
            body.AddForce(toSingularity.normalized * acceleration, ForceMode.Acceleration);

            if (phase.value == BoundaryPhase.InnerRing)
            {
                Vector3 radial = body.position - ArenaCenter;
                radial.y = 0f;
                if (radial.sqrMagnitude > 0.1f)
                {
                    Vector3 tangent = Vector3.Cross(Vector3.up, radial.normalized) * CurrentDirection;
                    float swirl = phase.value == BoundaryPhase.InnerRing ? 5.5f : 3.8f;
                    body.AddForce(tangent * swirl, ForceMode.Acceleration);
                }
            }
        }
    }

    private void SyncContinuousState()
    {
        if (CurrentTick - lastContinuousSyncTick < SecondsToTicks(0.1f))
            return;

        lastContinuousSyncTick = CurrentTick;
        if (transition.value == BoundaryTransition.ClosingOuterRing)
        {
            ringRadius.value = BoundaryMath.TransitionRadius(outerRadius, middleRadius, TransitionElapsed, transitionDuration);
            pullStrength.value = outerPull;
        }
        else if (transition.value == BoundaryTransition.ClosingMiddleRing)
        {
            ringRadius.value = BoundaryMath.TransitionRadius(middleRadius, innerRadius, TransitionElapsed, transitionDuration);
            pullStrength.value = middlePull;
        }
        else if (phase.value == BoundaryPhase.InnerRing)
        {
            ringRadius.value = Mathf.Max(minimumInnerRadius, innerRadius - PhaseElapsed * innerShrinkPerSecond);
            pullStrength.value = innerPull + PhaseElapsed * innerPullGrowthPerSecond;
        }
    }

    private Vector2 RandomPointInRing(float minimumNormalizedRadius, float maximumNormalizedRadius)
    {
        float angle = NextFloat(0f, Mathf.PI * 2f);
        float radius = Mathf.Sqrt(NextFloat(
            minimumNormalizedRadius * minimumNormalizedRadius,
            maximumNormalizedRadius * maximumNormalizedRadius)) * ringRadius.value;
        return new Vector2(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius);
    }

    private float NextFloat(float minimum, float maximum)
    {
        if (disasterRandom == null)
            disasterRandom = new System.Random(disasterSeed.value == 0 ? Environment.TickCount : disasterSeed.value);
        return Mathf.Lerp(minimum, maximum, (float)disasterRandom.NextDouble());
    }

    public static float ScaleBoundaryHazard(float authoredScale)
    {
        return Mathf.Max(0f, authoredScale) * HazardSizeMultiplier;
    }

    public static float ScaleEventBoundaryHazard(float authoredScale)
    {
        return ScaleBoundaryHazard(authoredScale) * EventHazardSizeMultiplier;
    }

    private uint SecondsToTicks(float seconds)
    {
        NetworkManager manager = NetworkManager.main;
        int rate = manager != null && manager.tickModule != null ? manager.tickModule.tickRate : 20;
        return (uint)Mathf.Max(1, Mathf.RoundToInt(seconds * rate));
    }

    private float SecondsSince(uint tick)
    {
        return tick == 0u ? 0f : Mathf.Max(0f, SecondsBetween(tick, CurrentTick));
    }

    private float SecondsBetween(uint from, uint to)
    {
        NetworkManager manager = NetworkManager.main;
        int rate = manager != null && manager.tickModule != null ? manager.tickModule.tickRate : 20;
        return (to - from) / (float)rate;
    }

    private bool TickReached(uint target)
    {
        return target != uint.MaxValue && CurrentTick >= target;
    }
}
