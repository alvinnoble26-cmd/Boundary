"use strict";

const {onRequest} = require("firebase-functions/v2/https");
const {SignedDataVerifier, Environment} = require("@apple/app-store-server-library");
const fs = require("fs");
const path = require("path");
const {onSchedule} = require("firebase-functions/v2/scheduler");
const {defineSecret} = require("firebase-functions/params");
const {initializeApp} = require("firebase-admin/app");
const {getAuth} = require("firebase-admin/auth");
const {getFirestore, FieldValue, Timestamp} = require("firebase-admin/firestore");
const {logger} = require("firebase-functions");

initializeApp();

const db = getFirestore();
const edgegapToken = defineSecret("EDGEGAP_API_TOKEN");

const EDGEGAP_API = "https://api.edgegap.com";
const EDGEGAP_APPLICATION = "entropy";
// Edgegap application-version configuration used by automation. The free-tier
// version is updated in place to point at each newly pushed immutable image tag.
const EDGEGAP_VERSION = "v21";
const GAME_PORT_NAME = "gameport";
const DEPLOYMENT_START_TIMEOUT_MS = 150000;
const DEPLOYMENT_POLL_INTERVAL_MS = 3000;
const REMATCH_CLEANUP_GRACE_MS = 90000;
const ABANDONED_DEPLOYMENT_MS = 10 * 60 * 1000;
const MAX_DEPLOYMENT_LIFETIME_MS = 10 * 60 * 1000;
const APPLE_BUNDLE_ID = "com.alvin.entropy";
const APPLE_ROOT_CA = fs.readFileSync(path.join(__dirname, "AppleRootCA-G3.cer"));

function decodeJwsPayload(jws) {
  const parts = String(jws).split(".");
  if (parts.length !== 3) throw new Error("Invalid Apple transaction JWS");
  return JSON.parse(Buffer.from(parts[1], "base64url").toString("utf8"));
}

async function verifyAppleJws(jws) {
  const unverified = decodeJwsPayload(jws);
  const environment = String(unverified.environment || "").toUpperCase() === "PRODUCTION"
    ? Environment.PRODUCTION : Environment.SANDBOX;
  const verifier = new SignedDataVerifier(
      [APPLE_ROOT_CA], true, environment, APPLE_BUNDLE_ID,
      Number(process.env.APPLE_APP_ID));
  return verifier.verifyAndDecodeTransaction(jws);
}
const SUN_DUCKER_PRODUCT_ID = "com.alvin.entropy.skin.sunducker";
const TURTLE_PRODUCT_ID = "com.alvin.entropy.skin.turtle";
const SKIN_PRODUCTS = Object.freeze({
  [SUN_DUCKER_PRODUCT_ID]: "sun_ducker",
  [TURTLE_PRODUCT_ID]: "turtle",
});

function sendJson(res, status, body) {
  res.status(status).set("Content-Type", "application/json").send(JSON.stringify(body));
}

function getClientIp(req) {
  const forwarded = req.get("x-forwarded-for");
  const candidate = forwarded ? forwarded.split(",")[0].trim() : req.ip;
  return String(candidate || "").replace(/^::ffff:/, "");
}

function isUsablePublicIp(value) {
  if (!value || value === "::1" || value === "127.0.0.1") return false;
  if (value.startsWith("10.") || value.startsWith("192.168.")) return false;
  if (/^172\.(1[6-9]|2\d|3[01])\./.test(value)) return false;
  return true;
}

function edgegapHeaders() {
  return {
    "Authorization": `token ${edgegapToken.value()}`,
    "Content-Type": "application/json",
  };
}

async function edgegapFetch(path, options = {}) {
  const response = await fetch(`${EDGEGAP_API}${path}`, {
    ...options,
    headers: {...edgegapHeaders(), ...(options.headers || {})},
  });

  const text = await response.text();
  let payload = {};
  if (text) {
    try {
      payload = JSON.parse(text);
    } catch (_) {
      payload = {message: text};
    }
  }

  if (!response.ok) {
    const error = new Error(`Edgegap ${response.status}: ${payload.message || text || response.statusText}`);
    error.status = response.status;
    error.payload = payload;
    throw error;
  }

  return payload;
}

function deploymentData(payload) {
  return payload && payload.data ? payload.data : payload;
}

