using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Routes ability hits to the local player's screen and speakers.
///
/// Deliberately client-only: it adds no RPCs and changes no networked state, so
/// the network surface - and therefore the published dedicated-server image -
/// is untouched. It works from signals both clients already receive:
/// <list type="bullet">
/// <item>the observer RPCs every ability already sends when it is cast or
/// lands, which identify the ability and its caster;</item>
/// <item>the replicated health SyncVar on each
/// <see cref="BoundaryPlayerState"/>, which supplies the damage number;</item>
/// <item>the networked projectiles themselves, which exist on both clients and
/// can therefore be located, measured and blamed locally.</item>
/// </list>
/// Because a match is one-versus-one, "the player who was not hit" is the
/// attacker, which is what lets both sides be shown without new messages.
/// </summary>
public static class AbilityFeedback
{
    private struct CastRecord
    {
        public AbilityId Id;
        public bool ByLocalPlayer;
        public float CreatedAt;
        public float ExpiresAt;
        public Vector3 Position;

        /// <summary>
        /// Set for a live projectile - a black hole, a force field - so its
        /// current position is used rather than where it was thrown from.
        /// </summary>
        public Transform Source;

        /// <summary>
        /// How close a player must be to <see cref="Source"/> to be plausibly
        /// hurt by it. Zero means no distance test, which is correct for an
        /// instant ability that reports its own hits.
        /// </summary>
        public float Radius;
    }

    private const int RecordCapacity = 8;

    /// <summary>
    /// Minimum gap between two impact sounds for the same ability and side.
    /// Without it, an explicit hit RPC and the health drop that follows it a
    /// frame later would both fire, and a damage-over-time ability such as
    /// Hollow would retrigger on every physics tick. The UI still refreshes
    /// during the gap, so the damage number keeps climbing silently.
    /// </summary>
    private const float SoundRepeatGapSeconds = 0.35f;

    /// <summary>
    /// Bullseye's center hit deals 12, its ring hit 7 (see
    /// <see cref="BullseyeAbility"/>). The real damage amount only becomes
    /// available once the replicated health drop confirms it, so that is
    /// also where a center hit is told apart from a ring hit for feedback
    /// purposes - the midpoint between the two makes a safe threshold.
    /// </summary>
    private const float BullseyeCenterHitDamageThreshold = 9.5f;

    private static readonly CastRecord[] Records = new CastRecord[RecordCapacity];
    private static readonly Dictionary<int, float> LastSoundAt = new Dictionary<int, float>();
    private static int nextRecordIndex;

    /// <summary>
    /// Abilities that already ship their own impact audio, played from the
    /// ability itself. Void slashes on a timer for its whole fifteen seconds
    /// and sounds each slash through <see cref="SfxManager"/>, so adding a
    /// synthesised impact on top of that only doubles it up.
    /// </summary>
    private static bool HasAuthoredImpactAudio(AbilityId id) => id == AbilityId.Void;

    // Keep authored cast, throw, swing, and explosion clips, but do not layer
    // generated hit sounds over these abilities' impact feedback.
    private static bool HasGeneratedImpactAudioDisabled(AbilityId id) =>
        id == AbilityId.Hollow ||
        id == AbilityId.Bullseye ||
        id == AbilityId.BlackThrow ||
        id == AbilityId.Charge ||
        id == AbilityId.Slice;

    // These sustained impacts already carry prominent world effects.
    private static bool HasAuthoredImpactVisual(AbilityId id) =>
        id == AbilityId.Hollow || id == AbilityId.Void;

    /// <summary>
    /// Records that an ability was cast, which gives later damage something to
    /// blame. Every ability plays its own cast audio through
    /// <see cref="SfxManager"/>; none is synthesised here.
    /// Only real cast sites call this; hit reports record silently.
    /// </summary>
    public static void NoteCast(AbilityId id, bool byLocalPlayer, Vector3 position)
    {
        Store(id, byLocalPlayer, position, null, 0f, -1f);
    }

    /// <summary>
    /// Records a live projectile that can hurt whoever comes near it. The
    /// thrower is resolved from the player nearest the spawn point, because a
    /// thrown ability is created just outside its owner's collider while the
    /// opponent is somewhere else entirely. Damage is only blamed on this
    /// source while the victim is actually within <paramref name="radius"/> of
    /// it, so a black hole across the arena can no longer take credit for an
    /// unrelated hit.
    /// </summary>
    /// <returns>True when the local player threw this projectile.</returns>
    public static bool NoteWorldSource(AbilityId id, Transform source, float radius, float duration)
    {
        if (source == null)
            return false;

        bool byLocalPlayer = ResolveThrownByLocalPlayer(source.position);
        Store(id, byLocalPlayer, source.position, source, Mathf.Max(0.5f, radius), duration);
        return byLocalPlayer;
    }

