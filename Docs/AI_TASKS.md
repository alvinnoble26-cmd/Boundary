# Boundary AI Task Queue

This queue is manually controlled by the project owner.

## Single-chat mode

The default workflow uses one Project Engineer chat for organization,
implementation, focused testing, and reporting. Separate role chats are
optional and should not be created unless the owner explicitly requests an
independent review or additional testing.

## Owner approval rule

- The project owner decides which task runs next.
- A task may remain `PENDING` indefinitely until explicitly approved.
- The Organizer may propose, split, clarify, or reprioritize tasks, but may not
  start implementation automatically.
- The Implementer may work only on a task explicitly identified by the owner.
- A Tester may test only a task explicitly identified by the owner.
- A bug handoff is a proposed fix until the owner sends it to the Implementer.

## Status definitions

- `IDEA` — Captured but not ready to schedule
- `PENDING` — Defined and waiting for owner approval
- `APPROVED` — Owner selected this task to run next
- `IN_PROGRESS` — Implementer is actively working on it
- `TESTING` — Implementation is complete and awaiting testing
- `BLOCKED` — Requires a decision or external dependency
- `DONE` — Implemented, tested, and accepted by the owner
- `FAILED` — Testing found unresolved problems

## Task ID rules

- Use the numbered TaskBoard ID from `Docs/TaskBoard.md` as the only task ID,
  such as `1.2.07.00`.
- Do not create or use a second internal ID such as `TASK-001`.
- Use the same TaskBoard ID in the task entry, Implementer report, Tester log,
  bug report, Fixer handoff, and chat responses.

## Fixer rule

- The Fixer may work only on a confirmed bug or review finding with a task ID
  and bug ID.
- The owner must explicitly approve the Fixer handoff.
- A Fixer change must be registered under the original task; do not create an
  untracked one-off code change.
- After a Fixer change, set the task status to `TESTING` until the Tester checks it.

## How to run a task manually

1. Add or ask the Organizer to define a task as `PENDING`.
2. Review its scope, risks, and acceptance criteria.
3. Change its status to `APPROVED` yourself, or tell Codex explicitly:
   `Approve TaskBoard item 1.2.07.00 for implementation.`
4. Tell the same Project Engineer chat to implement the approved task.
5. The Project Engineer changes the status to `IN_PROGRESS`, implements it,
   and then changes the status to `TESTING`.
6. The Project Engineer runs the recommended minimum focused validation.
7. Review the result and any bug reports in the same chat.
8. Approve any deployment or follow-up fix separately.
9. Mark the task `DONE` only after you accept the results.

## Task template

## 1.2.07.00 — [Title]

### Status

PENDING

### Priority

P0 / P1 / P2 / P3

### Recommended testing budget

- Tester passes recommended:
- Pass 1 coverage:
- Additional pass required only if:

### Owner decision

Not reviewed

### Requested outcome

[Player-facing result]

### TaskBoard source

- TaskBoard ID:
- Source location: `Docs/TaskBoard.md`

### Specifics from TaskBoard

[Copy the selected TaskBoard item's Specifics section here. If none exists,
write `None provided`.]

### Scope

-

### Non-goals

-

### Existing systems to reuse

-

### Likely files

-

### Risks

-

### Server and Edgegap impact

- Linux dedicated server rebuild required: Unknown / Yes / No
- New Edgegap image required: Unknown / Yes / No
- Edgegap application/version update required: Unknown / Yes / No
- Firebase or backend deployment required: Unknown / Yes / No
- Client-only change possible: Unknown / Yes / No
- Implementer assessment:
- Owner deployment approval: Not reviewed

### Acceptance criteria

- [ ]
- [ ]
- [ ]

### Implementer report

Status:
Files changed:
What was implemented:
Tests/checks run:
Results:
Manual test steps:
Known limitations:
Remaining risks:

Server and Edgegap assessment:
- Linux dedicated server rebuild required: Yes / No
- Why:
- New image or image tag required: Yes / No
- Edgegap update required: Yes / No
- Exact Edgegap action:
- Firebase/backend deployment required: Yes / No
- Client/server compatibility notes:
- Deployment approval needed from owner: Yes / No

### Tester result

Test log entry:
Result:
Open bugs:

### Fixer report

Bug ID fixed:
Owner approval:
Files changed:
Fix applied:
Validation run:
Results:
Remaining risks:

### Re-test result

Test log entry:
Result:
Notes:

### Implementer follow-up — Grapple menu placement

TaskBoard ID: `1.1.07.00`
Status: TESTING
Files changed:
- `Assets/Scripts/AbilitiesRegistry/LoadoutManager.cs`
- `Docs/AI_TASKS.md`
What was fixed:
- The Grapple selector was being positioned 220 units to the right of a
  right-anchored Teleport selector, which placed it beyond the visible menu
  edge. Teleport remains in its original position. Grapple uses the visible
  vertical space immediately above Teleport because this menu has a fixed
  two-column layout and no safe third column.
Tests/checks run:
- `git diff --check -- Assets/Scripts/AbilitiesRegistry/LoadoutManager.cs`
- `dotnet build Assembly-CSharp.csproj --no-restore --verbosity:minimal`
Results:
- Whitespace check passed.
- The command-line build could not compile because Unity's generated restore
  asset file is absent at `Temp/obj/Assembly-CSharp/project.assets.json`; no
  generated Unity folders were modified.
Manual test steps:
1. Open the Menu scene and enter the abilities panel.
2. Confirm `TELEPORT` remains in its original lower-right slot and `GRAPPLE`
   is visible directly above it.
3. Select Grapple, confirm it appears in a loadout slot, then restart the Menu
   scene and confirm Grapple remains available once with no duplicate selector.
Known limitations:
- Unity Editor visual validation was not run in this session.
Remaining risks:
- Canvas scaling or a Menu layout variant not present in the inspected scene
  could require a different spacing value; verify at the supported mobile and
  desktop resolutions.

### Implementer follow-up — Grapple Jump momentum

TaskBoard ID: `1.1.07.00`
Status: TESTING
Files changed:
- `Assets/Scripts/Abilities/GrappleAbility.cs`
- `Assets/Scripts/Player/PlayerMovement.cs`
- `Docs/AI_TASKS.md`
What was fixed:
- Ending Grapple now hands the current horizontal velocity to normal movement
  as a temporary speed cap and bypasses no-input braking for 0.6 seconds. Jump
  stops the rope and further Grapple force, but it no longer immediately clamps
  or brakes away the existing lunge momentum. The cap clears once normal
  deceleration returns the player to normal movement speed.
Tests/checks run:
- Focused source inspection of Grapple cancellation, Jump, and horizontal speed
  clamping paths.
Manual test steps:
1. Grapple a wall while airborne and press Jump during the lunge.
2. Confirm the rope retracts and the player keeps their forward horizontal
   momentum while receiving the normal eligible Jump impulse.
3. Confirm the momentum decelerates normally and that a subsequent Grapple,
   Dash, Slide, or Teleport does not retain stale Grapple momentum.
Known limitations and remaining risks:
- Unity Editor/host-and-client runtime validation remains required. This change
  is owner-local movement presentation and does not add network messages, but
  its feel and replicated motion need two-client verification.

Server and Edgegap assessment:
- Linux dedicated server rebuild required: Yes, for the broader Grapple task's
  server-authoritative implementation; this UI-only placement correction does
  not add server changes.
- New image or image tag required: Yes for a production Grapple rollout.
- Edgegap update required: Yes for a production Grapple rollout, after owner
  approval and a published matched server image.
- Exact Edgegap action: `node tools/update-edgegap-image.mjs --firebase-secret entropy v21 <image-tag>` (not run).
- Firebase/backend deployment required: No.
- Client/server compatibility notes: Release requires matched client and
  dedicated-server Grapple versions; this selector placement correction does
  not change the protocol.

### Owner acceptance

Decision:
Notes:
Date:

## 2.1.09.00 — Add vibration feedback on lethal black-object contact

### Status

TESTING

### Priority

P2

### Recommended testing budget

- Tester passes recommended: 2.
- Pass 1 coverage: iPhone device verification for one local player contacting an
  arena black hole, a black cube, and a player-thrown black hole; confirm one
  vibration per lethal contact and normal loss behavior.
- Pass 2 coverage: only if Pass 1 records a defect; re-test the failed contact
  path plus host/joining-client ownership behavior and repeated contact cases.

### Owner decision

Not reviewed

### Requested outcome

When a player touches a black hole or black cube, the player whose device owns
that local contact receives immediate vibration feedback on iPhone. The feedback
must be local presentation only and must not alter the existing lethal-contact,
loss, or multiplayer behavior.

### TaskBoard source

- TaskBoard ID: `2.1.09.00`
- Source location: `Docs/TaskBoard.md`, detailed task entry following Milestone
  task list.
- Numbered entry: `Add vibration feedback`.

### Specifics from TaskBoard

- Add Vibrations when a players touches a black hole and black cube.
- The vibrations should work on any iPhone.

### Scope

- Add a small reusable client-feedback seam that invokes the Unity/iOS-supported
  vibration API without adding a package, native plug-in, backend call, RPC, or
  persistent setting.
- Trigger feedback only from the already-authoritative local-owner contact/loss
  paths for black holes and black cubes, after the existing duplicate-contact
  guards accept the contact.
- Cover the existing arena/central black-hole contact path, black-cube contact
  path, arena black-hole hazard path where it uses the shared hazard collision
  flow, and player-thrown black-hole contact path if it remains player-lethal.
- Ensure each accepted local lethal contact produces at most one vibration, even
  when trigger and collision callbacks, replicated copies, or repeated overlap
  callbacks occur.
- Make unsupported platforms safely no-op; iPhone support is required, while
  Android/Desktop behavior is not a player-facing requirement for this task.

### Non-goals

- Do not add vibration for non-lethal proximity, black-hole throws, ability
  button presses, ordinary impacts, other disasters, menu buttons, damage not
  caused by a black hole/cube, or remote-player contacts.
- Do not change hazard collision geometry, lethality, loss messaging, immunity,
  cooldowns, force, player movement, sound effects, haptic intensity settings,
  accessibility settings, Firebase data, or the multiplayer protocol.
- Do not introduce an iOS native plug-in, third-party SDK, new package, or
  production deployment work.

### Existing systems to reuse

- `BlackKill`, `BlackCubeKill`, and `BlackHoleKill` for local-owner contact
  detection and their existing one-shot lethal-contact guards.
- `BoundaryHazard.OnCollisionEnter` and `BoundaryMath.IsLethalContactHazard`
  for networked arena cube and arena black-hole contact classification.
- `PlayerMovement.isOwner`, `BoundaryPlayerState`, and `GameManager` so haptic
  feedback stays local to the affected player and follows the accepted loss
  path.
- Existing `SfxManager.PlayLethalHit()` call locations as the presentation
  placement reference; vibration is additional local feedback, not a replacement.
- Unity's built-in handheld vibration facility, guarded so unsupported targets
  do nothing safely.

### Likely files

- `Assets/Scripts/Abilities/BlackKill.cs`
- `Assets/Scripts/Abilities/BlackCubeKill.cs`
- `Assets/Scripts/Abilities/BlackHoleKill.cs`
- `Assets/Scripts/Boundary/BoundaryHazard.cs`
- A narrowly scoped client-feedback helper under the existing scripts structure,
  only if the project has no suitable reusable local-feedback utility.
- Focused EditMode tests under `Assets/Tests/Editor/` for contact classification
  and one-shot feedback gating, where extractable without device APIs.

### Risks

- Unity's basic handheld vibration API may not expose intensity or haptic-style
  selection; the task requires reliable vibration on iPhone, not a particular
  haptic pattern.
- Contact callbacks can occur on every replicated player and may fire more than
  once through trigger/collision overlap. Feedback must stay behind local-owner
  and existing accepted-contact guards.
- Arena black holes, the central singularity, player-thrown black holes, and
  black cubes use more than one collision implementation. Missing a path would
  make feedback inconsistent; applying feedback before each path's loss guard
  could vibrate despite a rejected/immunized contact.
- The behavior cannot be fully proven in the Unity Editor; physical iPhone
  validation is required, including a device without a mute/silent-audio
  assumption because haptics are independent of game sound.

### Server and Edgegap impact

- Linux dedicated server rebuild required: No.
- New Edgegap image required: No.
- Edgegap application/version update required: No.
- Firebase or backend deployment required: No.
- Client-only change possible: Yes.
- Implementer assessment: Expected to be client-local presentation using the
  existing local-owner contact paths. No RPC, replicated state, hazard rule, or
  server behavior should change; reassess if implementation finds a currently
  server-only contact path.
- Owner deployment approval: Not reviewed.

### Acceptance criteria

- [ ] On a physical iPhone, an accepted local-player touch of a black hole
      produces one immediate vibration and retains the existing loss outcome.
- [ ] On a physical iPhone, an accepted local-player touch of a black cube
      produces one immediate vibration and retains the existing loss outcome.
- [ ] The same behavior covers every currently lethal black-hole path in scope,
      including arena/central and player-thrown black holes where applicable,
      without changing their existing immunity or lifetime rules.
- [ ] A remote-player replica touching a black hole or cube never vibrates the
      local device.
- [ ] Repeated collision/trigger callbacks for one accepted lethal contact do
      not create repeated vibration.
- [ ] Unsupported targets safely do nothing and introduce no new Console errors.
- [ ] Existing lethal SFX, loss reason, match result, hazard despawn, and
      multiplayer authority behavior remain unchanged.
- [ ] No RPC, networked serialized field, Firebase schema, Edgegap setting, or
      production configuration is added or changed.

### Required validation

- Run focused EditMode tests for any extracted hazard-contact/one-shot feedback
  gate, plus the narrowest existing Boundary hazard tests that cover cube and
  arena-black-hole lethality.
- In the Unity Editor, verify one local-owner feedback call is reached for each
  scoped hazard path and that remote-player replicas do not reach it; record
  any path that cannot be simulated without a device.
- On at least one physical iPhone, test arena black-hole, black-cube, and
  player-thrown-black-hole contact in a two-client match. Confirm one vibration
  on the affected owner's phone, no vibration on the uninvolved phone, and the
  unchanged loss/result behavior.
- Repeat a short-lived immunity/repeated-overlap case for each path to confirm
  no vibration occurs before an accepted loss and no contact creates multiple
  vibrations.
- Check the Unity Console during testing; record exact checks and results in the
  Implementer report. No production, Firebase, Edgegap, or dedicated-server
  deployment validation is required for this client-only change.

### Implementer report

Status: Implemented; awaiting focused device and multiplayer validation.

