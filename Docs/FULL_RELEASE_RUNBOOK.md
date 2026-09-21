# Entropy Zero Full Release Runbook

This is the reusable, end-to-end release procedure for Entropy Zero. It covers
source control, Unity validation, the Linux dedicated server, Docker and
Edgegap, the iOS/Xcode pipeline, and upload to App Store Connect.

Use this runbook with `Docs/FULL_RELEASE_PROMPT.md`. The prompt grants Codex the
normal release authority requested by the owner; this runbook supplies the
technical detail. Neither file contains credentials, tokens, passwords,
provisioning profiles, or authorization codes.

## Stable project facts

- Unity source of truth: `ProjectSettings/ProjectVersion.txt`
- Current Unity editor used successfully: `6000.3.6f1`
- Scenes: `Boot` -> `Menu` -> `Game`
- Canonical preproduction/test branch: `test-2`
- Production branch: `main` (never push a release directly to `main`)
- Firebase project: `entropy-7c113`
- Firebase Functions region: `us-central1`
- Edgegap application/version used by the backend: `entropy` / `v21`
- Edgegap registry image: `entropy-kp55lgz54hud/entropy-server`
- Dedicated-server port: UDP `7777`
- iOS bundle identifier: `com.alvin.entropy`
- Apple development team: `WXG9SG3PA2`
- Public version before the 2026-09-21 release: `1.12` build `20`
- 2026-09-21 candidate uploaded to App Store Connect: `1.13` build `21`

Build numbers are permanently consumed when App Store Connect accepts an
upload. Every later release must use a build number greater than `21`.

## Owner authorization and autonomy

For a task started with the reusable prompt, Codex is authorized to complete
the release without asking the owner to perform routine steps. This includes:

- updating and hardcoding the requested version/build;
- running Unity tests and builds;
- installing the matching Unity iOS/Linux build-support modules if absent;
- using the existing Firebase, Google, Edgegap, Apple, Xcode, Docker, and GitHub
  sessions on the machine;
- completing normal OAuth/login screens with the existing saved account;
- reading the Firebase `EDGEGAP_API_TOKEN` secret without printing it;
- building and publishing a new immutable Edgegap server image;
- updating `entropy/v21`, creating a smoke deployment, inspecting it, and
  stopping that deployment;
- archiving, signing, exporting, validating, and uploading iOS to App Store
  Connect;
- committing the complete tested candidate, fast-forwarding and pushing
  `test-2`, and pushing the annotated release tag.

Do not ask the owner to copy Firebase authorization codes, retrieve Edgegap
credentials, operate Xcode, or upload the IPA when the existing authenticated
sessions can do it. Use browser/UI automation for service login and dashboards
when no CLI/API route is available. The only acceptable owner handoff is a
provider-enforced obstacle that automation is not allowed or able to complete,
such as MFA, CAPTCHA, a new legal agreement, a missing paid entitlement, or an
expired account requiring the owner’s password. Explain that exact blocker and
resume immediately after it is cleared.

This authorization does not permit storing or exposing credentials, pushing to
`main`, releasing an App Store version to customers, submitting for App Review,
or deleting production resources. Those remain separate owner decisions.

## Release variables

Resolve these at the start and use them consistently:

```text
MARKETING_VERSION=<for example 1.14>
BUILD_NUMBER=<integer greater than the last accepted App Store build>
RELEASE_ID=<MARKETING_VERSION>-<BUILD_NUMBER>
RELEASE_DIR=Builds/Release-<RELEASE_ID>
SERVER_TAG=release-<MARKETING_VERSION>-build<BUILD_NUMBER>-<YYYY-MM-DD>
GIT_TAG=release-<MARKETING_VERSION>-build<BUILD_NUMBER>
TEST_BRANCH=test-2
EDGEGAP_APP=entropy
EDGEGAP_VERSION=v21
FIREBASE_PROJECT=entropy-7c113
APPLE_TEAM=WXG9SG3PA2
```

Never use `latest` or overwrite an existing container tag. Confirm the chosen
App Store build number has not already been uploaded before building.

