# Production deployment and recovery

This document records the known-good multiplayer deployment and the checks
required before changing production.

## Known-good baseline

- Unity build scenes, in order: `Boot`, `Menu`, `Game`
- PurrNet tick rate: 80 Hz
- Dedicated server internal transport: UDP `7777`
- Firebase project: `entropy-7c113`
- Cloud Functions region: `us-central1`
- Edgegap application: `entropy`
- Edgegap version referenced by `functions/index.js`: `v21`
- Maximum deployment lifetime: 10 minutes
- Lobby size: two players
- Privacy page: <https://entropy-7c113.web.app/privacy>
- Support page: <https://entropy-7c113.web.app/support>

The authoritative source snapshot is the Git tag `multiplayer-working-v1`.

## Required release record

Record these values in every release/tag description:

- Git commit and tag
- Unity editor version
- iOS version and build number
- Edgegap immutable container/image version
- Firebase Functions revision
- Firestore rules revision
- Network protocol compatibility notes

## Multiplayer release checklist

- [ ] Create a branch; do not edit production directly on `main`.
- [ ] Confirm no secrets or signing credentials are staged.
- [ ] Build the Unity client successfully.
- [ ] Build the Unity dedicated server successfully.
- [ ] Host creates a four-digit lobby.
- [ ] Edgegap reaches `ready` and publishes the external UDP port.
- [ ] A second physical client joins using the code.
- [ ] Both clients enter `Game` and spawn exactly one owned player.
- [ ] The round stays paused until both players are present.
- [ ] Movement, jump, camera, and every equipped ability replicate correctly.
- [ ] Network projectiles appear once and behave consistently for both players.
- [ ] Skin selection appears correctly to both players.
- [ ] A loss produces exactly one loss and one win in Firebase.
- [ ] Both players return to the menu cleanly.
- [ ] Both players can accept and complete a rematch.
- [ ] The Edgegap deployment terminates after the rematch/expiry window.
- [ ] The currently released App Store client remains compatible.
- [ ] Tag the verified release before deploying production.

## Firebase deployment commands

Run from the repository root and deploy the smallest possible target:

```sh
firebase deploy --only hosting --project entropy-7c113
firebase deploy --only firestore:rules --project entropy-7c113
firebase deploy --only functions --project entropy-7c113
```

Do not deploy all targets when only one changed.

## Updating the Edgegap image

Only run these commands after the Unity dedicated-server build, container image
build/publish, release checks, and explicit owner approval are complete.

The repository helper updates the configured Edgegap application version to use
an already-published image tag. It does not build or publish the image:

```sh
node tools/update-edgegap-image.mjs --firebase-secret entropy v21 <image-tag>
```

The helper reads the `EDGEGAP_API_TOKEN` Firebase secret and uses the local
Docker credential helper when available. Do not print, commit, or paste the
token. Record the image tag and successful Edgegap response in the release
record. If the image has not already been published to the configured registry,
stop before running this command.

## Rollback

1. Identify the last verified tag.
2. Compare the failing deployment with that tag before changing production.
3. Redeploy only the affected Firebase target from the verified source.
4. Point Edgegap automation back to the previous immutable server version.
5. Confirm that the live App Store client can create, join, play, finish, and
   rematch before declaring recovery complete.

Never delete the previous Edgegap image until the next release has been stable
in production.

## Work still required for stronger reliability

- Create separate staging Firebase and Edgegap environments.
- Add an explicit network protocol version to lobby and server handshakes.
- Add automated Unity client/server builds in CI.
- Add Firestore rules tests and backend tests.
- Add alerts for stuck deployments, connection failures, and cleanup failures.

These changes intentionally are not part of the known-good baseline because
they can affect working multiplayer and must be introduced and tested
separately.

## Owner release workflow — next client 1.13 (21)

Owner-confirmed public release: **1.12, build 20**. Planned next release:
**1.13, build 21**. Before the next iOS export, update both Unity Player Settings
and `Assets/Game/Editor/IosBuildPostprocessor.cs`. At the CPU implementation
checkpoint, the postprocessor still forces **1.12 (18)**; do not rely on manually
changing only the Xcode UI because the next Unity export overwrites it. The
version/build changes remain pending release preparation.

The owner's message **`deploymen`** requests a concrete, complete release plan:
review all changes since the actual public release, identify client/server/backend
requirements, specify Unity iOS and (if needed) Linux server builds, Docker
build/publish steps, Edgegap image selection, Xcode signing/archive/upload,
App Store Connect submission, compatibility checks, and rollback records.
Do not infer the live image or Functions revision from source constants alone.

The subsequent message **`deploy now`** authorizes the agreed deployment and Git
commit/push work. Complete the applicable release checks above first. For an
Edgegap replacement, use `tools/update-edgegap-image.mjs` only after the immutable
image is built and published, and record the actual application/version/tag and
result. Never publish an untested replacement merely because a build compiled.

The local CPU practice feature's implementation, validation limitations and
release impact are recorded in [Docs/CPU_PRACTICE.md](Docs/CPU_PRACTICE.md).
The CPU-only path does not run on Edgegap, but its current playtest follow-up
also changes shared player physics, Teleport validation, Charge speed and
BlackThrow force. The complete 1.13 candidate therefore requires a matching
Linux dedicated-server rebuild, immutable container publish and Edgegap image
update. No Firebase target is changed by this feature set. Reassess the complete
release diff when the owner requests deployment preparation.
