# Boundary Project Rules

## Project

- This is a Unity 6.0.3f1 multiplayer game.
- The exact Unity Editor version is recorded in `ProjectSettings/ProjectVersion.txt`;
  treat that file as the source of truth.
- Boundary is a two-player arena survival game built around a shrinking boundary,
  hazards, disasters, networked abilities, and mobile controls.
- The main scenes load in this order: `Boot` → `Menu` → `Game`.
- Read `README.md` and `DEPLOYMENT.md` before making multiplayer, Firebase,
  Edgegap, or release-related changes.

## Scope and safety

- Make the smallest complete change that satisfies the request.
- Inspect existing code, scenes, prefabs, documentation, and patterns before editing.
- Before editing, inspect `git status` and preserve all existing uncommitted changes.
- Do not modify unrelated files.
- Do not overwrite, revert, or clean up existing user changes.
- Do not delete assets, scenes, prefabs, packages, or project settings without
  explicit approval.
- Do not introduce new packages or third-party assets unless explicitly approved.
- Prefer a focused diff over a rewrite or broad refactor.
- Ask for approval before changing public networking interfaces, save data,
  Firebase data models, production configuration, or release settings.

## Unity conventions

- Preserve serialized field names and prefab compatibility.
- Treat scene and prefab references as part of the runtime contract.
- Prefer explicit serialized references over `Find`, `GameObject.Find`,
  `FindObjectOfType`, or repeated scene searches.
- Do not add per-frame allocations or expensive searches to gameplay loops.
- Avoid putting gameplay authority in UI scripts.
- Keep reusable gameplay rules in testable C# methods where practical.
- Match the existing component, naming, formatting, and folder conventions.
- Be careful with Unity lifecycle methods, especially network spawn, ownership,
  scene loading, and object destruction.

## Unity asset integrity

- Preserve existing `.meta` files and their GUIDs.
- Do not delete or manually alter existing `.meta` files unless explicitly approved.
- When moving or renaming Unity assets, use Unity-aware workflows where possible.
- Inspect scene and prefab references after asset changes.
- Do not hand-edit serialized scene, prefab, or asset YAML unless necessary for
  the requested task and reviewed carefully.
- Do not modify generated folders such as `Library/`, `Temp/`, `Logs/`, `Obj/`,
  `Build/`, or other ignored build artifacts.
- Do not commit generated build output, caches, local IDE files, or device logs.

## Networking

- PurrNet is the networking framework. Reuse its established patterns.
- Clearly distinguish server-authoritative behavior, owner-client behavior,
  local presentation, and observer synchronization.
- Never trust client-provided damage, win/loss, purchase, or match-result data.
- Networked abilities must preserve cooldown, ownership, validation, and
  replication behavior.
- Avoid changing networked serialized fields or RPC signatures without checking
  compatibility with existing clients and servers.
- Test for duplicate spawns, duplicate effects, duplicate damage, late ownership,
  disconnects, rematches, and scene transitions when relevant.
- Preserve the two-player match assumptions unless the task explicitly changes them.

## Boundary gameplay

- Reuse the existing Boundary systems before creating new ones:
  `BoundaryMatchController`, `BoundaryPlayerState`, `BoundaryHazard`,
  `BoundaryHUD`, `BoundaryRuntimeBootstrap`, and `BoundaryArenaPresentation`.
- Preserve deterministic math and stable seeded variation where existing code uses it.
- Add or update focused tests for the behavior changed by the task. For boundary
  work, consider phase transitions, hazards, disasters, movement forces, lethal
  contacts, and arena generation rules as applicable.
- Do not silently change gameplay constants, disaster pools, arena population, or
  match flow without documenting the intended player-facing effect.

## Abilities and player systems

- Reuse the existing ability registry, loadout, cooldown, and networked-ability
  interfaces.
- Preserve the three-slot loadout behavior unless explicitly changing it.
- Keep local camera, touch input, movement, and ability presentation separate from
  server-authoritative gameplay.
- Check both desktop/editor input and mobile touch behavior when relevant.
- Verify that local-player-only camera and UI setup does not run for remote players.

## Backend and production services

- Firebase and Edgegap are production systems. Treat changes as high risk.
- Never commit credentials, signing files, service-account JSON, tokens, private
  keys, or local environment files.
- Never hard-code new secrets into C#, JavaScript, Firebase rules, or project assets.
- Deploy the smallest affected Firebase target only.
- Do not deploy production or replace an Edgegap server image without explicit
  approval and completion of the checklist in `DEPLOYMENT.md`.
