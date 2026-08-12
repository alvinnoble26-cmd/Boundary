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
