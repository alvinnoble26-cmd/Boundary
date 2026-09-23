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
    private readonly Dictionary<int, BlackHoleContact> serverBlackHoleContacts =
        new Dictionary<int, BlackHoleContact>();
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
    private float lastObservedHealth;
    private bool healthFeedbackPrimed;

    public bool IsCpu => movement != null && movement.IsCpuControlled;
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
        healthFeedbackPrimed = false;
        if (isOwner)
            AbilityFeedback.Reset();
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

        WatchHealthForAbilityFeedback();

        if (movement == null || !movement.HasSimulationAuthority)
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
        if (!isServer) return;
        if (IsCpu)
            movement.ApplyAbilityImpulse(velocityChange);
        else if (owner.HasValue)
            PushOwner(owner.Value, velocityChange);
    }

    private struct BlackHoleContact
    {
        public float observedAt;
        public float damageMultiplier;
    }

    public void ServerRegisterBlackHoleContact(int sourceInstanceId, float damageMultiplier = 1f)
    {
        if (!isServer || sourceInstanceId == 0 || health.value <= 0f)
            return;

        serverBlackHoleContacts[sourceInstanceId] = new BlackHoleContact
        {
            observedAt = Time.time,
            damageMultiplier = Mathf.Max(0f, damageMultiplier)
        };
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

        float activeContactDamageMultiplier = 0f;
        staleContactIds.Clear();
        foreach (KeyValuePair<int, BlackHoleContact> pair in serverBlackHoleContacts)
        {
            if (now - pair.Value.observedAt <= ContactGraceSeconds)
                activeContactDamageMultiplier += pair.Value.damageMultiplier;
            else
                staleContactIds.Add(pair.Key);
        }
        foreach (int sourceId in staleContactIds)
            serverBlackHoleContacts.Remove(sourceId);

        float elapsed = Mathf.Min(0.25f, Mathf.Max(HealthTickSeconds, now - nextHealthTickAt + HealthTickSeconds));
        nextHealthTickAt = now + HealthTickSeconds;
        if (activeContactDamageMultiplier <= 0f || health.value <= 0f || IsServerInvulnerable)
            return;

        health.value = BoundaryMath.ApplyDamage(
            health.value,
            BoundaryMath.BlackHoleDamage(elapsed) * activeContactDamageMultiplier);
        ServerNotifyDeathIfNeeded();
    }

    private void ServerNotifyDeathIfNeeded()
    {
        if (health.value > 0f || serverDeathSent)
            return;

        serverDeathSent = true;
        serverBlackHoleContacts.Clear();
        if (IsCpu)
            ConsumePlayer("CPU health reached zero.");
        else if (owner.HasValue)
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
        // Repel and Attract land without dealing damage. Only an opposing
        // ability on record produces feedback, so a player's own recoil is
        // never reported as a hit against them.
        AbilityFeedback.ReportKnockback(true, transform.position);
    }

    /// <summary>
    /// Watches the replicated health of every player on this client and
    /// reports drops to <see cref="AbilityFeedback"/>, which decides whether
    /// the local player was the victim or the attacker. This is presentation
    /// only: it sends nothing and changes no networked state.
    /// </summary>
    private void WatchHealthForAbilityFeedback()
    {
        if (Application.isBatchMode)
            return;

        float current = health.value;
        if (!healthFeedbackPrimed)
        {
            healthFeedbackPrimed = true;
            lastObservedHealth = current;
            return;
        }

        float drop = lastObservedHealth - current;
        lastObservedHealth = current;
        if (drop <= 0f)
            return;

        AbilityFeedback.ReportHealthDrop(isOwner, drop, transform.position);
        if (!isOwner)
            BoundaryDamageNumber.Show(transform, drop);
    }

    private void SetState(BoundaryKnockoutState next)
    {
        if (state.value != next)
            state.value = next;
    }

    public void ConsumeFromHazard(string reason)
    {
        if (movement == null || !movement.HasSimulationAuthority)
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
        if (IsCpu)
        {
            if (GameManager.I != null && GameManager.I.IsCpuPractice)
                GameManager.I.EndGameWin("You defeated the CPU.");
            return;
        }
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
        position.y = match.PlatformSurfaceYAtRadius(flatOffset.magnitude) +
            PlayerMovement.StandingCenterHeight;
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

        // Host-created CPU objects briefly inherit local ownership before it is removed.
        // CPU identity wins over that transient state; normal multiplayer still hides only
        // the local player's own world bar and shows the replicated remote player's bar.
        bool visible = (playerState.IsCpu || !playerState.isOwner) &&
            playerState.CurrentHealth > 0f;
        canvas.enabled = visible;
        if (!visible)
            return;

        SetFillWidth(fill.rectTransform, playerState.Health01);
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
        // Runtime Images have no source sprite. Driving RectTransform width is reliable on
        // every client, whereas Image.fillAmount can remain visually full without a sprite.
        fill.type = Image.Type.Simple;
        Stretch(fill.rectTransform, 2f);
        SetFillWidth(fill.rectTransform, playerState != null ? playerState.Health01 : 1f);
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

    internal static void SetFillWidth(RectTransform rect, float health01)
    {
        if (rect == null)
            return;

        Vector2 anchorMax = rect.anchorMax;
        anchorMax.x = Mathf.Clamp01(health01);
        rect.anchorMax = anchorMax;
        rect.offsetMax = new Vector2(-2f, rect.offsetMax.y);
    }
}

