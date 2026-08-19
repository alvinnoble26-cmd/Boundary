# Boundary AI Test Log

This file is maintained by the Tester role. Every test cycle gets a new entry.
When a bug is found, the Tester must use the bug template below and produce the
Implementer handoff immediately after the test result.

## Test result template

## Test pass for TaskBoard [TASK-ID]

### Task

[TASK-ID] — [task title]

### Date

YYYY-MM-DD

### Validation run

- Unity compilation:
- EditMode tests:
- PlayMode tests:
- Unity Console:
- Manual Editor test:
- Two-client test:

### Result

PASS / FAIL / BLOCKED

### Acceptance criteria checked

- [ ]
- [ ]
- [ ]

### Bugs found

Use one bug entry for each concrete, reproducible bug.

#### BUG-[TASK-ID]-[NUMBER]

**Title:** [short bug title]

**Severity:** Critical / High / Medium / Low

**Status:** Open

**Reproduction steps:**

1.
2.
3.

**Expected result:**

[what should happen]

**Actual result:**

[what happened]

**Evidence:**

[console error, screenshot, log, scene, build, or test name]

**Likely area:**

[file or system, if known; do not speculate beyond the evidence]

**Regression checks required after fixing:**

-

### Tester notes

[Only concrete results and remaining untested areas]

## Required bug handoff

If any bug is found, the Tester must also output this ready-to-send message in
the chat immediately after recording the bug. This is a proposed handoff only.
The Tester must not send it to or start the Implementer automatically. The
project owner decides whether the fix should run.

```text
BUG HANDOFF — [BUG-ID]

Fix the confirmed bug below for TaskBoard item [TASK-ID].

Read AGENTS.md, Docs/AI_ORGANIZER.md, Docs/AI_TASKS.md, and Docs/AI_TEST_LOG.md.

Bug: [title]
Severity: [severity]

Reproduction steps:
1.
2.
3.

Expected result:
[expected result]

Actual result:
[actual result]

Evidence:
[evidence]

Make the smallest corrective change. Do not expand the task, redesign the
feature, or perform unrelated cleanup. Run the required regression checks,
append the fix details to the Implementer report in Docs/AI_TASKS.md, and do
not change the original acceptance criteria.
```

The Tester must not edit gameplay code, scenes, or prefabs. The Implementer
owns the fix only after the project owner approves and sends the handoff. After
the fix, the Tester must create a new test entry and re-test the original bug
plus the required regression checks.

## Fixer handoff rules

Use the Fixer for small, confirmed corrections only. Examples include a null
reference fix, an incorrect condition, a UI hookup correction, a typo in a
serialized label, or a narrowly scoped regression fix.

Do not use the Fixer for new features, architecture changes, broad refactors,
network authority changes, backend changes, or unclear bugs. Those return to the
Organizer and Implementer workflow.

The project owner must approve the Fixer handoff. The handoff must include the
task ID, bug ID, reproduction steps, expected result, actual result, and scope.

## Fixer handoff template

```text
FIXER HANDOFF — [BUG-ID]

Fix the confirmed small bug below under [TASK-ID].

Read AGENTS.md, Docs/AI_ORGANIZER.md, Docs/AI_TASKS.md, and Docs/AI_TEST_LOG.md.

Owner has approved this fix.

Bug: [title]
Severity: [severity]

Reproduction steps:
1.
2.
3.

Expected result:
[expected result]

Actual result:
[actual result]

Evidence:
[evidence]

Allowed scope:
[specific files or behavior that may be changed]

Make the smallest safe fix. Do not redesign the feature, change acceptance
criteria, perform unrelated cleanup, or create commits or deployments.

After fixing:
- Append a Fixer report under [TASK-ID] in Docs/AI_TASKS.md.
- Include files changed, the fix applied, validation run, exact results, and
  remaining risks.
- Set [TASK-ID] to TESTING.

## Server and Edgegap reporting

For every implementation or fix, the Tester must verify that the Implementer's
server assessment is present. If the change affects a networked object,
networked ability, server-authoritative validation, match state, dedicated
server startup, or backend contract, the Tester must include server/client
compatibility in the test result and identify whether a Linux server rebuild
and Edgegap update still need to be performed.
```

## Test pass for TaskBoard item 1.2.07.00

### Task

TaskBoard 1.2.07.00 — historical validation note

