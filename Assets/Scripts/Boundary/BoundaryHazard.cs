using System.Collections;
using System.Collections.Generic;
using PurrNet;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody))]
public sealed class BoundaryHazard : NetworkBehaviour
{
    private static readonly List<BoundaryHazard> ActiveHazards = new List<BoundaryHazard>();

    private readonly SyncVar<BoundaryHazardKind> kind = new(BoundaryHazardKind.Cube, ownerAuth: false);
    private readonly SyncVar<uint> spawnTick = new(0u, ownerAuth: false);
    private readonly SyncVar<float> lifetime = new(20f, ownerAuth: false);
    private readonly SyncVar<int> variant = new(0, ownerAuth: false);

    [Header("Orbit")]
    [SerializeField] private float orbitAcceleration = 18f;
    [SerializeField] private float radialSpring = 4.5f;
    [SerializeField] private float verticalSpring = 5f;
    [SerializeField] private float maximumSpeed = 22f;

    [Header("Impact")]
    [SerializeField] private float cubeImpact = 5.5f;
    [SerializeField] private float meteorImpact = 11f;

    private Rigidbody body;
    private BoxCollider boxCollider;
    private SphereCollider sphereCollider;
    private Renderer cubeRenderer;
    private Renderer sphereRenderer;
    private Vector3 serverTarget;
    private Vector3 pendingVelocity;
    private Vector3 initialPosition;
    private bool visualApplied;
    private float desiredOrbitRadius;
    private float desiredOrbitHeight;
    private Material cubeMaterial;
    private Material sphereMaterial;

    public BoundaryHazardKind Kind => kind.value;
    public int Variant => variant.value;
    public bool IsRealSingularity => kind.value == BoundaryHazardKind.BlackRainSingularity ||
                                     (kind.value == BoundaryHazardKind.FalseSingularity && variant.value == 1);

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

    private float Age
    {
        get
        {
            NetworkManager manager = NetworkManager.main;
            int rate = manager != null && manager.tickModule != null ? manager.tickModule.tickRate : 20;
            return spawnTick.value == 0u ? 0f : (CurrentTick - spawnTick.value) / (float)rate;
        }
    }

    private void Awake()
    {
        EnsureVisuals();
        body = GetComponent<Rigidbody>();
        boxCollider = GetComponent<BoxCollider>();
        sphereCollider = GetComponent<SphereCollider>();
        cubeRenderer = transform.Find("CubeVisual")?.GetComponent<Renderer>();
        sphereRenderer = transform.Find("SphereVisual")?.GetComponent<Renderer>();
        body.isKinematic = true;
        body.interpolation = RigidbodyInterpolation.Interpolate;
        initialPosition = transform.position;
        ActiveHazards.Add(this);
    }

    private void EnsureVisuals()
    {
        if (transform.Find("CubeVisual") == null)
            CreateRuntimeVisual(PrimitiveType.Cube, "CubeVisual", Vector3.one);
        if (transform.Find("SphereVisual") == null)
        {
            GameObject sphere = CreateRuntimeVisual(PrimitiveType.Sphere, "SphereVisual", Vector3.one * 1.65f);
            sphere.SetActive(false);
        }
    }

    private GameObject CreateRuntimeVisual(PrimitiveType primitive, string visualName, Vector3 scale)
    {
        GameObject visual = GameObject.CreatePrimitive(primitive);
        visual.name = visualName;
        visual.transform.SetParent(transform, false);
        visual.transform.localScale = scale;
        Collider visualCollider = visual.GetComponent<Collider>();
        if (visualCollider != null)
            Destroy(visualCollider);
        return visual;
    }

    protected override void OnSpawned()
    {
        kind.onChanged += OnKindChanged;
        ApplyVisual();
        StartCoroutine(ResolveAuthority());
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();
        ActiveHazards.Remove(this);
        if (cubeMaterial != null) Destroy(cubeMaterial);
        if (sphereMaterial != null) Destroy(sphereMaterial);
    }

    public void ServerConfigure(
        BoundaryHazardKind hazardKind,
        uint serverSpawnTick,
        float secondsAlive,
        int hazardVariant,
        Vector3 target,
        Vector3 velocity)
    {
        kind.value = hazardKind;
        spawnTick.value = serverSpawnTick;
        lifetime.value = Mathf.Max(1f, secondsAlive);
        variant.value = hazardVariant;
        serverTarget = target;
        pendingVelocity = velocity;
        initialPosition = transform.position;
        ConfigureOrbitLane();
        ApplyVisual();
    }

    public void ServerPulse(bool outward)
    {
        if (!isServer || body == null || body.isKinematic)
            return;

        BoundaryMatchController match = BoundaryMatchController.Instance;
        Vector3 center = match != null ? match.ArenaCenter : Vector3.zero;
        Vector3 direction = body.position - center;
        direction.y = 0.4f;
        if (!outward) direction = -direction;
        body.AddForce(direction.normalized * 16f, ForceMode.VelocityChange);
        SetEmission(outward ? new Color(1f, 0.2f, 0.8f) : new Color(0.2f, 0.85f, 1f), 5f);
    }

