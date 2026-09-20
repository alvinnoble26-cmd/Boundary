# Entropy Zero Menu UI Redesign Progress

Branch: `ui-redesign`  
Baseline commit: `d79874d` (`WIP baseline (pre-redesign user changes)`)  
Unity source of truth: `6000.3.6f1`

## Capability check

- CAN_RUN_UNITY: YES — `/Applications/Unity/Hub/Editor/6000.3.6f1/Unity.app/Contents/MacOS/Unity`; isolated batch compile, Menu Play-mode run, and macOS development build completed.
- CAN_CAPTURE: YES — the temporary player harness produced PNGs, including `Artifacts/MenuUIFirstBatch/Main-1920x1080.png`.
- CAN_VIEW_IMAGES: YES — PNGs were opened and visually inspected; the first generated sprites had zero alpha, were corrected, rebuilt, and recaptured.
- Project lock: two pre-existing Unity processes point at the repo. Validation runs use `/tmp/boundary-ui-validation.I9mP3a` to avoid disturbing them.
- Fonts: Orbitron and Exo 2 are absent from `Assets/Game/UI/Fonts`; using the prompt-authorized LiberationSans SDF fallback and logging this deviation.
- Product name: `ProjectSettings/ProjectSettings.asset` already says `Entropy Zero`; unchanged.

## Phase status

- P1 Verification tooling + D1 root cause: DONE
- P2 Foundation: DONE
- P3 Main/Start/Multiplayer/Options: DONE
- P4 Runtime-built and abilities/control panels: NOT STARTED
- P5 Join/Host/Results and D10-D12: NOT STARTED
- P6 Full regression/final report: NOT STARTED

## Rubric status

- R1-R12: NOT YET PASSED. Earlier first-batch screenshots cover only three panels and do not satisfy the master audit.

## Decisions and known facts

- Preserve all original names, GUIDs, serialized targets, and persistent events.
- `MuiltiplayerMenu` spelling remains unchanged.
- Networking, Firebase, IAP, server, and production configuration remain out of scope.
- Lobby presentation hooks will remain independent of Firebase/PurrNet unless existing read-only state is available.
- Existing first-batch commits are retained as history; the master prompt requires a broader replacement/refinement.

## Iteration log

### Iteration 1 — P1

- Failing: R1, R2, R4-R10, R12.
- Evidence: first-batch images expose only Main/Start/Multiplayer; background is two oversized outlined rectangles, not the required persistent space scene; runtime panels are unaudited; safe area was injected directly rather than through `SafeAreaFitter`.
- D1 preliminary cause: the old appearance came from the serialized `Canvas/Panel` Image plus legacy button sprites/materials. The initial replacement sprite generator also used `Mathf.SmoothStep` with GLSL-style arguments, producing fully transparent PNGs; fixed in commit `4d4341f`.
- Next: replace the temporary capture helpers with committed, single-command verification tooling; add an injectable safe-area source; capture every serialized and runtime panel; emit JSON/text audit and before-baseline evidence.

### Iteration 2 — P1

- Build passed and the real player produced Main, Start, Multiplayer, and Join captures.
- Verification stopped on Host because macOS LaunchServices returned `-600` during rapid sequential relaunches; this is a harness orchestration failure, not a game compile failure.
- Fix: force a fresh player instance with `open -n -W`, add a short teardown gap, then rerun the same matrix.

### Iteration 3 — P1

- Full matrix completed: 14 states at 1920×1080, 2532×1170 simulated safe area, 2048×1536, and 1400×1750; Main also captured at 0.3s, 1.5s, and 4.0s.
- Audit: no active legacy `Text`, no TMP overflow, `MenuLobbyUI` fields wired; 24 active button targets remain under 88px and fail the rubric.
- D1 confirmed: the serialized full-screen `Canvas/Panel` Image, legacy orange TMP materials/button sprites, and runtime builders applying their own navy/orange styling recreate the old look. These must be neutralized both in the scene styler and in each runtime builder; the current decorative rectangle background also fails R1 and will be replaced in P2.
- Verification tooling now builds/reuses a development player, injects the notch safe area, captures all states, writes JSON/text audit output, and emits one contact sheet per resolution.
- Evidence: `/tmp/ez-ui/iter-3`; compilation recheck passed with Unity 6000.3.6f1 (`/tmp/ez-ui-p1-compile.log`).

### Iteration 4 — P2

- Added the shared foundation: responsive 1920×1080 canvas matching, safe areas on all serialized roots, four-size typography (110/56/32/24; button 40), shared styling helpers, and a single-writer unscaled-time motion system.
- Replaced the rectangle motif with a deterministic star field and soft cyan horizon glow. The first verification image exposed a solid teal lower band from using a sliced glow sprite; replaced it with a dedicated fading horizon texture before committing.
- Full player matrix and contact sheets completed at `/tmp/ez-ui/iter-4`; Unity build and compilation passed. Remaining failures are panel-level styling and undersized legacy controls, assigned to P3-P5.

### Iteration 5 — P3

- Main now visibly identifies the game as ENTROPY ZERO, retains the original actions, and uses the prescribed upper-left identity / lower-right CTA composition. Start and Multiplayer use the same hierarchy and ≥88px actions.
- Options now has a centered glass settings card, labeled volume/accessibility groups, themed runtime Edit Controls and Other Information buttons, and a separated Back action. Runtime builders use the shared theme so orange legacy styling no longer reappears there.
- Full matrix `/tmp/ez-ui/iter-5` and targeted rebuild `/tmp/ez-ui/p3-targeted` exposed button collisions in Options; spacing was corrected and Unity compilation/apply passed in `/tmp/ez-ui-p3-core-3.log`.

## Blockers

- None yet. Missing preferred fonts are an allowed documented fallback, not a blocker.