Files changed: `Assets/Scripts/GameRegistry/LocalLethalFeedback.cs`,
`Assets/Scripts/Abilities/BlackKill.cs`, `BlackCubeKill.cs`, `BlackHoleKill.cs`,
`Assets/Scripts/Boundary/BoundaryPlayerState.cs`,
`Assets/Tests/Editor/LocalLethalFeedbackTests.cs`, `Docs/AI_TASKS.md`, and
`Docs/AI_TEST_LOG.md`.

What was implemented: Added a local-only feedback helper that calls
`Handheld.Vibrate()` on iOS player builds and is a safe no-op in the Editor and
on unsupported targets. It runs only after the existing accepted, locally-owned,
one-shot lethal-contact guards for central/legacy black holes, black cubes,
player-thrown black holes, and the shared arena-hazard loss path. No lethal
rules, loss messages, SFX, RPCs, replicated fields, or configuration changed.

Tests/checks run: Targeted `git diff --check` passed for all task files. One
focused Unity EditMode command for `LocalLethalFeedbackTests` was attempted with
Unity 6000.3.6f1 but blocked because another Unity instance has this project
open.

Results: Static inspection confirms every call is after its accepted local-owner
and duplicate-contact guard. Unity batch mode refused before compilation or
testing.

Manual test steps: Build to an iPhone and start a two-client match. On each
owner device, separately touch the arena/central black hole, black cube, and a
player-thrown black hole after owner immunity expires. Confirm one immediate
vibration and normal loss/SFX only on the affected owner's device. Repeat an
immunity/overlap case to confirm no early or duplicate vibration.

Known limitations: Focused automated, physical iPhone, and two-client checks
could not run here while the project is open in Unity.

Remaining risks: Actual iPhone haptic delivery and runtime contact ordering
need the manual validation above. No confirmed defect was found, so no second
validation pass was run.

Server and Edgegap assessment:
- Linux dedicated server rebuild required: No; this is iOS client presentation
  with no server, authority, or protocol change.
- New image or image tag required: No.
- Edgegap update required: No; exact action: none.
- Firebase/backend deployment required: No.
- Client/server compatibility notes: No RPC, networked serialized field, enum,
  payload, or data contract changed; feedback is local to the affected client.
- Deployment approval needed from owner: Yes, for any eventual client release;
  no deployment was performed.

### Tester result

Test log entry: `TEST-2.1.09.00-001`
Result: BLOCKED — focused automated run cannot acquire the project while the
active Unity Editor owns it; physical iPhone validation remains required.
Open bugs: None.

### Fixer report

Bug ID fixed:
Owner approval:
Files changed:
Fix applied:
Validation run:
Results:
Remaining risks:

### Re-test result

Test log entry:
Result:
Notes:

### Owner acceptance

Decision:
Notes:
Date:

## Current queue

## 1.1.07.00 — Add a server-authoritative Grapple ability

### Status

TESTING

### Priority

P2

### Recommended testing budget

- Tester passes recommended: 2.
- Pass 1: focused targeting/availability, player-pull and movable-target modes,
  Jump cancellation, visuals, cooldown, and full two-client authority testing.
- Pass 2: only after the Tester records a confirmed bug and the owner approves a
  fix; cover the failed scenario plus targeted regressions for movement,
  projectiles, arena masses, loadouts, disconnects, and scene transitions.

### Owner decision

Not reviewed

### Requested outcome

Grapple is available in the existing three-slot ability selection flow with a
3-second cooldown. When a valid target is under the crosshair within 50 units,
activation shows the equipped-skin arm firing a black rope to that exact target.
Static targets pull the player forward; arena black cubes, arena black holes,
and player-thrown Black Hole projectiles are instead pulled toward the player.
The rope retracts when the player reaches the target or presses Jump, with Jump
still executing normally. Both players see the grapple action and resulting
movement, and the central arena singularity can never be grappled.

### TaskBoard source

- TaskBoard ID: `1.1.07.00`
- Source location: `Docs/TaskBoard.md`
- Numbered entry: `Grappling gun — P2`

### Specifics from TaskBoard

- Add a grappling ability. The ability should be chosen in the abilities panel and should follow the same flow as the rest of the abilities.
- When you press the button you arm will shoot a black string to where ever your crosshair is pointing.
- the string will then attach to the object you are looking at and it will lunge you forward.
- This should include the wind animation
- it should have a 3 second cool down
- if there is no object that you are looking at the ability wont be able to be pressed.
- to cancel mid grappling players can press the jump button. which will still jump but the rope will stop pulling you.
- if a player grapples onto an object like a black hole or cube the cube will come to them. They can cancel this as will by pressing jump.
- Owner clarification: maximum targeting range is 50 units.
- Owner clarification: movable grapple targets are black holes and black cubes,
  excluding the singularity in the middle.
- Owner clarification: Grapple visuals and movement must be visible to both
  players in multiplayer.
- Owner clarification: the rope retracts when the player reaches the target or
  when the player presses Jump.
- Owner clarification: eligible black holes include arena-spawned black holes
  and player-thrown Black Hole projectiles.

### Scope

- Add `Grapple` additively to `AbilityId`, `AbilityRegistry`, loadout selection,
  three-slot persistence/display, cooldown UI, and network activation routing.
- Use the owning game camera/crosshair ray as the targeting source with a maximum
  initial hit distance of 50 units and an explicit collision mask/filter.
- Continuously expose Grapple availability to its equipped ability button: the
  button is interactable only when the local owner has a currently valid target
  under the crosshair, is otherwise unavailable, and still reflects cooldown.
- Capture the selected collider, hit point, and aim ray on accepted input, then
  send only the minimum target request to the server. The server must independently
  revalidate ownership, cooldown, range, line of sight, target identity/type, and
  current object existence before starting Grapple.
- For a valid static world target, attach the rope at the validated hit point and
  lunge the owning player toward it using the established owner-authoritative
  movement/impulse path. Stop/retract once the player reaches a small,
  collision-safe arrival radius around the anchor.
- For a valid movable target, keep the player as the anchor and pull the target
  toward the player's validated position until it reaches a collision-safe
  arrival radius, the player cancels, or the target becomes invalid/destroyed.
- Allow movable-target mode only for:
  - arena black cubes represented by an arena-mass `BoundaryHazard` of kind `Cube`;
  - arena black holes represented by an arena-mass `BoundaryHazard` of kind
    `ArenaBlackHole`; and
  - player-thrown Black Hole projectiles carrying the established `BlackHoleKill`
    and server-simulated `NetworkProjectilePhysics` path.
- Explicitly reject `BoundaryMatchController`'s central singularity/core and all
  non-allowlisted singularity variants, regardless of collider hierarchy or
  visual similarity.
- Keep movable-object physics on the server. Clients may request a target but
  must never choose pull force, velocity, position, target type, completion, or
  authoritative rope state.
- Show the local equipped-skin arm firing toward the captured hit point and a
  black rope/string from the arm/player to the live anchor. Replicate a suitable
  third-person arm action and the rope/anchor state so both players see one
  consistent grapple without initializing remote first-person cameras or arms.
- Reuse the speed-responsive wind presentation during the grapple lunge/pull;
  actual speed should drive wind intensity and no duplicate wind system should
  be introduced.
- On Jump during either mode, end/retract Grapple first and pass the same input
  through to the existing grounded/airborne/wall/slide Jump logic so the player
  still jumps exactly once when normally eligible.
- End and retract safely when the arrival condition is met, Jump cancels, the
  target is destroyed/despawned/becomes invalid, ownership is lost, the player
  dies/despawns/disconnects, the match ends, or the scene changes.
- Start the 3-second cooldown only for an activation accepted by the server;
  rejected/no-target requests must not consume cooldown or play a successful
  rope/arm/pull presentation.
- Preserve existing movement caps/safety, player and target collisions, lethal
  contacts, arena-mass behavior, projectile ownership/damage, and boundary forces.

### Non-goals

- Do not grapple or pull the central arena singularity, Black Rain/False
  singularities, other players, triggers, UI, scenery without a valid solid
  collider, or arbitrary movable Rigidbodies.
- Do not make Attract/Repel projectiles, ordinary cubes, barrels, hazards, or
  unrelated dynamic objects movable grapple targets unless they match the
  explicit allowlist above.
- Do not add swinging, orbiting, rope wrapping, multiple simultaneous hooks,
  grappling around corners, manual rope-length control, charge behavior, or a
  fixed-duration grapple.
- Do not add a new input button outside the existing ability-slot UI or change
  the three-slot loadout.
- Do not change Grapple range beyond 50 units, cooldown beyond 3 seconds, or
  retarget continuously after activation.
- Do not allow clients to authoritatively move arena masses/projectiles or report
  completion, damage, kills, rewards, or match results.
- Do not redesign the general wind, crosshair, skin, first-person arm, movement,
  Jump, cooldown, or ability-selection systems beyond the integrations required.
- Do not add packages or third-party assets, change Firebase data, deploy
  production, or update Edgegap as part of implementation.

### Existing systems to reuse

- `AbilityId`, `IAbility`, `INetworkedAbility`, `AbilityRegistry`,
  `PlayerAbilities`, `LoadoutManager`, and `AbilityCooldownButton` for ability
  identity, the three-slot flow, activation, and cooldown presentation.
- `Cam` and the center-screen/crosshair ray used by existing aiming/obstruction
  logic; use owner-only camera data and avoid scene searches.
- `FirstPersonArmPresentation` plus selected-skin synchronization for the local
  Beard/Turtle/Sun Ducker arm action; add Grapple presentation without replacing
  existing throw/Teleport/Slide/Dash behavior.
- `PlayerMovement`, `PlayerInputReader`, `ApplyAbilityImpulse`, movement safety,
  and Jump handling for the player lunge and cancel/pass-through behavior.
- `PlayerWindPresentation` and actual planar speed for the requested wind effect.
- `BoundaryHazard.IsArenaMass`, `BoundaryHazardKind.Cube`, and
  `BoundaryHazardKind.ArenaBlackHole` for server-side arena target allowlisting.
- `NetworkArenaCubePhysics` for server-simulated arena mass motion and existing
  NetworkTransform replication.
- `BlackThrow`, `BlackHoleKill`, and `NetworkProjectilePhysics` for identifying
  and server-simulating player-thrown Black Hole projectiles.
- `BoundaryMatchController.SingularityPosition` and the central presentation/core
  hierarchy for explicit central-singularity exclusion.
- PurrNet `ServerRpc`/`ObserversRpc` and established ownership patterns for
  validation and synchronized grapple start, live anchor, cancel, and completion.

### Likely files

- `Assets/Scripts/Abilities/GrappleAbility.cs` and its Unity-generated `.meta`.
- `Assets/Scripts/AbilitiesRegistry/AbilityId.cs`
- `Assets/Scripts/AbilitiesRegistry/AbilityRegistry.cs` only if registration
  requires an explicit integration beyond component discovery.
- `Assets/Scripts/AbilitiesRegistry/PlayerAbilities.cs`
- The existing loadout/ability-selection scripts and serialized registry used by
  the abilities panel.
- `Assets/Scripts/Player/PlayerMovement.cs`
- `Assets/Scripts/Player/PlayerInputReader.cs`
- `Assets/Scripts/Player/Cam.cs`
- `Assets/Scripts/Player/FirstPersonArmPresentation.cs`
- `Assets/Scripts/Player/PlayerWindPresentation.cs` only if Grapple needs a
  lifecycle hook not already covered by actual speed.
- `Assets/Scripts/Boundary/BoundaryHazard.cs` and/or a narrow server-side grapple
  target interface only if existing arena-mass motion cannot be invoked safely.
- `Assets/Scripts/Abilities/NetworkArenaCubePhysics.cs`
- `Assets/Scripts/Abilities/NetworkProjectilePhysics.cs`
- `Assets/Player.prefab` and the existing ability/loadout configuration assets;
  preserve all dirty serialized changes and GUIDs.
- Focused EditMode tests under `Assets/Tests/Editor/` for target classification,
  range/line-of-sight validation, pull-mode selection, and state transitions.
- Focused PlayMode/multiplayer tests under `Assets/Tests/` if the existing setup
  supports owner/server physics and synchronized rope presentation.

### Risks

- Adding `Grapple` to the network-transmitted `AbilityId` enum is a client/server
  protocol change. Append without renumbering existing values, require matched
  updated clients/server, and obtain owner approval before implementation.
- The client owns player Rigidbody simulation while arena masses/projectiles are
  server simulated. Grapple crosses those authority domains and can jitter,
  diverge, double-apply forces, or enable cheating if mode/force/completion are
  trusted to the client.
- A client-supplied hit point or object reference can be stale, spoofed, outside
  50 units, behind cover, or refer to a different collider hierarchy on the
  server; robust identity and line-of-sight revalidation are mandatory.
- PurrNet object/reference serialization may not safely identify runtime arena
  masses and thrown projectiles through a generic collider. A stable existing
  NetworkIdentity reference/ID must be used without inventing fragile names.
- The central singularity, arena black holes, false singularities, and thrown
  Black Holes have similar visuals/collider trees. Classification mistakes could
  move the central hazard or let clients target unintended lethal objects.
- Pulling lethal arena black holes or thrown Black Holes toward a player can
  trigger contact deaths, affect the other player, alter arena population, or
  interact with singularity forces; existing server-side lethal-contact and
  ownership rules must remain authoritative.
- Availability checked locally can differ from server acceptance because of
  latency or object motion. UI must tolerate rejection without consuming
  cooldown or playing a false success.
- A 50-unit ray and continuously updated button state can add physics cost or
  garbage if it allocates every frame; use a focused mask and non-allocating or
  appropriately throttled checks.
- Jump input is currently shared by grounded, wall, and Slide behaviors. Consuming
  it in Grapple can suppress the real jump or produce duplicate jump/cancel
  actions unless one owner controls input ordering.
- Rope endpoints can lag, clip, stretch through walls, survive despawn, or show
  twice if observer lifecycle and target destruction are not handled atomically.
- Pull forces can tunnel the player/target through geometry, exceed movement
  safety caps, destabilize NetworkTransform, or cause collision explosions at
  arrival without continuous collision and bounded acceleration/velocity.
- Grapple integration overlaps multiple currently modified files and systems,
  including abilities, movement, arms, wind, arena masses, and `Player.prefab`;
  implementation must preserve all user work and remain narrowly scoped.

### Acceptance criteria

- [ ] `Grapple` appears in the existing abilities panel, can occupy any one of
      the three loadout slots, persists/synchronizes through the established
      flow, and has a 3-second cooldown display.
- [ ] When Grapple is equipped and ready, its local ability button is interactable
      only while the crosshair has a server-eligible target within 50 units; no
      target, out-of-range target, occlusion, invalid collider, or cooldown makes
      it unavailable.