    private IEnumerator ResolveAuthority()
    {
        NetworkManager manager = NetworkManager.main;
        while (manager == null)
        {
            yield return null;
            manager = NetworkManager.main;
        }

        if (body == null)
            yield break;

        bool kinematicHazard = IsSingularityKind(kind.value);
        body.isKinematic = !manager.isServer || kinematicHazard;
        body.useGravity = manager.isServer && !kinematicHazard;
        if (manager.isServer && !body.isKinematic)
        {
            body.linearVelocity = pendingVelocity;
            body.angularVelocity = new Vector3(1.5f, 2f, -1f);
        }
    }

    private void FixedUpdate()
    {
        if (!visualApplied)
            ApplyVisual();

        if (!isServer)
            return;

        if (Age >= lifetime.value)
        {
            Despawn();
            return;
        }

        switch (kind.value)
        {
            case BoundaryHazardKind.BlackRainSingularity:
                TickRainSingularity();
                break;
            case BoundaryHazardKind.FalseSingularity:
                TickFalseSingularity();
                break;
            case BoundaryHazardKind.OrbitalDebris:
            case BoundaryHazardKind.TornadoDebris:
                TickOrbit();
                break;
            case BoundaryHazardKind.Meteor:
                if (body != null && !body.isKinematic)
                    body.AddForce(Vector3.down * 11f, ForceMode.Acceleration);
                break;
        }
    }

    private void TickRainSingularity()
    {
        BoundaryMatchController match = BoundaryMatchController.Instance;
        if (match == null)
            return;

        float age = Age;
        Vector3 position;
        if (age < 1.6f)
        {
            position = Vector3.Lerp(initialPosition, serverTarget, BoundaryMath.EaseInOut(age / 1.6f));
        }
        else if (age < 6.5f)
        {
            position = serverTarget + Vector3.up * (Mathf.Sin(age * 3f) * 0.18f);
        }
        else
        {
            float rise = Mathf.InverseLerp(6.5f, Mathf.Max(6.6f, lifetime.value), age);
            position = Vector3.Lerp(serverTarget, match.SingularityPosition, BoundaryMath.EaseInOut(rise));
        }

        body.MovePosition(position);
        PullServerObjects(IsRealSingularity ? 16f : 4f, 7f * transform.localScale.x);
    }

    private void TickFalseSingularity()
    {
        body.MovePosition(serverTarget + Vector3.up * (Mathf.Sin(Age * 2.7f + variant.value) * 0.12f));
        PullServerObjects(IsRealSingularity ? 18f : 2.2f, IsRealSingularity ? 8f : 4f);
    }

    private void TickOrbit()
    {
        if (body == null || body.isKinematic)
            return;

        BoundaryMatchController match = BoundaryMatchController.Instance;
        if (match == null)
            return;

        Vector3 offset = body.position - match.ArenaCenter;
        Vector3 flat = new Vector3(offset.x, 0f, offset.z);
        if (flat.sqrMagnitude < 0.1f)
            return;

        Vector3 radialDirection = flat.normalized;
        Vector3 tangent = Vector3.Cross(Vector3.up, radialDirection) * match.CurrentDirection;
        float radialError = flat.magnitude - desiredOrbitRadius;
        float heightError = body.position.y - (match.ArenaFloorY + desiredOrbitHeight);

        float orbitForce = kind.value == BoundaryHazardKind.TornadoDebris
            ? orbitAcceleration * 1.2f
            : orbitAcceleration;
        body.AddForce(tangent * orbitForce, ForceMode.Acceleration);
        body.AddForce(-radialDirection * radialError * radialSpring, ForceMode.Acceleration);
        body.AddForce(Vector3.down * heightError * verticalSpring, ForceMode.Acceleration);

        if (kind.value == BoundaryHazardKind.TornadoDebris)
        {
            body.AddForce(Vector3.up * 1.4f, ForceMode.Acceleration);
            desiredOrbitRadius = Mathf.Max(2.5f, desiredOrbitRadius - Time.fixedDeltaTime * 0.08f);
            desiredOrbitHeight += Time.fixedDeltaTime * 0.16f;
        }

        if (body.linearVelocity.magnitude > maximumSpeed)
            body.linearVelocity = body.linearVelocity.normalized * maximumSpeed;
    }