function findGamePort(data) {
  const ports = data && (data.ports || data.ports_mapping);
  if (!ports) return null;

  if (ports[GAME_PORT_NAME]) return ports[GAME_PORT_NAME];

  for (const value of Object.values(ports)) {
    if (value && Number(value.internal) === 7777 &&
        String(value.protocol || "").toUpperCase() === "UDP") {
      return value;
    }
  }

  return null;
}

function sleep(ms) {
  return new Promise((resolve) => setTimeout(resolve, ms));
}

function deploymentStatus(data) {
  const raw = String(data.current_status || data.status || "").trim();
  // Edgegap currently returns enum strings such as "Status.READY". Older
  // responses and some endpoints use plain "READY", so accept both forms.
  return raw.split(".").pop().toLowerCase();
}

async function markDeploymentReady(lobbyRef, requestId, data) {
  const port = findGamePort(data);
  const fqdn = data.fqdn || data.url || "";
  const externalPort = port ? Number(port.external) : 0;

  if (!fqdn || !externalPort) return false;

  await lobbyRef.update({
    serverStatus: "ready",
    serverHost: fqdn,
    serverPort: externalPort,
    deploymentRequestId: requestId,
    serverReadyAt: FieldValue.serverTimestamp(),
    // The match lifetime starts when the server is actually ready, not while
    // Edgegap is still downloading/starting the container. Keeping this value
    // backend-owned also prevents stale Unity Inspector values from shutting a
    // healthy match down early.
    expiresAt: Timestamp.fromMillis(Date.now() + MAX_DEPLOYMENT_LIFETIME_MS),
    serverError: "",
  });

  logger.info("Edgegap deployment ready", {lobbyCode: lobbyRef.id, requestId, fqdn, externalPort});
  return true;
}

async function pollDeploymentUntilReady(lobbyRef, requestId) {
  const deadline = Date.now() + DEPLOYMENT_START_TIMEOUT_MS;

  while (Date.now() < deadline) {
    const payload = await edgegapFetch(`/v1/status/${encodeURIComponent(requestId)}`);
    const data = deploymentData(payload);
    const status = deploymentStatus(data);

    // Edgegap can expose the FQDN and the configured internal port while the
    // deployment is still starting. The randomized public port is authoritative
    // only after the status is READY. Publishing earlier can send clients to
    // internal port 7777 instead of the assigned external port.
    if (status === "ready" && await markDeploymentReady(lobbyRef, requestId, data))
      return data;

    if (status === "error" || status === "terminated" || status === "stopped") {
      throw new Error(`Edgegap deployment entered ${status}`);
    }

    await sleep(DEPLOYMENT_POLL_INTERVAL_MS);
  }

  throw new Error("Timed out waiting for Edgegap deployment readiness");
}