### Date

2026-08-18

### Validation run

- Task specification and Implementer report review: BLOCKED. `Docs/AI_TASKS.md` contains only the unpopulated TASK-001 template; its status, outcome, scope, risks, acceptance criteria, and Implementer report fields are blank. The current queue states that no tasks have been approved.
- Workspace evidence review: Ran `git status --short`, `git diff --stat`, `git diff --name-only`, `git log --oneline -12`, and a repository-wide search for `TASK-001` and Implementer-report content. No task-specific report or other TASK-001 artifact was found.
- Unity compilation: NOT RUN — no identified implementation or task scope to compile/attribute; the worktree also contains pre-existing, unrelated uncommitted changes.
- EditMode tests: NOT RUN — blocked by missing task and acceptance criteria.
- PlayMode tests: NOT RUN — blocked by missing task and acceptance criteria.
- Unity Console: NOT INSPECTED — blocked by missing task and no defined Editor scenario.
- Manual Editor test: NOT RUN — blocked by missing requested behavior and steps.
- Two-client test: NOT RUN — applicability cannot be determined without the task scope; no task-specific multiplayer implementation was identified.

### Result

BLOCKED

### Acceptance criteria checked

- [ ] Cannot check: TASK-001 acceptance criteria are not defined.

### Bugs found

None reported. A concrete, reproducible product bug cannot be established without a defined task or implementation to test.

### Tester notes

No gameplay code, scenes, or prefabs were changed. To start validation, provide a populated TASK-001 entry in `Docs/AI_TASKS.md` (including status `TESTING`, scope, acceptance criteria, and Implementer report) or identify the exact implementation files/commit. Then compilation, relevant EditMode/PlayMode tests, Console inspection, manual Editor checks, and a two-client check if networking is involved can be run against that defined scope.

## TEST-TASK-001-002

### Task

TASK-001 — Show equipped skin arms during abilities

### Date

2026-08-18

### Validation run

- Task eligibility and approval: BLOCKED. `Docs/AI_TASKS.md` lists TASK-001 as `PENDING`, with owner decision `Not reviewed`; no task is marked `TESTING`. The current queue also says that no tasks have been approved to run.
- TaskBoard Specifics review: COMPLETED. Reviewed TaskBoard item `1.2.07.00` and its Specifics: local camera arm presentations for Beard, Turtle, and Sun Ducker across the six stated abilities, including the 0.5-second Teleport wind-up.
- Acceptance criteria review: COMPLETED. Reviewed the TASK-001 criteria; they cannot be executed until the task is owner-approved and an implementation is identified.
- Implementer report review: BLOCKED. TASK-001's Status, Files changed, implementation, tests/checks, results, manual-test steps, limitations, and risks are blank.
- Implementer server and Edgegap assessment: BLOCKED. All assessment fields are blank; consequently there is no claimed Linux-server, image, Edgegap, Firebase, or compatibility conclusion to verify.
- Unity compilation: NOT RUN — there is no owner-approved implementation under test, and the worktree contains unrelated uncommitted changes.
- EditMode tests: NOT RUN — no implementation under test.
- PlayMode tests: NOT RUN — no implementation under test.
- Unity Console: NOT INSPECTED — no task-specific Editor scenario is authorized or defined.
- Manual Editor test: NOT RUN — no implementation report or test-ready task.
- Two-client test: NOT RUN — relevant once TASK-001 is implemented because it changes ability presentation around networked abilities, but no implementation is available to exercise.

### Result

BLOCKED

### Acceptance criteria checked

- [ ] Not executable: the task is not approved or marked `TESTING`.
- [ ] Not executable: no Implementer report identifies the changed files or behavior.
- [ ] Not executable: no server/Edgegap assessment was supplied for verification.

### Bugs found

None reported. No concrete product behavior has been implemented and made available for reproduction.

### Tester notes

The requested Linux-server assessment, client/dedicated-server compatibility determination, Edgegap-image need, Firebase/backend deployment need, and duplicate network-event checks are all blocked by the absence of an implementation and assessment. Once the owner moves the task to `TESTING` and the Implementer adds the completed report, test the exact changes rather than the unrelated dirty worktree. No gameplay code, scenes, prefabs, or project settings were changed.

## TEST-TASK-001-003

### Task

TASK-001 — Show equipped skin arms during abilities (TaskBoard `1.2.07.00`)