- Every implementation report must state whether the change requires a Linux
  dedicated-server rebuild, a new container image, an Edgegap application/version
  update, Firebase deployment, or client-only validation.
- Do not run Edgegap image-update commands automatically. With explicit owner
  approval, use the repository helper `tools/update-edgegap-image.mjs` and record
  the application, version, image tag, and result. The helper updates which image
  an Edgegap version uses; it does not build or publish the Docker image itself.
- Preserve compatibility with the currently released App Store client unless the
  task explicitly includes a version migration.
- Before changing authentication, matchmaking, Firestore documents, Cloud
  Functions, RPC payloads, ability identifiers, enum values, serialized data, or
  API contracts, document compatibility with the released client.
- Prefer additive, backward-compatible changes. Do not remove or rename fields,
  routes, RPCs, or data values used by released clients without an explicit
  migration and rollback plan.

## Execution discipline

- Default to one Project Engineer chat that organizes, implements, validates,
  and reports on the selected task. Do not create separate role handoffs unless
  explicitly requested.
- Optimize for implementation efficiency: read the relevant task and systems,
  make the smallest complete change, and run only the minimum useful focused
  validation. Do not repeat broad discovery or unrelated tests.

- For a small, well-scoped task, inspect the relevant files and implement directly.
- For cross-system, networking, backend, or high-risk work, first provide a concise
  plan containing affected files, risks, validation steps, and decisions requiring
  approval.
- Do not add speculative abstractions, generic frameworks, new managers, service
  locators, or future-proofing layers unless explicitly requested.
- Prefer existing patterns over introducing new architecture.
- Do not perform unrelated cleanup, renames, formatting-only edits, or
  documentation rewrites during feature work.
- Stop and ask when an ambiguous decision would change player-facing behavior,
  networking authority, compatibility, data contracts, production settings, or scope.

## Multiplayer validation

For changes affecting networked objects, abilities, match state, damage, movement,
spawning, or scene transitions, verify when feasible:

- Host and joining client see consistent state.
- Owner and non-owner behavior are correct.
- Server-side validation rejects invalid client actions.
- Remote players do not initialize local-only camera, input, HUD, audio, or UI.
- Reconnect, disconnect, rematch, late join, and scene-transition behavior is
  considered or explicitly marked as untested.
- RPCs and replicated state do not create duplicate effects, damage, spawns,
  rewards, or match-end events.
- Network traffic is not added every frame unless explicitly required.

## Testing and validation

- Never claim a test passed unless it was actually run.
- Use the Unity Test Runner for EditMode and PlayMode tests.
- Use EditMode tests for pure rules and deterministic calculations.
- Use PlayMode or multiplayer tests for scene behavior, spawning, ownership,
  collisions, UI hookups, save/load, and match flow when feasible.
- Run the narrowest relevant checks first, then broader checks when risk warrants it.
- If Unity, a device, credentials, or a service is unavailable, state exactly what
  could not be run and provide the shortest manual verification procedure.
- Do not introduce new Unity Console errors or warnings caused by the change.
- For meaningful changes, report:
  - files changed
  - exact tests, builds, or checks run
  - results
  - known limitations and remaining risks
  - manual Unity Editor test steps
- When working from `Docs/AI_TASKS.md`, always append the required Implementer or
  Fixer report to the task entry before finishing. Include both the internal AI
  Task ID and the original TaskBoard ID, then set an implemented task to
  `TESTING` automatically. Do not wait for the owner to repeat this instruction.
- For multiplayer changes, include a two-client manual test when possible.

## Definition of done

A task is complete only when all applicable requirements are met:

- The requested player-facing behavior is implemented.
- Relevant automated checks were run when supported.
- Existing behavior likely to regress was checked.
- Serialized references, prefab references, scene references, and network object
  configuration were inspected when relevant.
- The change is limited to the approved task scope.
- The final report includes changed files, exact validation results, manual test
  steps, known limitations, and remaining risks.

## Documentation

- Keep durable project rules here, not temporary task plans.
- For game rules, read or update the relevant game-design documentation when it exists.
- For networking changes, document important protocol or authority assumptions.
- For release work, read `DEPLOYMENT.md` and keep detailed deployment procedures there.
- Do not put passwords, tokens, keys, or other secrets in documentation.

## Git and release discipline

- Keep proposed commits small and focused.
- Do not create commits, push branches, open pull requests, tag releases, deploy,
  or modify remote repositories unless explicitly asked.
- Use a separate branch for multiplayer, backend, deployment, or production work.
- Do not edit production directly on `main`.
- Preserve the known-good baseline identified in `DEPLOYMENT.md`.
- Do not remove old deployment images or rollback references without approval.