## Phase 1: inspect and freeze the candidate

1. Read `AGENTS.md`, `README.md`, `DEPLOYMENT.md`, this runbook, and relevant
   feature documentation.
2. Record `git status --short`, current branch, HEAD, remotes, existing tags,
   and the diff from `origin/test-2`.
3. Preserve all pre-existing changes. Do not reset, clean, or overwrite the
   owner’s work.
4. Confirm the candidate is descended from `origin/test-2`:

   ```sh
   git fetch origin
   git merge-base --is-ancestor origin/test-2 HEAD
   ```

5. Review all tracked and untracked candidate files for generated output,
   credentials, service-account JSON, signing material, tokens, `.env` files,
   and unrelated local files. Builds remain ignored and must not be committed.
6. Close Unity before editing release settings. A running editor can retain old
   serialized values and write them back while closing.

Do not silently discard a dirty tree. The release artifacts must be built from
the exact source later committed to `test-2` and tagged.

## Phase 2: hardcode release identity

Update all three release identity locations before building:

1. `ProjectSettings/ProjectSettings.asset`
   - `bundleVersion`
   - `buildNumber.iPhone`
   - `applicationIdentifier.iPhone: com.alvin.entropy`
   - `appleDeveloperTeamID: WXG9SG3PA2`
   - `appleEnableAutomaticSigning: 1`
2. `Assets/Game/Editor/IosBuildPostprocessor.cs`
   - marketing version constant
   - build number constant
3. `Assets/Editor/ReleaseBuilder.cs`
   - marketing version constant
   - build number constant
   - release output directory

The postprocessor must write both `CFBundleShortVersionString` and
`CFBundleVersion` into the generated `Info.plist` and set
`MARKETING_VERSION`/`CURRENT_PROJECT_VERSION` in the generated Xcode project.
Do not rely on manual Xcode edits: the next Unity export overwrites them.

After Unity closes and again after every Unity batch run, grep these values and
confirm they have not reverted.

## Phase 3: authenticate tooling without exposing secrets

### Firebase

Check the current login first:

```sh
pnpm dlx firebase-tools login:list
```

If authentication expired, run `pnpm dlx firebase-tools login --reauth` and
complete the generated Google/Firebase flow using browser automation and the
existing owner account. Do not ask the owner to copy the code unless the
provider has made automation impossible. Never print the retrieved secret.

Read the Edgegap API token only inside the process that consumes it:

```sh
pnpm dlx firebase-tools functions:secrets:access EDGEGAP_API_TOKEN \
  --project entropy-7c113
```

Pipe or capture it in memory. Never paste it into chat, shell history,
documentation, Git, or a file.

### Edgegap

The Edgegap dashboard supports Google sign-in using the existing owner account.
The API token is the Firebase secret above. The registry push credential is
different from the pull credential stored on an application version.

For image pushes, obtain the `client-push` username/token from:

`Edgegap dashboard -> Tools -> Container Registry -> Credentials`

Pass the token to `docker login registry.edgegap.com --password-stdin`, push the
image, then `docker logout registry.edgegap.com`. Do not display or persist the
credential. A username containing `app-version-pull` is pull-only and cannot
push.

### Apple/Xcode

Verify Xcode has the Apple account and team `WXG9SG3PA2`. If the account has
expired, use Xcode Settings -> Accounts and the saved owner account. Automatic
signing may download/create development or cloud-managed distribution assets.
Do not export signing credentials from the keychain or commit them.

## Phase 4: Unity validation

Install Unity Hub modules matching the exact editor if necessary:

- iOS Build Support
- Linux Dedicated Server/Build Support (Mono)

Run the full EditMode suite with the exact Unity editor. Use a results XML and a
log under `/tmp`, and verify the process exit code and XML totals. Do not claim
success from the log alone.

Example:

```sh
/Applications/Unity/Hub/Editor/6000.3.6f1/Unity.app/Contents/MacOS/Unity \
  -batchmode -nographics -projectPath "$PWD" \
  -runTests -testPlatform EditMode \
  -testResults /tmp/boundary-editmode-<RELEASE_ID>.xml \
  -logFile /tmp/boundary-editmode-<RELEASE_ID>.log
```

When tests fail, compare the asserted constants with the runtime code and Git
history before changing gameplay. During 1.13, seven failures were stale tests,
not runtime regressions. The intentional values were arena population
`17/13/60`, Slice radius `10`, Slice swing `0.2`, darker platform colors,
Bullseye range `570`, and fracture lifetime `3.35`. The tests and editor
validator were brought in line with the already-intended runtime values. Never
change production gameplay merely to satisfy a stale assertion.

Minimum automated gate: all EditMode tests pass. For networking changes, also
perform the two-client checklist when devices/clients are available. If a
physical two-client test is impossible, say so; do not convert a server-ready
smoke test into a claim that multiplayer gameplay was fully tested.

## Phase 5: clean Unity builds

Build from `ReleaseBuilder` so scene order and release identity are validated.

Linux dedicated server:

```sh
/Applications/Unity/Hub/Editor/6000.3.6f1/Unity.app/Contents/MacOS/Unity \
  -batchmode -nographics -quit -projectPath "$PWD" \
  -executeMethod ReleaseBuilder.BuildLinuxServer \
  -logFile /tmp/boundary-linux-<RELEASE_ID>.log
```

iOS export:

```sh
/Applications/Unity/Hub/Editor/6000.3.6f1/Unity.app/Contents/MacOS/Unity \
  -batchmode -nographics -quit -projectPath "$PWD" \
  -executeMethod ReleaseBuilder.BuildIos \
  -logFile /tmp/boundary-ios-<RELEASE_ID>.log
```

Verify:

- Linux executable is an x86-64 ELF and includes its data directory and
  `UnityPlayer.so`.
- iOS export contains the Xcode workspace/project.
- generated `Info.plist` and build settings contain the exact bundle ID,
  marketing version, build number, and team.
- Unity logs end in successful build summaries with zero build errors.

Unity may auto-add `com.unity.toolchain.macos-arm64-linux`, reorder
`Packages/manifest.json`, update `Packages/packages-lock.json`, or disable
`ProjectSettings/UnityConnectSettings.asset`. These occurred during 1.13 and
were not approved product changes. Inspect the post-build diff and restore only
such generated changes with a careful patch unless the release explicitly
needs to retain them. Do not use destructive Git reset/checkout commands.

## Phase 6: Linux container and registry

Build the exact server output for `linux/amd64`:

```sh
docker buildx build \
  --platform linux/amd64 \
  --file Builds/Dockerfile \
  --build-arg SERVER_BUILD_PATH=Builds/Release-<RELEASE_ID>/EdgegapServer \
  --tag entropy-server-local:<RELEASE_ID>-candidate \
  --load .
```

Inspect the image OS, architecture, size, and digest. Tag and push it as:

```text
registry.edgegap.com/entropy-kp55lgz54hud/entropy-server:<SERVER_TAG>
```

Use the dashboard `client-push` registry credential as described above. Record
the pushed digest and verify the Edgegap repository shows the new artifact/tag.

On Apple Silicon, running this `linux/amd64` Unity/Mono server under Docker’s
QEMU emulation crashed in Mono initialization with an x86 code-generation
assertion. That is not a valid native server failure signal. Do not weaken or
rebuild gameplay around it. The authoritative smoke test is a native Edgegap
deployment reaching Ready and exposing UDP 7777 through an assigned external
port.

## Phase 7: Git test branch and release tag

Do this after tests/builds pass and the immutable image is published, but before
switching the live Edgegap version.

1. Run `git status`, review every file, and confirm ignored build outputs remain
   unstaged.
2. Run a credential/secret scan over staged text without printing secret values.
3. Stage the complete source candidate, including the runbook/prompt and Unity
   `.meta` files.