    /// <summary>
    /// A confirmed hit the local player landed. The damage number arrives
    /// separately from the victim's replicated health, so this opens the marker
    /// and <see cref="ReportHealthDrop"/> fills in the number.
    /// </summary>
    public static void ReportDealt(AbilityId id, Vector3 victimPosition)
    {
        Store(id, true, victimPosition, null, 0f, -1f);
        Show(id, false, 0f, victimPosition);
    }

    /// <summary>A confirmed hit the local player took.</summary>
    public static void ReportTaken(AbilityId id, Vector3 attackerPosition)
    {
        Store(id, false, attackerPosition, null, 0f, -1f);
        Show(id, true, 0f, attackerPosition);
    }

    /// <summary>
    /// Called by <see cref="BoundaryPlayerState"/> whenever any player's
    /// replicated health falls. This is what covers abilities with no hit RPC
    /// of their own - Hollow's beam, black hole contact - and what supplies the
    /// damage number for the abilities that do.
    /// </summary>
    public static void ReportHealthDrop(bool victimIsLocalPlayer, float amount, Vector3 victimPosition)
    {
        if (amount < 0.05f)
            return;

        if (TryAttribute(!victimIsLocalPlayer, victimPosition, out CastRecord record))
        {
            Show(record.Id, victimIsLocalPlayer, amount, SourcePosition(record));
            return;
        }

        // Nothing to blame: an arena hazard, the closing ring or a fall. The
        // HUD already tints for that, so only give it a neutral thud rather
        // than dressing it up as an ability hit.
        if (victimIsLocalPlayer && TryClaimSound(-1, true))
        {
            AbilitySfx.PlayUnattributedTaken();
            AbilityImpactVfx.PlayUnattributed(victimPosition);
        }
    }

    /// <summary>
    /// Called when an ability shoves the local player. Repel and Attract land
    /// without dealing damage and would otherwise have no victim feedback at
    /// all. Self-inflicted movement - Hollow's recoil, a grapple pull - finds
    /// no opposing source on record and is correctly ignored.
    /// </summary>
    public static void ReportKnockback(bool victimIsLocalPlayer, Vector3 victimPosition)
    {
        if (TryAttribute(!victimIsLocalPlayer, victimPosition, out CastRecord record))
            Show(record.Id, victimIsLocalPlayer, 0f, SourcePosition(record));
    }

    /// <summary>
    /// Called by a projectile that has just caught the opponent, on the client
    /// that threw it. The victim learns of a shove through the existing
    /// knockback TargetRpc, but that message never reaches the thrower, so a
    /// knockback ability needs this to confirm the hit on the attacker's side.
    /// </summary>
    public static void ReportDealtByWorldSource(AbilityId id, Vector3 victimPosition)
    {
        Show(id, false, 0f, victimPosition);
    }

    private static void Show(AbilityId id, bool victimIsLocalPlayer, float amount, Vector3 otherPosition)
    {
        AbilityFeedbackProfile profile = AbilityFeedbackCatalog.Get(id);
        bool generatedImpactAudioDisabled = HasGeneratedImpactAudioDisabled(id);
        bool allowImpactVisual = !HasAuthoredImpactVisual(id) &&
            TryClaimSound((int)id, victimIsLocalPlayer);
        bool allowSound = !generatedImpactAudioDisabled && !HasAuthoredImpactAudio(id) &&
            allowImpactVisual;

        // Only the health-drop-triggered call carries the real damage amount
        // (the RPC-triggered call that opens the panel reports 0 first), so
        // this only ever fires once per hit - exactly when it is knowable.
        bool bullseyeCenterHit = id == AbilityId.Bullseye && amount >= BullseyeCenterHitDamageThreshold;
        // Bullseye's first call has no damage amount. Wait for health to
        // distinguish a ring hit from its already elaborate center impact.
        if (id == AbilityId.Bullseye)
        {
            if (amount > 0f && !bullseyeCenterHit)
                AbilityImpactVfx.PlayImpact(id, otherPosition);
        }
        else if (allowImpactVisual)
            AbilityImpactVfx.PlayImpact(id, otherPosition);

        if (victimIsLocalPlayer)
        {
            AbilityHitFeedback.ShowTaken(profile, amount, otherPosition, bullseyeCenterHit);
            if (allowSound)
                AbilitySfx.PlayTaken(id);
            if (bullseyeCenterHit && !generatedImpactAudioDisabled)
                AbilitySfx.PlayBullseyeCenterTaken();
        }
        else
        {
            AbilityHitFeedback.ShowDealt(profile, amount, bullseyeCenterHit);
            if (allowSound)
                AbilitySfx.PlayDealt(id);
            if (bullseyeCenterHit && !generatedImpactAudioDisabled)
                AbilitySfx.PlayBullseyeCenterDealt();
        }
    }

