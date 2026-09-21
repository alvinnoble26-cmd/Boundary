# CPU practice implementation and testing

## Requested behavior

Practice opens a Playground / CPU panel. Playground uses the previous local free-play path. CPU starts the same Game scene with the human and one Beard opponent. The CPU receives three distinct random enabled abilities on every new round; disabled Slide is excluded without changing its serialized ID. It uses the additive Base ability as a recovery tool when airborne, outside safety, or approaching missing floor, and consumes either independently cooling charge through the same server handler as a human player. Winning or losing shows the existing result screen. Play Again immediately starts another CPU round with a fresh loadout, without lobby/rematch negotiation. Back reopens the practice chooser. CPU matches do not write wins, losses, purchases, or lobby records.

The opponent uses normal player health, damage, movement speed, jumping, wall jumping, environmental forces, cooldowns, immunity and throwing ammunition. It uses utility scores, predictive aiming, moving-threat avoidance, short- and long-range floor checks, wall escape steering, and inward retreat before and during ring collapses. It treats a missing landing surface or a position below the current arena tier as an emergency and favors inward traversal abilities. Decisions run at 10 Hz with reused physics buffers. A new round rerolls three abilities and rejects the immediately previous three-ability set. No external AI service, model download, new package, or dedicated CPU server is used.

The 2026-09-19 playtest follow-up also increases every player root and skin to 1.3×, scales camera/ground/wall/ability geometry with it, uses continuous collision detection for simulated players, darkens arena floors and walls, and adds a red depth-tested outline to each player. Void temporarily makes the opponent outline stronger and visible through walls. Ability audio now uses finite-distance 3D emitters; jump audio is quieter and local-owner-only. Charge projectile speed is 54 (1.5×), BlackThrow horizontal force is 1.5× its configured value, and both Bullseye target circles are 1.3×. Teleport rechecks a world-scaled capsule after its wind-up. Overlapping Void presentations share the original render-state snapshot so the final cleanup cannot leave the arena dark.