- [ ] An accepted activation uses the exact captured crosshair hit, shows the
      equipped-skin arm firing a black rope to it, and does not silently retarget.
- [ ] Grappling a valid static solid target lunges the owning player toward the
      validated hit point and retracts the rope when the arrival radius is reached.
- [ ] Grappling an arena black cube pulls that cube toward the player while the
      player remains the anchor; both clients observe one server-authoritative move.
- [ ] Grappling an arena-spawned `ArenaBlackHole` pulls it toward the player while
      preserving its lethal contact/hazard behavior.
- [ ] Grappling a player-thrown Black Hole projectile pulls that same networked
      projectile toward the player while preserving its owner, lifetime, damage,
      and single-spawn behavior.
- [ ] The central arena singularity and all non-allowlisted singularity variants
      are rejected and never move, start a rope, or consume cooldown.
- [ ] Other players and arbitrary movable Rigidbodies are not movable Grapple
      targets and receive no unauthorized force.
- [ ] Pressing Jump during player-pull or movable-target mode retracts the rope,
      stops Grapple force, and executes the existing eligible jump exactly once.
- [ ] Target arrival, Jump, invalidation/destruction, death, despawn, ownership
      loss, disconnect, match end, rematch, and scene transition each end Grapple
      once and leave no stale pull, rope, arm, wind, cooldown, or input state.
- [ ] Both players see one consistent third-person arm/black rope and the same
      player/target motion; only the owner sees the local first-person arm and
      local camera presentation.
- [ ] Grapple motion activates the existing speed-responsive wind according to
      actual speed without spawning a duplicate wind system.
- [ ] Server rejection for spoofed target, stale target, range, occlusion,
      cooldown, ownership, or invalid type produces no successful presentation,
      force, or cooldown consumption.
- [ ] Grapple adds no per-frame RPC, duplicate force, duplicate spawn, damage,
      reward, or match-result event and does not trust client-provided force,
      position, completion, or target classification.
- [ ] Existing ability IDs retain their numeric values; Teleport, Slide, Dash,
      Black Hole, Attract, Repel, loadout slots, cooldowns, Jump, wall movement,
      boundary forces, arena masses, and thrown projectile behavior still work.
- [ ] No existing serialized field or GUID changes unintentionally, all new
      prefab/registry references survive reload, and Unity reports no new errors
      or warnings.

### Required validation

- Before implementation, record the proposed appended `AbilityId.Grapple` value,
  RPC/object-reference representation, server validation path, force authority,
  released-client compatibility, and rollback plan for owner approval.
- Run focused EditMode tests for 50-unit boundary conditions, target/collider
  hierarchy classification, central/non-arena singularity exclusion, static vs
  movable mode, cooldown acceptance, arrival/cancel state transitions, and
  invalid/stale request rejection.
- In PlayMode, test button availability and crosshair targeting at just inside,
  exactly at, and just beyond 50 units; include empty space, occlusion, triggers,
  self, another player, static geometry, arena cube, arena black hole, central
  singularity, false/rain singularities, and thrown Black Hole projectiles.
- Test player pull toward floors, walls, ceilings, slopes, moving/removed targets,
  and collision-obstructed anchors. Confirm bounded motion, no tunneling, correct
  arrival retraction, and no unintended retargeting.
- Test each movable allowlisted type with both clients observing: arrival,
  collision, target destruction, lethal contact, singularity/boundary forces,
  and preservation of NetworkTransform/ownership/projectile damage behavior.
- Test Jump cancellation from grounded, airborne, wall, Slide, and Grapple-driven
  motion using desktop/editor and mobile touch. Confirm one cancel, one eligible
  jump, one sound, and no duplicate impulse.
- Test cooldown spam, aim changes after firing, simultaneous target destruction,
  death, respawn, ownership delay/loss, disconnect, rematch, and Game-to-Menu
  transition for duplicate or stale state.
- Run a host plus joining-client dedicated-server-compatible session with each
  player owning Grapple in turn. Verify valid/rejected requests, static player
  pull, all three movable target categories, arm/rope observer presentation,
  Jump cancellation, wind, and exactly one authoritative outcome.
- Under simulated latency when feasible, verify availability may be rejected
  safely, endpoints remain coherent, and no duplicate movement or rope replay
  occurs for late observers/state changes.
- Regression-test all existing abilities, three-slot loadouts, cooldown UI,
  normal movement/Jump/wall/Slide/Dash, player cameras, skin arms, wind,
  arena-mass lifecycle, central singularity immobility, projectile damage, match
  completion, disconnect, and rematch.
- Inspect `Assets/Player.prefab`, ability registry/loadout assets, network prefab
  registration, arena masses, thrown Black Hole prefab, LineRenderer/material,
  and scene references in Unity; confirm assigned references, preserved GUIDs,
  no missing scripts, and no unrelated serialization changes.
- Run relevant EditMode/PlayMode suites, client build, and Linux dedicated-server
  build. Record exact results, unavailable physical-device/two-client checks, and
  remaining risks in the Implementer report.

### Server and Edgegap impact

- Dedicated Linux server rebuild: Yes. Grapple adds server validation and
  authoritative movement for networked arena masses/projectiles.
- New container image: Yes for production rollout after the updated dedicated
  server passes the full multiplayer checklist and an immutable image is built
  and published outside this task.
- Edgegap application/version update: Yes for production rollout, only with a
  matched client/server compatibility plan and explicit owner deployment approval.
  Do not run `tools/update-edgegap-image.mjs` during implementation.
- Firebase deployment: No expected; Grapple must not change Firebase data or
  backend functions.
- Client validation: Yes, including loadout UI, crosshair availability, input,
  first-person arm, rope, wind, and mobile controls.
- Multiplayer validation: Yes, mandatory host/joining-client and dedicated-server
  compatible testing for both ownership directions and all target modes.
- Released-client compatibility: Existing `AbilityId` values must remain stable,
  but released clients do not know Grapple or its RPC/state. Production requires
  a matched updated client/server release or an explicit backward-compatible
  protocol gate; owner approval is required before changing the enum/RPC contract.

### Implementer report

Status:
TaskBoard ID: `1.1.07.00`
Files changed:
What was implemented:
Tests/checks run:
Results:
Manual test steps:
Known limitations:
Remaining risks:

Server and Edgegap assessment:
- Linux dedicated server rebuild required:
- New image or image tag required:
- Edgegap update required:
- Exact Edgegap action:
- Firebase/backend deployment required:
- Client/server compatibility notes:
- Deployment approval needed from owner:

### Tester result

Test log entry:
Result:
Open bugs:

### Fixer report

TaskBoard ID: `1.1.07.00`
Bug IDs fixed:
- `BUG-1.1.07.00-001`
- `BUG-1.1.07.00-002`
- `BUG-1.1.07.00-003`
Owner approval: Approved in chat on 2026-08-19.
Files changed:
- `Assets/Scripts/AbilitiesRegistry/PlayerAbilities.cs`
- `Assets/Scripts/Abilities/GrappleAbility.cs`
- `Docs/AI_TASKS.md`
Fix applied:
- Grapple cooldown now starts only from a server acceptance TargetRpc and the server rejects requests during its own three-second cooldown.
- One server coroutine now ends both static and movable grapples, sending one observer end event; Jump cancellation stops that same path.
- Server validation rejects triggers, players, arbitrary Rigidbody targets, non-allowlisted hazards, and non-Black-Hole projectiles while retaining solid static targets, allowed arena masses, and Black Hole projectiles.
Validation run:
- Focused `git diff --check` and new-source whitespace checks.
Results:
- Whitespace checks passed. Unity Test Runner and host/joining-client validation remain unrun because the active Unity editor holds the project lock.
Remaining risks:
- The new RPC/cooldown/end-state behavior requires a matched client/server build and the documented two-client verification before release. No deployment was run.

### Fixer follow-up — Grapple Jump pull handoff

TaskBoard ID: `1.1.07.00`
Bug ID fixed: Owner-described Grapple Jump pull handoff; no separate bug ID was supplied.
Owner approval: Explicit fix request in chat.
Files changed:
- `Assets/Scripts/Abilities/GrappleAbility.cs`
- `Assets/Scripts/AbilitiesRegistry/PlayerAbilities.cs`
- `Docs/AI_TASKS.md`
Fix applied: Jump cancellation now converts the pull acceleration that would
have occurred on the cancellation physics tick into one mass-corrected impulse
toward the captured anchor before ending Grapple. The existing accumulated
horizontal velocity, normal jump impulse, and temporary momentum preservation
remain intact.
Validation run:
- `git diff --check -- Assets/Scripts/Abilities/GrappleAbility.cs Assets/Scripts/AbilitiesRegistry/PlayerAbilities.cs Docs/AI_TASKS.md`
- Static source review of Grapple FixedUpdate, Jump cancellation ordering, and
  normal jump velocity handling.
Results:
- The pull constant is shared by the active acceleration and Jump handoff.
- The handoff applies only for active static-target Grapples still outside the
  arrival radius; movable-target Grapples retain their existing behavior.
- Unity compilation, EditMode/PlayMode, and two-client runtime validation were
  not run because no Unity Editor executable was available in this shell.
Manual test steps:
1. Grapple a static target while airborne and press Jump during the lunge.
2. Verify the rope retracts, the player receives the normal jump, and the
   current pull direction adds launch momentum instead of losing the boost.
3. Verify static arrival, movable-target Grapple, normal jump, Slide, Dash,
   Teleport, and Grapple cancellation still clean up without stale force.
Remaining risks: Runtime feel, exact impulse tuning, and host/joining-client
replication remain unverified.

### Re-test result

Test log entry:
Result:
Notes:

### Owner acceptance

Decision:
Notes:
Date:

## 5.1.01.00 — Add a hold-to-exit Game back button

### Status

TESTING

### Priority

P1

### Recommended testing budget

- Tester passes recommended: 2.
- Pass 1: focused Game-scene UI, pointer/touch hold-state, Menu Play-panel return,
  practice cleanup, and host/joining-client disconnect validation.
- Pass 2: only if Pass 1 records a confirmed UI-state, scene-transition, or
  multiplayer-cleanup bug and the owner approves a fix; otherwise one pass is
  sufficient.

### Owner decision

Approved by the owner's direct implementation request.

### Requested outcome

The Game scene shows an X button in the top-right corner. The first tap arms the
button and changes its prompt to `Hold to exit`. Holding it continuously for
1.5 seconds then leaves the active game safely, loads the Menu scene, and opens
the Play panel rather than a result, settings, or multiplayer subpanel.

### TaskBoard source

- TaskBoard ID: `5.1.01.00`
- Source location: `Docs/TaskBoard.md`
- Numbered entry: `Add a back button`

### Specifics from TaskBoard

- the back button should be in the topright of the screen
- it should be an x
- after pressing it once it should say hold to exit
- after holding it should bring you back to the menu scene specifically the play panel.
- Owner clarification: the required continuous hold duration is 1.5 seconds.
- Owner clarification: the button appears only in the Game scene.

### Scope

- Add one safe-area-aware X button anchored to the top-right of the Game scene UI.
- Show the compact `X` state initially; the first completed tap must not exit and
  instead arms confirmation and visibly changes the prompt to `Hold to exit`.
- Once armed, require one continuous 1.5-second pointer/touch hold on the button
  before starting the exit. Use unscaled time so the hold works while the match
  start gate or another valid game state has `Time.timeScale == 0`.
- Cancel/reset hold progress when the pointer/touch is released, canceled, moves
  off the button, the UI loses focus, the object is disabled, or the scene exits;
  a partial hold must never carry into a later press.
- Prevent double submission after the threshold is reached, disable further
  interaction, and show stable leaving feedback while cleanup/scene loading runs.
- Reuse `GameManager.DisconnectToMenu()` and its established PurrNet client/host
  shutdown path; do not call `SceneManager.LoadScene("Menu")` directly from the
  button while a client or practice host may still be connected.
- Before returning, clear stale match-result/rematch routing and establish an
  explicit one-shot Menu destination so `MenuUIController` opens its main
  `StartMenu`/Play panel after the Menu scene loads.
- Handle online clients and local Practice consistently: stop the local client,
  stop the local Practice server when applicable, wait for network cleanup as
  required by existing patterns, then load Menu exactly once.
- Treat leaving an in-progress match as navigation/disconnect only. Do not invent
  a win, loss, reward, purchase, Firebase match-result write, or rematch result.
- Keep button interaction entirely client-side; dedicated/server-only builds
  must not create or operate the Game exit UI.
- Preserve the existing Boot -> Menu -> Game scene order and existing result-screen
  return/rematch behavior outside this explicit in-game exit path.

### Non-goals

- Do not add the X button to Boot, Menu, result panels, settings, skin shop, or
  any scene other than Game.
- Do not implement Task `5.1.02.00` or standardize every screen's back behavior.
- Do not add a pause menu, confirmation modal, countdown overlay, sound, vibration,
  animation, or general menu-transition redesign.
- Do not change match scoring, win/loss authority, Firebase documents/functions,
  Edgegap deployment lifecycle, lobby schema, rematch protocol, or release settings.
- Do not allow the first tap alone, a short hold, a hold begun outside the button,
  or a canceled pointer to exit the game.
- Do not use per-frame scene searches or place networking authority in the UI
  component.
- Do not change public RPC signatures, ability/network identifiers, or save data.

### Existing systems to reuse

- `GameManager.DisconnectToMenu()` and its disconnect coroutine for stopping the
  PurrNet client/server before loading Menu.
- `GameManager.ClearLastResult()` and existing return-routing flags/patterns for
  preventing stale win/loss/rematch panels from overriding the requested Play
  panel destination.
- `MenuUIController.ShowMainMenu()` behavior and its serialized `mainMenuPanel`,
  which maps to the Menu scene's `StartMenu`/Play panel.
- The Game scene's existing Canvas and safe-area conventions; reuse
  `SafeAreaFilter` or the established safe-area transform pattern where suitable.
- Unity UI pointer interfaces (`IPointerDownHandler`, `IPointerUpHandler`,
  `IPointerExitHandler`, cancellation/disable lifecycle) for desktop mouse and
  mobile touch parity.
- Existing project UI typography/button feedback where it does not expand scope.

### Likely files

- A focused new UI component under `Assets/Scripts/GameRegistry/` or the existing
  UI folder, such as `GameExitButton.cs`, plus its Unity-generated `.meta` file.
- `Assets/Scripts/GameRegistry/GameManager.cs` for an idempotent, explicit
  in-game-exit-to-Play-panel request and complete practice/client cleanup if the
  existing `DisconnectToMenu()` path is insufficient.