### Date

2026-08-18

### Validation run

- TaskBoard Specifics, TASK-001 acceptance criteria, and Implementer report: REVIEWED. The implementation covers the six specified abilities and three skins; the report identifies the changed runtime files and states the remaining manual/multiplayer risks.
- Unity compilation: PARTIALLY VERIFIED. The active Unity 6000.3.6f1 Editor compiled `Library/ScriptAssemblies/Assembly-CSharp.dll` at 17:54:46 after the task files (timestamped 17:46) were present. The current Editor log has no C# compiler error, compilation-failed, or TASK-001 script error entries. A separate batch-mode compilation/Test Runner run was attempted at 18:01:54 and aborted because the active Editor holds this project open.
- EditMode tests: BLOCKED. Attempted `Unity -batchmode -nographics -quit -projectPath /Users/alvinnoble/Boundary -runTests -testPlatform EditMode -testResults /tmp/boundary-task-001/editmode-results.xml`; Unity aborted with `It looks like another Unity instance is running with this project open.` No test result XML was produced.
- PlayMode tests: NOT RUN. No PlayMode test scripts are present under `Assets/Tests/`, and the active Editor prevents an isolated batch-mode run.
- Source/static checks: PASSED. `git diff --check` passed for all six TASK-001 runtime files. Source review confirms the arm component is runtime-created under the local camera, is not a network object or prefab reference, and every arm entry point is gated by `isOwner && isReady`. Throws use the same locally captured aim vector passed to the ServerRpc request. No existing RPC signature, `AbilityId`, or serialized field name was changed in the task files.
- Unity Console: INSPECTED. The active Editor log has no TASK-001 compilation errors, exceptions, or warnings. It contains a Unity Connect project request 401/400 at lines 528-529, unrelated to the task files; no baseline was available to attribute it to this task.
- Manual Editor test: NOT RUN. The 18 skin/ability combinations, camera framing at mobile aspect ratios/FOV limits, Teleport timing, and interruption/scene-exit paths could not be exercised through the currently open Editor from this test environment.
- Two-client test: NOT RUN. Host/join-owner swap, duplicate projectile/teleport/event, late ownership, disconnect, rematch, and scene-transition behavior remain unverified.

### Result

BLOCKED

### Acceptance criteria checked

- [x] Static implementation review: arm creation is local-camera-only and owner/ready-gated; remote camera-arm initialization is prevented by the public entry points.
- [x] Static implementation review: Turtle/Beard/Sun Ducker map respectively to green/white/black, and throw orientation receives the same captured aim vector supplied to the activation request.
- [x] Static implementation review: Teleport waits `0.5f` seconds between arm start and movement; no RPC payload, ability ID, or existing serialized field name changed.
- [ ] Manual proof still required for all 18 combinations, non-obstruction, exact timing, failed destinations, Slide/Dash interruption, respawn, and scene transitions.
- [ ] Multiplayer proof still required for owner/non-owner presentation, late ownership, duplicate gameplay results, disconnects, rematches, and client/server timing.

### Bugs found

None confirmed. No reproducible product bug was established without Editor Play Mode and two-client execution.

### Tester notes

The Implementer's Linux dedicated-server assessment is justified for a production rollout: `TeleportAbility`, `PlayerAbilities`, and `DashAbility` are in the shared gameplay assembly, and Teleport execution timing changed. A dedicated Linux rebuild and a new immutable server image are therefore required before deploying this feature. Updating Edgegap remains required only after that image is built/published, full release validation passes, and the owner approves the version change; it was not performed. Firebase/backend deployment is not indicated because no Functions, Firebase configuration, Firestore schema/rules, or RPC payload changed.

Source review supports wire-format compatibility because RPC signatures, ability IDs, and serialized field names are unchanged, but client/dedicated-server compatibility is not runtime-verified. Deploy matched client and server builds because the Teleport timing semantics changed. The server assessment's statement that a released server would not apply the wind-up is not demonstrated by this test; the required two-client/dedicated-server session must verify the actual authority path and ensure exactly one projectile, teleport, effect, and ability event per activation.

No gameplay code, scenes, prefabs, or project settings were changed by testing.

## TEST-2.1.13.00-001

### Task

2.1.13.00 — Add a multiplayer-visible teleport wind-up

### Date

2026-08-18

### Validation run