exports.createEdgegapDeployment = onRequest({
  region: "us-central1",
  timeoutSeconds: 180,
  memory: "256MiB",
  secrets: [edgegapToken],
  cors: false,
  invoker: "public",
}, async (req, res) => {
  if (req.method !== "POST") {
    res.set("Allow", "POST");
    return sendJson(res, 405, {error: "POST required"});
  }

  const authorization = String(req.get("authorization") || "");
  const idToken = authorization.startsWith("Bearer ") ? authorization.slice(7) : "";
  if (!idToken) {
    return sendJson(res, 401, {error: "Firebase authentication required"});
  }

  let caller;
  try {
    caller = await getAuth().verifyIdToken(idToken);
  } catch (error) {
    logger.warn("Rejected deployment request with invalid Firebase token", {
      error: error.message,
    });
    return sendJson(res, 401, {error: "Invalid Firebase authentication"});
  }

  const lobbyCode = String(req.body && req.body.lobbyCode || "").trim();
  if (!/^\d{4}$/.test(lobbyCode)) {
    return sendJson(res, 400, {error: "A four-digit lobbyCode is required"});
  }

  const lobbyRef = db.collection("lobbies").doc(lobbyCode);
  let claimed = false;

  try {
    const existingRequestId = await db.runTransaction(async (transaction) => {
      const snapshot = await transaction.get(lobbyRef);
      if (!snapshot.exists) throw new Error("Lobby not found");

      const lobby = snapshot.data();
      if (lobby.hostUid && lobby.hostUid !== caller.uid)
        throw new Error("Only the lobby host can create its server deployment");
      if (lobby.deploymentRequestId) return lobby.deploymentRequestId;
      if (lobby.deploymentClaimed) throw new Error("Deployment is already being created");
      if (lobby.serverStatus !== "deploying") throw new Error("Lobby is not awaiting deployment");

      transaction.update(lobbyRef, {
        deploymentClaimed: true,
        deploymentClaimedAt: FieldValue.serverTimestamp(),
        deploymentOwnerUid: caller.uid,
      });
      claimed = true;
      return "";
    });

    if (existingRequestId) {
      await pollDeploymentUntilReady(lobbyRef, existingRequestId);
      return sendJson(res, 200, {requestId: existingRequestId, status: "ready", reused: true});
    }

    const clientIp = getClientIp(req);
    const user = isUsablePublicIp(clientIp) ? {
      user_type: "ip_address",
      user_data: {ip_address: clientIp},
    } : {
      // Local/emulator fallback only. Production requests normally have a public IP.
      user_type: "geo_coordinates",
      user_data: {latitude: 42.3314, longitude: -83.0458},
    };

    const deployPayload = await edgegapFetch("/v2/deployments", {
      method: "POST",
      body: JSON.stringify({
        application: EDGEGAP_APPLICATION,
        version: EDGEGAP_VERSION,
        require_cached_locations: false,
        users: [user],
        tags: [`lobby-${lobbyCode}`],
      }),
    });

    const deployData = deploymentData(deployPayload);
    const requestId = String(deployData.request_id || deployPayload.request_id || "");
    if (!requestId) throw new Error("Edgegap did not return a request_id");

    await lobbyRef.update({
      deploymentRequestId: requestId,
      deploymentClaimed: false,
      serverStatus: "deploying",
      serverError: "",
    });

    await pollDeploymentUntilReady(lobbyRef, requestId);
    return sendJson(res, 200, {requestId, status: "ready"});
  } catch (error) {
    logger.error("Edgegap deployment creation failed", {lobbyCode, error: error.message});

    const update = {
      serverStatus: "error",
      serverError: String(error.message || error).slice(0, 500),
    };
    if (claimed) update.deploymentClaimed = false;

    try {
      await lobbyRef.update(update);
    } catch (updateError) {
      logger.error("Could not update failed lobby", {lobbyCode, error: updateError.message});
    }

    return sendJson(res, 500, {error: "Server deployment failed"});
  }
});

async function verifyAppleReceipt(receiptData, sandbox = false) {
  const endpoint = sandbox ? "https://sandbox.itunes.apple.com/verifyReceipt" :
    "https://buy.itunes.apple.com/verifyReceipt";
  const response = await fetch(endpoint, {
    method: "POST",
    headers: {"Content-Type": "application/json"},
    body: JSON.stringify({"receipt-data": receiptData, "exclude-old-transactions": false}),
  });
  if (!response.ok) throw new Error(`Apple verification HTTP ${response.status}`);
  const payload = await response.json();
  if (payload.status === 21007 && !sandbox) return verifyAppleReceipt(receiptData, true);
  return payload;
}

