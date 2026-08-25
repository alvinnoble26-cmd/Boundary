using System.Collections;
using System.Collections.Generic;
using PurrNet;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class Cam : NetworkBehaviour
{
    public const float DefaultFirstPersonNearClip = 0.05f;
    public const float DefaultFirstPersonFieldOfView = 80f;
    public const float DefaultLookDegreesPerPixel = 0.32f;
    public const float MinimumFirstPersonEyeHeight = 0.72f;

    [Header("Sensitivity")]
    public float xSens = ControlLayoutSettings.DefaultCameraSensitivity;
    public float ySens = ControlLayoutSettings.DefaultCameraSensitivity;

    [Header("Refs")]
    public Transform orientation;
    public Transform cam;
    public Transform camPivot;

    [Header("Mobile Settings")]
    public TouchLookHandler swipe;

    [Header("First Person View")]
    [SerializeField] private Vector3 firstPersonEyeOffset = new Vector3(0f, 0.90f, 0.28f);
    [SerializeField, Min(MinimumFirstPersonEyeHeight)]
    private float minimumFirstPersonEyeHeight = 0.90f;
    [SerializeField, Range(-89f, -45f)] private float firstPersonMinPitch = -85f;
    [SerializeField, Range(45f, 89f)] private float firstPersonMaxPitch = 85f;
    [SerializeField, Range(0.01f, 0.2f)] private float firstPersonNearClip = DefaultFirstPersonNearClip;
    [SerializeField, Range(60f, 110f)] private float firstPersonFieldOfView = DefaultFirstPersonFieldOfView;
    [SerializeField, Range(0.05f, 0.5f)] private float lookDegreesPerPixelAtDefault = DefaultLookDegreesPerPixel;
    [SerializeField, Range(0f, 60f)] private float wallRunCameraTiltAngle = 18f;
    [SerializeField, Min(1f)] private float wallRunCameraTiltResponse = 16f;
    [SerializeField, Range(0f, 1f)] private float wallRunCameraWallOffset = 0.4f;

    [Header("First Person Obstruction Protection")]
    [SerializeField, Range(0.02f, 0.2f)] private float obstructionRadius = 0.07f;
    [SerializeField, Range(0.005f, 0.1f)] private float obstructionPadding = 0.025f;
    [SerializeField] private LayerMask obstructionMask = ~0;

    private readonly RaycastHit[] obstructionHits = new RaycastHit[16];
    private readonly List<Renderer> localBodyRenderers = new List<Renderer>();
    private Transform playerRoot;
    private float pitch;
    private float yaw;
    private float wallRunCameraRoll;
    private bool isReady;
    private bool setupRoutineRunning;
    private bool ownerViewWasUnexpectedlyDisabled;
    private PlayerMovement playerMovement;
    private FirstPersonArmPresentation firstPersonArm;
    private string equippedSkinId = "beard";
    private bool lookInputSuppressed;
    private float damageShakeEndsAt;
    private float damageShakeStartedAt;
    private float screenShakeStrength;
    private LocalPlayerCameraBodyFilter localBodyFilter;

    protected override void OnSpawned()
    {
        SetCameraComponentsEnabled(false);
        BeginOwnerSetup();
    }

    protected override void OnOwnerChanged(PlayerID? oldOwner, PlayerID? newOwner, bool asServer)
    {
        if (asServer)
            return;

        if (isOwner)
        {
            BeginOwnerSetup();
        }
        else
        {
            StopAllCoroutines();
            setupRoutineRunning = false;
            DeactivateOwnerView();
        }
    }

    private void OnDisable()
    {
        setupRoutineRunning = false;
        DeactivateOwnerView();
    }

    private void BeginOwnerSetup()
    {
        if (!isActiveAndEnabled || isReady || setupRoutineRunning)
            return;

        StartCoroutine(SetupCameraRoutine());
    }

    private IEnumerator SetupCameraRoutine()
    {
        setupRoutineRunning = true;

        // Ownership can arrive shortly after the network object itself. A
        // non-owned clone simply times out with its camera disabled; it is not
        // an error and must remain visible to the actual owner.
        float timeoutAt = Time.realtimeSinceStartup + 10f;
        while (!isOwner && Time.realtimeSinceStartup < timeoutAt)
            yield return new WaitForSecondsRealtime(0.05f);

        if (!isOwner)
        {
            setupRoutineRunning = false;
            yield break;
        }

        ResolveReferences();
        if (cam == null)
        {
            Debug.LogError("[Cam] The player prefab has no assigned first-person Camera transform.");
            setupRoutineRunning = false;
            yield break;
        }

        float savedSensitivity = ControlLayoutSettings.LoadCameraSensitivity();
        xSens = savedSensitivity;
        ySens = savedSensitivity;

        DisableOtherCamerasAndListeners();

        GameObject fallback = GameObject.Find("FallbackCamera");
        if (fallback != null)
            fallback.SetActive(false);

        if (swipe == null)
            swipe = Object.FindFirstObjectByType<TouchLookHandler>();

        Camera unityCamera = cam.GetComponent<Camera>();
        if (unityCamera == null)
        {
            Debug.LogError("[Cam] The assigned first-person Camera transform has no Camera component.");
            setupRoutineRunning = false;
            yield break;
        }

        cam.gameObject.SetActive(true);
        firstPersonFieldOfView = ControlLayoutSettings.LoadCameraFieldOfView();
        ConfigureFirstPersonCamera(unityCamera, firstPersonNearClip, firstPersonFieldOfView);
        unityCamera.gameObject.tag = "MainCamera";
        unityCamera.targetDisplay = 0;
        unityCamera.targetTexture = null;
        unityCamera.depth = 10f;

        if (cam.TryGetComponent<UniversalAdditionalCameraData>(out UniversalAdditionalCameraData data))
            data.renderType = CameraRenderType.Base;

        yaw = orientation != null ? orientation.eulerAngles.y : playerRoot.eulerAngles.y;
        pitch = 0f;
        isReady = true;

        // Establish the eye pose and hide the local body before the camera is
        // enabled, preventing a one-frame flash of the old third-person pose.
        UpdateFirstPersonPose(Vector2.zero);
        SetLocalVisualVisibility(true);
        RefreshLocalFirstPersonVisuals();
        SetCameraComponentsEnabled(true);

        setupRoutineRunning = false;
        Debug.Log("[Cam] Owner first-person camera enabled.");
    }

    private void LateUpdate()
    {
        if (!isOwner || !isReady)
            return;

        // Player objects can finish spawning after ownership is assigned.
        // Some of those spawn-time components toggle cameras/listeners while
        // they establish their own local view. Keep this player's view alive
        // without ever enabling a remote player's camera.
        MaintainOwnerView();

        Vector2 lookDelta = !lookInputSuppressed && swipe != null ? swipe.ConsumeLookDelta() : Vector2.zero;
        UpdateFirstPersonPose(lookDelta);
    }

    private void MaintainOwnerView()
    {
        if (cam == null)
            return;

        if (!cam.gameObject.activeSelf)
        {
            cam.gameObject.SetActive(true);
            ownerViewWasUnexpectedlyDisabled = true;
        }

        bool cameraWasDisabled = cam.TryGetComponent<Camera>(out Camera unityCamera) && !unityCamera.enabled;
        bool listenerWasDisabled = cam.TryGetComponent<AudioListener>(out AudioListener listener) && !listener.enabled;
        if (!cameraWasDisabled && !listenerWasDisabled)
            return;

        SetCameraComponentsEnabled(true);
        if (!ownerViewWasUnexpectedlyDisabled)
        {
            ownerViewWasUnexpectedlyDisabled = true;
            Debug.LogWarning("[Cam] Restored the local owner camera after a spawn-time component disabled it.");
        }
    }

    private void ResolveReferences()
    {
        playerMovement = GetComponentInParent<PlayerMovement>();
        playerRoot = playerMovement != null ? playerMovement.transform : transform.root;

        if (orientation == null)
            orientation = playerMovement != null && playerMovement.orientation != null
                ? playerMovement.orientation
                : playerRoot;
        if (camPivot == null)
            camPivot = transform;
    }

    private void UpdateFirstPersonPose(Vector2 lookDelta)
    {
        if (playerRoot == null)
            ResolveReferences();

        float activeSensitivity = ControlLayoutSettings.LoadCameraSensitivity();
        xSens = activeSensitivity;
        ySens = activeSensitivity;
        float sensitivityScale = activeSensitivity / ControlLayoutSettings.DefaultCameraSensitivity;
        float degreesPerPixel = lookDegreesPerPixelAtDefault * sensitivityScale;

        if (cam.TryGetComponent<Camera>(out Camera unityCamera))
        {
            firstPersonFieldOfView = ControlLayoutSettings.LoadCameraFieldOfView();
            unityCamera.fieldOfView = firstPersonFieldOfView;
        }

        yaw += lookDelta.x * degreesPerPixel;
        pitch = Mathf.Clamp(
            pitch - lookDelta.y * degreesPerPixel,
            firstPersonMinPitch,
            firstPersonMaxPitch);

        Quaternion yawRotation = Quaternion.Euler(0f, yaw, 0f);
        float targetWallRunRoll = playerMovement != null && playerMovement.IsWallRunning
            ? Mathf.Sign(playerMovement.CurrentWallRunTilt) * wallRunCameraTiltAngle
            : 0f;
        float rollBlend = 1f - Mathf.Exp(-wallRunCameraTiltResponse * Time.deltaTime);
        wallRunCameraRoll = Mathf.Lerp(wallRunCameraRoll, targetWallRunRoll, rollBlend);
        Quaternion viewRotation = CalculateFirstPersonViewRotation(pitch, yaw, wallRunCameraRoll);
        if (orientation != null)
            orientation.rotation = yawRotation;
        if (playerMovement != null)
            playerMovement.SetViewYaw(yaw);

        Vector3 effectiveEyeOffset = firstPersonEyeOffset;
        effectiveEyeOffset.y = Mathf.Max(effectiveEyeOffset.y, minimumFirstPersonEyeHeight);
        Vector3 desiredEyePosition = CalculateFirstPersonEyePosition(
            playerRoot.position,
            yaw,
            effectiveEyeOffset);
        if (playerMovement != null && playerMovement.IsWallRunning)
            desiredEyePosition += playerMovement.WallRunNormal * wallRunCameraWallOffset;
        Vector3 safeEyePosition = ResolveObstructionSafeEyePosition(desiredEyePosition);
        Vector3 shakeOffset = DamageShakeOffset();
        safeEyePosition += viewRotation * shakeOffset;
        if (shakeOffset.sqrMagnitude > 0f)
            viewRotation *= Quaternion.Euler(shakeOffset.y * 14f, shakeOffset.x * 12f, shakeOffset.x * -18f);

        if (camPivot != null)
            camPivot.SetPositionAndRotation(safeEyePosition, viewRotation);
        cam.SetPositionAndRotation(safeEyePosition, viewRotation);
    }

    public void RequestDamageShake()
    {
        if (!isOwner || !SettingsMenu.ScreenShakeEnabled)
            return;

        StartScreenShake(0.18f, 0.055f);
    }

    public void RequestBoundaryCollapseShake()
    {
        if (!isOwner || !SettingsMenu.ScreenShakeEnabled)
            return;

        StartScreenShake(0.36f, 0.12f);
    }

    public void RequestHollowShake()
    {
        if (!isOwner || !SettingsMenu.ScreenShakeEnabled)
            return;

        StartScreenShake(HollowAbility.ChargeDuration + HollowAbility.BlastDuration, 0.132f);
    }

    public void RequestVoidShake()
    {
        if (!isOwner || !SettingsMenu.ScreenShakeEnabled)
            return;

        StartScreenShake(VoidAbility.DurationSeconds, 0.16f);
    }

    private void StartScreenShake(float duration, float strength)
    {
        damageShakeStartedAt = Time.unscaledTime;
        damageShakeEndsAt = damageShakeStartedAt + duration;
        screenShakeStrength = strength;
    }

    private Vector3 DamageShakeOffset()
    {
        float now = Time.unscaledTime;
        if (now >= damageShakeEndsAt)
            return Vector3.zero;

        float duration = Mathf.Max(0.01f, damageShakeEndsAt - damageShakeStartedAt);
        float strength = 1f - Mathf.Clamp01((now - damageShakeStartedAt) / duration);
        float x = Mathf.PerlinNoise(now * 54f, 2.31f) * 2f - 1f;
        float y = Mathf.PerlinNoise(7.17f, now * 61f) * 2f - 1f;
        return new Vector3(x, y, 0f) * (screenShakeStrength * strength);
    }

    private Vector3 ResolveObstructionSafeEyePosition(Vector3 desiredEyePosition)
    {
        Vector3 castOrigin = playerRoot != null ? playerRoot.position : desiredEyePosition;
        Vector3 castVector = desiredEyePosition - castOrigin;
        float castDistance = castVector.magnitude;
        if (castDistance <= 0.001f)
            return desiredEyePosition;

        Vector3 castDirection = castVector / castDistance;
        float nearestDistance = castDistance;
        int hitCount = Physics.SphereCastNonAlloc(
            castOrigin,
            obstructionRadius,
            castDirection,
            obstructionHits,
            castDistance,
            obstructionMask,
            QueryTriggerInteraction.Ignore);

        for (int i = 0; i < hitCount; i++)
        {
            Collider hitCollider = obstructionHits[i].collider;
            if (hitCollider == null || hitCollider.transform.root == playerRoot)
                continue;
            if (hitCollider.GetComponentInParent<NetworkProjectilePhysics>() != null)
                continue;

            nearestDistance = Mathf.Min(nearestDistance, obstructionHits[i].distance);
        }

        if (nearestDistance >= castDistance)
            return desiredEyePosition;

        float safeDistance = Mathf.Max(0f, nearestDistance - obstructionPadding);
        return castOrigin + castDirection * safeDistance;
    }

    private void DisableOtherCamerasAndListeners()
    {
        foreach (Camera otherCamera in Object.FindObjectsByType<Camera>(FindObjectsSortMode.None))
        {
            if (cam != null && otherCamera.transform == cam)
                continue;
            otherCamera.enabled = false;
        }

        foreach (AudioListener otherListener in Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None))
        {
            if (cam != null && otherListener.transform == cam)
                continue;
            otherListener.enabled = false;
        }
    }

    private void SetCameraComponentsEnabled(bool enabled)
    {
        if (cam == null)
            return;

        if (cam.TryGetComponent<Camera>(out Camera unityCamera))
            unityCamera.enabled = enabled;
        if (cam.TryGetComponent<AudioListener>(out AudioListener listener))
            listener.enabled = enabled;
    }

    private void DeactivateOwnerView()
    {
        isReady = false;
        ownerViewWasUnexpectedlyDisabled = false;
        SetCameraComponentsEnabled(false);
        SetLocalVisualVisibility(false);
        firstPersonArm?.Hide();
    }

    public void RefreshLocalFirstPersonVisuals(string skinId = null)
    {
        if (!string.IsNullOrEmpty(skinId))
            equippedSkinId = skinId;

        if (isOwner && isReady)
        {
            SetLocalVisualVisibility(true);
            GetFirstPersonArm()?.SetSkin(equippedSkinId);
        }
    }

    public void ShowThrowArm(Vector3 aimDirection)
    {
        if (isOwner && isReady)
            GetFirstPersonArm()?.ShowThrow(aimDirection);
    }

    public void ShowTeleportArm()
    {
        if (isOwner && isReady)
            GetFirstPersonArm()?.ShowTeleport();
    }

    public void SetHollowArmsActive(bool active, Vector3 worldTarget)
    {
        if (isOwner && isReady)
            GetFirstPersonArm()?.SetHollowActive(active, worldTarget);
    }

    public void SetGrappleArmActive(bool active, Vector3 aimDirection)
    {
        if (isOwner && isReady)
            GetFirstPersonArm()?.SetGrappleActive(active, aimDirection);
    }

    public void PlayGrappleYank()
    {
        if (isOwner && isReady)
            GetFirstPersonArm()?.PlayGrappleYank();
    }

    public Vector3 GetGrappleArmOrigin()
    {
        return isOwner && isReady && GetFirstPersonArm() != null
            ? GetFirstPersonArm().GrappleOrigin
            : transform.position;
    }

    public void SetMovementArmActive(bool active)
    {
        if (isOwner && isReady)
            GetFirstPersonArm()?.SetMovementActive(active);
    }

    public void SetLookInputSuppressed(bool suppressed)
    {
        if (isOwner)
            lookInputSuppressed = suppressed;
    }

    private FirstPersonArmPresentation GetFirstPersonArm()
    {
        if (firstPersonArm == null && cam != null)
            firstPersonArm = cam.GetComponent<FirstPersonArmPresentation>() ??
                cam.gameObject.AddComponent<FirstPersonArmPresentation>();
        return firstPersonArm;
    }

    public void SetLocalVisualVisibility(bool hiddenFromOwner)
    {
        if (playerRoot == null)
        {
            PlayerMovement movement = GetComponentInParent<PlayerMovement>();
            playerRoot = movement != null ? movement.transform : transform.root;
        }

        if (!hiddenFromOwner)
        {
            localBodyRenderers.Clear();
            localBodyFilter?.SetBodyRenderers(localBodyRenderers);
            return;
        }

        localBodyRenderers.Clear();
        AddRenderer(playerRoot.GetComponent<Renderer>());
        AddRenderersUnder(playerRoot.Find("Visual"));
        AddRenderersUnder(playerRoot.Find("eye"));

        if (cam != null)
        {
            localBodyFilter = cam.GetComponent<LocalPlayerCameraBodyFilter>() ??
                cam.gameObject.AddComponent<LocalPlayerCameraBodyFilter>();
            localBodyFilter.Initialize(cam.GetComponent<Camera>());
            localBodyFilter.SetBodyRenderers(localBodyRenderers);
        }
    }

    private void AddRenderersUnder(Transform visualRoot)
    {
        if (visualRoot == null)
            return;

        foreach (Renderer renderer in visualRoot.GetComponentsInChildren<Renderer>(true))
            AddRenderer(renderer);
    }

    private void AddRenderer(Renderer renderer)
    {
        if (renderer == null || localBodyRenderers.Contains(renderer))
            return;
        localBodyRenderers.Add(renderer);
    }

    public static Vector3 CalculateFirstPersonEyePosition(
        Vector3 playerPosition,
        float yawDegrees,
        Vector3 localEyeOffset)
    {
        return playerPosition + Quaternion.Euler(0f, yawDegrees, 0f) * localEyeOffset;
    }

    public static Quaternion CalculateFirstPersonViewRotation(float pitchDegrees, float yawDegrees)
    {
        return CalculateFirstPersonViewRotation(pitchDegrees, yawDegrees, 0f);
    }

    public static Quaternion CalculateFirstPersonViewRotation(float pitchDegrees, float yawDegrees, float rollDegrees)
    {
        return Quaternion.Euler(pitchDegrees, yawDegrees, rollDegrees);
    }

    public static void ConfigureFirstPersonCamera(Camera unityCamera, float nearClip, float fieldOfView)
    {
        if (unityCamera == null)
            return;

        unityCamera.orthographic = false;
        unityCamera.nearClipPlane = Mathf.Clamp(nearClip, 0.01f, 0.2f);
        unityCamera.fieldOfView = Mathf.Clamp(fieldOfView, 60f, 110f);
    }
}