- TaskBoard Specifics, acceptance criteria, Implementer report, and server/Edgegap assessment: REVIEWED. The task is marked `TESTING`; the report identifies a server-owned wind-up using new private observer RPCs.
- Unity compilation: PASSED. The active Unity 6000.3.6f1 Editor compiled `Library/ScriptAssemblies/Assembly-CSharp.dll` at 18:20:58, after the changed task files at 18:19:06–18:19:40. The current Editor log contains no C# compiler errors, compilation-failed entries, or task-script errors/exceptions.
- EditMode tests: BLOCKED. A batch-mode Unity Test Runner invocation was attempted earlier in this test environment but Unity cannot open the project while the active Editor owns it; no test results were produced. No focused wind-up test exists in `Assets/Tests/Editor/`.
- PlayMode tests: NOT RUN. No PlayMode tests are present under `Assets/Tests/`; the active Editor prevents an isolated batch-mode run.
- Source/static checks: PASSED for whitespace. `git diff --check` passed for `TeleportAbility.cs`, `PlayerAbilities.cs`, `Cam.cs`, and `Docs/AI_TASKS.md`. Static authority review confirms the server activation path captures the teleport parameters once, starts one server coroutine guarded by `serverTeleportWindup`, and sends private observer begin/complete/failure RPCs.
- Unity Console: INSPECTED. No task-script compiler error, exception, or warning was found in the active Editor log. The log retains an unrelated Unity Connect 401/400 project request; no baseline was available to attribute it to this task.
- Manual Editor test: NOT RUN. Timing, one full spin, VFX persistence, input pause/recovery, invalid destination, and cancellation paths could not be exercised in the currently open Editor from this test environment.
- Two-client/dedicated-server test: NOT RUN. Owner/non-owner visibility, host/join owner swap, observer ordering, latency, duplicate effects/events, disconnect, rematch, and scene-transition behavior remain unverified.

### Result

FAIL

### Acceptance criteria checked

- [x] Static review: the new server path validates/captures the destination before the observer begin RPC and guards against concurrent server wind-up coroutines.
- [x] Static review: observer presentation drives the third-person `Visual/Tilt` spin while `Cam` gates first-person arm and look suppression to the owner.
- [ ] Failed: the activating player is not reliably paused for the required 0.5 seconds; see BUG-2.1.13.00-001.
- [ ] Manual and multiplayer proof remains required for spin duration/visibility, particle lifetime, camera/facing preservation, exactly-once teleport, cancellation, and client/server consistency.

### Bugs found

#### BUG-2.1.13.00-001

**Title:** Teleport wind-up movement pause is cleared on the next physics tick

**Severity:** High

**Status:** Open

**Reproduction steps:**

1. Start a match with a player standing on normal ground and Teleport equipped.
2. Activate a valid Teleport destination and hold movement input during the 0.5-second wind-up.
3. Observe the movement state after the next `FixedUpdate` before Teleport completes.

**Expected result:**

The player remains movement-suppressed and cannot translate for the entire 0.5-second wind-up, then control returns only after completion or an explicit cancellation.

**Actual result:**

`TeleportAbility.BeginWindupPresentation()` sets `PlayerMovement.SetMovementSuppressed(true)`, but the next `PlayerMovement.FixedUpdate()` calls `HandleWallDetection()`. That method clears `MovementSuppressed` when grounded (the normal Teleport case) and also when airborne without a wall. The player can therefore move before the wind-up completes.

**Evidence:**

`Assets/Scripts/Abilities/TeleportAbility.cs:151` enables suppression. `Assets/Scripts/Player/PlayerMovement.cs:173` calls `HandleWallDetection()` before testing `MovementSuppressed`; `PlayerMovement.cs:305-326` clears suppression for grounded players and for non-wall airborne players. This execution path is deterministic from the current source. Unity compiled the task assembly successfully at 18:20:58; runtime confirmation remains required after the fix.

**Likely area:**

`Assets/Scripts/Player/PlayerMovement.cs` suppression lifecycle, in interaction with `TeleportAbility` wind-up.

**Regression checks required after fixing:**

- Teleport pause lasts 0.5 seconds when grounded, airborne, and near walls, with no early translation or jump.
- Slide, Dash, wall-slide, wall-jump, and normal jump retain their existing suppression-release behavior.
- Completion, invalid teleport, death/despawn, disconnect, rematch, and Game-to-Menu clear the Teleport pause exactly once.
- Host plus joining-client runs verify one visible spin/VFX sequence and one final teleport for each owner.