Design references: [Game AI Pro: utility decisions](https://www.gameaipro.com/GameAIPro/GameAIPro_Chapter10_Building_Utility_Decisions_into_Your_Existing_Behavior_Tree.pdf) and [Craig Reynolds: steering, pursuit and evasion](https://www.red3d.com/cwr/steer/gdc99/).

## Runtime and compatibility

- Boot → Menu → Game remains the scene flow. Practice starts the existing loopback PurrNet host.
- The start gate waits for one connection and two player objects for CPU, one of each for Playground, and two of each for online multiplayer.
- The CPU is instantiated from the human player's registered prefab, preserving its network identity layout, serialized ability assets, colliders and skin geometry. It is placed at a different authored spawn point.
- The project uses PurrNet's Unsafe spawn rules, which briefly assign local ownership to host-created objects. The CPU marker is set before spawning; camera, input and loadout setup ignore that marker during ownership callbacks. Ownership is then removed with propagation. The local server simulates the CPU's existing PlayerMovement and BoundaryPlayerState.
- Human RPCs delegate to the same validated handlers the CPU calls locally. Existing released RPC attributes, payload signatures and declaration order are retained, and Base RPCs are appended after them. Existing ability numeric values remain unchanged, but Base adds numeric ID `12`; no prefab registrations, Firebase contracts or production configuration change.
- CPU pushes go directly through the existing movement impulse method. Human pushes retain their TargetRpc path. CPU elimination produces a local win; human elimination produces a local loss. The existing result latch accepts only the first result.
- Void uses the competitive health-advantage rule in CPU mode. Only Playground keeps the free-play exemption. Void speed effects apply to both locally simulated fighters and are removed on cleanup.
- The public client is owner-reported as version 1.12 build 20. Compatibility with that binary is intended, but has not been exercised with two physical clients in this implementation session. Do not point production matchmaking at this server until a 1.12 build 20 client has completed a full match against it, including a match where the new peer equips Base; an old client must not receive an unsupported Base presentation if that test fails.

## Changed files

New files (with new `.meta` GUIDs):

- `Assets/Game/Scripts/Boundary/BoundaryCpuController.cs`: spawn lifecycle, bounded sensing, navigation and ability decisions.
- `Assets/Game/Scripts/Boundary/BoundaryCpuRules.cs`: deterministic loadout, interception and threat/retreat math.
- `Assets/Game/Scripts/GameRegistry/PracticeModePanel.cs`: runtime practice chooser.
- `Assets/Game/Scripts/Player/PlayerOutlinePresentation.cs`: persistent and Void-strength red outlines.
- `Assets/Game/Tests/Editor/BoundaryCpuTests.cs`: ten focused EditMode tests.
- `Assets/Game/Tests/Editor/PlayerPresentationBalanceTests.cs`: scale, force, target and audio balance checks.

Existing integration files:

- `GameManager.cs`, `MenuUIController.cs`, `MainMenu.cs`, `MenuButton.cs`: practice routing, round gate and result/rematch flow.
- `PlayerMovement.cs`, `PlayerInputReader.cs`, `Cam.cs`: CPU simulation and human-only controls/camera setup.
- `PlayerAbilities.cs`: shared validated handlers and active-attack sensing.
- `BoundaryPlayerState.cs`, `BoundaryHazard.cs`, `BoundaryMatchController.cs`: CPU damage, elimination, forces and phase/terrain information.
- `GrappleAbility.cs`, `TeleportAbility.cs`, `VoidAbility.cs`: traversal and effect parity.
- `DEPLOYMENT.md`: owner release instructions and feature release impact.

No existing scene, prefab or asset sidecar was edited. The pre-existing change to `tools/update-edgegap-image.mjs` was preserved and is outside this feature.

## Validation performed — 2026-09-19

1. **C# compilation passed** using the compiler bundled with Unity 6000.3.6f1 and the current editor/iOS `Assembly-CSharp.rsp`. New sources were appended and outputs redirected to `/tmp`. It completed with exit 0 and 11 existing obsolete-API warnings; no warning originated in the new feature code. The initial CPU baseline was also compiled previously with the then-available Linux dedicated-server response file, but the playtest follow-up has not received a current Linux build. These are compiler checks, **not** full Unity/IL2CPP builds or PurrNet postprocessing tests.
2. **All EditMode test sources compiled** against the newly compiled game assembly using existing Unity/NUnit references. The stale missing WebGL editor-module reference was omitted. Exit 0, no compiler output.
3. **Static network contract review passed** for the current candidate: existing RPC signatures/attributes remain in their prior order and the new Base RPCs are appended. Base adds ability ID `12`, so mixed-version runtime validation is still mandatory.
4. **Serialized reference checks passed**: Game's spawner resolves the existing Player prefab GUID and its movement, abilities, state, input and camera scripts. Four tagged authored spawn points exist. Existing scene/prefab/asset/GUID files are unchanged.
5. **`git diff --check` passed.**
6. Rosetta 2 was installed successfully with Apple's `softwareupdate` after owner approval. A focused batch Test Runner invocation then exited with code 1 before running tests because another Unity instance already had this project open: “Multiple Unity instances cannot open the same project.” It produced no test report. The open editor log showed no C# errors from these changes. Existing unrelated imported Piloto HDRP shaders still report missing HDRP include errors in this URP project.

   ```sh
   /Applications/Unity/Hub/Editor/6000.3.6f1/Unity.app/Contents/MacOS/Unity \
     -batchmode -nographics -projectPath /Users/alvinthomasnoble/Boundary \
     -runTests -testPlatform EditMode \
     -testFilter 'BoundaryCpuTests|BoundaryMathTests|VoidAbilityTests|HollowAbilityTests|BullseyeAbilityTests|ChargeAbilityTests|SliceAbilityTests' \
     -testResults /tmp/boundary-cpu-editmode.xml \
     -logFile /tmp/boundary-cpu-editmode.log
   ```

Not run: PlayMode, actual CPU matches, mobile performance, iOS export/IL2CPP, Xcode archive, Linux executable build, PurrNet postprocessing/runtime, two-client multiplayer, disconnect/reconnect/late join and scene-transition soak tests. CPU difficulty and navigation quality need actual playtesting; compilation does not establish how hard the opponent is to beat.

**2026-09-20 follow-up:** the complete runtime assembly and Editor test assembly compiled with Unity 6000.3.6f1's Roslyn compiler (exit 0). Runtime compilation reported 12 existing obsolete-API warnings and no errors; Editor test compilation produced no output. A new focused Test Runner attempt was again blocked by the Unity process holding `Temp/UnityLockfile`, so no new tests were executed. Static review found and fixed Base receiving zero CPU utility, plus Bullseye's rendered target being scaled twice under the 1.3× player root while authoritative scoring used a different center/radius. Focused regression tests were added for both fixes.

## Owner testing checklist

1. Open this project in Unity **6000.3.6f1** after resolving Rosetta. Let scripts import. Run `BoundaryCpuTests` and the existing Boundary/ability EditMode tests using Window → General → Test Runner. Confirm no new Console errors.
2. Start from **Boot**, open Practice. Confirm Playground / CPU / Back appear and the background menu cannot receive taps. Test mouse and device touch.
3. Choose Playground. Confirm one player, unchanged free play, the selected human skin/loadout, and no CPU.
4. Return to Menu, choose CPU. Confirm exactly two player objects, one owned by the local human and one unowned Beard CPU. Only your camera, AudioListener, controls and HUD should activate. Inspect the `[CPU]` log for three distinct enabled abilities.
5. Let the CPU approach and fight. Check dodging, grounded jumps, wall jumps, wall/ledge escape, ranged prediction, and retreat from hazards and narrowing rings. Continue through Outer, Middle and Inner phases and remain near it while each platform band falls.
6. Test each human ability against the CPU. BlackThrow and arena black holes must drain health; Hollow/Bullseye/Charge/Slice must damage it; Attract/Repel and Void must move it. Charge must retain caster self-damage. Void must be unavailable at equal/lower caster health and must apply its speed/immunity rules to the correct fighter. Teleport/Grapple/Dash must preserve normal traversal and cooldown behavior for either fighter. Repeat rounds until all CPU ability types are observed.
7. Win, press Play Again, then lose and press Play Again. Each should create exactly one fresh CPU match without a lobby code, waiting-for-other-player message or stale cooldowns/effects. Repeat several times. Back should reopen the practice chooser. The in-game exit button should still return to the normal play panel.
8. Verify practice does not change online wins/losses. Exit during a cast and during startup; confirm local host cleanup and working controls on re-entry.
9. Regression-test online play with two clients: one spawn per client, only local camera/input, all equipped effects once, one result, disconnect and rematch. Exercise a 1.12 build 20 client with the release candidate/current server before any production server replacement.

## Deployment impact

The CPU opponent itself remains local-client-only, but the playtest follow-up changes shared player scale/collision, Teleport validation, Charge speed, and BlackThrow force. Releasing this complete candidate therefore requires both the updated client and a matching Linux dedicated-server rebuild, immutable container image, and Edgegap application-version image update. Human multiplayer RPC payloads and replicated fields remain unchanged. No Firebase deployment is required by these changes.

Client-only automated compilation is not sufficient validation: local host gameplay and normal two-client regression testing remain necessary because shared player scripts changed. At release preparation, compare the entire candidate against the actually deployed client/server/backend revisions. Other unreleased changes may still require a Linux rebuild and new immutable image. No commits, pushes, image builds/pushes, Firebase deployments or Edgegap updates were performed for this feature.