- `Assets/Scripts/GameRegistry/MenuUIController.cs` for a narrowly scoped one-shot
  Play-panel destination API if the existing default routing is not reliable.
- `Assets/Scenes/Game.unity` for the Game-only button and explicit references, or
  `Assets/Scripts/Boundary/BoundaryHUD.cs` if the established runtime HUD is the
  project-owned creation point; avoid duplicating both approaches.
- Focused EditMode tests for the two-stage confirmation/hold state machine and
  Play-panel routing if extracted into testable methods.
- Focused PlayMode tests for pointer/touch lifecycle, Game-only installation,
  scene return, and cleanup when supported by the existing test environment.

### Risks

- `GameManager.DisconnectRoutine()` currently stops client/server and loads Menu
  after one frame; practice-host or live-client teardown may race PurrNet's Boot
  restoration and overwrite the requested Menu destination unless it follows the
  more defensive match-end cleanup pattern.
- Loading Menu before disconnect completes can trigger `Menu loaded while
  connected`, leave stale network state, duplicate scene loads, or strand the
  user outside the Play panel.
- Existing last-match result and `returnToServerSelector` state can route Menu to
  Win, Loss, or the multiplayer selector instead of the required Play panel.
- A UI script that reads `Time.deltaTime` will never complete a hold while the
  Game scene is paused waiting for players; it must use unscaled time.
- Pointer events differ across mouse, touch, multi-touch, drag-off, app focus
  loss, and safe-area edges. Incorrect cancellation can trigger accidental exits
  or preserve partial progress.
- Runtime-created UI can duplicate after scene reload/rematch; scene-authored UI
  can lose serialized references or overlap the existing HUD/mobile controls.
- The top-right placement can collide with notches, rounded corners, device
  status areas, or existing HUD elements if safe-area anchoring is ignored.
- If only one online player exits, the remaining peer/server must handle the
  disconnect without duplicate match-end records, rewards, or scene transitions.
- `GameManager.cs`, Menu UI scripts, and documentation currently contain
  uncommitted work; implementation must preserve it and avoid unrelated changes.

### Acceptance criteria

- [ ] Exactly one X button appears in the top-right safe area of the Game scene.
- [ ] The button does not appear in Boot, Menu, or any non-Game scene.
- [ ] Its initial visible state is `X`, and the first completed tap does not leave
      the game; it changes the prompt to `Hold to exit`.
- [ ] Once armed, holding continuously on the button for less than 1.5 seconds
      never exits and releasing/canceling resets all hold progress.
- [ ] Once armed, one continuous 1.5-second hold initiates exactly one exit,
      disables repeat interaction, and cannot start duplicate disconnect or
      scene-load routines.
- [ ] The 1.5-second hold works while `Time.timeScale` is zero and with desktop
      mouse and mobile touch input.
- [ ] Dragging/releasing outside, pointer cancellation, multi-touch interference,
      app focus loss, object disable, and scene unload cannot complete an
      accidental exit or retain partial progress.
- [ ] Exiting an online match stops the local client cleanly before Menu becomes
      active; exiting Practice stops both its local client and local server.
- [ ] Menu loads exactly once and opens the Play (`StartMenu`) panel with server
      selector, Win, Loss, settings, and other panels inactive.
- [ ] Leaving does not create a win/loss, duplicate match result, Firebase write,
      reward, rematch event, or duplicate network/scene transition.
- [ ] The remaining multiplayer peer and dedicated server handle the disconnect
      through existing behavior without initializing the exiting player's UI or
      camera remotely.
- [ ] The button respects representative mobile safe areas and does not overlap
      the Boundary HUD, crosshair, status display, or ability/touch controls.
- [ ] Existing normal match-end, Win/Loss panels, rematch, return-to-selector,
      Boot/Menu/Game flow, and direct Menu navigation remain unchanged.
- [ ] No existing serialized field or GUID is changed unintentionally, no missing
      scene reference/script is introduced, and the Unity Console has no new
      errors or warnings.

### Required validation

- Run focused EditMode tests for initial/armed/holding/leaving state transitions,
  exact 1.5-second threshold behavior, cancellation/reset, duplicate-submit
  protection, and one-shot Play-panel routing where practical.
- In Game PlayMode with `Time.timeScale == 1`, verify first tap, short hold,
  release, drag-off, re-press, full hold, leaving feedback, and exactly one Menu
  load using mouse and touch simulation.
- Repeat the full hold while `Time.timeScale == 0` during the match-start gate;
  confirm unscaled timing completes at 1.5 seconds.
- Test narrow/wide representative mobile aspect ratios and notched safe areas;
  verify top-right placement, readable prompt, and no HUD/control overlap.
- Start Practice from Boot/Menu, enter Game, exit with the X, and verify both
  local client/server stop, Menu loads once, and the Play panel is active.
- Run a host plus joining-client session. Exit from the joining client, then in a
  separate run exit from the host/owner path; confirm the exiting player reaches
  the Play panel and the remaining peer/server follows existing disconnect rules
  without duplicate results or scene loads.
- Test exit during waiting-for-player, active match, movement/ability use, and
  immediately after an interrupted short hold. Test scene transition/object
  disable/app focus loss during a partial hold for stale state.
- Regression-test normal match completion, Win/Loss routing, return to multiplayer
  selector, rematch, and subsequent new match startup.
- Inspect Game and Menu scene references in Unity if scene-authored UI changes;
  confirm one button, correct Canvas/safe-area parent, assigned handlers/panels,
  no missing scripts, and no unrelated serialized scene changes.
- Run the relevant Unity EditMode/PlayMode suites and review the Console. Record
  exact results, unavailable device/multiplayer checks, and remaining risks in
  the Implementer report.

### Server and Edgegap impact

- Dedicated Linux server rebuild: No, provided implementation remains client UI
  plus existing disconnect/menu-routing calls. If shared `GameManager.cs` runtime
  behavior changes, the Implementer must reassess and record whether matched
  client/server binaries are required.
- New container image: No expected.
- Edgegap application/version update: No expected.
- Firebase deployment: No.
- Client validation: Yes; Game/Menu UI and scene-transition validation required.
- Multiplayer validation: Yes; a host plus joining-client disconnect test is
  required because exiting Game changes live connection and scene state.
- Released-client compatibility: No network payload, RPC, schema, ability ID, or
  save-data change is planned. Any deviation requires owner approval and an
  explicit compatibility assessment before implementation continues.

### Implementer report

Status: TESTING
TaskBoard ID: `5.1.01.00`
Files changed:
- `Assets/Scripts/GameRegistry/GameExitButton.cs` (new)
- `Assets/Scripts/GameRegistry/GameExitButton.cs.meta` (new)
- `Assets/Scripts/Boundary/BoundaryHUD.cs`
- `Assets/Scripts/GameRegistry/GameManager.cs`
- `Assets/Tests/Editor/BoundaryMathTests.cs`
- `Docs/AI_TASKS.md`
What was implemented:
- Added one runtime-created, safe-area-aware Game-only X button through the existing `BoundaryHUD` canvas. It arms on a completed initial tap, then requires a continuous 1.5-second unscaled hold; release, drag-off, focus loss, multi-touch interference, disable, and scene cleanup reset partial progress.
- The button becomes non-interactive while leaving and calls `GameManager.ExitGameToPlayPanel()` exactly once.
- Added the explicit exit route, which clears result/rematch routing, stops active result/rematch listeners, uses the existing local disconnect path, waits for client and Practice-server teardown, then loads Menu. With cleared routing, `MenuUIController` selects `StartMenu`/Play rather than result or server-selector panels.
- Added focused pure checks for initial inside-tap arming and the exact 1.5-second hold threshold.
Tests/checks run:
- `git diff --check -- Assets/Scripts/GameRegistry/GameManager.cs Assets/Scripts/Boundary/BoundaryHUD.cs Assets/Tests/Editor/BoundaryMathTests.cs`
- Attempted Unity 6000.3.6f1 EditMode filter `BoundaryMathTests` with `-batchmode -nographics -runTests`.
Results:
- Focused tracked-file whitespace check passed.
- Unity exited with code 1 before running tests because another Unity instance currently has this project open; that session was not interrupted.
Manual test steps:
1. In Game, verify exactly one X appears in the top-right safe area; tap it once, then release and confirm it changes to `Hold to exit` without leaving.
2. Hold for less than 1.5 seconds, release, drag off, switch focus, and try a second touch; verify no exit occurs. Then hold continuously for 1.5 seconds and verify `Leaving...`, one Menu load, and the Play panel only.
3. Repeat while `Time.timeScale` is zero during the start gate, then in Practice and in separate joining-client and host exits. Confirm Practice stops both local client/server and no result, reward, Firebase write, rematch, or duplicate scene transition occurs.
4. Check representative notched/narrow/wide mobile layouts and normal match-end/rematch/return-to-selector regressions.
Known limitations:
- Unity compilation, EditMode execution, device safe-area checks, and live two-client teardown validation remain unrun because the currently open Unity editor prevents a second batchmode instance.
Remaining risks:
- PurrNet teardown timing, existing peer disconnect behavior, and mobile pointer cancellation need the prescribed in-editor/two-client validation before release.

Server and Edgegap assessment:
- Linux dedicated server rebuild required: No.
- New image or image tag required: No.
- Edgegap update required: No.
- Exact Edgegap action: None.
- Firebase/backend deployment required: No.
- Client/server compatibility notes: No RPC, replicated state, schema, ability identifier, or server authority behavior changed. The button is not created in batchmode/null-graphics dedicated-server builds; client-only validation is sufficient for the implementation, with the required two-client disconnect manual validation before release.
- Deployment approval needed from owner: No; no deployment is required.

### Tester result

Test log entry:
Result:
Open bugs:

### Fixer report

TaskBoard ID: `5.1.01.00`
Bug ID fixed:
Owner approval:
Files changed:
Fix applied:
Validation run:
Results:
Remaining risks:

### Re-test result

Test log entry:
Result:
Notes:

### Owner acceptance

Decision:
Notes:
Date:

## 3.1.06.00 — Add speed-responsive player wind effects

### Status

TESTING

### Priority

P2

### Owner decision

Approved by the owner in the organizing request. This approval authorizes the
defined implementation task, but does not authorize production deployment,
third-party packages, destructive asset changes, or unrelated modifications.

### Requested outcome

Running players produce a readable wind effect that strengthens with movement,
and the local game camera gains a more intense speed-line effect whenever the
player moves faster than the normal movement cap, including during Slide and
Dash. The presentation makes speed feel powerful without obscuring gameplay or
changing movement physics.

### TaskBoard source

- TaskBoard ID: `3.1.06.00`
- Source location: `Docs/TaskBoard.md`
- Numbered entry: `Add wind`

### Specifics from TaskBoard

- Search for a wind particle affect in the assets. See which on is the coolest. Give a wind affect to players running.
- Add extra wind to a players screen when they are goin above a certain speed. For example if they are using the slide or dash ability.

### Organizer-selected existing assets

- Base running/world wind: `Assets/GameElements/Sherbbs Particle Collection/Particles/Atmosphere/Wind.prefab`.
- High-speed local camera streaks: `Assets/GameElements/Sherbbs Particle Collection/Particles/Trails/EtherialStreaksTrail.prefab`.
- Selection rationale: `Wind.prefab` is the project's purpose-built looping wind
  atmosphere effect; `EtherialStreaksTrail.prefab` provides short luminous
  streaks that read as high speed more clearly than smoke, fog, or long-lived
  ambient lines.
- Both are source assets. Create narrowly tuned variants if their default scale,
  color, simulation space, emission, or lifetime is unsuitable. Do not overwrite
  the currently modified `EtherialStreaksTrail.prefab` or its `.meta` file.

### Scope

- Add a cosmetic world-space wind presentation to each player while that player
  is actively running, driven by actual planar Rigidbody speed rather than raw
  input alone.
- Fade the base effect in above a small movement threshold, scale its emission
  smoothly through the normal running-speed range, and fade/stop it when the
  player slows, stops, dies, despawns, disconnects, or leaves the Game scene.
- Orient the world effect consistently with the player's actual travel direction
  so strafing, backward movement, external forces, and direction changes do not
  produce obviously incorrect wind.
- Make the world-space wind visible for both the local and remote player without
  spawning a networked particle object or sending per-frame RPCs; derive it from
  each already-replicated player's observed motion on each client.
- Add a separate owner-only camera-space speed-streak effect using the selected
  `EtherialStreaksTrail` source or a tuned variant.
- Define the high-speed threshold relative to the existing normal movement cap:
  camera streaks begin only when planar speed exceeds `PlayerMovement.maxSpeed`,
  ensuring ordinary running uses the base wind while Slide and Dash naturally
  enter the stronger tier when their real speed crosses the threshold.
- Scale camera-streak intensity smoothly above the threshold up to the highest
  expected movement/ability speed; avoid a single-frame on/off flicker by using
  hysteresis or a short fade.
- Drive both effects from actual speed, so boundary forces or future in-scope
  movement that exceeds the threshold receives consistent feedback; Slide/Dash
  activity may be used only as supporting lifecycle context, not as a substitute
  for the speed check.
- Keep particles behind/around the view, outside the crosshair and mobile UI,
  with conservative emission and lifetime suitable for mobile performance.
- Reuse pooled/persistent ParticleSystems where practical; do not instantiate or
  destroy effects every frame or allocate in the movement/camera update loop.
- Preserve all existing movement, Slide, Dash, camera, skin, networking, and
  boundary behavior.

### Non-goals

- Do not add physical wind forces, change velocity, acceleration, friction,
  gravity, Slide/Dash tuning, cooldowns, or player authority.
- Do not add map-wide weather, wind hazards, directional arena wind, audio,
  vibration, camera shake, field-of-view zoom, or post-processing.
- Do not make camera streaks visible to remote players or initialize them on a
  non-owned player camera.
- Do not create networked particle objects, new replicated fields, per-frame
  RPCs, or changes to public networking interfaces.
- Do not import a package or third-party asset; use the existing particle
  collection already in the project.
- Do not overwrite, rename, move, or delete the selected source prefabs,
  materials, textures, or `.meta` files.
- Do not redesign existing Slide/Dash trails or unrelated ability effects.
- Do not deploy, publish a server image, or change Firebase, Edgegap, production,
  release, scene order, or project settings.

### Existing systems to reuse

- `PlayerMovement.rb.linearVelocity` for authoritative/observed actual player
  motion and `PlayerMovement.maxSpeed` for the normal-to-high-speed threshold.
- `PlayerMovement.LastFlatMoveDir` and current Rigidbody velocity for stable
  travel orientation when speed is near zero or changing quickly.
