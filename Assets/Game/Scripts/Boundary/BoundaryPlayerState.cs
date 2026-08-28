using System.Collections.Generic;
using PurrNet;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerMovement))]
public sealed class BoundaryPlayerState : NetworkBehaviour
{
    private const float SpawnRecoveryDurationSeconds = 3f;
    private const float HealthTickSeconds = 0.1f;
    private const float ContactGraceSeconds = 0.05f;
    private static readonly List<BoundaryPlayerState> ActivePlayers = new List<BoundaryPlayerState>(2);

    [Header("Event horizon")]
    [SerializeField, Min(2f)] private float horizonDistanceBelowCore = 5.5f;
    [SerializeField, Min(0.5f)] private float escapeWindowSeconds = 1.6f;
    [SerializeField, Min(1f)] private float horizonHorizontalRadius = 10f;

    [Header("Void")]
    [SerializeField, Min(1f)] private float voidKillDepthBelowArena = 4f;

    private readonly SyncVar<BoundaryKnockoutState> state = new(BoundaryKnockoutState.Grounded, ownerAuth: true);
    private readonly SyncVar<float> health = new(BoundaryMath.MaximumHealth, 0.01f, ownerAuth: false);
    private readonly Dictionary<int, float> serverBlackHoleContacts = new Dictionary<int, float>();
    private readonly List<int> staleContactIds = new List<int>();

    private PlayerMovement movement;
    private float horizonEnteredAt = -1f;
    private float outOfBoundsEnteredAt = -1f;
    private float spawnRecoveryEndsAt;
    private bool reportedLoss;
    private bool loggedSpawnRecovery;
    private bool serverDeathSent;
    private float nextHealthTickAt;
    private float serverInvulnerableUntil;

    public BoundaryKnockoutState State => state.value;
    public float EscapeProgress
    {
        get
        {
            float enteredAt = state.value == BoundaryKnockoutState.EventHorizon
                ? horizonEnteredAt
                : state.value == BoundaryKnockoutState.OutOfBounds ? outOfBoundsEnteredAt : -1f;
            return enteredAt >= 0f
                ? Mathf.Clamp01((Time.time - enteredAt) / escapeWindowSeconds)
                : 0f;
        }
    }

    public bool IsOutOfBounds => state.value == BoundaryKnockoutState.OutOfBounds;
    public float CurrentHealth => health.value;
    public float Health01 => Mathf.Clamp01(health.value / BoundaryMath.MaximumHealth);
    public bool IsServerInvulnerable => isServer && Time.time < serverInvulnerableUntil;

    private void OnEnable()
    {
        if (!ActivePlayers.Contains(this))
            ActivePlayers.Add(this);
    }

    private void OnDisable()
    {
        ActivePlayers.Remove(this);
    }

    public static bool TryGetOpponent(BoundaryPlayerState player, out BoundaryPlayerState opponent)
    {
        for (int index = 0; index < ActivePlayers.Count; index++)
        {
            BoundaryPlayerState candidate = ActivePlayers[index];
            if (candidate != null && candidate != player &&
                candidate.transform.root != player.transform.root)
            {
                opponent = candidate;
                return true;
            }
        }

        opponent = null;
        return false;
    }

    public static bool HasVoidHealthAdvantage(float casterHealth, float opponentHealth)
    {
        return opponentHealth < casterHealth;
    }

    private void Awake()
    {
        movement = GetComponent<PlayerMovement>();
    }

    protected override void OnSpawned()
    {
        spawnRecoveryEndsAt = Time.unscaledTime + SpawnRecoveryDurationSeconds;
        if (isServer)
        {
            health.value = BoundaryMath.MaximumHealth;
            nextHealthTickAt = Time.time + HealthTickSeconds;
            serverDeathSent = false;
            serverBlackHoleContacts.Clear();
            serverInvulnerableUntil = 0f;
        }

        if (!Application.isBatchMode && GetComponent<BoundaryWorldHealthBar>() == null)
            gameObject.AddComponent<BoundaryWorldHealthBar>().Initialize(this);
    }