    private void PullServerObjects(float strength, float radius)
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, radius, ~0, QueryTriggerInteraction.Ignore);
        var handled = new HashSet<Rigidbody>();
        foreach (Collider hit in hits)
        {
            Rigidbody otherBody = hit != null ? hit.attachedRigidbody : null;
            if (otherBody == null || otherBody == body || otherBody.isKinematic || !handled.Add(otherBody))
                continue;
            if (otherBody.GetComponentInParent<PlayerMovement>() != null)
                continue;
            if (otherBody.GetComponent<NetworkArenaCubePhysics>() == null &&
                otherBody.GetComponent<BoundaryHazard>() == null &&
                otherBody.GetComponent<NetworkProjectilePhysics>() == null)
                continue;

            Vector3 delta = transform.position - otherBody.position;
            float distance = Mathf.Max(0.5f, delta.magnitude);
            otherBody.AddForce(delta.normalized * strength * (1f - Mathf.Clamp01(distance / radius)), ForceMode.Acceleration);
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (collision == null)
            return;

        PlayerMovement movement = collision.collider.GetComponentInParent<PlayerMovement>();
        if (movement == null || !movement.isOwner)
            return;

        Vector3 direction = movement.transform.position - transform.position;
        direction.y = Mathf.Max(0.45f, direction.y);
        float impact = kind.value == BoundaryHazardKind.Meteor ? meteorImpact : cubeImpact;
        if (kind.value == BoundaryHazardKind.OrbitalDebris || kind.value == BoundaryHazardKind.TornadoDebris)
            impact *= 1.2f;
        movement.ApplyBoundaryImpulse(direction.normalized * impact);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!isServer || !IsRealSingularity || other == null)
            return;

        NetworkArenaCubePhysics cube = other.GetComponentInParent<NetworkArenaCubePhysics>();
        if (cube == null)
            return;

        float nextScale = Mathf.Min(2.25f, transform.localScale.x + 0.12f);
        transform.localScale = Vector3.one * nextScale;
    }

    public static void ApplyLocalFields(PlayerMovement movement)
    {
        if (movement == null || !movement.isOwner || movement.rb == null)
            return;

        for (int i = ActiveHazards.Count - 1; i >= 0; i--)
        {
            BoundaryHazard hazard = ActiveHazards[i];
            if (hazard == null)
            {
                ActiveHazards.RemoveAt(i);
                continue;
            }

            if (!IsSingularityKind(hazard.kind.value))
                continue;

            Vector3 delta = hazard.transform.position - movement.rb.position;
            float radius = (hazard.IsRealSingularity ? 8f : 4f) * Mathf.Max(1f, hazard.transform.localScale.x);
            float distance = delta.magnitude;
            if (distance >= radius || distance < 0.05f)
                continue;

            float strength = hazard.IsRealSingularity ? 22f : 3f;
            float falloff = 1f - Mathf.Clamp01(distance / radius);
            movement.rb.AddForce(delta.normalized * strength * falloff, ForceMode.Acceleration);
        }
    }

    private static bool IsSingularityKind(BoundaryHazardKind value)
    {
        return value == BoundaryHazardKind.BlackRainSingularity ||
               value == BoundaryHazardKind.FalseSingularity;
    }

    private void ConfigureOrbitLane()
    {
        BoundaryMatchController match = BoundaryMatchController.Instance;
        Vector3 center = match != null ? match.ArenaCenter : serverTarget;
        Vector3 flat = transform.position - center;
        flat.y = 0f;
        desiredOrbitRadius = Mathf.Max(2f, flat.magnitude);
        desiredOrbitHeight = Mathf.Max(1f, transform.position.y - center.y);
    }

    private void OnKindChanged(BoundaryHazardKind _)
    {
        visualApplied = false;
        ApplyVisual();
    }

    private void ApplyVisual()
    {
        if (cubeRenderer == null || sphereRenderer == null || boxCollider == null || sphereCollider == null)
            return;

        bool sphere = IsSingularityKind(kind.value);
        cubeRenderer.gameObject.SetActive(!sphere);
        sphereRenderer.gameObject.SetActive(sphere);
        boxCollider.enabled = !sphere;
        sphereCollider.enabled = sphere;
        sphereCollider.isTrigger = true;

        if (cubeMaterial == null)
            cubeMaterial = CreateMaterial(new Color(0.12f, 0.08f, 0.20f), new Color(0.65f, 0.16f, 1f), 2.5f);
        if (sphereMaterial == null)
            sphereMaterial = CreateMaterial(new Color(0.005f, 0.002f, 0.012f), new Color(0.45f, 0.05f, 1f), 5f);

        cubeRenderer.sharedMaterial = cubeMaterial;
        sphereRenderer.sharedMaterial = sphereMaterial;

        switch (kind.value)
        {
            case BoundaryHazardKind.Meteor:
                SetEmission(new Color(1f, 0.18f, 0.04f), 4f);
                break;
            case BoundaryHazardKind.OrbitalDebris:
                SetEmission(new Color(0.2f, 0.7f, 1f), 3f);
                break;
            case BoundaryHazardKind.TornadoDebris:
                SetEmission(new Color(0.75f, 0.16f, 1f), 4f);
                break;
            case BoundaryHazardKind.FalseSingularity:
                SetEmission(new Color(0.35f, 0.08f, 0.9f), 4.5f);
                break;
        }

        visualApplied = true;
    }

    private void SetEmission(Color color, float intensity)
    {
        Material target = IsSingularityKind(kind.value) ? sphereMaterial : cubeMaterial;
        if (target == null)
            return;
        target.EnableKeyword("_EMISSION");
        target.SetColor("_EmissionColor", color * intensity);
    }

    private static Material CreateMaterial(Color baseColor, Color emission, float intensity)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");
        Material material = new Material(shader);
        material.color = baseColor;
        material.EnableKeyword("_EMISSION");
        material.SetColor("_EmissionColor", emission * intensity);
        return material;
    }
}