- `SlideAbility.IsActive` and `DashAbility.IsActive` only for cleanup/regression
  context; actual speed remains the presentation source of truth.
- `Cam` ownership and first-person setup/teardown lifecycle for owner-only
  camera-space streak creation, positioning, and cleanup.
- PurrNet ownership plus the player's existing NetworkTransform motion so each
  client can render world wind for both players locally without added traffic.
- Existing `Wind.prefab` and `EtherialStreaksTrail.prefab` particle systems,
  materials, textures, and GUIDs.
- `Player.prefab` serialized-reference conventions if explicit particle variant
  references are required; avoid scene searches and runtime `Find` calls.

### Likely files

- A focused new presentation component under `Assets/Scripts/Player/`, such as
  `PlayerWindPresentation.cs`, plus its Unity-generated `.meta` file.
- `Assets/Scripts/Player/Cam.cs` if owner camera setup/teardown must expose a
  safe mount or lifecycle hook for speed streaks.
- `Assets/Scripts/Player/PlayerMovement.cs` only if a read-only speed/direction
  property is needed; do not place particle authority inside movement logic.
- `Assets/Player.prefab` for explicit presentation references if runtime-local
  construction is not sufficient; preserve all current uncommitted changes.
- New tuned particle prefab variants in a focused project-owned VFX folder if
  the selected source prefabs cannot be used unchanged; preserve source GUIDs.
- `Assets/Tests/Editor/` for pure speed-to-intensity/hysteresis calculations.
- `Assets/Tests/` PlayMode coverage if the existing setup supports owner/remote
  presentation lifecycle and ParticleSystem assertions.

### Risks

- The chosen `EtherialStreaksTrail.prefab` already has uncommitted user changes;
  implementation must not overwrite or normalize that asset while creating the
  speed effect.
- Legacy/imported particle materials may not render correctly under the current
  URP setup, may appear magenta, or may use expensive shaders/overdraw on mobile.
- Default particle simulation space, scale, lifetime, direction, and prewarm may
  be inappropriate when parented to a moving player or camera; direct reuse can
  leave trails detached, moving backward, or filling the screen.
- Using raw velocity without smoothing can flicker around thresholds, spike from
  knockback/boundary forces, or point particles incorrectly during vertical
  motion and abrupt direction changes.
- Using only `SlideAbility.IsActive`/`DashAbility.IsActive` would miss other real
  high-speed motion and could show strong wind when an ability is blocked or has
  already slowed; actual planar speed must remain authoritative for presentation.
- Remote Rigidbody/NetworkTransform samples may be intermittent or kinematic,
  causing observer wind to jitter unless velocity is derived/smoothed safely.
- Camera particles can obscure the crosshair, targets, first-person arm, ability
  buttons, safe areas, or screen edges at narrow mobile aspect ratios and high FOV.
- Continuous particles on both players can add fill-rate, material instances,
  allocations, and battery cost; renderer count and emission must be profiled on
  a representative mobile device.
- Incorrect owner gating can enable a remote camera effect, duplicate particles
  after ownership changes/respawn, or leave effects alive across rematch and
  scene transitions.
- `Assets/Player.prefab`, `Cam.cs`, `PlayerMovement.cs`, Slide, and Dash currently
  contain uncommitted work; implementation must preserve it and avoid unrelated
  serialization or formatting changes.

### Acceptance criteria

- [ ] The project uses the existing `Wind.prefab` as the base visual source and
      `EtherialStreaksTrail.prefab` as the high-speed camera visual source, or
      documented tuned variants that preserve the source assets and GUIDs.
- [ ] A player moving at normal running speed produces a visible, tasteful
      world-space wind effect whose intensity scales smoothly with planar speed.
- [ ] The base wind fades/stops when the player drops below the movement threshold
      and does not play merely because movement input is held against an obstacle.
- [ ] Both players can see the running wind attached to and oriented with each
      moving player's actual travel, including forward, backward, and strafe motion.
- [ ] Only the local owner's game camera shows high-speed streaks; remote-player
      cameras, AudioListeners, HUD, and camera-local effects remain disabled.
- [ ] High-speed streaks begin only after planar speed exceeds the current normal
      `PlayerMovement.maxSpeed`, intensify smoothly with additional speed, and
      fade without flicker when speed falls back below the threshold.
- [ ] Slide and Dash show the extra camera wind whenever their actual speed is
      above the threshold, and the effect stops when they slow below it even if
      an ability lifecycle has not yet finished.
- [ ] External motion above the threshold receives the same speed feedback without
      changing physics, damage, boundary forces, or networking.
- [ ] Wind and streaks remain outside the crosshair/primary target area, do not
      block the first-person arm or mobile controls, and remain readable at the
      supported aspect ratios and minimum/maximum configurable field of view.
- [ ] Effects clean up on stop, death, despawn, ownership loss, disconnect,
      rematch, and Game-to-Menu transition without duplicate or orphan particles.
- [ ] No particle object is instantiated/destroyed every frame, no per-frame RPC
      or scene search is added, and steady-state presentation introduces no
      avoidable managed allocations.
- [ ] Existing walking, jumping, Slide, Dash, trails, camera, skins, abilities,
      two-player replication, and boundary gameplay remain unchanged.
- [ ] Materials render correctly in URP without pink/missing shaders, no existing
      `.meta`/GUID is changed, and no new Unity Console error or warning appears.

### Required validation

- Open the selected source particle prefabs in Unity and record whether direct
  reuse or tuned variants are used; inspect simulation space, renderer material,
  alignment, lifetime, emission, culling, prewarm, and mobile suitability.
- Run focused EditMode tests for speed normalization, the `maxSpeed` high-speed
  threshold, intensity clamping, hysteresis/fade behavior, and zero-speed cleanup
  if those calculations are extracted into testable methods.
- In PlayMode, test idle, walking below threshold, normal forward/back/strafe
  running, slopes, jumping/falling, abrupt stops, collisions, and direction
  reversals; confirm intensity follows planar motion without flicker or stale wind.
- Test Slide and Dash from activation through slowdown/end and verify camera
  streaks follow actual speed rather than button press or cooldown state.
- Test high-speed external impulses and boundary pull to confirm the effect is
  cosmetic, clamped, and does not alter Rigidbody motion or gameplay results.
- Run a host plus joining-client session with each player moving normally,
  sliding, and dashing. Confirm both peers see each player's world wind once,
  each owner sees only its own camera streaks, and remote camera/UI setup does
  not run.
- Test death, respawn, ownership changes, disconnect, rematch, and Game-to-Menu
  transitions for duplicate, orphaned, or still-emitting particle systems.
- Check representative supported mobile aspect ratios and minimum/maximum FOV;
  confirm crosshair, targets, first-person arms, safe areas, and ability controls
  remain unobstructed.
- Profile on a representative mobile device when available: record ParticleSystem
  count, renderer/material count, overdraw/fill-rate concerns, allocations, and
  frame-time impact with both players at maximum effect intensity.
- Inspect `Assets/Player.prefab` and any new particle variants/references in Unity;
  verify assigned references, preserved GUIDs, no missing scripts/materials, and
  no unintended changes to the existing dirty prefab or source VFX.
- Run the relevant Unity EditMode/PlayMode suites, review the Console, and record
  exact results and untested cases in the Implementer report. Because this is a
  cosmetic client presentation task with no network contract change, no backend
  deployment is expected; complete multiplayer/client release checks before any
  production release.

### Implementer report

Status: Complete; awaiting Tester and owner acceptance.

Files changed:
- `Assets/Scripts/Player/PlayerWindPresentation.cs` (new)
- `Assets/Scripts/Player/PlayerWindPresentation.cs.meta`
- `Assets/Player.prefab`
- `Assets/Tests/Editor/BoundaryMathTests.cs`
- `Docs/AI_TASKS.md`

What was implemented:
- Owner-only, custom camera-space particles create small white speed streaks; no particle prefab is used.
- Follow-up: streaks are now very small, thin, white, and generated in an evenly spaced four-line radial pattern. They originate at the crosshair and travel outward to communicate forward motion.
- Follow-up: each burst is biased by actual player velocity in camera space, so the flow responds to where the local player is looking as well as forward, backward, and strafe movement.
- Follow-up: the custom renderer now uses an explicitly white URP-compatible particle material. Streaks are thinner and smaller, stay in an outer camera band, use full 3D player velocity (forward, backward, strafe, up, and down), and increase in density as speed rises.
- Follow-up: streaks now use an explicit white texture/color on the renderer material, a white-to-transparent lifetime fade, a farther outer-edge spawn band, thinner sizes, and a 2-to-8 burst count with a faster high-speed emission interval.
- The effect is enabled only for the owned player camera, is driven by actual planar Rigidbody speed, and is destroyed on ownership loss or disable. No remote player receives a world or camera wind effect.
- Added pure speed-intensity mapping tests. No existing particle prefab or metadata was modified.

Tests/checks run:
- `git diff --check -- Assets/Scripts/Player/PlayerWindPresentation.cs Assets/Scripts/Player/PlayerWindPresentation.cs.meta Assets/Player.prefab Assets/Tests/Editor/BoundaryMathTests.cs`
- Source inspection of selected particle prefab references, Player prefab serialization, speed mapping, owner gating, and movement velocity sources.

Results:
- Diff whitespace check passed.
- Focused tests were added but not run because no Unity Editor/Test Runner executable was available in the current shell environment.

Manual test steps:
1. In Game, verify only the local player sees small white streaks while moving and that no streaks appear for the remote player.
2. Verify very small white streaks originate at the crosshair, travel outward in evenly spaced radial groups, and remain readable without obscuring controls.
3. Verify Slide, Dash, and external high-speed motion increase the local streak rate only through actual planar speed.
4. Test death, respawn, ownership change, disconnect, rematch, and Game-to-Menu transition for orphan particles.
5. Inspect at supported mobile aspect ratios and FOV extremes for sizing, culling, and particle-material correctness.

Known limitations:
- Unity visual, mobile-performance, URP-material, and two-client validation remain outstanding because Unity was unavailable from this shell.
- Final particle lifetime, edge band, and emission tuning requires in-editor visual review.

Remaining risks:
- The runtime particle material and fill-rate must be profiled on target mobile hardware.
- Edge-band placement needs representative aspect-ratio checks to confirm it remains outside safe areas.

Server and Edgegap assessment:
- Linux dedicated server rebuild required: No.
- Why: this is client-only cosmetic presentation derived from the owning player's local motion; it adds no server simulation, RPC, replicated field, or authority change.
- New image or image tag required: No.
- Edgegap update required: No.
- Exact Edgegap action: None.
- Firebase/backend deployment required: No.
- Client/server compatibility notes: The added prefab component is non-networked and introduces no protocol/data compatibility change. It intentionally has no remote-player presentation.
- Deployment approval needed from owner: No.

Follow-up Implementer report (pink streak fix):
- Status: TESTING.
- Files changed: `Assets/Scripts/Player/PlayerWindPresentation.cs`, `Docs/AI_TASKS.md`.
- What changed: the local camera streak and owner-visible world-wind renderers, including both enabled trail renderers, now share runtime-created `Universal Render Pipeline/Particles/Unlit` white materials. This replaces the legacy prefab shaders that produce the magenta fallback; streak placement, count, size, speed response, and lifetime were not changed.
- Follow-up: removed the world-wind presentation update and clean up any existing instance every frame. This removes the oversized world-space arcs so only the small, local camera streaks remain.
- Checks: `git diff --check --no-index /dev/null Assets/Scripts/Player/PlayerWindPresentation.cs` passed. Unity 6000.3.6f1 EditMode filter `BoundaryMathTests` was attempted but exited with code 1 before tests because another Unity instance has this project open.
- Manual test: in the already-open editor, enter Game and move at a speed that enables streaks; confirm the existing streak shape remains unchanged and both the particle body and trail render white, with no magenta lines.
- Known limitation / remaining risk: visual validation is still required in the currently open editor; if the URP particle shader is stripped from a player build, the material assignment will fall back to the prefab material and needs build validation.
- Server and deployment assessment: Linux dedicated-server rebuild: No (local cosmetic renderer material only). New container image: No. Edgegap update: No. Firebase/backend deployment: No. Client-only validation sufficient: Yes. Released client/server compatibility concern: None; no networked state, RPC, or data contract changed.

### Tester result

Test log entry:
Result:
Open bugs:

### Fixer report

Bug IDs fixed:
- `BUG-3.1.06.00-001`
- `BUG-3.1.06.00-002`
- `BUG-3.1.06.00-003`
Owner approval: Approved in chat before implementation.
Files changed:
- `Assets/Scripts/Player/PlayerWindPresentation.cs`
- `Assets/Player.prefab`
- `Docs/AI_TASKS.md`
Fix applied:
- Added a world-space `Wind.prefab` instance for every player on each client,
  driven by owner Rigidbody speed or observed remote transform motion, with
  travel-direction orientation and stop/clear cleanup.
- Kept camera streaks owner-only and gated their intensity by actual planar
  speed above `PlayerMovement.maxSpeed`, with a smoothed fade for threshold
  transitions.
- Reused serialized `Wind.prefab` and `EtherialStreaksTrail.prefab` references
  without modifying either source asset, material, or GUID.
Validation run:
- `git diff --check -- Assets/Scripts/Player/PlayerWindPresentation.cs Assets/Player.prefab Assets/Tests/Editor/BoundaryMathTests.cs Docs/AI_TASKS.md`
- Static source review of owner gating, remote observed-motion sampling,
  `PlayerMovement.maxSpeed` threshold use, lifecycle cleanup, and prefab GUIDs.
- Focused EditMode test source review: existing
  `PlayerWindIntensity_UsesActualSpeedThresholdsAndClamps` covers base and
  high-speed threshold mapping; Unity Test Runner was unavailable.
- Unity compilation, PlayMode, Console, URP/material, mobile-aspect/FOV, and
  two-client runtime checks were not run because no Unity Editor executable or
  attached Unity terminal was available in this shell.
Results:
- Diff whitespace check passed.
- Static checks confirm no per-frame RPCs, replicated fields, networking
  changes, runtime material construction, or per-frame particle instantiation.
- Task status remains `TESTING` pending Unity/runtime validation.
Manual test steps:
1. In Game, test idle, normal forward/back/strafe movement, slopes, jumping,
   abrupt stops, and obstacle-held input; verify world wind follows actual
   planar motion and stops cleanly.
2. Test Slide, Dash, and external speed; verify camera streaks begin only when
   actual planar speed exceeds `PlayerMovement.maxSpeed` and fade below it.
3. Run host and joining-client sessions with each player moving, sliding, and
   dashing; verify both peers see one world wind per moving player while each
   owner alone sees camera streaks.