### Tester notes

The Implementer's Linux dedicated-server assessment is justified: server-side `PlayerAbilities` now owns validation, timing, and final Rigidbody movement. A Linux server rebuild and immutable image are required before production deployment; Edgegap must be updated only after that image is published, the multiplayer release validation passes, and the owner approves. Firebase/backend deployment is not required because no Functions, Firebase config, rules, schema, or external payload changed.

The new private observer RPCs require matched updated client and dedicated-server builds. Their runtime compatibility and exact-once behavior cannot be certified until the required two-client/dedicated-server validation runs. No gameplay code, scenes, prefabs, or project settings were changed by testing.

## TEST-3.1.06.00-001

### Task

3.1.06.00 — Add speed-responsive player wind effects

### Date

2026-08-18

### Validation run

- TaskBoard Specifics, acceptance criteria, Implementer report, and server/Edgegap assessment: REVIEWED. The task is owner-approved and marked `TESTING`.
- Unity compilation: PARTIALLY VERIFIED. The active Unity Editor refreshed `Assembly-CSharp.dll` at 19:29:03 after `PlayerWindPresentation.cs` changed at 19:28:27. The Editor log records an earlier, superseded `ParticleSystemRenderMode.StretchedBillboard` compiler error; the current source uses `ParticleSystemRenderMode.Stretch`. No fresh full compilation or Test Runner run could be launched because the active Editor owns the project.
- EditMode tests: BLOCKED. `PlayerWindIntensity_UsesActualSpeedThresholdsAndClamps` exists but was not run. Batch mode cannot open the project while the active Editor is running.
- PlayMode tests: NOT RUN. No PlayMode suite is present under `Assets/Tests/`, and the active Editor prevents isolated batch mode.
- Source/static checks: PASSED for whitespace. `git diff --check` passed for the task-report and wind implementation files. Static behavior review found the defects listed below.
- Unity Console: INSPECTED. The log contains the historical superseded compiler error above, repeated `There are no audio listeners in the scene` warnings, and unrelated existing service warnings. No task-scoped clean PlayMode Console session could be performed.
- Manual Editor test: NOT RUN. No visual check of asset rendering, crosshair/UI obstruction, aspect ratios, FOVs, speed response, or cleanup could be performed from this environment.
- Two-client test: NOT RUN. Remote world-wind visibility, owner-only camera streaks, respawn/ownership changes, disconnect, rematch, and scene transitions remain unverified.

### Result

FAIL

### Acceptance criteria checked

- [ ] Failed: both players must see each moving player's world-space wind; no world-wind system exists and non-owner instances destroy their only effect.
- [ ] Failed: camera streaks must begin only above `PlayerMovement.maxSpeed`; the implementation uses `startSpeed` (1.2) and never invokes `HighSpeedIntensity`.
- [ ] Failed: the selected `Wind.prefab` and `EtherialStreaksTrail.prefab` sources (or documented variants) are not used.
- [ ] Manual proof remains required for rendering, motion orientation, non-obstruction, cleanup, allocations, mobile performance, and all two-client cases.

### Bugs found

#### BUG-3.1.06.00-001

**Title:** No world wind is rendered for moving players or remote observers

**Severity:** High

**Status:** Open

**Reproduction steps:**

1. Start a host and joining-client match.
2. Move either player at normal running speed.
3. Observe that player's third-person model from both clients.

**Expected result:**

Both clients see one world-space wind presentation attached to and oriented with the moving player.

**Actual result:**

`PlayerWindPresentation` is explicitly owner-only and creates only a camera-child particle system. Non-owner instances immediately call `DestroyStreaks()` and return, so no remote/world effect can exist.

**Evidence:**

`Assets/Scripts/Player/PlayerWindPresentation.cs:3` identifies the component as owner-only camera-space. Lines 27-30 destroy effects for every non-owner; lines 83-85 parent the sole particle system to `cameraController.cam`. The Implementer report also states that no remote player receives a world effect.

**Likely area:**

`Assets/Scripts/Player/PlayerWindPresentation.cs`.

**Regression checks required after fixing:**