    private void FixedUpdate()
    {
        if (isServer)
            ServerUpdateHealth();

        if (!isOwner || movement == null)
            return;

        if (state.value == BoundaryKnockoutState.Consumed)
            return;

        BoundaryMatchController match = BoundaryMatchController.Instance;
        if (match == null)
            return;

        if (BoundaryMath.IsBelowVoidKillPlane(
                transform.position.y,
                match.ArenaFloorY,
                voidKillDepthBelowArena))
        {
            if (match.Phase == BoundaryPhase.Waiting || Time.unscaledTime < spawnRecoveryEndsAt)
            {
                RecoverInitialSpawn(match);
                return;
            }

            ConsumePlayer("You fell into the void.");
            return;
        }

        if (match.Phase == BoundaryPhase.Waiting)
        {
            SetState(movement.IsGrounded ? BoundaryKnockoutState.Grounded : BoundaryKnockoutState.Airborne);
            return;
        }

        float horizontalDistance = Vector2.Distance(
            new Vector2(transform.position.x, transform.position.z),
            new Vector2(match.ArenaCenter.x, match.ArenaCenter.z));
        float outOfBoundsRadius = match.RingRadius + BoundaryMath.OutOfBoundsMargin(match.Phase);
        if (match.Phase == BoundaryPhase.OuterRing &&
            BoundaryArenaPresentation.Instance != null)
        {
            outOfBoundsRadius = Mathf.Max(
                outOfBoundsRadius,
                BoundaryArenaPresentation.Instance.AuthoredPlayableRadius);
        }
        if (horizontalDistance > outOfBoundsRadius)
        {
            if (state.value != BoundaryKnockoutState.OutOfBounds &&
                state.value != BoundaryKnockoutState.Consumed)
            {
                outOfBoundsEnteredAt = Time.time;
                SetState(BoundaryKnockoutState.OutOfBounds);
            }
            else if (state.value == BoundaryKnockoutState.OutOfBounds &&
                     Time.time - outOfBoundsEnteredAt >= escapeWindowSeconds)
            {
                ConsumePlayer("You went out of bounds.");
            }
            return;
        }

        if (state.value == BoundaryKnockoutState.OutOfBounds &&
            horizontalDistance > match.RingRadius - 1f)
            return;

        outOfBoundsEnteredAt = -1f;

        float horizonY = match.SingularityPosition.y - horizonDistanceBelowCore;
        Vector3 flatOffset = transform.position - match.ArenaCenter;
        flatOffset.y = 0f;
        bool insideHorizonColumn = flatOffset.magnitude <= horizonHorizontalRadius;
        bool beyondHorizon = transform.position.y >= horizonY && insideHorizonColumn;

        if (beyondHorizon)
        {
            if (state.value != BoundaryKnockoutState.EventHorizon &&
                state.value != BoundaryKnockoutState.Consumed)
            {
                horizonEnteredAt = Time.time;
                SetState(BoundaryKnockoutState.EventHorizon);
            }
            else if (state.value == BoundaryKnockoutState.EventHorizon &&
                     Time.time - horizonEnteredAt >= escapeWindowSeconds)
            {
                ConsumePlayer("You crossed the event horizon.");
            }
            return;
        }

        // A full meter of separation prevents boundary jitter from repeatedly
        // entering and leaving the final escape window.
        if (state.value == BoundaryKnockoutState.EventHorizon && transform.position.y > horizonY - 1f)
            return;

        horizonEnteredAt = -1f;
        SetState(movement.IsStableGrounded
            ? BoundaryKnockoutState.Grounded
            : BoundaryKnockoutState.Airborne);
    }

    public void ServerPushOwner(Vector3 velocityChange)
    {
        if (!isServer || !owner.HasValue)
            return;

        PushOwner(owner.Value, velocityChange);
    }

    public void ServerRegisterBlackHoleContact(int sourceInstanceId)
    {
        if (!isServer || sourceInstanceId == 0 || health.value <= 0f)
            return;

        serverBlackHoleContacts[sourceInstanceId] = Time.time;
    }

    public void ServerApplyAbilityDamage(float damage)
    {
        if (!isServer || damage <= 0f || health.value <= 0f || IsServerInvulnerable)
            return;

        health.value = BoundaryMath.ApplyDamage(health.value, damage);
        ServerNotifyDeathIfNeeded();
    }

    public void ServerGrantInvulnerability(float durationSeconds)
    {
        if (!isServer || durationSeconds <= 0f)
            return;

        serverInvulnerableUntil = Mathf.Max(serverInvulnerableUntil, Time.time + durationSeconds);
    }

    private void ServerUpdateHealth()
    {
        float now = Time.time;
        if (now < nextHealthTickAt)
            return;

        int activeContacts = 0;
        staleContactIds.Clear();
        foreach (KeyValuePair<int, float> contact in serverBlackHoleContacts)
        {
            if (now - contact.Value <= ContactGraceSeconds)
                activeContacts++;
            else
                staleContactIds.Add(contact.Key);
        }
        foreach (int sourceId in staleContactIds)
            serverBlackHoleContacts.Remove(sourceId);

        float elapsed = Mathf.Min(0.25f, Mathf.Max(HealthTickSeconds, now - nextHealthTickAt + HealthTickSeconds));
        nextHealthTickAt = now + HealthTickSeconds;
        if (activeContacts <= 0 || health.value <= 0f || IsServerInvulnerable)
            return;

        health.value = BoundaryMath.ApplyDamage(
            health.value,
            BoundaryMath.BlackHoleDamage(elapsed) * activeContacts);
        ServerNotifyDeathIfNeeded();
    }