4. Check death, respawn, ownership loss, disconnect, rematch, Game-to-Menu,
   URP materials, Console, mobile aspect ratios, and FOV extremes.
Remaining risks: Unity visual/runtime, mobile performance, cleanup, and
two-client validation remain outstanding.

### Re-test result

Test log entry:
Result:
Notes:

### Owner acceptance

Decision:
Notes:
Date:

## 2.1.16.00 — Add slide-jumps and horizontal wall slides

### Status

TESTING

### Priority

P1

### Owner decision

Not reviewed

### Requested outcome

Slide becomes a continuous traversal move: the player can carry a slide
horizontally onto vertical or tilted walls, press Jump to cancel any active
slide with a jump whose upward force is `1.5x` the normal jump, and automatically
launch up and away when the supporting floor or wall ends.

### TaskBoard source

- TaskBoard ID: `2.1.16.00`
- Source location: `Docs/TaskBoard.md`
- Numbered entry: `Upgrade the slide ability by adding a wall slides and a slide jump.`

### Specifics from TaskBoard

- The slide ability should be canceled by a jump. This jump should be higher than the normal jump.
- you should be able to slide on walls.
- if the player runs out of floor/wall to slide on it should automatically jump up
- Owner clarification: wall sliding means continuing horizontally along a vertical or tilted wall.
- Owner clarification: the slide-jump uses `1.5x` the normal jump force.
- Owner clarification: losing the supporting floor or wall automatically launches the player up and away.

### Scope

- Keep one active Slide state as the player transitions between supported floor
  sliding and horizontal sliding along valid vertical or tilted wall surfaces.
- Detect valid slide support using the existing ground/wall collision layers and
  surface rules, excluding triggers, the player's own colliders, other players,
  projectiles, hazards, and unintended dynamic objects.
- When contacting a valid wall during an active slide, project the slide travel
  horizontally along that wall and maintain the established slide speed/cap for
  the remaining Slide duration.
- Support tilted walls as well as vertical walls while keeping wall travel
  horizontal rather than turning the move into a downward wall slide.
- When Jump is pressed during any active floor or wall slide, consume that input
  once, stop Slide, restore normal movement state/visual scale, and apply an
  upward impulse equal to `PlayerMovement.jumpForce * 1.5`.
- Preserve useful horizontal slide momentum on a manual slide-jump without
  applying both the normal jump and slide-jump impulses.
- When floor support ends during a floor slide, automatically stop Slide and
  launch upward and forward/away from the departed floor edge using the current
  slide travel direction.
- When wall support ends during a wall slide, automatically stop Slide and launch
  upward and outward from the departed wall using its last valid surface normal.
- Ensure manual and automatic slide-jumps share one guarded execution path so a
  support loss and Jump input in the same physics interval produce one jump.
- Preserve Slide cooldown, total duration, speed tuning, trail/audio, local
  first-person arm lifecycle, owner-authoritative Rigidbody control, and
  NetworkTransform replication unless a documented change is required by the
  requested behavior.
- Clean up Slide suppression, wall state, visual scale/tilt, arm state, gravity,
  and cached surface data after every completion, cancellation, despawn, or
  scene transition.

### Non-goals

- Do not add general wall-running, wall-climbing, wall-sticking, or downward wall
  sliding outside an active Slide ability.
- Do not change the existing normal grounded jump or ordinary wall-jump force.
- Do not extend Slide duration, speed, cooldown, loadout behavior, or ability ID.
- Do not make hazards, projectiles, other players, black holes, or arbitrary
  movable objects valid slide walls.
- Do not add new controls; reuse the existing desktop and mobile Jump input.
- Do not redesign unrelated Dash behavior, camera controls, trails, audio, VFX,
  skins, abilities, arena geometry, or boundary rules.
- Do not change public RPC signatures, networked serialized fields, or released
  client/server contracts without separate owner approval and a compatibility
  plan.
- Do not deploy Firebase, replace an Edgegap image, or modify production/release
  settings as part of implementation.

### Existing systems to reuse

- `SlideAbility` for activation validation, duration, cooldown, slide direction,
  speed maintenance, movement suppression, trail/audio, visual scale, and
  `IsActive` lifecycle.
- `PlayerMovement` for owner-authoritative Rigidbody physics, `jumpForce`, Jump
  input consumption, ground detection, wall detection, surface normals, slope
  handling, speed limiting, gravity, and movement-suppression cleanup.
- `PlayerMovement.TryFindWall`/`IsWallJumpSurface` patterns as the starting point
  for valid wall filtering; extend deliberately for tilted slide walls without
  broadening ordinary wall-jump targets unintentionally.
- `PlayerInputReader.ConsumeJump()` and the existing `JumpPressedThisFrame`
  plumbing so mobile and desktop use the same one-shot input.
- `PlayerAbilities` and `AbilityRegistry` for the existing Slide activation,
  cooldown UI, three-slot loadout, and observer activation path.
- `Cam`/`FirstPersonArmPresentation` and `SlideAbility.IsActive` for keeping the
  existing side-arm presentation synchronized with the true Slide lifecycle.
- PurrNet ownership and the player's existing NetworkTransform for replicating
  the owner-simulated movement to the other player.

### Likely files

- `Assets/Scripts/Abilities/SlideAbility.cs`
- `Assets/Scripts/Player/PlayerMovement.cs`
- `Assets/Scripts/Player/PlayerInputReader.cs` only if existing one-shot Jump
  consumption cannot expose the required event safely.
- `Assets/Scripts/AbilitiesRegistry/PlayerAbilities.cs` only if Slide activation
  or presentation lifecycle needs a small integration adjustment.
- `Assets/Scripts/Player/FirstPersonArmPresentation.cs` or
  `Assets/Scripts/Player/Cam.cs` only if Slide termination does not already hide
  the local side arm correctly.
- `Assets/Player.prefab` only if new tunable references or surface masks must be
  serialized; preserve its existing uncommitted changes and GUIDs.
- Focused EditMode tests under `Assets/Tests/Editor/` for surface classification,
  wall-tangent direction, and jump-vector calculations.
- Focused PlayMode/multiplayer tests under `Assets/Tests/` if the existing test
  setup supports owner physics, support transitions, and replicated movement.

### Risks

- `PlayerMovement` and `Assets/Player.prefab` already contain uncommitted changes;
  implementation must preserve them and avoid overwriting current movement,
  Teleport suppression, camera, or arm work.
- The existing `SlideAbility.FixedUpdate()` stops as soon as `IsGrounded` is
  false, so transition ordering can cause a one-frame cancellation before wall
  support is detected.
- `PlayerMovement` currently consumes and clears Jump while movement is
  suppressed. Slide must receive the one-shot request before it is discarded,
  without causing both normal Jump and slide-jump paths to fire.
- Existing wall-jump classification accepts nearly vertical normals only.
  Broadening that shared rule for tilted slide walls could turn floors, ramps,
  hazards, or dynamic objects into unintended wall-jump surfaces.
- Projecting direction onto a wall can reverse, stall, climb, descend, or snap at
  corners unless the tangent preserves incoming travel and is constrained to
  horizontal motion.
- At convex corners or uneven colliders, transient support loss can trigger an
  unwanted automatic jump; detection needs a small physics-stable tolerance
  that does not make the player float past the real edge.
- Floor-edge and wall-edge loss can occur in the same physics interval as a
  manual Jump or collision change, risking duplicate impulses and duplicated
  sound/presentation cleanup.
- Automatic up-and-away impulses can exceed intended arena speed or interact
  strongly with boundary forces, black holes, slopes, ceilings, and moving
  geometry if velocity composition is not bounded consistently.
- Owner-only physics must still replicate a smooth, single wall slide and jump
  to the observer; remote clients must not simulate a second impulse.
- Changing shared movement or wall detection can regress normal walking,
  grounded jumps, ordinary wall jumps, Dash, Teleport pause, slopes, or boundary
  movement suppression.

### Acceptance criteria

- [ ] An active floor Slide continues horizontally onto a valid vertical wall
      instead of ending when ground contact is lost.
- [ ] An active Slide continues horizontally along a valid tilted wall without
      climbing or becoming the existing downward wall-slide behavior.
- [ ] Wall-slide direction follows the surface tangent that best preserves the
      incoming slide direction, including at supported wall transitions, and
      remains within the existing Slide speed cap and duration.
- [ ] Pressing Jump once during an active floor slide immediately cancels Slide
      and applies exactly one upward impulse of `1.5 * PlayerMovement.jumpForce`.
- [ ] Pressing Jump once during an active wall slide immediately cancels Slide
      and applies exactly one upward impulse of `1.5 * PlayerMovement.jumpForce`.
- [ ] A manual slide-jump preserves useful horizontal movement, restores normal
      input/control, plays Jump feedback once, and cannot also trigger the normal
      grounded/wall-jump impulse.
- [ ] Running out of floor during an active Slide automatically produces exactly
      one jump upward and forward/away along the current slide direction.
- [ ] Running out of wall during an active wall slide automatically produces
      exactly one jump upward and outward using the last valid wall normal.
- [ ] Simultaneous Jump input and support loss produce one slide-jump only.
- [ ] Invalid surfaces—including triggers, self colliders, other players,
      projectiles, hazards, black holes, and unintended movable objects—cannot
      become slide walls.
- [ ] Slide ends cleanly on duration expiry, collision interruption, death,
      despawn, disconnect, rematch, or scene transition, restoring movement
      suppression, gravity, scale/tilt, arm visibility, and cached wall state.
- [ ] Slide cooldown, duration, speed cap, trail, sound, ability loadout, normal
      jump, normal wall jump, Dash, Teleport, slopes, and boundary forces retain
      their existing behavior outside the requested Slide upgrade.
- [ ] The owner and other player observe one consistent wall slide and one jump;
      the observer does not apply duplicate movement or local-only camera/UI.
- [ ] No existing serialized field is renamed, no asset GUID changes, and no new
      Unity Console error or warning is introduced.

### Required validation

- Add/run focused EditMode tests for horizontal tangent selection on vertical and
  tilted normals, preservation of incoming direction, surface eligibility, the
  `1.5x` upward force calculation, floor-edge launch direction, wall-edge launch
  direction, and duplicate-impulse guarding.
- In Unity PlayMode, test manual slide-jumps on flat ground at low and high slide
  speed; measure the vertical impulse/trajectory against a normal jump and
  confirm the upward impulse is exactly `1.5x` normal jump force.
- Test floor-edge automatic jumps while sliding straight, diagonally, and across
  slopes; confirm one up-and-forward launch and no premature jump before support
  genuinely ends.
- Test horizontal continuation on representative vertical walls, tilted walls,
  convex/concave corners, short wall segments, tier-transition geometry, and the
  arena's intended wall-tagged surfaces.
- Test wall-edge automatic jumps at the top/end/corner of each supported wall
  type; confirm one up-and-outward launch from the last valid surface normal.
- Test Jump on the exact physics interval that floor/wall support ends and verify
  one impulse, one sound, one cooldown use, and one cleanup sequence.
- Test invalid contacts with another player, cubes/movable masses, black holes,
  projectiles, triggers, ceilings, and non-wall scenery; confirm none become
  slide support.
- Test Slide duration expiry, collision interruption, death, respawn, disconnect,
  rematch, and Game-to-Menu transition during floor and wall slides for stale
  suppression, gravity, scale/tilt, trail, arm, or cached wall state.
- Regression-test ordinary movement, grounded jump, existing wall jump, slopes,
  Dash, Teleport wind-up pause, boundary forces, cooldown UI, and three-slot
  loadouts with desktop/editor and mobile touch input.
- Run a host plus joining-client session with each client owning the action.
  Verify floor-to-wall transition, manual floor/wall slide-jumps, and automatic
  floor/wall edge jumps appear once and consistently to both players under
  normal and simulated-latency conditions when feasible.
- Inspect `Assets/Player.prefab` and relevant Game-scene references in Unity if
  serialized configuration changes; confirm assigned references, collision
  masks, NetworkTransform setup, and no missing scripts.
- Run the relevant Unity EditMode/PlayMode suites, review the Console, and record
  exact results and untested cases. A Linux dedicated-server rebuild and the
  applicable `DEPLOYMENT.md` multiplayer checklist are required before any
  production rollout; no deployment is authorized by this task.

### Implementer report

Status: Complete; awaiting Tester and owner acceptance.

Files changed:
- `Assets/Scripts/Abilities/SlideAbility.cs`
- `Assets/Scripts/Player/PlayerMovement.cs`
- `Assets/Tests/Editor/BoundaryMathTests.cs`
- `Docs/AI_TASKS.md`

What was implemented:
- Active Slide now retains its state across floor-to-valid-wall transitions, finds eligible vertical/tilted wall support, and projects travel onto the horizontal tangent that best preserves incoming direction.
- Follow-up: Slide can now begin directly while touching a valid wall side; the support cast starts at the player's collider center so it detects side contact at the player's actual height rather than only near the root/floor.
- Slide disables environmental suppression release while active, so ordinary floor/wall detection cannot end it before Slide resolves its own support transition.
- The existing owner-side Jump request is handed to active Slide before normal jump handling. Manual and support-loss exits use one guarded jump path, restoring Slide state and applying one upward impulse of `1.5 * jumpForce`.
- Floor-edge exits retain existing forward slide momentum; wall-edge exits add a conservative outward impulse from the last valid wall normal. Dynamic objects, triggers, players, projectiles, hazards, and unrelated scenery are excluded from slide-wall eligibility.
- Added cleanup on disable and focused tests for tangent selection, intended-platform eligibility, dynamic-object rejection, and the 1.5x calculation.

Tests/checks run:
- `git diff --check -- Assets/Scripts/Abilities/SlideAbility.cs Assets/Scripts/Player/PlayerMovement.cs Assets/Tests/Editor/BoundaryMathTests.cs`
- Source review of Slide lifecycle, owner input consumption, wall filtering, suppression cleanup, and NetworkTransform owner-physics pattern.

Results:
- Diff whitespace check passed.
- Focused tests were added but not run: no Unity Editor/Test Runner executable was available in the current shell environment.

Manual test steps:
1. On flat ground, trigger Slide and press Jump at low and high speed; confirm exactly one jump with 1.5x normal upward force and preserved horizontal momentum.
2. Slide into vertical and tilted intended walls; confirm horizontal tangent travel, no climbing/downward wall-slide, and normal duration/speed cap.
3. Leave floor and wall support, including simultaneous Jump input; confirm one up-forward (floor) or up-outward (wall) launch and one jump sound.
4. Verify invalid contacts—players, dynamic cubes, hazards, black holes, projectiles, triggers, ceilings, and ordinary scenery—cannot sustain Slide.
5. Run host and joining-client cases for floor/wall transitions and all jump exits, then regress ordinary jump, wall jump, Dash, Teleport wind-up, mobile Jump input, rematch, and Game-to-Menu cleanup.

