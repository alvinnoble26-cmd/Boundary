# Copy/Paste Prompt: Full Entropy Zero Release

Copy everything inside the block below into a new Codex task opened on the
Boundary repository. Select **Terra** with **low/light reasoning effort** before
starting the task.

```text
Perform the complete Entropy Zero release autonomously from the current tested
candidate. Read AGENTS.md, README.md, DEPLOYMENT.md, and
Docs/FULL_RELEASE_RUNBOOK.md completely before acting, then follow that runbook
end to end. Use the Unity version from ProjectSettings/ProjectVersion.txt.

Release identity:
- Marketing version: <INSERT VERSION>
- App Store build number: <INSERT BUILD NUMBER>
- Release notes/intent: <INSERT SHORT DESCRIPTION>

This task is intended for gpt-5.6-terra at low/light reasoning effort. Keep the
workflow direct and sequential. Do not create subagents or new Codex tasks.

You have the owner’s explicit authorization to complete all normal release
operations without handing routine work back to me. That includes updating and
hardcoding the release version/build, running tests, installing matching Unity
build-support modules if needed, building the Linux dedicated server and iOS
client, using existing Firebase/Google/Edgegap/Apple/Xcode/Docker/GitHub login
sessions, completing normal service login/OAuth flows, reading the Firebase
EDGEGAP_API_TOKEN secret without exposing it, obtaining the Edgegap registry
push credential from the authenticated dashboard, publishing an immutable
server image, updating entropy/v21, creating/stopping a smoke deployment,
signing/exporting/uploading iOS to App Store Connect, committing the tested
candidate, pushing it to the test-2 branch, and pushing an annotated release
tag. Codex has full authorization for those operations.

Do not ask me to copy Firebase authorization codes, fetch Edgegap credentials,
operate Xcode, sign the build, upload the IPA, or push Git. Use CLI/API/browser
automation and existing saved accounts. Only stop for a provider-enforced
obstacle that cannot legally or technically be automated, such as MFA, CAPTCHA,
a new legal agreement, a required password that is not saved, or a missing paid
entitlement. If that happens, identify the exact single action needed and resume
as soon as it is cleared.

Never print, paste into chat, save in the repository, or commit passwords,
tokens, Firebase secrets, Edgegap credentials, Apple credentials, signing
material, or authorization codes. Feed secrets through process memory/stdin and
log out of the Edgegap Docker registry after pushing. Never push directly to
main, overwrite an existing container tag, use latest, delete the rollback
image, submit for App Review, or release the app to customers unless I separately
ask for that action.

Before changing anything, inspect and preserve the dirty worktree. The release
must include the complete intended current candidate and its Unity .meta files,
while excluding ignored build output, caches, logs, local settings, and secrets.
Close Unity before editing release settings. Confirm the candidate descends from
origin/test-2.

Hardcode the requested version/build in ProjectSettings/ProjectSettings.asset,
Assets/Game/Editor/IosBuildPostprocessor.cs, and
Assets/Editor/ReleaseBuilder.cs. Preserve bundle ID com.alvin.entropy, Apple team
WXG9SG3PA2, and automatic signing. Recheck these values after every Unity run so
an open/stale editor cannot revert them.

Run the full Unity EditMode suite and require all tests to pass. Diagnose failures
against runtime code and Git history before modifying gameplay; do not change
intentional gameplay values merely to satisfy stale tests. Build clean Linux
Dedicated Server and iOS exports through ReleaseBuilder. Inspect and remove only
unintended Unity-generated package/UnityConnect diffs. Verify the Linux build is
x86-64 and the generated Xcode project has the exact bundle/version/build/team.

Build the Docker image for linux/amd64 from the new server output. Use a new
immutable tag in the form release-<version>-build<build>-<YYYY-MM-DD>. A Mono
crash under Apple-Silicon x86 emulation is not a native server verdict; perform
the authoritative smoke test on Edgegap. For registry pushes, use the Edgegap
Tools -> Container Registry client-push credential, not the pull-only credential
returned on an app version. Record the pushed digest without exposing the
credential.

After tests and builds pass and the immutable image exists, review/stage the
complete source candidate, scan for secrets, commit it, verify origin/test-2 is
an ancestor, and push HEAD to test-2. Create and push an annotated tag named
release-<version>-build<build> containing the release record required by
DEPLOYMENT.md. Do not push to main.

Record the current entropy/v21 image/configuration for rollback, then use
tools/update-edgegap-image.mjs to select the new published tag. Verify the API
reports the new tag and unchanged UDP 7777/resources/TTL. Create exactly one
temporary Edgegap deployment, wait for Ready, verify its FQDN and nonzero
external UDP port, inspect startup health/logs, then stop it and verify
termination. Roll back immediately to the recorded old tag if readiness fails.
Do not claim this smoke test is a full two-client gameplay test.

Resolve Xcode packages into an isolated cache, archive with automatic signing
and DEVELOPMENT_TEAM=WXG9SG3PA2, export an IPA locally, verify its Info.plist,
provisioning profile, entitlements, Apple Distribution signature, team, bundle
ID, version, build, and strict codesign status, then upload with xcodebuild using
an App Store Connect upload export-options plist. Require the upload success
messages and report that Apple processing has started. Upload only; do not
submit for review or release to customers.

Compare Firebase Functions, Firestore rules, and Hosting against the released
baseline. Deploy only the smallest changed Firebase target. Do not deploy any
Firebase target when those sources are unchanged merely because Firebase was
used for authentication/secret access.

Keep working until every authorized phase is complete or a concrete external
blocker remains. Send concise progress updates, especially before Git push,
Edgegap production replacement, and App Store upload. The final response must
include the commit, test-2 push, tag, tests, build paths, container tag/digest,
Edgegap old/new image and smoke-deployment result, iOS signing and upload result,
Firebase deployment status, unperformed physical-device/two-client checks, and
rollback information. Never claim a test or validation passed unless it ran.
```