4. Inspect `git diff --cached --name-status`, `--stat`, and `--check`.
5. Do not mass-edit Unity scene YAML to remove unrelated whitespace. During
   1.13, `Menu.unity` had pre-existing blank serialized names reported as
   trailing whitespace.
6. Commit with a focused release message, for example:

   ```sh
   git commit -m "Prepare Entropy Zero <MARKETING_VERSION> build <BUILD_NUMBER>"
   ```

7. Verify the remote test branch is an ancestor, then push the exact candidate
   commit to `test-2`:

   ```sh
   git fetch origin
   git merge-base --is-ancestor origin/test-2 HEAD
   git push origin HEAD:test-2
   ```

8. Move the local `test-2` pointer to the pushed commit if it is not checked out.
9. Create an annotated tag containing the required release record, then push it:

   ```sh
   git tag -a <GIT_TAG> -m "Entropy Zero <MARKETING_VERSION> (<BUILD_NUMBER>)"
   git push origin <GIT_TAG>
   ```

The annotated message/release report must record the commit, tag, Unity version,
iOS version/build, server image/tag/digest, Edgegap app/version, Firebase
revision status, Firestore rules status, and compatibility/validation notes.
Do not push directly to `main`.

## Phase 8: switch and smoke-test Edgegap

Before changing `entropy/v21`, record its complete safe configuration and old
image tag for rollback. Confirm the new tag exists in the registry.

Use the repository helper only after the preceding gates:

```sh
node tools/update-edgegap-image.mjs --firebase-secret \
  entropy v21 <SERVER_TAG>
```

The helper reads the API token from Firebase and requests image validation. It
must not print the token. Verify with a fresh Edgegap API read that
`entropy/v21` now names the new tag while preserving UDP 7777, TTL, resources,
and all unrelated configuration.

Create a temporary deployment through Edgegap’s API using `entropy/v21` and one
fallback geo-coordinate user. Poll `/v1/status/<request_id>` until it reaches
Ready. Verify:

- the reported FQDN is present;
- `gameport` maps internal UDP 7777 to a nonzero external port;
- the container remains alive long enough for initialization;
- logs contain no new server crash or startup error;
- exactly one deployment was created.

Stop that temporary deployment through Edgegap immediately after the smoke
check and verify it reaches stopped/terminated. This is a smoke test, not a
substitute for host/join testing on two clients.

If the new deployment cannot reach Ready, immediately point `entropy/v21` back
to the recorded previous immutable tag and verify a rollback deployment.

## Phase 9: Xcode archive, signing, and App Store Connect

Resolve Swift packages into an isolated cache so stale package state does not
pollute the project:

```sh
xcodebuild -resolvePackageDependencies \
  -workspace Builds/Release-<RELEASE_ID>/iOSClient/Unity-iPhone.xcworkspace \
  -scheme Unity-iPhone \
  -clonedSourcePackagesDirPath /tmp/boundary-xcode-packages-<RELEASE_ID>
```

Archive using automatic signing and the explicit team:

```sh
xcodebuild \
  -workspace Builds/Release-<RELEASE_ID>/iOSClient/Unity-iPhone.xcworkspace \
  -scheme Unity-iPhone -configuration Release \
  -destination 'generic/platform=iOS' \
  -archivePath Builds/Release-<RELEASE_ID>/EntropyZero-<RELEASE_ID>.xcarchive \
  -clonedSourcePackagesDirPath /tmp/boundary-xcode-packages-<RELEASE_ID> \
  DEVELOPMENT_TEAM=WXG9SG3PA2 CODE_SIGN_STYLE=Automatic \
  -allowProvisioningUpdates archive
```

Generate an ignored export-options plist with:

```text
method = app-store-connect
destination = export       # local IPA validation pass
signingStyle = automatic
teamID = WXG9SG3PA2
manageAppVersionAndBuildNumber = false
uploadSymbols = true
stripSwiftSymbols = true
```

Export locally first. Inspect the IPA’s embedded `Info.plist`, provisioning
profile, entitlements, signing authorities, team identifier, bundle ID,
marketing version, and build number. Run strict `codesign` verification.