Known limitations:
- PlayMode, device/touch, dedicated-server, and two-client validation are outstanding because Unity was unavailable from the current shell.
- Geometry-specific support tolerance and the outward wall-exit impulse require tuning verification in the intended arena.

Remaining risks:
- Existing owner-simulated movement and NetworkTransform replication must be verified for smooth remote wall transitions and single exit impulses under latency.
- Interactions with unusual arena collider layouts, corners, slopes, boundary forces, and concurrent abilities remain manual regression risks.

Server and Edgegap assessment:
- Linux dedicated server rebuild required: Yes.
- Why: shared Slide and PlayerMovement gameplay scripts are included in the dedicated-server Unity assembly, and multiplayer movement behavior must match the client build.
- New image or image tag required: Yes, for any production dedicated-server rollout after validation.
- Edgegap update required: Yes, only after owner approval and publication of an immutable image.
- Exact Edgegap action: `node tools/update-edgegap-image.mjs --firebase-secret entropy v21 <image-tag>` (not executed; no approved, published image tag is available).
- Firebase/backend deployment required: No.
- Client/server compatibility notes: No RPC payload, networked serialized field, ability ID, or save-data contract changed. Updated client and server gameplay builds should be tested and deployed together.
- Deployment approval needed from owner: Yes.

### Tester result

Test log entry:
Result:
Open bugs:

### Fixer report

Bug ID fixed:
Owner approval:
Files changed:
Fix applied:
Validation run:
Results:
Remaining risks:

### Re-test result

Test log entry:
Result:
Notes:

### Owner acceptance

Decision:
Notes:
Date:

## 2.1.13.00 — Add a multiplayer-visible teleport wind-up

### Status

TESTING

### Priority

P2

### Owner decision

Not reviewed

### Requested outcome

Before a successful Teleport, the activating player pauses and completes one
360-degree spin over 0.5 seconds while the existing first-person arm animation
and teleport particle effect play. Both players can see the character spin, and
the teleport occurs once only after the wind-up finishes.

### TaskBoard source

- TaskBoard ID: `2.1.13.00`
- Source location: `Docs/TaskBoard.md`
- Numbered entry: `Add teleport wind-up effect`

### Specifics from TaskBoard

- The wind up to the teleport ability should include the arm animation(which is already made) and the character should spin in a 360 while it is in a paused state. this spin should take 0.5 seconds. After the spin the teleport should take place.
- The other player should be able to see the spin. It should not be local. It should work with multiplayer
- The particle affect for the teleport should be active during the spin

### Scope

- Extend the existing accepted Teleport activation with a 0.5-second wind-up.
- Reuse the existing Teleport first-person arm animation; do not create a second
  competing arm animation or timer.
- Pause the activating character's locomotion/displacement for the wind-up so
  the character remains in place while spinning.
- Rotate the character visual through one complete 360-degree turn during the
  same 0.5-second interval and return it to a valid gameplay-facing orientation
  when the wind-up completes.
- Replicate the wind-up spin so the owner and the other player observe the same
  activation once, including on a dedicated server session.
- Start and keep the existing teleport-start particle effect active during the
  spin, then perform the existing teleport exactly once after the wind-up.
- Release the paused state and clean up all wind-up presentation when Teleport
  completes, fails, is interrupted, the player despawns/disconnects, or the
  scene changes.
- Preserve server authority, destination validation, cooldown enforcement,
  start/end/failure VFX, audio, and NetworkTransform behavior.

### Non-goals

- Do not change Teleport range, destination rules, collision checks, cooldown,
  damage, loadout behavior, or ability identifier.
- Do not add wind-ups or spins to other abilities.
- Do not redesign the existing first-person arm or teleport particle assets.
- Do not change Firebase, Edgegap configuration, save data, matchmaking, match
  results, or released-client data formats.
- Do not alter public RPC signatures or networked serialized fields unless the
  owner separately approves a compatibility plan.
- Do not deploy, build a production image, or change the production multiplayer
  baseline.

### Existing systems to reuse

- `TeleportAbility` for cooldown checks, destination validation, the current
  0.5-second completion coroutine, teleport VFX, teleport execution, and audio.
- `FirstPersonArmPresentation` and `Cam.ShowTeleportArm()` for the existing
  owner-only 0.5-second arm animation.
- `PlayerAbilities` and its established PurrNet owner/server/observer activation
  paths for validated networked ability execution.
- `PlayerMovement.SetMovementSuppressed(...)` or the established movement pause
  mechanism for temporarily preventing locomotion without moving authority into
  presentation code.
- The player's existing visual/tilt transform for the visible spin, keeping the
  first-person camera and authoritative Rigidbody orientation separate.
- Existing `teleportStartVFX`, `teleportEndVFX`, and `teleportFailVFX` references
  on `TeleportAbility`.
- Existing PurrNet `NetworkBehaviour`, RPC, and NetworkTransform patterns; keep
  the two-player dedicated-server assumptions documented in `README.md` and
  `DEPLOYMENT.md`.

### Likely files

- `Assets/Scripts/Abilities/TeleportAbility.cs`
- `Assets/Scripts/AbilitiesRegistry/PlayerAbilities.cs`
- `Assets/Scripts/Player/PlayerMovement.cs`
- `Assets/Scripts/Player/FirstPersonArmPresentation.cs` only if its existing
  0.5-second arm timing needs a shared completion/cancellation hook.
- `Assets/Scripts/Player/Cam.cs` only if the current Teleport arm entry point
  needs a lifecycle-safe cancellation hook.
- `Assets/Player.prefab` only if an explicit serialized visual transform or VFX
  reference is missing; preserve the prefab's existing uncommitted changes.
- Focused EditMode/PlayMode tests under `Assets/Tests/` for extracted wind-up
  state and timing rules where practical.

### Risks

- Teleport currently has uncommitted 0.5-second wind-up changes. Implementation
  must preserve them and avoid creating nested timers, duplicate arm playback,
  or a second teleport execution path.
- Rotating the authoritative player root or Rigidbody can change aim, movement,
  collision, destination direction, camera yaw, or NetworkTransform state. The
  visible spin should not silently alter gameplay orientation.
- A local-only coroutine or visual rotation would violate the requirement that
  the other player see the spin; an observer-only effect could instead be late,
  duplicated, or out of phase with the authoritative teleport.
- Pausing only presentation may allow the Rigidbody to drift, while pausing the
  wrong movement layer may leave suppression active after interruption.
- Destination and aim must be captured/validated consistently so turning the
  visual during the wind-up does not redirect the eventual teleport.
- Start particles with a lifetime shorter than the wind-up, or particles spawned
  only on one peer, may not remain visible to both players throughout the spin.
- Disconnect, death, respawn, match end, rematch, or scene transition during the
  wind-up can leave a stale rotation, paused movement, particle effect, or pending
  teleport unless cancellation is explicit.
- Network timing and latency can make the spin and teleport appear discontinuous
  unless all peers derive them from one accepted activation and duration.

### Acceptance criteria

- [ ] An accepted Teleport activation plays the existing first-person arm
      animation for the local owner during the wind-up.
- [ ] The activating character remains paused in place and its visible model
      completes exactly one 360-degree spin over 0.5 seconds.
- [ ] The owner and the other player both see the same single spin; it is not a
      local-only effect.
- [ ] The existing teleport-start particle effect is active and visible during
      the spin for both players.
- [ ] The teleport occurs exactly once after the 0.5-second spin completes, not
      before or during it.
- [ ] The spin does not change the captured teleport direction/destination,
      authoritative gameplay facing, first-person camera yaw, or post-teleport
      movement direction.
- [ ] The player's paused state, temporary visual rotation, arm, and wind-up VFX
      are cleared after success, failure, interruption, despawn, disconnect,
      death, rematch, or scene transition.
- [ ] A blocked/invalid Teleport does not begin a successful wind-up or move the
      player, and repeated taps/cooldown rejection do not create duplicate spins,
      particles, arm animations, or teleports.
- [ ] Existing Teleport range, collision safety, ground snap, cooldown, end/fail
      effects, sound, loadout behavior, and multiplayer authority still work.
- [ ] Host and joining client observe consistent wind-up and final player state
      with either client owning the Teleport activation.
- [ ] No new Unity Console errors or warnings are introduced.

### Required validation

- Run focused automated tests for any extracted wind-up state/timing and cleanup
  rules, followed by the relevant Unity EditMode/PlayMode tests available.
- In the Unity Editor, measure activation-to-teleport timing and confirm one full
  spin and one teleport occur after approximately 0.5 seconds.
- Verify the player cannot translate during the wind-up and regains normal
  movement immediately after success and every cancellation path.
- Verify the first-person camera does not perform the third-person 360-degree
  spin and retains its pre-wind-up aim/facing after the teleport.
- Confirm the start particle remains active throughout the wind-up and that
  start, end, and failure effects occur at their intended positions.
- Run a host plus joining-client test on the dedicated-server-compatible path.
  Activate Teleport once as host owner and once as joining-client owner; confirm
  both peers see one synchronized spin, active wind-up particles, and one final
  teleport.
- Under simulated latency when feasible, check that the spin, particles, and
  teleport remain ordered and do not replay or visibly snap backward.
- Test invalid destination, cooldown spam, death/despawn, disconnect, rematch,
  and Game-to-Menu transition during the wind-up for stale movement suppression,
  rotation, particles, arms, or delayed teleports.
- Confirm remote players never initialize the activating player's local camera
  or first-person arm while still seeing the third-person spin and particles.
- Inspect `Assets/Player.prefab` and relevant Game-scene references in Unity if
  serialized references change; confirm no missing scripts/references and no
  existing GUID or serialized field name changes.
- Review the Unity Console throughout and record exact test/build results,
  untested cases, and remaining release risk in the Implementer report. Before
  release, complete the applicable multiplayer checklist in `DEPLOYMENT.md`.

### Implementer report

Status: Complete; awaiting Tester and owner acceptance.

Files changed:
- `Assets/Scripts/Abilities/TeleportAbility.cs`
- `Assets/Scripts/AbilitiesRegistry/PlayerAbilities.cs`
- `Assets/Scripts/Player/Cam.cs`
- `Docs/AI_TASKS.md`

What was implemented:
- Added a server-owned Teleport wind-up: the server validates and captures the destination once, tells observers to begin presentation, waits 0.5 seconds, moves the authoritative Rigidbody once, then tells observers to complete presentation.
- Added private observer RPCs for the wind-up begin, completion, and rejected/failed presentation. No existing RPC signature, serialized field, ability ID, loadout, Firebase schema, or save data changed.
- During the wind-up, the existing owner-only arm plays, the local owner cannot move or change camera aim, the third-person `Visual/Tilt` rotates one 360-degree spin, and the start particle is retained for the whole wind-up.
- Completion, failure, and disable paths restore local movement/camera input, reset the temporary visual rotation, and remove the retained wind-up particle.

Tests/checks run:
- `git diff --check` for the TASK-001/2.1.13.00 source and task-report files.
- Source review of the server RPC activation path, observer presentation path, `PlayerMovement.SetMovementSuppressed`, camera look ownership, and the existing player `Visual/Tilt` hierarchy.

Results:
- Diff whitespace check passed.
- The final authoritative position change is now invoked only by the server completion coroutine; clients only play presentation and await normal `NetworkTransform` replication.
- Unity Editor/Test Runner, dedicated-server, and two-client validation were not run because no Unity Editor executable was available in this shell environment.

Manual test steps:
1. Start a host and joining client using the dedicated-server-compatible flow; have each client activate Teleport once.
2. Verify both peers see one 0.5-second third-person spin and start particle, while only the owner sees the first-person arm.
3. Verify the owner cannot translate or turn during the wind-up, the camera resumes at its prior aim, and exactly one server-replicated teleport follows the spin.
4. Test invalid destination, cooldown spam, death/despawn, disconnect, rematch, and Game-to-Menu transition during wind-up for restored movement/input and no stale spin, particle, or delayed teleport.
5. Inspect the Unity Console and verify no errors or warnings; repeat under simulated latency when available.

Known limitations:
- Automated Unity, device, and multiplayer validation remains outstanding because Unity was unavailable in the current shell.
- Observer presentation begins when each client receives the server RPC; latency behavior requires the specified two-client verification.

Remaining risks:
- The existing broader ability activation path and deployed PurrNet configuration must be exercised on a dedicated server to confirm expected observer-RPC ordering and host/client presentation timing.
- Interactions with untested simultaneous movement abilities and match-specific death handling require manual regression coverage.

Server and Edgegap assessment:
- Linux dedicated server rebuild required: Yes.
- Why: Teleport validation, timing, and final Rigidbody movement now execute from `PlayerAbilities` on the dedicated server.
- New image or image tag required: Yes, for production rollout after a rebuilt server completes multiplayer release validation.
- Edgegap update required: Yes, only after owner approval and publication of an immutable image.
- Exact Edgegap action: `node tools/update-edgegap-image.mjs --firebase-secret entropy v21 <image-tag>` (not executed; no approved, published image tag is available).
- Firebase/backend deployment required: No.
- Client/server compatibility notes: This adds private observer RPCs, so updated client and dedicated-server builds must be deployed together. No existing RPC payload changed.
- Deployment approval needed from owner: Yes.

### Tester result

Test log entry:
Result:
Open bugs:

### Fixer report

Bug ID fixed: `BUG-2.1.13.00-001`
Owner approval: Approved in chat before implementation.
Files changed:
- `Assets/Scripts/Player/PlayerMovement.cs`
- `Assets/Scripts/Abilities/TeleportAbility.cs`
- `Docs/AI_TASKS.md`
Fix applied: Added an explicit-release option to movement suppression. Teleport
uses it during its wind-up, preventing grounded, wall-contact, and collision
handling from clearing suppression before the 0.5-second wind-up completes.
Existing Slide/environmental suppression behavior remains unchanged, and
Teleport cleanup still releases suppression explicitly.
Validation run:
- `git diff --check -- Assets/Scripts/Player/PlayerMovement.cs Assets/Scripts/Abilities/TeleportAbility.cs Docs/AI_TASKS.md`
- Static source review of all `MovementSuppressed` clear paths and Teleport
  wind-up cleanup.
- Unity Editor/Test Runner and two-client runtime validation attempted by
  environment inspection; no Unity Editor executable was available in this
  shell.
