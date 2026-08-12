import {execFileSync} from "node:child_process";

let token = (await new Promise((resolve, reject) => {
  let input = "";
  process.stdin.setEncoding("utf8");
  process.stdin.on("data", (chunk) => input += chunk);
  process.stdin.on("end", () => resolve(input.trim()));
  process.stdin.on("error", reject);
}));

let args = process.argv.slice(2);
if (args[0] === "--firebase-secret") {
  const firebase = "/Users/alvinnoble/.cache/codex-runtimes/codex-primary-runtime/dependencies/bin/fallback/pnpm";
  token = execFileSync(firebase, ["dlx", "firebase-tools", "functions:secrets:access",
    "EDGEGAP_API_TOKEN", "--project", "entropy-7c113"], {
    encoding: "utf8",
    env: {...process.env, PATH: "/Users/alvinnoble/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/bin:" +
      "/Users/alvinnoble/.cache/codex-runtimes/codex-primary-runtime/dependencies/bin/fallback:" +
      "/usr/local/bin:/usr/bin:/bin"},
  }).trim();
  args = args.slice(1);
}

const [application, version, imageTag] = args;
if (!token || !application || !version || !imageTag) {
  throw new Error("Usage: secret | node update-edgegap-image.mjs <application> <version> <image-tag>");
}

const url = `https://api.edgegap.com/v1/app/${encodeURIComponent(application)}` +
  `/version/${encodeURIComponent(version)}`;
const authHeaders = {authorization: `token ${token}`};
const currentResponse = await fetch(url, {headers: authHeaders});
if (!currentResponse.ok)
  throw new Error(`Could not read current Edgegap version (${currentResponse.status}).`);

const current = await currentResponse.json();
const update = {docker_tag: imageTag, verify_image: true};
try {
  // Reuse the working registry login that successfully pushed this image.
  // Nothing from the credential helper is printed or written to disk.
  const credentialJson = execFileSync("docker-credential-desktop", ["get"], {
    input: "registry.edgegap.com",
    encoding: "utf8",
  });
  const credential = JSON.parse(credentialJson);
  update.private_username = credential.Username;
  update.private_token = credential.Secret;
} catch {
  if (current.private_username) update.private_username = current.private_username;
  if (current.private_token && !/^\*+$/.test(current.private_token))
    update.private_token = current.private_token;
}

let lastError = "";
for (let attempt = 1; attempt <= 5; attempt++) {
  const response = await fetch(url, {
    method: "PATCH",
    headers: {
      ...authHeaders,
      "content-type": "application/json",
    },
    body: JSON.stringify(update),
  });

  const text = await response.text();
  if (response.ok) {
    console.log(`Edgegap ${application}/${version} now uses image tag ${imageTag}.`);
    process.exit(0);
  }

  lastError = `Edgegap update failed (${response.status}): ${text}`;
  if (attempt < 5)
    await new Promise((resolve) => setTimeout(resolve, 15000));
}

throw new Error(lastError);