- Both clients see each player's single world wind during forward, backward, and strafe running.
- Only the owner sees camera streaks; remote camera/UI remains disabled.
- Death, despawn, ownership change, disconnect, rematch, and scene transitions do not duplicate or orphan effects.

#### BUG-3.1.06.00-002

**Title:** Camera streaks activate below the required high-speed threshold

**Severity:** High

**Status:** Open

**Reproduction steps:**

1. Run at any speed above 1.2 but at or below `PlayerMovement.maxSpeed`.
2. Observe the local camera effect.
3. Compare its activation condition with the task requirement that high-speed streaks begin only above `maxSpeed`.

**Expected result:**

Normal running shows only base world wind. Camera streaks begin only when actual planar speed exceeds the current normal movement cap.

**Actual result:**

The only effect is emitted whenever `WorldWindIntensity` exceeds zero, using the serialized `startSpeed` of 1.2 and `maximumPresentationSpeed` of 24. `HighSpeedIntensity`, the only function that implements a normal-speed threshold, is never called. `PlayerMovement.maxSpeed` is never read.

**Evidence:**

`PlayerWindPresentation.cs:37-50` derives the active intensity from `WorldWindIntensity(smoothedPlanarSpeed, startSpeed, maximumPresentationSpeed)`. `startSpeed` is 1.2 at lines 7 and 854 of `Player.prefab`; `HighSpeedIntensity` is defined at lines 66-71 but has no call site. No `maxSpeed` reference exists in the component.

**Likely area:**

`Assets/Scripts/Player/PlayerWindPresentation.cs`.

**Regression checks required after fixing:**

- Idle/obstacle-held input produces no effect.
- Normal running produces base world wind but no camera streaks.
- Slide, Dash, and external speed above `maxSpeed` produce camera streaks that fade cleanly below the threshold.

#### BUG-3.1.06.00-003

**Title:** Approved particle sources are not used or replaced by documented variants

**Severity:** Medium

**Status:** Open

**Reproduction steps:**

1. Review `PlayerWindPresentation` and the Player prefab component.
2. Search the implementation for the approved `Wind.prefab` and `EtherialStreaksTrail.prefab` source assets or tuned variants.

**Expected result:**

The implementation uses the selected existing source assets or clearly documented tuned variants while preserving the source assets and GUIDs.

**Actual result:**

The component constructs a new `ParticleSystem` and a runtime material from scratch. It contains no source-prefab reference, load, or instantiation. The report explicitly says “no particle prefab is used.”

**Evidence:**

`PlayerWindPresentation.cs:83-113` creates and configures `LocalOuterSpeedStreaks` with `new GameObject` and `AddComponent<ParticleSystem>()`; repository search finds no `Wind.prefab` or `EtherialStreaksTrail` reference in the component or Player prefab.

**Likely area:**

`Assets/Scripts/Player/PlayerWindPresentation.cs` and the task implementation's VFX asset selection.

**Regression checks required after fixing:**

- Confirm source assets/GUIDs remain unchanged.
- Verify selected/tuned particle materials render in URP without pink/missing shaders.
- Profile both-player maximum intensity on a representative mobile device.

### Tester notes

The Implementer's assessment that no Linux server rebuild, Edgegap update, or Firebase/backend deployment is needed is justified only if the completed feature remains client-only cosmetic and adds no protocol or authority changes. The current implementation has no RPCs, replicated fields, or backend changes. The missing remote world wind must still be rendered locally from observed replicated motion rather than by adding per-frame network traffic.

These three findings are not suitable for the narrow Fixer workflow: completing world wind, separating base and high-speed presentation, and using the approved VFX source assets requires coordinated implementation work. No gameplay code, scenes, prefabs, or project settings were changed by testing.

## TEST-1.1.07.00-001

### Task

1.1.07.00 — Add a server-authoritative Grapple ability

### Date

2026-08-18

### Validation run