/// <summary>
/// A local-only world-space damage number for the opponent. Health is already
/// replicated to both clients, so this intentionally sends no additional RPC.
/// </summary>
internal sealed class BoundaryDamageNumber : MonoBehaviour
{
    private const float ConsecutiveDamageGapSeconds = 1f;
    private const float LifetimeSeconds = 1.8f;
    private const float RiseDistance = 2.25f;
    private const float WorldScale = 0.025f;

    private static readonly Dictionary<int, BoundaryDamageNumber> ActiveNumbers =
        new Dictionary<int, BoundaryDamageNumber>();

    private Canvas canvas;
    private Text label;
    private float startedAt;
    private float lastHitAt;
    private float totalDamage;
    private int targetId;
    private Vector3 startPosition;
    private Vector3 drift;

    public static void Show(Transform target, float damage)
    {
        if (target == null || damage <= 0f || Application.isBatchMode)
            return;

        int key = target.GetInstanceID();
        float now = Time.unscaledTime;
        if (ActiveNumbers.TryGetValue(key, out BoundaryDamageNumber active) && active != null)
        {
            if (now - active.lastHitAt <= ConsecutiveDamageGapSeconds)
            {
                active.AddDamage(damage, now);
                return;
            }

            Destroy(active.gameObject);
        }

        GameObject root = new GameObject("Opponent Damage Number", typeof(RectTransform),
            typeof(Canvas));
        BoundaryDamageNumber number = root.AddComponent<BoundaryDamageNumber>();
        number.Initialize(target.position, damage, key, now);
    }

    private void Initialize(Vector3 targetPosition, float damage, int key, float now)
    {
        targetId = key;
        totalDamage = damage;
        startPosition = targetPosition + Vector3.up * 2.15f;
        drift = new Vector3(Random.Range(-0.28f, 0.28f), 0f, Random.Range(-0.08f, 0.08f));
        transform.position = startPosition;
        transform.localScale = Vector3.one * WorldScale;

        canvas = GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.overrideSorting = true;
        canvas.sortingOrder = 300;

        RectTransform rootRect = (RectTransform)transform;
        rootRect.sizeDelta = new Vector2(360f, 150f);
        GameObject textObject = new GameObject("Damage", typeof(RectTransform),
            typeof(CanvasRenderer), typeof(Text), typeof(Outline));
        textObject.transform.SetParent(transform, false);
        label = textObject.GetComponent<Text>();
        label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        SetDamageText();
        label.fontSize = 76;
        label.fontStyle = FontStyle.Bold;
        label.alignment = TextAnchor.MiddleCenter;
        label.color = Color.white;
        label.raycastTarget = false;
        Outline outline = textObject.GetComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 0.9f);
        outline.effectDistance = new Vector2(7f, -7f);

        RectTransform textRect = (RectTransform)textObject.transform;
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;
        startedAt = now;
        lastHitAt = now;
        ActiveNumbers[targetId] = this;
    }

    private void AddDamage(float damage, float now)
    {
        totalDamage += damage;
        lastHitAt = now;
        SetDamageText();
    }

    private void SetDamageText()
    {
        if (label != null)
            label.text = "-" + Mathf.CeilToInt(totalDamage);
    }

    private void LateUpdate()
    {
        float now = Time.unscaledTime;
        float elapsed = now - startedAt;
        float progress = Mathf.Clamp01(elapsed / LifetimeSeconds);
        float idleTime = now - lastHitAt;
        transform.position = startPosition + drift * progress + Vector3.up * (RiseDistance * progress);

        Camera camera = Camera.main;
        if (camera != null)
            transform.rotation = Quaternion.LookRotation(transform.position - camera.transform.position);

        if (label != null)
        {
            Color color = Color.white;
            color.a = 1f - Mathf.SmoothStep(0.7f, LifetimeSeconds, idleTime);
            label.color = color;
            float hitProgress = Mathf.Clamp01((now - lastHitAt) / 0.20f);
            float punch = 1f + Mathf.Sin(hitProgress * Mathf.PI) * 0.48f;
            transform.localScale = Vector3.one * (WorldScale * punch);
        }

        if (idleTime >= LifetimeSeconds)
            Destroy(gameObject);
    }

    private void OnDestroy()
    {
        if (targetId != 0 && ActiveNumbers.TryGetValue(targetId, out BoundaryDamageNumber active) &&
            active == this)
        {
            ActiveNumbers.Remove(targetId);
        }
    }
}