    /// <summary>
    /// True when this ability and side has not sounded recently. The UI is
    /// never gated by this - only the audio, which is what would otherwise
    /// double up or machine-gun.
    /// </summary>
    private static bool TryClaimSound(int abilityKey, bool takenSide)
    {
        int key = abilityKey * 2 + (takenSide ? 1 : 0);
        float now = Time.unscaledTime;
        if (LastSoundAt.TryGetValue(key, out float last) && now - last < SoundRepeatGapSeconds)
            return false;

        LastSoundAt[key] = now;
        return true;
    }

    private static void Store(AbilityId id, bool byLocalPlayer, Vector3 position, Transform source,
        float radius, float duration)
    {
        AbilityFeedbackProfile profile = AbilityFeedbackCatalog.Get(id);
        float window = duration > 0f ? duration : profile.AttributionWindow;
        Records[nextRecordIndex] = new CastRecord
        {
            Id = id,
            ByLocalPlayer = byLocalPlayer,
            CreatedAt = Time.unscaledTime,
            ExpiresAt = Time.unscaledTime + Mathf.Max(0.2f, window),
            Position = position,
            Source = source,
            Radius = radius
        };
        nextRecordIndex = (nextRecordIndex + 1) % RecordCapacity;
    }

    /// <summary>
    /// The most recently cast live source from the side that did the damage,
    /// discarding any projectile that has since died or is too far from the
    /// victim to be responsible.
    /// </summary>
    private static bool TryAttribute(bool castByLocalPlayer, Vector3 victimPosition, out CastRecord match)
    {
        float now = Time.unscaledTime;
        float newest = float.MinValue;
        match = default;
        bool found = false;

        for (int index = 0; index < Records.Length; index++)
        {
            CastRecord record = Records[index];
            if (record.ExpiresAt <= now || record.ByLocalPlayer != castByLocalPlayer)
                continue;

            if (record.Radius > 0f)
            {
                // A pulse is destroyed shortly after it fires, so fall back to
                // where it last was rather than dropping the record entirely.
                if (record.Source != null)
                {
                    record.Position = record.Source.position;
                    Records[index].Position = record.Position;
                }

                if (Vector3.Distance(victimPosition, record.Position) > record.Radius)
                    continue;
            }

            if (record.CreatedAt > newest)
            {
                newest = record.CreatedAt;
                match = record;
                found = true;
            }
        }

        return found;
    }

    private static Vector3 SourcePosition(CastRecord record)
    {
        return record.Source != null ? record.Source.position : record.Position;
    }

    /// <summary>
    /// Works out whether the local player threw a projectile by finding the
    /// player nearest its spawn point. Thrown abilities are created just
    /// outside their owner's own collider, so the nearest player is the thrower.
    /// </summary>
    private static bool ResolveThrownByLocalPlayer(Vector3 spawnPosition)
    {
        BoundaryPlayerState[] players =
            Object.FindObjectsByType<BoundaryPlayerState>(FindObjectsSortMode.None);
        BoundaryPlayerState nearest = null;
        float nearestDistance = float.MaxValue;

        for (int index = 0; index < players.Length; index++)
        {
            BoundaryPlayerState player = players[index];
            if (player == null)
                continue;

            float distance = Vector3.SqrMagnitude(player.transform.position - spawnPosition);
            if (distance < nearestDistance)
            {
                nearestDistance = distance;
                nearest = player;
            }
        }

        return nearest != null && nearest.isOwner;
    }

    /// <summary>Clears attribution and audio throttling between matches.</summary>
    public static void Reset()
    {
        for (int index = 0; index < Records.Length; index++)
            Records[index] = default;
        nextRecordIndex = 0;
        LastSoundAt.Clear();
        AbilityImpactVfx.Reset();
    }
}
