# Entropy Zero

Entropy Zero is a two-player Unity multiplayer game. Firebase coordinates
authenticated lobby codes, player records, purchases, match results, and
Edgegap deployment lifecycle. PurrNet connects both clients to a dedicated
Unity server hosted by Edgegap.

## Production services

- Firebase project: `entropy-7c113`
- Firebase Hosting: <https://entropy-7c113.web.app>
- Edgegap application: `entropy`
- Edgegap application version used by the backend baseline: `v21`
- Internal UDP game port: `7777`
- App bundle ID: `com.alvin.entropy`
- In-app purchase: `com.alvin.entropy.skin.sunducker`

## Source-of-truth rules

- `main` contains only tested, production-ready changes.
- Make multiplayer changes on a separate branch.
- Never commit credentials, signing files, service-account JSON, or local
  environment files.
- Tag every released client/server/backend combination.
- Do not replace a production server image until it has passed the checklist
  in [DEPLOYMENT.md](DEPLOYMENT.md).

The tag `multiplayer-working-v1` identifies the first preserved known-good
multiplayer baseline.