exports.verifyAppleSkinPurchase = onRequest({
  region: "us-central1",
  timeoutSeconds: 60,
  memory: "256MiB",
  cors: false,
  invoker: "public",
  secrets: ["APPLE_ISSUER_ID", "APPLE_KEY_ID", "APPLE_PRIVATE_KEY", "APPLE_APP_ID"],
}, async (req, res) => {
  if (req.method !== "POST") return sendJson(res, 405, {error: "POST required"});
  const authorization = String(req.get("authorization") || "");
  const idToken = authorization.startsWith("Bearer ") ? authorization.slice(7) : "";
  let caller;
  try {
    caller = await getAuth().verifyIdToken(idToken);
  } catch (_) {
    return sendJson(res, 401, {error: "Firebase authentication required"});
  }

  try {
    const rawReceipt = String(req.body && req.body.receipt || "");
    const jws = String(req.body && req.body.jws || "");
    // Validate the requested item before handling either StoreKit receipt
    // format. Previously these declarations were below the JWS branch, which
    // raised a JavaScript temporal-dead-zone ReferenceError for every StoreKit
    // 2 transaction and made successful purchases appear recoverable.
    const productId = String(req.body && req.body.productId || SUN_DUCKER_PRODUCT_ID);
    const skinId = SKIN_PRODUCTS[productId];
    if (!skinId) throw new Error("Unsupported skin product ID");
    let unityReceipt = null;
    if (rawReceipt) {
      try { unityReceipt = JSON.parse(rawReceipt); } catch (_) { /* StoreKit 2 */ }
    }
    if (jws) {
      const transaction = await verifyAppleJws(jws);
      if (transaction.bundleId !== APPLE_BUNDLE_ID) throw new Error("Transaction bundle ID does not match");
      if (transaction.productId !== productId) throw new Error("Transaction product ID does not match");
      if (transaction.revocationDate) throw new Error("Transaction has been revoked");
      if (!transaction.transactionId) throw new Error("Transaction ID is missing");
      const transactionId = String(transaction.originalTransactionId || transaction.transactionId);
      const entitlementRef = db.collection("purchaseEntitlements").doc(transactionId);
      const skinRef = db.collection("players").doc(caller.uid).collection("skins").doc(skinId);
      await db.runTransaction(async (transactionWriter) => {
        transactionWriter.set(entitlementRef, {
          uid: caller.uid, uids: FieldValue.arrayUnion(caller.uid), productId,
          store: "apple", environment: transaction.environment || "unknown",
          transactionId: String(transaction.transactionId), verifiedAt: FieldValue.serverTimestamp(),
        }, {merge: true});
        transactionWriter.set(skinRef, {
          owned: true, acquisitionType: "apple_iap", productId,
          transactionId: String(transaction.transactionId), acquiredAt: FieldValue.serverTimestamp(),
        }, {merge: true});
      });
      return sendJson(res, 200, {owned: true, skinId});
    }
    if (!unityReceipt || unityReceipt.Store !== "AppleAppStore" || !unityReceipt.Payload)
      throw new Error("A StoreKit 2 transaction JWS is required");
    const verification = await verifyAppleReceipt(unityReceipt.Payload);
    if (verification.status !== 0) throw new Error(`Apple receipt status ${verification.status}`);
    const receipt = verification.receipt || {};
    if (receipt.bundle_id !== APPLE_BUNDLE_ID) throw new Error("Receipt bundle ID does not match");
    const purchases = Array.isArray(receipt.in_app) ? receipt.in_app : [];
    const skinPurchases = purchases.filter((item) => item.product_id === productId);
    const purchase = skinPurchases.filter((item) =>
      !item.cancellation_date).sort((a, b) =>
      Number(b.purchase_date_ms || 0) - Number(a.purchase_date_ms || 0))[0];

    // A validated Apple receipt with only cancelled/refunded transactions
    // transactions is authoritative. Remove the entitlement from this Firebase
    // profile so refunded content is not left playable indefinitely.
    if (!purchase || !purchase.transaction_id) {
      if (skinPurchases.some((item) => item.cancellation_date)) {
        const profileRef = db.collection("players").doc(caller.uid);
        await db.runTransaction(async (transaction) => {
          transaction.delete(profileRef.collection("skins").doc(skinId));
          transaction.set(profileRef, {
            selectedSkin: "beard",
            purchaseRevokedAt: FieldValue.serverTimestamp(),
          }, {merge: true});
        });
        logger.info("Removed refunded skin entitlement", {uid: caller.uid, skinId});
        return sendJson(res, 200, {owned: false, skinId, revoked: true});
      }
      throw new Error("Skin purchase not found");
    }

    const transactionId = String(purchase.original_transaction_id || purchase.transaction_id);
    const entitlementRef = db.collection("purchaseEntitlements").doc(transactionId);
    const skinRef = db.collection("players").doc(caller.uid).collection("skins").doc(skinId);
    await db.runTransaction(async (transaction) => {
      const existing = await transaction.get(entitlementRef);
      transaction.set(entitlementRef, {
        // Keep every Firebase profile that has successfully restored this
        // Apple-owned non-consumable. Anonymous Auth may issue a new UID after
        // reinstall, but Apple's original transaction remains authoritative.
        uid: caller.uid,
        uids: FieldValue.arrayUnion(caller.uid),
        productId,
        store: "apple",
        environment: verification.environment || "unknown",
        verifiedAt: FieldValue.serverTimestamp(),
      }, {merge: true});
      transaction.set(skinRef, {
        owned: true,
        acquisitionType: "apple_iap",
        productId,
        transactionId,
        acquiredAt: FieldValue.serverTimestamp(),
      }, {merge: true});
    });
    return sendJson(res, 200, {owned: true, skinId});
  } catch (error) {
    logger.warn("Apple skin purchase rejected", {uid: caller.uid, error: error.message});
    return sendJson(res, 400, {error: error.message});
  }
});

