# Boundary AI Organizer

## Default workflow: single Project Engineer chat

Use one Codex chat as the default Project Engineer. This chat has permission to
organize the selected TaskBoard item, implement the approved task, run focused
validation, and record the result. It should not create separate Organizer,
Implementer, Tester, or Fixer handoffs unless the owner explicitly requests
them.

The owner still controls task selection and deployment:

1. The owner names one numbered TaskBoard item.
2. The Project Engineer reads its full entry, including `Specifics`.
3. The Project Engineer creates or updates the matching entry in
   `Docs/AI_TASKS.md`.
4. The Project Engineer implements the task in the same chat.
5. The Project Engineer runs the minimum useful focused validation.
6. The Project Engineer records the implementation, testing, risks, and server
   assessment in `Docs/AI_TASKS.md` and `Docs/AI_TEST_LOG.md`.
7. The owner decides whether to approve any deployment or move to the next task.

Do not repeat broad project discovery when the shared files already contain the
needed context. Read only the relevant systems and files for the selected task.

## Role

The Organizer plans and tracks work. It does not edit gameplay code, scenes,
prefabs, project settings, or production services.

## Required reading

Before planning or reviewing a task, read:

- `AGENTS.md`
- `README.md`
- `DEPLOYMENT.md` when the task involves networking, Firebase, Edgegap, or release work
- `Docs/TaskBoard.md`
- `Docs/AI_TASKS.md`
- `Docs/AI_TEST_LOG.md` when reviewing completed or tested work

## Project map

### Boundary gameplay

Purpose: shrinking arena, hazards, disasters, match phases, and player survival.

Primary files:

- `Assets/Scripts/Boundary/BoundaryMatchController.cs`
- `Assets/Scripts/Boundary/BoundaryPlayerState.cs`
- `Assets/Scripts/Boundary/BoundaryHazard.cs`
- `Assets/Scripts/Boundary/BoundaryHUD.cs`
- `Assets/Scripts/Boundary/BoundaryRuntimeBootstrap.cs`
- `Assets/Scripts/Boundary/BoundaryArenaPresentation.cs`

### Player and movement

Primary files:

- `Assets/Scripts/Player/PlayerMovement.cs`
- `Assets/Scripts/Player/PlayerInputReader.cs`
- `Assets/Scripts/Player/Cam.cs`
- `Assets/Scripts/Player/CameraLookTouch.cs`
- `Assets/Scripts/Player/MobileLook.cs`

Risks: remote players receiving local input, mobile/editor input differences,
and Unity lifecycle or ownership timing.

### Abilities and loadouts

Primary files:

- `Assets/Scripts/Abilities/`
- `Assets/Scripts/AbilitiesRegistry/`

Risks: client-authority exploits, duplicate effects or damage, cooldown
desynchronization, and RPC or serialized-field incompatibility.

### Backend and deployment

Primary files:

- `functions/`
- `firestore.rules`
- `firebase.json`
- `Assets/Scripts/GameRegistry/FirebaseManager.cs`
- `Assets/Scripts/GameRegistry/EdgegapServerLifecycle.cs`

Risks: released-client compatibility, duplicate match results, production
deployment mistakes, and exposed credentials.

## Architecture decisions

- PurrNet handles networking.
- Firebase handles authentication, player records, purchases, and match results.
- Edgegap hosts dedicated servers.
- The game supports two players.
- Competitive outcomes must be server-authoritative.
- EditMode tests are located in `Assets/Tests/Editor/`.

## Organizer rules

- Break requests into the smallest independently implementable tasks.
- When given a TaskBoard ID, find the exact numbered item in
  `Docs/TaskBoard.md` and read the entire item, including any nested notes,
  requirements, dependencies, and `Specifics` section, before planning.
- If the selected TaskBoard item has a `Specifics` section, copy its meaning into
  the AI task's requested outcome, scope, and acceptance criteria. Do not omit,
  replace, or reinterpret those specifics without explaining the conflict.
- If no `Specifics` section exists, record `Specifics: None provided` and use the
  task title and notes as the starting scope.
- If the specifics are ambiguous, contradictory, or incomplete, create the task
  as `BLOCKED` or ask the project owner for clarification instead of guessing.
- Each task must define an outcome, scope, non-goals, likely files, risks, and
  acceptance criteria.
- Each task must include a `Server and Edgegap impact` section stating whether
  the change requires a dedicated Linux server rebuild, a new Edgegap image,
  an Edgegap application/version update, Firebase deployment, or client-only
  testing. If uncertain, mark it `Needs implementer assessment`.
- Write tasks to `Docs/AI_TASKS.md` with status `PENDING`.
- Use the numbered TaskBoard ID as the only task identifier everywhere, such as
  `1.2.07.00`. Do not create a second internal task ID.
- When creating a test-log entry or bug entry, include the same TaskBoard ID in
  the heading and never invent a separate task number.
- Never mark a task `APPROVED`; that decision belongs to the project owner.
- Keep testing frugal. For every organized task, recommend a minimum testing
  budget before implementation. The recommendation must state the number of
  planned Tester passes and what each pass covers.
- Default to one Tester pass for a small, isolated change. Recommend two passes
  only when the first pass may produce a confirmed bug that requires a fix and
  re-test, or when the task affects a meaningful multiplayer/runtime path.
- Recommend more than two passes only when the task is high-risk and explain why.
- Do not test unrelated systems or repeat a passing test without a concrete
  regression reason.
- Do not start implementation automatically.
- Update this file only when architecture, file ownership, or a durable system
  decision changes.
- Keep this file compact and link to detailed documentation instead of duplicating it.

## Fixer workflow

- Small fixes may be handled by the Fixer role only after a concrete bug or
  review finding has been recorded in `Docs/AI_TEST_LOG.md` or `Docs/AI_TASKS.md`.
- The Fixer must not expand the original task or perform unrelated cleanup.
- The Fixer records files changed, validation results, and remaining risks in the
  related task entry in `Docs/AI_TASKS.md`.
- The Organizer reviews the Fixer report and decides whether the task returns to
  `TESTING`, becomes `DONE`, or remains `BLOCKED`.
- The Fixer does not modify this file or change acceptance criteria.
