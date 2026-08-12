using UnityEngine;
using PurrNet;
using System.Collections;
using UnityEngine.Rendering.Universal; // Required for URP data



public class Cam : NetworkBehaviour 
{
    
    [Header("Sensitivity")]
    public float xSens = 15f; 
    public float ySens = 15f;

    [Header("Refs")]
    public Transform orientation;
    public Transform cam;      // Drag the 'Main Camera' child here
    public Transform camPivot; // Drag the 'CameraPivot' child here

    [Header("Mobile Settings")]
    public TouchLookHandler swipe;
    public Vector3 camOffset = new Vector3(0f, 1.6f, -4f);

    [Header("Camera Limits & Collision")]
    [SerializeField] private float minPitch = -35f;
    [SerializeField] private float maxPitch = 55f;
    [SerializeField] private float collisionRadius = 0.2f;
    [SerializeField] private float collisionPadding = 0.08f;
    [SerializeField] private LayerMask collisionMask = ~0;

    private float pitch;
    private float yaw;
    private bool isReady = false;
    private readonly RaycastHit[] collisionHits = new RaycastHit[12];

    protected override void OnSpawned()
    {
            if (cam != null)
    {
        if (cam.TryGetComponent<Camera>(out var c)) c.enabled = false;
        if (cam.TryGetComponent<AudioListener>(out var a)) a.enabled = false;
    }

        StartCoroutine(SetupCameraRoutine());
    }

private IEnumerator SetupCameraRoutine()
{
    float t = 0f;
    while (!isOwner && t < 10f)
    {
        t += 0.1f;
        yield return new WaitForSeconds(0.1f);
    }
    
    if (!isOwner)
    {
        Debug.LogError("[Cam] Ownership never arrived.");
        yield break;
    }
    yield return new WaitUntil(() => isOwner);

    // Disable all OTHER cameras/listeners
    foreach (var c in Object.FindObjectsByType<Camera>(FindObjectsSortMode.None))
    {
        if (cam != null && c.transform == cam) continue;
        c.enabled = false;
    }
    foreach (var a in Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None))
    {
        if (cam != null && a.transform == cam) continue;
        a.enabled = false;
    }

    if (swipe == null)
        swipe = Object.FindFirstObjectByType<TouchLookHandler>();

    if (cam == null)
    {
        Debug.LogError("[Cam] cam Transform is NOT assigned on the prefab!");
        yield break;
    }

    // Make sure the camera object is actually active
    cam.gameObject.SetActive(true);

    if (cam.TryGetComponent<Camera>(out var unityCamera))
    {
        unityCamera.gameObject.tag = "MainCamera";
        unityCamera.enabled = true;

        unityCamera.targetDisplay = 0;     // Display 1
        unityCamera.targetTexture = null;  // MUST be null to render to Game view
        unityCamera.depth = 10;

        Debug.Log($"[Cam] Camera enabled. display={unityCamera.targetDisplay} targetTexture={(unityCamera.targetTexture ? unityCamera.targetTexture.name : "null")}");
    }
    else
    {
        Debug.LogError("[Cam] No Camera component found on cam Transform.");
    }

    if (cam.TryGetComponent<AudioListener>(out var listener))
    {
        listener.enabled = true;
        Debug.Log("[Cam] AudioListener enabled = " + listener.enabled);
    }
    else
    {
        Debug.LogError("[Cam] No AudioListener component found on cam Transform.");
    }

    if (cam.TryGetComponent<UniversalAdditionalCameraData>(out var data))
    {
        data.renderType = CameraRenderType.Base;
    }

    yaw = orientation.eulerAngles.y;
    isReady = true;
}


    void LateUpdate()
    {
        // Only the owner should calculate camera movement
        if (!isOwner || !isReady) return;
        var fallback = GameObject.Find("FallbackCamera");
        if (fallback) fallback.SetActive(false);


        Vector2 lookDelta = swipe != null ? swipe.LookDelta : Vector2.zero;

        // Apply sensitivity and time smoothing
        yaw += lookDelta.x * Time.deltaTime * xSens;
        pitch -= lookDelta.y * Time.deltaTime * ySens;
        pitch = Mathf.Clamp(pitch, minPitch, maxPitch);
        if (orientation == null) orientation = transform;
        if (camPivot == null) camPivot = transform;


        // Update rotations
        orientation.rotation = Quaternion.Euler(0f, yaw, 0f);
        camPivot.rotation = Quaternion.Euler(pitch, yaw, 0f);

        // Pull the camera in front of walls instead of letting the near clip
        // plane pass through them and reveal the other side.
        Vector3 desiredPosition = camPivot.TransformPoint(camOffset);
        Vector3 castOrigin = camPivot.position;
        Vector3 castVector = desiredPosition - castOrigin;
        float castDistance = castVector.magnitude;
        Vector3 castDirection = castDistance > 0.001f
            ? castVector / castDistance
            : Vector3.back;
        float nearestHitDistance = castDistance;
        bool cameraBlocked = false;
        int hitCount = castDistance > 0.001f
            ? Physics.SphereCastNonAlloc(
                castOrigin,
                collisionRadius,
                castDirection,
                collisionHits,
                castDistance,
                collisionMask,
                QueryTriggerInteraction.Ignore)
            : 0;

        for (int i = 0; i < hitCount; i++)
        {
            Collider hitCollider = collisionHits[i].collider;
            if (hitCollider == null || hitCollider.transform.root == transform.root)
                continue;

            if (collisionHits[i].distance < nearestHitDistance)
            {
                nearestHitDistance = collisionHits[i].distance;
                cameraBlocked = true;
            }
        }

        if (cameraBlocked)
        {
            float safeDistance = Mathf.Max(0.05f, nearestHitDistance - collisionPadding);
            cam.position = castOrigin + castDirection * safeDistance;
        }
        else
        {
            cam.position = desiredPosition;
        }
        cam.LookAt(camPivot.position);
    }
}