Only after the IPA passes, use a second plist with `destination = upload`:

```sh
xcodebuild -exportArchive \
  -archivePath Builds/Release-<RELEASE_ID>/EntropyZero-<RELEASE_ID>.xcarchive \
  -exportPath Builds/Release-<RELEASE_ID>/AppStoreUpload \
  -exportOptionsPlist Builds/Release-<RELEASE_ID>/UploadOptions.plist \
  -allowProvisioningUpdates
```

Success requires `Upload succeeded`, `Uploaded Unity-iPhone`, and
`** EXPORT SUCCEEDED **`. App Store Connect will then process the build. Upload
does not authorize submitting for App Review or releasing to customers.

During 1.13, the first archive failed because no Apple team/account was set.
The account was added in Xcode, the team was passed explicitly, and the verified
team/automatic-signing settings were persisted in Unity. Check this before a
long archive rather than discovering it at the end.

## Phase 10: Firebase scope

Do not deploy Firebase merely because Edgegap uses a Firebase-held secret.
Compare the candidate with the released baseline for changes under `functions/`,
Firebase Hosting, and Firestore rules. Deploy only the smallest changed target:

```sh
firebase deploy --only functions --project entropy-7c113
firebase deploy --only firestore:rules --project entropy-7c113
firebase deploy --only hosting --project entropy-7c113
```

The 1.13 release had no Firebase target changes and required no Firebase
deployment. Record “unchanged/not deployed” for Functions, rules, and hosting.

## Problems encountered during the 1.13 deployment

Treat this list as regression knowledge:

1. Unity was initially open, causing project-lock risk and later rewriting
   version settings. Close it before release edits and batch jobs.
2. The postprocessor still forced `1.12 (18)` while the public client was
   `1.12 (20)`. Update every version source, not only Xcode.
3. Seven EditMode failures were stale expectations. Runtime code and Git history
   proved the gameplay values were intentional; tests were corrected instead.
4. The Linux module/build inserted a macOS ARM Linux toolchain package and
   changed Unity Connect state. Those unrelated generated diffs were removed.
5. The first local `linux/amd64` container smoke test crashed inside Mono under
   Apple-Silicon emulation. Native Edgegap is the meaningful runtime test.
6. Firebase CLI was not logged in. The browser/device authorization flow was
   completed and the secret was thereafter read in-process only.
7. The application-version `private_username/private_token` was a pull profile,
   not a registry push credential; Docker returned 401. The working push
   credential came from Edgegap Tools -> Container Registry and was removed
   from Docker after the push.
8. A container-registry tag API lookup using the full slash-qualified image name
   returned 404. Docker’s successful digest plus the dashboard artifact-count
   increase provided the verification signal.
9. The first Xcode archive lacked a development team. Explicit automatic signing
   with `WXG9SG3PA2` fixed it.
10. Swift/Firebase iOS packages were resolved into an isolated `/tmp` cache to
    avoid stale package state.
11. App Store Connect permanently consumes an accepted build number. Tests and
    IPA validation must precede upload.
12. `git diff --check` reported pre-existing whitespace in serialized
    `Menu.unity`; do not perform a broad scene-YAML cleanup during release work.
13. Builds are large and ignored. Never stage `Builds/`, archives, IPAs, Docker
    credentials, Unity caches, logs, or `/tmp` artifacts.

## Required final report

The release task is not complete until the report states:

- exact source commit, pushed branch, and Git tag;
- files intentionally changed for release preparation;
- Unity editor version;
- exact automated tests and totals;
- Linux server build result and path;
- Docker image tag and digest;
- Edgegap previous/new tag, update result, deployment request ID, Ready result,
  public port check, stop result, and rollback status;
- iOS archive/IPA paths, signing team, bundle ID, version/build, codesign result,
  and App Store Connect upload/processing result;
- Firebase Functions/rules/hosting deployment status;
- two-client/device tests performed or explicitly not performed;
- known warnings, limitations, and the shortest remaining manual checks.