- TaskBoard Specifics and acceptance criteria: REVIEWED. The task is still marked `PENDING` and its owner decision is `Not reviewed`; the user explicitly requested this test.
- Implementer report and server/Edgegap assessment: BLOCKED. Both sections are unpopulated despite Grapple implementation files being present in the worktree.
- Unity compilation: PARTIALLY VERIFIED. `Assembly-CSharp.dll` was refreshed at 22:50:59 after the Grapple files changed at 21:13–21:14. Historical, unrelated compilation failures remain in the Editor log; no Grapple-specific compiler error was found. A new batch compilation/Test Runner run could not open the project while the active Editor owns it.
- EditMode tests: BLOCKED. No Grapple-focused test exists under `Assets/Tests/Editor/`; Unity batch mode is unavailable while the active Editor has the project open.
- PlayMode tests: NOT RUN. No PlayMode Grapple test exists and batch mode is blocked by the active Editor.
- Source/static checks: PASSED for whitespace. `git diff --check` passed for the Grapple source, integration, and prefab files. Static behavior review found the defects below.
- Unity Console: INSPECTED. No Grapple-specific Console error/exception was found. Historical compile errors and unrelated warnings remain in the active Editor log, so a clean Grapple PlayMode Console session was not available.
- Manual Editor test: NOT RUN. Crosshair availability, 50-unit/occlusion boundaries, rope/arm rendering, player/target forces, Jump cancellation, and lifecycle cleanup could not be exercised from this environment.
- Two-client/dedicated-server test: NOT RUN. Owner/non-owner presentation, server validation, duplicate forces/events, arena mass/projectile behavior, late ownership, disconnect, rematch, and scene transitions remain unverified.

### Result

FAIL

### Acceptance criteria checked

- [ ] Failed: rejected/no-target Grapple requests must not consume cooldown, while accepted requests require a 3-second server-enforced cooldown.
- [ ] Failed: static Grapple completion must retract presentation for both observers; the current implementation ends it only locally for the owner.
- [ ] Failed: Grapple must reject other players and arbitrary movable Rigidbodies; the server accepts them as static targets.
- [ ] Not testable: loadout UI availability, exact target validation, all allowed movable targets, arm/wind integration, visual correctness, Jump behavior, and multiplayer lifecycle coverage.

### Bugs found

#### BUG-1.1.07.00-001

**Title:** Grapple consumes client cooldown before server acceptance and has no server cooldown gate

**Severity:** High

**Status:** Open

**Reproduction steps:**

1. Equip Grapple in any ability slot.
2. Aim at empty space, an out-of-range object, or another server-rejected target.
3. Press the ability button, then attempt another Grapple before three seconds pass.

**Expected result:**

The button is unavailable without a valid local target. A server-rejected request consumes no cooldown or successful presentation; accepted requests receive one server-enforced 3-second cooldown.

**Actual result:**

`UseSlot` starts the local slot cooldown before calling `RequestGrapple`; the server request has no Grapple cooldown check or state. Rejected requests therefore consume the local cooldown, and a client can re-send requests after its local timer without a server-side cooldown authority check.

**Evidence:**

`PlayerAbilities.cs:542-554` calculates and starts `slotCooldownEnds` before the Grapple branch at lines 567-570. `RequestGrapple` at lines 327-354 checks only an active movable-target routine and aim-vector magnitude; `GrappleAbility` exposes a duration but contains no cooldown state.

**Likely area:**

`Assets/Scripts/AbilitiesRegistry/PlayerAbilities.cs` and `Assets/Scripts/Abilities/GrappleAbility.cs`.

**Regression checks required after fixing:**

- Empty, occluded, stale, out-of-range, invalid-type, and spoofed requests leave button/cooldown/presentation unchanged.
- Accepted requests receive exactly one 3-second server cooldown for either client owner.
- Repeated taps cannot start duplicate rope, force, or observer events.

#### BUG-1.1.07.00-002

**Title:** Static Grapple leaves remote rope presentation active indefinitely

**Severity:** High

**Status:** Open

**Reproduction steps:**

1. Start a two-client session and grapple a valid static target.
2. Let the owner reach the 1.5-unit arrival radius without pressing Jump.
3. Observe the rope from the other client.

**Expected result:**

Arrival retracts the rope once for both clients and clears all Grapple state.

**Actual result:**

Only the owner-side `GrappleAbility.FixedUpdate` calls `EndPresentation()` on arrival. Static Grapple starts no server routine and never sends `ObserversEndGrapple`, so remote observers retain the rope.

**Evidence:**

`GrappleAbility.cs:19-30` runs arrival/end logic only when `movement.isOwner`. `PlayerAbilities.cs:352-354` starts `PullGrappleTarget` only for movable targets; lines 357-365 are the sole normal path that sends `ObserversEndGrapple`.

**Likely area:**