    private void ServerNotifyDeathIfNeeded()
    {
        if (health.value > 0f || serverDeathSent)
            return;

        serverDeathSent = true;
        serverBlackHoleContacts.Clear();
        if (owner.HasValue)
            NotifyOwnerHealthDepleted(owner.Value);
    }

    [TargetRpc]
    private void NotifyOwnerHealthDepleted(PlayerID target)
    {
        if (isOwner)
            ConsumePlayer("Your health reached zero.");
    }

    [TargetRpc]
    private void PushOwner(PlayerID target, Vector3 velocityChange)
    {
        if (!isOwner || movement == null)
            return;

        movement.ApplyAbilityImpulse(velocityChange);
    }

    private void SetState(BoundaryKnockoutState next)
    {
        if (state.value != next)
            state.value = next;
    }

    public void ConsumeFromHazard(string reason)
    {
        if (!isOwner)
            return;

        ConsumePlayer(string.IsNullOrWhiteSpace(reason)
            ? "You were consumed by the black hole."
            : reason);
    }

    private void ConsumePlayer(string reason)
    {
        if (reportedLoss)
            return;

        reportedLoss = true;
        SetState(BoundaryKnockoutState.Consumed);
        LocalLethalFeedback.VibrateForAcceptedLocalContact();
        SfxManager.PlayLethalHit();
        if (GameManager.I != null)
            GameManager.I.ReportLocalPlayerLost(reason);
    }

    private void RecoverInitialSpawn(BoundaryMatchController match)
    {
        if (movement.rb == null)
            return;

        Vector3 position = movement.rb.position;
        Vector3 flatOffset = position - match.ArenaCenter;
        flatOffset.y = 0f;
        position.y = match.PlatformSurfaceYAtRadius(flatOffset.magnitude) + 1.15f;
        movement.rb.position = position;

        Vector3 velocity = movement.rb.linearVelocity;
        movement.rb.linearVelocity = new Vector3(velocity.x, Mathf.Max(0f, velocity.y), velocity.z);

        if (!loggedSpawnRecovery)
        {
            loggedSpawnRecovery = true;
            Debug.LogWarning("[Boundary] Recovered a player that spawned below the arena.");
        }
    }
}

[DisallowMultipleComponent]
internal sealed class BoundaryWorldHealthBar : MonoBehaviour
{
    private BoundaryPlayerState playerState;
    private Canvas canvas;
    private Image fill;

    public void Initialize(BoundaryPlayerState state)
    {
        playerState = state;
        Build();
    }

    private void Awake()
    {
        if (playerState == null)
            playerState = GetComponent<BoundaryPlayerState>();
    }

    private void Start()
    {
        if (canvas == null)
            Build();
    }

    private void LateUpdate()
    {
        if (canvas == null || playerState == null)
            return;

        bool visible = !playerState.isOwner && playerState.CurrentHealth > 0f;
        canvas.enabled = visible;
        if (!visible)
            return;

        fill.fillAmount = playerState.Health01;
        Camera targetCamera = Camera.main;
        if (targetCamera != null)
            canvas.transform.rotation = Quaternion.LookRotation(canvas.transform.position - targetCamera.transform.position);
    }

    private void Build()
    {
        if (canvas != null || Application.isBatchMode)
            return;

        GameObject root = new GameObject("Opponent Health Bar", typeof(RectTransform), typeof(Canvas));
        root.layer = 5;
        root.transform.SetParent(transform, false);
        root.transform.localPosition = new Vector3(0f, 2.25f, 0f);
        root.transform.localScale = Vector3.one * 0.01f;
        canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 40;
        RectTransform rootRect = (RectTransform)root.transform;
        rootRect.sizeDelta = new Vector2(120f, 14f);

        Image background = CreateImage(root.transform, "Background", new Color(0.25f, 0.25f, 0.25f, 0.95f));
        Stretch(background.rectTransform);
        fill = CreateImage(background.transform, "Health", Color.white);
        fill.type = Image.Type.Filled;
        fill.fillMethod = Image.FillMethod.Horizontal;
        fill.fillOrigin = 0;
        Stretch(fill.rectTransform, 2f);
    }

    private static Image CreateImage(Transform parent, string name, Color color)
    {
        GameObject obj = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        obj.layer = 5;
        obj.transform.SetParent(parent, false);
        Image image = obj.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private static void Stretch(RectTransform rect, float inset = 0f)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.one * inset;
        rect.offsetMax = Vector2.one * -inset;
    }
}
