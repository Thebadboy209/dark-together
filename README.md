# DARK TOGETHER — Unity 2D / Co-op horror starter (PC + Mobile)

A **source-code MVP**, not a finished commercially released game. It implements a basic 2-player top-down game:

- Two-player rooms with a shareable 6-character code, lobby, READY and START.
- Server-authoritative directional movement, simple wall collision, sprint and state sync.
- PC keyboard and Android/iOS touch joystick, interact and flashlight actions.
- A map, a server-controlled roaming/chasing monster with non-graphic reset on contact.
- Cooperative objective: activate generator G, unlock door D, gather both players near exit X.
- Optional Firebase anonymous sign-in via REST from the client, server token verification and
  server-only Firebase Realtime Database room metadata.
- Colyseus (MIT) reused as the networking library, rather than reinventing matchmaking/WebSocket sync.

**Caveats:** Unity build has not been opened/tested in the current build environment (Unity is not installed here),
so Unity SDK API or platform-specific import adjustments may be necessary. Cloud Run and Firebase have not been deployed.
The project does **not** yet implement reconnection, persistent checkpoints, polished sprites, audio, anti-cheat-hardening,
full mobile QA or a production account system. Avoid public production deployment until those items are completed.

## 0. Prerequisites

- Node.js **20+** and npm, on the computer hosting the server.
- Unity Hub + Unity 6 LTS, with **2D Core** template; install Android Build Support and/or iOS Build Support as needed.
- Unity active input handling **Both** (Edit → Project Settings → Player → Other Settings → Active Input Handling).
- Git access for installing the Colyseus Unity SDK through UPM.

## 1. Start the server locally

Open a terminal in the **server** directory:

```bash
npm install
npm test
npm run dev
```

The server listens on `ws://127.0.0.1:2567`. Run the server *before* pressing Play in Unity.
If you change port via `PORT`, change the Unity server URL too.

## 2. Open the Unity game directly

The repository now contains a Unity project at `unity-client/`, including
`Assets/`, `Packages/manifest.json` (Colyseus SDK as a Git package) and
`ProjectSettings/ProjectVersion.txt`. Unity Hub can import this folder directly.

1. Install **Unity 6.3 LTS** (project version: `6000.3.28f1`) and Git.
2. Unity Hub → **Add project from disk** → select `unity-client/`.
3. Open the project. Unity Package Manager installs Colyseus automatically from Git.
4. Set **Active Input Handling = Both** in Player Settings (legacy keyboard/touch API used by this prototype).
5. Menu **Dark Together → Generate Playable Scene** (first time only).
6. Select the **DARK TOGETHER - Game Controller** and configure `serverUrl`
   (local PC: `ws://127.0.0.1:2567`, phone: `ws://YOUR_PC_LAN_IP:2567`).
7. Start the server (`cd server && npm install && npm run dev`).
8. Press Play → **CREATE ROOM** → copy the code; join from another Unity build/client.
9. Both players press READY; the host presses START.

### Build menus

Unity Editor provides these commands. Install the required Unity Build Support module first:

- **Dark Together → Build → Windows x64** → `Builds/Windows/DarkTogether.exe`
- **Dark Together → Build → Android APK** → `Builds/Android/DarkTogether.apk`
- **Dark Together → Build → iOS Xcode Project** → `Builds/iOS/` (requires Mac/Xcode to sign)

These menus run the Unity build pipeline; they do **not** prove that this project
has passed actual device QA. Set the server URL and platform-specific permissions before building.
For Android/iOS over the public Internet, use `wss://` (TLS) and a properly deployed backend.

### PC controls

`WASD` or arrows = move, `Left Shift` = sprint, `E` = interact, `F` = flashlight.

### Android / iOS

- On Android/iOS, set **Landscape Left/Right** and enable the desired build support module in Unity Hub.
- Use the **bottom-left virtual joystick**, buttons on the right for interact/flashlight/run.
- If your phone shares the computer's local network, use `ws://<computer-LAN-IP>:2567`
  instead of `127.0.0.1`. Some platform/device network policies reject cleartext WebSockets.
  For Internet play and public deployments use a valid **`wss://`** endpoint instead.
- Android: build APK from Unity Build Profiles; iOS: build an Xcode project using a Mac and signing setup.
- The source is cross-platform by design, but Android/iOS device builds have **not** been verified here.

