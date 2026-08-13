using PurrNet;
using UnityEngine;

public class  ForceField : MonoBehaviour
{
    public enum Mode { Attract, Repel }

    [Header("Mode")]
    [SerializeField] private Mode mode = Mode.Repel;

    [Header("Timing")]
    [SerializeField] private float delayBeforePulse = 1.5f;   
    [SerializeField] private float destroyAfterPulse = 0.2f;   

    [Header("Force")]
    [SerializeField] private float radius = 6f;
    [SerializeField] private float strength = 25f;            
    [SerializeField] private float maxAccel = 120f;          
    [Tooltip("Extra force applied only to player rigidbodies. Physics props remain unchanged.")]
    [SerializeField] private float playerForceMultiplier = 1.5f;
    [SerializeField] private AnimationCurve falloff = AnimationCurve.EaseInOut(0, 1, 1, 0);
    [SerializeField] private bool affectOwner = true;
    [SerializeField] private Rigidbody ownerRb;

    [Header("Filtering")]
    [SerializeField] private LayerMask affectMask = ~0;
    [SerializeField] private QueryTriggerInteraction triggerInteraction = QueryTriggerInteraction.Ignore;

    [Header("VFX")]
    [SerializeField] private ParticleSystem pulseVFXPrefab;   // burst prefab
    [SerializeField] private float vfxLifetime = 2f;

    [Header("Performance")]
    [SerializeField] private int maxHits = 64;

    private Collider[] hits;
    private float timer;
    private bool pulsed;

    void Awake()
    {
        hits = new Collider[Mathf.Max(8, maxHits)];

        // This component is part of the networked projectile on every client.
        // Playing here gives both players throw feedback without adding another
        // RPC to PlayerAbilities (which would require a matching server build).
        if (mode == Mode.Repel)
            SfxManager.PlayRepelThrow();
        else
            SfxManager.PlayAttractThrow();
    }

    void Update()
    {
        if (pulsed) return;

        timer += Time.deltaTime;
        if (timer >= delayBeforePulse)
        {
            PulseOnce();
        }
    }

    private void PulseOnce()
    {
        pulsed = true;

        // The networked projectile exists on every client, so playing the pulse
        // here makes the explosion audible to both players at the correct time.
        if (mode == Mode.Repel)
            SfxManager.PlayRepelExplosion();
        else
            SfxManager.PlayAttractExplosion();

        if (pulseVFXPrefab != null)
        {
            ParticleSystem ps = Instantiate(pulseVFXPrefab, transform.position, Quaternion.identity);
            ps.Play();
            Destroy(ps.gameObject, vfxLifetime);
        }

        Vector3 center = transform.position;

        // The projectile and its VFX exist on every peer, but physics must
        // have one source of truth. Clients receive rigidbody movement through
        // each affected object's server-authoritative NetworkTransform.
        NetworkManager net = NetworkManager.main;
        if (net == null || !net.isServer)
        {
            Destroy(gameObject, destroyAfterPulse);
            return;
        }

        int count = Physics.OverlapSphereNonAlloc(center, radius, hits, affectMask, triggerInteraction);

        for (int i = 0; i < count; i++)
        {
            Collider c = hits[i];
            if (!c) continue;

            Rigidbody rb = c.attachedRigidbody;
            if (!rb) continue;

            if (!affectOwner && ownerRb != null && rb == ownerRb)
                continue;

            if (rb.transform == transform || rb.transform.IsChildOf(transform))
                continue;

            Vector3 toBody = rb.position - center;
            float dist = toBody.magnitude;
            if (dist < 0.01f || dist > radius) continue;

            Vector3 dirOut = toBody / dist;        
            Vector3 dir = (mode == Mode.Repel) ? dirOut : -dirOut;

            float t = Mathf.Clamp01(dist / radius); 
            float f = Mathf.Clamp01(falloff.Evaluate(t));
            float accel = Mathf.Min(strength * f, maxAccel);

            PlayerMovement player = rb.GetComponentInParent<PlayerMovement>();
            if (player != null)
            {
                accel *= playerForceMultiplier;

                BoundaryPlayerState boundaryState = player.GetComponent<BoundaryPlayerState>();
                BoundaryMatchController match = BoundaryMatchController.Instance;
                if (boundaryState != null && boundaryState.State != BoundaryKnockoutState.Grounded &&
                    match != null && match.Phase == BoundaryPhase.InnerRing)
                {
                    // Airborne targets have less stability in the vortex, so
                    // Repel becomes the intended final-phase knockout tool.
                    accel *= mode == Mode.Repel ? 1.42f : 1.12f;
                }

                if (boundaryState != null)
                {
                    // Player rigidbodies are owner-authoritative and kinematic
                    // on the server. Send the velocity change to that owner;
                    // applying AddForce here would silently do nothing.
                    boundaryState.ServerPushOwner(dir * Mathf.Clamp(accel * 0.105f, 1.35f, 15f));
                    continue;
                }
            }

            rb.AddForce(dir * accel, ForceMode.Acceleration);
        }

        Destroy(gameObject, destroyAfterPulse);
    }

    public void SetOwner(Rigidbody owner) => ownerRb = owner;

    void OnDrawGizmosSelected()
    {
        Gizmos.color = (mode == Mode.Attract) ? Color.cyan : Color.red;
        Gizmos.DrawWireSphere(transform.position, radius);
    }
}