exports.deletePlayerAccount = onRequest({
  region: "us-central1",
  timeoutSeconds: 60,
  memory: "256MiB",
  cors: false,
  invoker: "public",
}, async (req, res) => {
  if (req.method !== "POST") return sendJson(res, 405, {error: "POST required"});
  const authorization = String(req.get("authorization") || "");
  const idToken = authorization.startsWith("Bearer ") ? authorization.slice(7) : "";
  let caller;
  try {
    caller = await getAuth().verifyIdToken(idToken);
  } catch (_) {
    return sendJson(res, 401, {error: "Firebase authentication required"});
  }

  try {
    const entitlementSnapshots = await db.collection("purchaseEntitlements")
        .where("uids", "array-contains", caller.uid).get();
    const batch = db.batch();
    for (const document of entitlementSnapshots.docs) {
      const update = {uids: FieldValue.arrayRemove(caller.uid)};
      if (document.data().uid === caller.uid) update.uid = FieldValue.delete();
      batch.update(document.ref, update);
    }
    await batch.commit();
    await db.recursiveDelete(db.collection("players").doc(caller.uid));
    await getAuth().deleteUser(caller.uid);
    logger.info("Deleted player account", {uid: caller.uid});
    return sendJson(res, 200, {deleted: true});
  } catch (error) {
    logger.error("Player account deletion failed", {uid: caller.uid, error: error.message});
    return sendJson(res, 500, {error: "Account deletion failed"});
  }
});

exports.recordMatchResult = onRequest({
  region: "us-central1",
  timeoutSeconds: 30,
  memory: "256MiB",
  cors: false,
  invoker: "public",
}, async (req, res) => {
  if (req.method !== "POST") {
    res.set("Allow", "POST");
    return sendJson(res, 405, {error: "POST required"});
  }

  const authorization = String(req.get("authorization") || "");
  const idToken = authorization.startsWith("Bearer ") ? authorization.slice(7) : "";
  let caller;
  try {
    caller = await getAuth().verifyIdToken(idToken);
  } catch (_) {
    return sendJson(res, 401, {error: "Firebase authentication required"});
  }

  const lobbyCode = String(req.body && req.body.lobbyCode || "").trim();
  const round = Math.max(0, Number(req.body && req.body.round || 0) || 0);
  if (!/^\d{4}$/.test(lobbyCode))
    return sendJson(res, 400, {error: "Invalid lobby code"});

  try {
    const outcome = await db.runTransaction(async (transaction) => {
      const lobbyRef = db.collection("lobbies").doc(lobbyCode);
      const profileRef = db.collection("players").doc(caller.uid);
      const defaultSkinRef = profileRef.collection("skins").doc("beard");
      const eventRef = db.collection("matchStatEvents")
          .doc(`${lobbyCode}_${round}_${caller.uid}`);
      const [lobbySnapshot, eventSnapshot, profileSnapshot, skinSnapshot] = await Promise.all([
        transaction.get(lobbyRef),
        transaction.get(eventRef),
        transaction.get(profileRef),
        transaction.get(defaultSkinRef),
      ]);

      if (!lobbySnapshot.exists) throw new Error("Lobby not found");
      if (eventSnapshot.exists) return "already-recorded";

      const lobby = lobbySnapshot.data();
      if (lobby.matchEnded !== true || !lobby.loserRole)
        throw new Error("Match has not ended");
      const lobbyRound = Math.max(0, Number(lobby.rematchRound || 0) || 0);
      if (round !== lobbyRound)
        throw new Error("Round does not match the lobby");

      let role = "";
      if (lobby.hostUid === caller.uid) role = "host";
      else if (lobby.joinerUid === caller.uid) role = "joiner";
      if (!role) throw new Error("Player was not in this lobby");

      const won = lobby.loserRole !== role;
      transaction.set(eventRef, {
        uid: caller.uid,
        lobbyCode,
        round,
        result: won ? "win" : "loss",
        createdAt: FieldValue.serverTimestamp(),
      });
      const profileUpdate = {
        wins: FieldValue.increment(won ? 1 : 0),
        losses: FieldValue.increment(won ? 0 : 1),
        matchesPlayed: FieldValue.increment(1),
        lastSeenAt: FieldValue.serverTimestamp(),
      };
      if (!profileSnapshot.exists || !profileSnapshot.data().uid)
        profileUpdate.uid = caller.uid;
      if (!profileSnapshot.exists || !profileSnapshot.data().accountType)
        profileUpdate.accountType = caller.firebase.sign_in_provider === "anonymous" ? "guest" : "linked";
      if (!profileSnapshot.exists || !profileSnapshot.data().selectedSkin)
        profileUpdate.selectedSkin = "beard";
      if (!profileSnapshot.exists || !profileSnapshot.data().createdAt)
        profileUpdate.createdAt = FieldValue.serverTimestamp();

      transaction.set(profileRef, profileUpdate, {merge: true});
      if (!skinSnapshot.exists) {
        transaction.set(defaultSkinRef, {
          owned: true,
          acquisitionType: "default",
          acquiredAt: FieldValue.serverTimestamp(),
        });
      }
      return won ? "win" : "loss";
    });

    return sendJson(res, 200, {status: outcome});
  } catch (error) {
    logger.warn("Match result was not recorded", {uid: caller.uid, lobbyCode, error: error.message});
    return sendJson(res, 400, {error: error.message});
  }
});