## 3. Enable Firebase (optional; required before public release)

Local guest mode (no Firebase API key / no server `FIREBASE_PROJECT_ID`) is **for private testing only**.
For actual Internet deployment require Firebase auth.

1. Create a Firebase project, enable **Authentication → Sign-in method → Anonymous**.
2. In Firebase Project Settings copy the **Web API key** and paste into Unity `firebaseWebApiKey`.
   The Web API key is client configuration, not a service-account private key.
3. Create a **Realtime Database**, choose a region, and deploy the rules from `firebase/database.rules.json`.
   The sample rules deny all client reads/writes; only the Admin SDK service writes room metadata.
4. Configure the server with env vars:
   - `FIREBASE_PROJECT_ID=your-project-id`
   - `FIREBASE_DATABASE_URL=https://your-project-default-rtdb.<region>.firebasedatabase.app`
   - For local backend execution only, point `GOOGLE_APPLICATION_CREDENTIALS` to a service-account key **outside this repository**.
     Cloud Run should use its runtime identity/service account and ADC; never upload the JSON key.
5. Restart the server. It now verifies Firebase ID tokens on every room join and rejects guest clients.

Room metadata path: `rooms/<ROOMCODE>`. Live high-frequency positions remain in Colyseus state, **not** in Firebase.
Firebase anonymous sign-in in this MVP creates a fresh anonymous account on each app session; token refresh,
account linking and offline token retention are future production work.

## 4. Deploy server to Cloud Run (optional)

From repository root, with Google Cloud CLI authorized and billing enabled:

```bash
gcloud run deploy dark-together \
  --source ./server \
  --region asia-southeast1 \
  --allow-unauthenticated \
  --min-instances 1 --max-instances 1 \
  --concurrency 100 --timeout 3600 \
  --set-env-vars FIREBASE_PROJECT_ID=YOUR_PROJECT_ID,FIREBASE_DATABASE_URL=YOUR_DATABASE_URL
```

Set Unity `serverUrl` to the returned **`wss://...run.app`** host; Android/iOS and PC can then reach it.
Cloud Run ingress is public because the **game server authenticates Firebase ID tokens at the application layer**.
Keep `FIREBASE_PROJECT_ID` configured for any publicly accessible deployment.
Grant the Cloud Run runtime service account the Firebase/Realtime Database permissions needed to verify tokens and manage metadata.

**Scaling limits:** this MVP intentionally runs **one Cloud Run instance** (in-memory Colyseus presence/room state).
For multiple instances, implement Redis-backed presence/driver, proper Colyseus routing and a persistent session strategy.
Cloud Run WebSocket connections have request timeout limits; graceful reconnect/recovery is not implemented here.

## 5. GitHub

At repository root:

```bash
git init
git add .
git commit -m "Initial Unity 2D cooperative horror prototype"
git branch -M main
git remote add origin https://github.com/YOUR_USERNAME/dark-together.git
git push -u origin main
```

Create the empty repository in GitHub before pushing. Install Git LFS before tracking large binary files (`git lfs install`).
Never commit service account JSON, local `.env`, tokens or credentials.

## 6. Architecture

```text
unity-client/Assets/Scripts/DarkTogether.cs  Unity movement, UI, rendering, Colyseus client
unity-client/Assets/Editor/CreateGameScene.cs  Unity Editor one-click scene builder
unity-client/Assets/Resources/hospital_map.json  Shared ASCII map
server/src/main.ts                   Colyseus boot
server/src/room.ts                   Rooms, ready, movement, puzzle, monster AI
server/src/state.ts                  Schema sync
server/src/world.ts                  Collision + pathfinding
server/src/firebase.ts               Optional server-side Admin SDK
server/data/hospital_map.json        Exact same map
firebase/database.rules.json         Deny client RTDB reads/writes
```

Original sources and actual third-party dependencies are documented in `docs/OPEN_SOURCE_CREDITS.md`.

## 7. Next development steps

- Verify Unity compilation using a pinned Colyseus Unity SDK commit.
- Add reconnect / resumption with safe host handover, checkpoint persistence and token refresh.
- Replace IMGUI with responsive UGUI/UIToolkit, tactile joystick and a proper menu.
- Add licensed sprites, animation, sound, dynamic 2D lighting, multiple puzzles and saved progress.
- Add complete server integration tests with 2 real Colyseus clients and device QA on Android and iOS.