/// <summary>
/// Hides the owning player's body only for its gameplay camera. Scene view and
/// observer cameras render the normal model, while physics layers stay intact.
/// </summary>
public sealed class LocalPlayerCameraBodyFilter : MonoBehaviour
{
    private readonly List<Renderer> bodyRenderers = new List<Renderer>();
    private readonly Dictionary<Renderer, ShadowCastingMode> renderStates =
        new Dictionary<Renderer, ShadowCastingMode>();
    private Camera ownerCamera;
    private bool filterApplied;

    public void Initialize(Camera camera)
    {
        ownerCamera = camera;
    }

    public void SetBodyRenderers(IReadOnlyList<Renderer> renderers)
    {
        RestoreOwnerCameraFilter();
        bodyRenderers.Clear();
        if (renderers == null)
            return;

        for (int i = 0; i < renderers.Count; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer != null && !bodyRenderers.Contains(renderer))
                bodyRenderers.Add(renderer);
        }
    }

    private void OnEnable()
    {
        RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;
        RenderPipelineManager.endCameraRendering += OnEndCameraRendering;
    }

    private void OnDisable()
    {
        RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
        RenderPipelineManager.endCameraRendering -= OnEndCameraRendering;
        RestoreOwnerCameraFilter();
    }

    private void OnBeginCameraRendering(ScriptableRenderContext context, Camera renderingCamera)
    {
        if (renderingCamera == ownerCamera)
            ApplyOwnerCameraFilter();
    }

    private void OnEndCameraRendering(ScriptableRenderContext context, Camera renderingCamera)
    {
        if (renderingCamera == ownerCamera)
            RestoreOwnerCameraFilter();
    }

    public void ApplyOwnerCameraFilter()
    {
        if (filterApplied)
            return;

        renderStates.Clear();
        foreach (Renderer renderer in bodyRenderers)
        {
            if (renderer == null || renderer.forceRenderingOff)
                continue;
            renderStates[renderer] = renderer.shadowCastingMode;
            renderer.shadowCastingMode = ShadowCastingMode.ShadowsOnly;
        }
        filterApplied = true;
    }

    public void RestoreOwnerCameraFilter()
    {
        if (!filterApplied)
            return;

        foreach (KeyValuePair<Renderer, ShadowCastingMode> state in renderStates)
        {
            if (state.Key != null)
                state.Key.shadowCastingMode = state.Value;
        }
        renderStates.Clear();
        filterApplied = false;
    }
}