async function stopDeployment(lobbyRef, lobby, reason) {
  const requestId = String(lobby.deploymentRequestId || "");
  if (!requestId) return;

  try {
    await edgegapFetch(`/v1/stop/${encodeURIComponent(requestId)}`, {method: "DELETE"});
  } catch (error) {
    // 404/410 means there is no live deployment left to clean up.
    if (error.status !== 404 && error.status !== 410) throw error;
  }

  await lobbyRef.update({
    serverStatus: "terminated",
    serverHost: "",
    serverPort: 0,
    serverTerminatedAt: FieldValue.serverTimestamp(),
    serverTerminationReason: reason,
  });

  logger.info("Stopped stale Edgegap deployment", {lobbyCode: lobbyRef.id, requestId, reason});
}

function millis(value) {
  return value instanceof Timestamp ? value.toMillis() : 0;
}

exports.cleanupEdgegapDeployments = onSchedule({
  schedule: "every 1 minutes",
  region: "us-central1",
  timeoutSeconds: 120,
  memory: "256MiB",
  secrets: [edgegapToken],
}, async () => {
  const snapshot = await db.collection("lobbies")
      .where("serverStatus", "in", ["deploying", "ready", "error"])
      .limit(100)
      .get();

  const now = Date.now();

  for (const doc of snapshot.docs) {
    const lobby = doc.data();
    if (!lobby.deploymentRequestId) continue;

    if (lobby.serverStatus === "deploying") {
      try {
        const payload = await edgegapFetch(
            `/v1/status/${encodeURIComponent(lobby.deploymentRequestId)}`);
        const data = deploymentData(payload);
        const status = deploymentStatus(data);
        if (status === "ready" &&
            await markDeploymentReady(doc.ref, lobby.deploymentRequestId, data))
          continue;
      } catch (error) {
        logger.warn("Could not reconcile deploying Edgegap server", {
          lobbyCode: doc.id,
          requestId: lobby.deploymentRequestId,
          error: error.message,
        });
      }
    }

    const endedAt = millis(lobby.endedAt);
    const expiresAt = millis(lobby.expiresAt);
    const claimedAt = millis(lobby.deploymentClaimedAt);
    const createdAt = millis(lobby.createdAt);

    let reason = "";
    if (lobby.matchEnded === true && endedAt && now - endedAt >= REMATCH_CLEANUP_GRACE_MS) {
      reason = "rematch-window-expired";
    } else if (expiresAt && now >= expiresAt) {
      reason = "lobby-expired";
    } else if (lobby.serverStatus === "deploying" &&
               now - (claimedAt || createdAt) >= ABANDONED_DEPLOYMENT_MS) {
      reason = "deployment-start-timeout";
    } else if (lobby.serverStatus === "error") {
      reason = "deployment-error";
    }

    if (!reason) continue;

    try {
      await stopDeployment(doc.ref, lobby, reason);
    } catch (error) {
      logger.error("Scheduled Edgegap cleanup failed", {
        lobbyCode: doc.id,
        requestId: lobby.deploymentRequestId,
        error: error.message,
      });
    }
  }
});