`Assets/Scripts/Abilities/GrappleAbility.cs` and `Assets/Scripts/AbilitiesRegistry/PlayerAbilities.cs`.

**Regression checks required after fixing:**

- Static and movable target arrival, Jump cancel, target destruction, death, despawn, ownership loss, disconnect, rematch, and Game-to-Menu each send one end event and leave no rope/force.
- Host and joining client see the same single start/end sequence.

#### BUG-1.1.07.00-003

**Title:** Server accepts other players and arbitrary dynamic objects as static Grapple targets

**Severity:** High

**Status:** Open

**Reproduction steps:**

1. Aim at another player or an arbitrary movable Rigidbody that is not an allowlisted arena mass/projectile.
2. Activate Grapple within 50 units.

**Expected result:**

Other players and arbitrary movable Rigidbodies are rejected without rope, force, or cooldown consumption.

**Actual result:**

The server rejects only the requesting player's own root and hazards that are not allowlisted. A different player or ordinary Rigidbody has no `BoundaryHazard` and therefore falls through as a permitted static target.

**Evidence:**

`PlayerAbilities.cs:333-349` excludes only `hit.collider.transform.root == transform.root`; `movable` is false for non-hazard/non-projectile objects, and the `hazard != null && !movable` rejection is skipped. The static target then reaches `ObserversBeginGrapple` at line 352.

**Likely area:**

`Assets/Scripts/AbilitiesRegistry/PlayerAbilities.cs` server target classification.

**Regression checks required after fixing:**

- Reject other players, arbitrary Rigidbody objects, triggers, UI, central/false/rain singularities, and non-allowlisted hazards.
- Preserve valid static-solid, arena cube, arena black-hole, and player-thrown Black Hole behavior.
- Verify spoofed/stale/occluded/range-invalid requests are rejected server-side.

### Tester notes

The required dedicated Linux server rebuild and matched updated client/server release are justified: `AbilityId.Grapple = 6` changes the network-transmitted ability set, and new ServerRpc/ObserversRpc methods were added. Edgegap must be updated only after a rebuilt server image, full multiplayer validation, a compatibility/rollback plan, and explicit owner deployment approval. Firebase/backend deployment is not indicated by the current source.

These confirmed gaps require a complete Implementer pass, not the narrow Fixer workflow. No gameplay code, scenes, prefabs, or project settings were changed by testing.

## TEST-2.1.09.00-001

### Task

2.1.09.00 — Add vibration feedback on lethal black-object contact

### Date

2026-08-19

### Validation run

- Targeted whitespace check: PASSED. `git diff --check` passed for the task's
  implementation and test files. The whole-worktree check was not used because
  unrelated user scene/material edits already contain trailing whitespace.
- EditMode test: BLOCKED. Unity 6000.3.6f1 was invoked with
  `-testFilter LocalLethalFeedbackTests`, but batch mode stopped before
  compilation because another Unity instance currently has the project open.
- Source/static review: PASSED. `Handheld.Vibrate()` is compiled only for iOS
  player builds; all four contact routes invoke it only after their existing
  local-owner and accepted/one-shot loss guards. The arena-hazard route uses
  `BoundaryPlayerState` after its `reportedLoss` guard.
- Manual iPhone and two-client test: NOT RUN. A physical iPhone and an available
  two-client session were not available in this environment.

### Result

BLOCKED

### Acceptance criteria checked

- [x] Static review: no RPC, replicated field, Firebase data, Edgegap setting,
  hazard rule, or loss behavior was changed.
- [x] Static review: remote-player contact cannot invoke vibration because every
  contact path checks ownership before its feedback call.
- [x] Static review: repeat callbacks are guarded by `hasTriggered`,
  `hasKilledLocalPlayer`, or `BoundaryPlayerState.reportedLoss` before feedback.
- [ ] Physical iPhone confirmation of one vibration for arena/central black
  hole, black cube, and player-thrown black hole remains required.
- [ ] Two-client confirmation of no vibration on the uninvolved device and
  unchanged loss/result behavior remains required.

### Tester notes

No confirmed defect was found, so the testing budget does not justify a second
pass. Re-run the one focused EditMode test after the active Editor releases the
project, then perform the listed iPhone two-client manual test. This remains a
client-only change: no Linux dedicated-server rebuild, image, Edgegap update,
or Firebase deployment is required.