Results:
- Diff whitespace check passed.
- The reported next-`FixedUpdate` grounded and airborne wall-detection paths
  no longer clear Teleport suppression; collision-based clearing is protected
  as well.
- Unity compilation, EditMode/PlayMode tests, Unity Console inspection, and
  multiplayer runtime validation were not run because Unity was unavailable.
Manual test steps:
1. Start a host and joining client with Teleport equipped.
2. Activate a valid Teleport while grounded, airborne, and near a wall while
   holding movement; verify no translation or jump occurs for 0.5 seconds.
3. Verify movement resumes immediately after successful completion and after
   cancellation/disable, and verify Slide still releases normally.
4. Repeat with both host and joining-client ownership and inspect the Console.
Remaining risks: Runtime confirmation is still required for the exact wind-up
duration, cancellation paths, Slide interaction, and two-client behavior.

### Re-test result

Test log entry:
Result:
Notes:

### Owner acceptance

Decision:
Notes:
Date:

## 1.2.07.00 — Show equipped skin arms during abilities

### Status

TESTING

### Priority

P1

### Recommended testing budget

- Tester passes recommended: 2
- Pass 1 coverage: focused Unity/runtime validation, all scoped skin and ability
  combinations, and the required multiplayer checks when available.
- Pass 2: only if Pass 1 finds a confirmed bug and the owner approves a fix.

### Owner decision

Not reviewed

### Requested outcome

When the local player activates Black Hole, Repel, Attract, Teleport, Slide, or
Dash, the game camera shows a small first-person arm presentation that matches
the equipped Beard, Turtle, or Sun Ducker skin and communicates the selected
ability without obstructing play.

### TaskBoard source

- TaskBoard ID: `1.2.07.00`
- Source location: `Docs/TaskBoard.md`
- Numbered entry: `Make skin arms show in the abilities - P1`

### Specifics from TaskBoard

- I want the skins arm to be seen when activating the abilities. This should be affect for all of the skins(Beard, Turtle, and Sun Ducker).
- For each ability the arm animation will be slightl different. For the throw abilities, the arm will simply be across the screen. Tthe arm should not block the screen. It should be small and point to the direction the orb is going.
- The turtle arm should be green. The beard skin arm should be white. The Sun Ducker arm should be black.
- For the teleport ability the arm should swing across the screen. This should take 0.5 seconds. When this is done the player will teleport.
- For the slide and dash ability the arm should be on the side  for the duration of the slide/dash.
- the arm should be shown on the game camera so make sure it is seen.
- Owner clarification: the throw abilities are Black Hole, Repel, and Attract.

### Scope

- Add a camera-visible, local-player-only arm presentation for the equipped
  Beard, Turtle, and Sun Ducker skins.
- Support exactly these existing abilities: Black Hole (`BlackThrow`), Repel
  (`RepelThrow`), Attract (`AttractThrow`), Teleport, Slide, and Dash.
- For Black Hole, Repel, and Attract, show a small arm across the game camera,
  pointing in the projectile/orb aim direction without blocking the player's
  view.
- For Teleport, swing the arm across the game camera for 0.5 seconds, then
  perform the teleport.
- For Slide and Dash, keep the arm at the side of the game camera for the full
  active duration of the slide or dash, then hide it.
- Match the arm appearance to the equipped skin: green for Turtle, white for
  Beard, and black for Sun Ducker.
- Reuse the equipped-skin state and refresh the first-person arm whenever the
  local equipped skin visual changes.
- Preserve each ability's existing cooldown, ownership, server-authoritative
  projectile spawning, replication, direction, movement, and completion
  behavior except for the explicitly requested 0.5-second Teleport wind-up.
- Ensure rejected or unavailable ability activations do not leave an arm visible
  or play a misleading completed activation.

### Non-goals

- Do not add arm presentations for abilities other than the six listed above.
- Do not redesign the full third-person skin models, skin shop, ownership,
  purchasing, persistence, or Firebase skin data.
- Do not change ability balance, projectile force, damage, cooldown length,
  slide duration, dash duration, loadout size, or ability identifiers.
- Do not replace the existing ability registry, camera ownership, skin sync, or
  PurrNet authority model.
- Do not make the first-person camera arm a networked gameplay object or show a
  remote player's camera-only arm presentation.
- Do not redesign unrelated ability VFX, audio, UI, or camera motion.

### Existing systems to reuse

- `AbilityId`, `AbilityRegistry`, `IAbility`, and `PlayerAbilities` for the
  existing six-ability registry, loadout activation path, and owner checks.
- `PlayerAbilities` selected-skin synchronization and
  `RefreshLocalFirstPersonVisuals()` for Beard, Turtle, and Sun Ducker state.
- `Cam` for local-player-only first-person camera setup and presentation refresh.
- `BlackThrow`, `AttractThrow`, and `RepelThrow` for validated throw timing,
  projectile aim direction, and server-authoritative spawning.
- `TeleportAbility` for destination validation, teleport execution, cooldown,
  VFX, and audio after the requested wind-up.
- `SlideAbility.IsActive` and the existing Dash active-duration lifecycle for
  keeping the side arm visible only while movement is active.
- Existing `Player.prefab` serialized references and current skin visual assets;
  preserve all existing GUIDs and prefab compatibility.

### Likely files

- `Assets/Scripts/AbilitiesRegistry/PlayerAbilities.cs`
- `Assets/Scripts/Player/Cam.cs`
- `Assets/Scripts/Abilities/BlackThrow.cs`
- `Assets/Scripts/Abilities/AttractThrow.cs`
- `Assets/Scripts/Abilities/RepelThrow.cs`
- `Assets/Scripts/Abilities/TeleportAbility.cs`
- `Assets/Scripts/Abilities/SlideAbility.cs`
- `Assets/Scripts/Abilities/DashAbility.cs`
- `Assets/Player.prefab`
- A focused new first-person arm presentation script under `Assets/Scripts/Player/`
  if the existing components cannot cleanly own the visual lifecycle.
- Focused EditMode or PlayMode tests under `Assets/Tests/` if the presentation
  state/timing can be isolated for automated validation.

### Risks

- Delaying Teleport by 0.5 seconds changes activation timing and may desynchronize
  cooldown, destination calculation, VFX, sound, or replicated movement if the
  owner and server paths do not share one validated execution point.
- Starting the Teleport wind-up before destination validation could show a
  successful animation for a failed teleport or permit aim/destination changes
  during the delay without an explicit snapshot.
- Throw arms could disagree with the projectile direction if presentation uses
  current camera aim while the network request uses a captured aim direction.
- Camera-space geometry can clip through the near plane, disappear at different
  fields of view/aspect ratios, or obscure mobile controls and combat targets.
- Owner checks must prevent camera, arm, input, and presentation initialization
  for remote players while retaining normal third-person skin synchronization.
- Rapid skin synchronization, respawn, scene transition, interrupted movement,
  cooldown rejection, or object destruction could leave the wrong arm or a stale
  visible arm behind.
- Editing `Assets/Player.prefab` is high-conflict because it already has
  uncommitted changes; implementation must preserve and inspect those changes.

### Acceptance criteria

- [ ] Activating Black Hole, Repel, or Attract displays the equipped skin's arm
      across the local player's game camera.
- [ ] Each throw arm is small, does not materially block the screen, and points
      in the same direction as the orb/projectile launched by that activation.
- [ ] Activating Teleport swings the equipped skin's arm across the game camera
      for 0.5 seconds, and the player teleports only after that animation finishes.
- [ ] Activating Slide keeps the equipped skin's arm at the side of the game
      camera for the full slide duration and hides it when the slide ends or is
      interrupted.
- [ ] Activating Dash keeps the equipped skin's arm at the side of the game
      camera for the full dash duration and hides it when the dash ends or is
      interrupted.
- [ ] The Turtle arm is green, the Beard arm is white, and the Sun Ducker arm is
      black for all six scoped abilities.
- [ ] The arm is visible on the local game camera for every scoped skin/ability
      combination and remains readable at supported mobile aspect ratios and
      configured camera fields of view.
- [ ] Remote players do not initialize or render another player's camera-only
      arm, and the existing networked third-person skin remains correct.
- [ ] Cooldown-rejected, invalid, failed, interrupted, respawned, disconnected,
      or scene-transitioned activations do not leave the arm visible or create a
      duplicate projectile, teleport, effect, or ability execution.
- [ ] Existing cooldowns, three-slot loadouts, throw direction, projectile
      authority, slide/dash movement, skin synchronization, and ability effects
      remain unchanged except for the requested Teleport wind-up.
- [ ] No existing serialized field is renamed, no existing asset GUID is changed,
      and all added prefab references are assigned and survive a scene reload.
- [ ] No new Unity Console errors or warnings are introduced.

### Required validation

- Inspect the final `Player.prefab` and relevant Game-scene references in Unity;
  confirm all camera-arm references are assigned and no missing scripts exist.
- In the Unity Editor, test all 18 combinations of three skins (Beard, Turtle,
  Sun Ducker) and six abilities (Black Hole, Repel, Attract, Teleport, Slide,
  Dash) from the local game camera.
- For each throw, compare the displayed arm direction with the actual projectile
  direction and confirm the arm does not block the crosshair, target, or mobile
  ability controls.
- Measure Teleport from accepted activation to movement and confirm a 0.5-second
  arm swing precedes exactly one teleport; also test a blocked/failed destination.
- Confirm Slide and Dash arms appear and disappear with their real active
  durations, including early interruption, death, respawn, and scene exit.
- Run a host plus joining-client test with each client as ability owner. Confirm
  each client sees only its own camera arm, both clients observe the same single
  projectile/teleport/movement result, and remote-player local camera/UI setup
  does not run.
- Test repeated taps during cooldown, rapid different-ability inputs, skin
  changes before a match, disconnect, rematch, and Game-to-Menu transition for
  stale arms or duplicate ability results.
- Check desktop/editor input and mobile touch input at representative supported
  aspect ratios and minimum/maximum configurable field of view.
- Run focused automated tests for any extracted arm state/timing rules, followed
  by the relevant Unity EditMode/PlayMode suite available in the project.
- Review the Unity Console during all manual checks and record exact automated
  and manual results in the Implementer report; do not mark the task `DONE` until
  the owner accepts the Tester result.

### Implementer report

Status: Complete; awaiting Tester and owner acceptance.

Files changed:
- `Assets/Scripts/Player/FirstPersonArmPresentation.cs` (new runtime-only local-camera presenter)
- `Assets/Scripts/Player/FirstPersonArmPresentation.cs.meta`
- `Assets/Scripts/Player/Cam.cs`
- `Assets/Scripts/AbilitiesRegistry/PlayerAbilities.cs`
- `Assets/Scripts/Abilities/TeleportAbility.cs`
- `Assets/Scripts/Abilities/DashAbility.cs`
- `Docs/AI_TASKS.md`

What was implemented:
- A local-only, runtime-created camera arm that uses green for Turtle, white for Beard, and black for Sun Ducker. It is not serialized, spawned, or networked.
- Throw activation uses the same captured aim vector submitted to the server to orient a brief, small arm presentation.
- Slide and Dash presentation follows their existing active lifecycle; Dash now exposes its existing active state like Slide.
- Teleport validates its destination, starts its cooldown as before, presents a 0.5-second arm swing, then performs the existing teleport once. Disable/scene-transition cancellation clears the pending state.
- Equipped-skin synchronization refreshes the local camera arm color.

Tests/checks run:
- `git diff --check -- Assets/Scripts/Player/FirstPersonArmPresentation.cs Assets/Scripts/Player/FirstPersonArmPresentation.cs.meta Assets/Scripts/Player/Cam.cs Assets/Scripts/AbilitiesRegistry/PlayerAbilities.cs Assets/Scripts/Abilities/TeleportAbility.cs Assets/Scripts/Abilities/DashAbility.cs`
- Source inspection of the owner camera setup, selected-skin refresh, captured throw aim path, and existing Slide/Dash active lifecycles.

Results:
- Diff whitespace check passed for all TASK-001 files.
- The arm is constructed only from `Cam` after local ownership and camera readiness, and no prefab reference or existing serialized field was changed by this implementation.
- Unity Editor/Test Runner, device/aspect-ratio, and host-plus-client checks were not run: no Unity Editor executable was available from the current shell environment.

Manual test steps:
1. In Unity 6000.3.6f1, open the Game scene and enter a match as Beard, Turtle, then Sun Ducker.
2. For each skin, activate Black Hole, Repel, and Attract; confirm the arm is small, colored correctly, and aligned with the launched orb direction.
3. Activate Teleport toward valid and blocked destinations; verify a 0.5-second swing, exactly one teleport only after it completes, and no arm for a failed destination.
4. Activate Slide and Dash, including interruption, death/respawn, and scene exit; verify the side arm lasts only while movement is active.
5. Run host and joining client with each player as owner; verify each sees only its own arm and that throws and teleports occur exactly once for both observers.

Known limitations:
- Runtime-generated cube geometry is intentionally used to avoid modifying the already-dirty `Player.prefab`; final framing at supported mobile aspect ratios and FOVs requires Editor/device review.
- No automated Unity test was added because the visual timing is camera/runtime behavior and the local Unity Test Runner was unavailable.

Remaining risks:
- The existing non-throw observer-RPC authority/timing model remains unchanged; Teleport's added delay must be verified in a two-client session for replicated movement timing.
- Repeated-tap, disconnect, rematch, and full scene-transition behavior still requires the specified manual multiplayer validation.

Server and Edgegap assessment:
- Linux dedicated server rebuild required: Yes.
- Why: `TeleportAbility.cs`, `PlayerAbilities.cs`, and `DashAbility.cs` are included in the shared Unity gameplay assembly used by the dedicated server; the 0.5-second teleport timing must match server-hosted gameplay.
- New image or image tag required: Yes, for a production dedicated-server rollout after the rebuilt server has passed release validation.
- Edgegap update required: Yes, only after owner approval and publication of an immutable server image.
- Exact Edgegap action: `node tools/update-edgegap-image.mjs --firebase-secret entropy v21 <image-tag>` (not executed; image tag not yet approved or published).
- Firebase/backend deployment required: No.
- Client/server compatibility notes: No RPC payload, serialized field, ability ID, loadout, or Firebase schema changed. Released clients will not receive the local arm, and a released server will not apply the Teleport wind-up; release client/server versions should therefore be deployed as a matched pair.
- Deployment approval needed from owner: Yes.

### Tester result

Test log entry:
Result:
Open bugs:

### Fixer report

Bug ID fixed:
Owner approval:
Files changed:
Fix applied:
Validation run:
Results:
Remaining risks:

### Re-test result

Test log entry:
Result:
Notes:

### Owner acceptance

Decision:
Notes:
Date:
