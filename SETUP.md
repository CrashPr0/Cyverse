# CyVerse — Setup

A web-based (WebGL), desktop, first-person cybersecurity learning game. No VR.
This document covers opening, running, and extending the levels.

## Game flow

The game now boots through a scene chain instead of straight into a level:

```
PasswordLock  →  Hub  →  Level 1 (I/AM) → Level 2 (Cyber Defense) → …
 (entry scene)   (level select)           (unlock in order)
```

- **PasswordLock** — the first thing a player sees: a login terminal. Two
  modes on the `PasswordLockController` component:
  - **Gate mode (default)** — a real beta-access gate: the code is never
    shown; testers get it from the team, and repeated failures trigger a
    30s cooldown. Set the actual code in the Inspector (`password` field).
  - **`revealPassword` on** — the original educational mode: the memo *gives*
    the password and teaches why it's strong (a passphrase in disguise);
    typing it in is the lesson.

  > **Security reality check:** the check runs entirely in the player's
  > browser — the code ships inside the WebGL build and is visible in this
  > repository's source. It will keep casual visitors out of a beta, and
  > nothing more. Anything genuinely private must be protected by the host
  > (itch.io restricted page, HTTP basic auth, a private link), not by this
  > scene.
- **Hub** — a level-select room. Four doors on the east wall (Level 1 I/AM,
  Level 2 Cyber Defense, Levels 3–4 in development) plus an optional
  **Orientation** door on the west wall that loads the original Level 0 demo.
  Doors unlock in order: finishing Level *N* unlocks Level *N+1*
  (`Core/LevelProgress.cs`, persisted in PlayerPrefs). A concierge NPC
  comments on your progress.
- **Levels** — every level connects back: each has a gold **Return to Hub**
  door (self-added on the south wall in Level 0 / Cyber Defense, so even
  hand-saved scenes gain it), results screens offer **[R] replay / [H] return
  to Hub**, and all scene changes fade to black instead of hard-cutting.

**Required once per machine:** run the menu **CyVerse → Add Scenes To Build
Settings**. Scene loading (`SceneManager.LoadScene`) only works for scenes
registered in Build Settings; the menu registers the whole chain in the right
order (PasswordLock first, so it's also the entry scene of a build). If you
skip this, doors show a toast telling you to run it instead of failing silently.

Feature status: **Level 0** (Onboarding: I/AM, CIA Triad, NICE Roles) is
feature-complete; **Level 2 — Cyber Defense** (SIEM, EDR, Incident Response;
scene file `Level1.unity`, built before the renumbering) is a playable
blockout — see [below](#level-2-cyber-defense--blockout); **Level 1 — I/AM**
is a blockout of the new standard level template — see
[the level template](#level-1-iam--the-standard-level-template).

## Requirements

- **Unity 2022.3 LTS** (the project is pinned to `2022.3.40f1`; any `2022.3.x`
  works — Unity Hub will offer to open with your installed patch version).
- WebGL Build Support module (install via Unity Hub → Installs → Add Modules)
  when you're ready to build for the browser.

## Opening the project

1. Open **Unity Hub → Add → Add project from disk** and select this folder.
2. Open it with Unity 2022.3.x. Unity will import packages and generate the
   `Library/`, `ProjectSettings/` defaults, and `.meta` files on first launch.
3. Run the menu **CyVerse → Add Scenes To Build Settings** (once).
4. To play the full flow, open `Assets/Scenes/PasswordLock.unity` and press
   **Play**. To jump straight into a level, open its scene instead
   (`Hub.unity`, `Level0.unity`, `Level1_IAM.unity`, `Level1.unity`).

> **If `Level0.unity` won't open** in your Unity version, you don't need it:
> create a new empty scene (`File → New Scene → Empty`), add an empty
> GameObject, and attach the **`Level0Bootstrap`** script
> (`Assets/Scripts/Level/Level0Bootstrap.cs`). Press Play — the level builds
> itself. This is the only object the scene needs.

### Building an editable scene (recommended for tweaking)

To lay the level out and tune it by hand instead of at runtime:

1. `File → New Scene → Empty`.
2. Menu **CyVerse → Build Level 0 Scene**. This generates the whole level as
   real GameObjects (floor, walls, lights, player, systems, stations).
3. Move/retune/replace objects, swap materials, etc., then **File → Save**.
4. Press Play — it's fully playable. The systems self-wire: `Level0Manager`
   discovers the stations and `StationSetup` (on each station) loads its
   content from its **Topic** dropdown, so you can add/remove stations freely.

Don't keep a `Level0Bootstrap` object in a hand-built scene, or the level will
be built twice. Both paths use the same `SceneFactory`, so they look identical.

## Level 1 (I/AM) — the standard level template

`Assets/Scenes/Level1_IAM.unity` is the blockout of the layout every story
level will follow (the "8-step" template):

1. **Enter** the room from the Main Hub (door in the Hub's east wall).
2. The first room is a **video viewing room** with a **locked door** ahead.
3. A **Briefing Screen** (`Interaction/VideoStation.cs`) plays the lesson —
   `E` to play/pause/replay, `←`/`→` to **scrub**; a progress bar tracks it.
4. The locked door (`Interaction/LockedDoor.cs`) **unlocks after one complete
   viewing** (`VideoStation.FirstCompleted` → `LockedDoor.Unlock()`).
5. The video stays **repeatable and scrubbable** afterwards.
6. The door slides open into the **working space**.
7. The player completes the **tasks** there. I/AM is fully gamified — one
   hands-on mechanic per "A", not multiple-choice cards:
   - **Badge Enrollment** (Identification) — a kiosk issues your ID under
     your callsign. Must be done first: every other station refuses you
     until you have a badge, which *is* the lesson.
   - **MFA Vault** (Authentication) — clear three factors physically: type
     the daily passcode from the wall memo (something you KNOW), fetch the
     security token from its charger across the room and slot it (something
     you HAVE — `E` pick up, `Q` put down), and take the biometric pad scan
     (something you ARE). Three lights fill; the vault slides open.
   - **Data Triage** (Authorization) — carry four labelled data crates to
     the role pedestals that should have access (INTERN / HR / SYSADMIN).
     Wrong pedestal = denied with a least-privilege explanation.
   - **Audit Hunt** (Accountability) — an in-world access-log board: `↑`/`↓`
     move the highlight, `E` flags the anomalous entry; two rounds
     (role abuse, impossible travel).
   The four knowledge checks now form one **Certification Exam** terminal
   that unlocks after all tasks — the boss check, with streaks/combos.
8. Completion is **persisted** (`LevelProgress.MarkCompleted(1)`), the results
   screen shows, and the **exit door back to the Hub unlocks** — where the
   next level's door is now open.

   The carry mechanic (`Interaction/Carryable.cs` + `DropZone.cs`) and the
   typed-answer modal (`UI/TypingChallenge.cs`) are level-agnostic — reuse
   them for future levels' tasks.

`Level/Level1IamManager.cs` orchestrates the phases (Watch → Task → Complete);
`Level/Level1IamSceneFactory.cs` builds the two-room layout on BuildKit.
Build an editable copy via **CyVerse → Build Level 1 (I-AM) Scene** in an
empty scene, tweak, save.

**Swapping in a real video:** the Briefing Screen currently plays timed text
"slides" (`Level1IamContent.BriefingSlides()`) so the blockout needs no video
asset. `VideoStation` already supports real video: assign a `VideoClip` (or,
better for WebGL, a **URL** to an `.mp4` — streaming keeps the build small
and WebGL doesn't ship clip transcoding) on the component and it renders to
the screen via `VideoPlayer` + RenderTexture with the same unlock/scrub rules.

## The Hub

`Assets/Scenes/Hub.unity` — built by `Level/HubSceneFactory.cs`, editable copy
via **CyVerse → Build Hub Scene**. Doors are `Interaction/HubDoor.cs`: each
shows its status on the sign (green READY / red LOCKED / gold COMPLETE / grey
IN DEVELOPMENT) and loads its scene on `E`.

The hub is furnished as an **atrium**, not an office: a dais + ceiling halo
under the rotating holo core, lounge seating, plants framing the door bays,
SJSU banners on the north wall, and — new in the polish pass — **wayfinding**:
each door projects a floor guide strip and a slowly rotating landing pad in
its accent colour, plus an accent light over the doorway, so players can read
"which way to Level 1" from anywhere in the room. A free-standing **mission
board** faces the spawn point (built by `HubSceneFactory.BuildMissionBoard()`,
kept live by `Level/MissionBoard.cs`): it greets the player by callsign,
shows overall clearance, and lists every door with its current status.

Door → scene mapping lives in one table (`HubSceneFactory.Doors()`) that
drives the doors, the wayfinding, *and* the mission board — to add Level 3
later, point its entry at the new scene name and add the scene in
`CyverseSceneTools.AddScenesToBuildSettings`; everything else follows.

## Level 2: Cyber Defense (blockout)

A playable vertical-slice blockout of Room 2 from the concept tables — **SOC
Analyst / Protection & Defense** — teaching SIEM, EDR, and Incident Response,
gated by a **Threat Response Console** instead of the Security Scanner.
(Scene file: `Level1.unity` — named before the Hub renumbering; in the Hub
it is **Level 2** and unlocks after Level 1 I/AM.)

**Open it:** `Assets/Scenes/Level1.unity` and press Play, or build an editable
copy the same way as Level 0: `File → New Scene → Empty` → menu
**CyVerse → Build Level 1 Scene** → tweak → save. Upgrade menus:
**CyVerse → Add Threat Response Console** / **Add SOC Lead NPC**.

**How it shares Level 0's foundation — the `BuildKit` refactor:** the parts of
`SceneFactory` that had nothing to do with Level 0's story (materials,
signage, the room shell, the player rig, the common systems bundle, the
generic learning-station geometry) were extracted into `Level/BuildKit.cs`.
`SceneFactory` (Level 0) and `Level1SceneFactory` (Level 1) both build on top
of `BuildKit`, so neither reinvents the floor shader wiring, the hologram
signage, or the station geometry — they only supply their own room palette,
station topics/content, centerpiece, and completion gate.

To keep `StationSetup`, `QuizSystem`, and the glossary reusable across levels
without duplicating them, three call sites were generalized with **optional**
delegates that default to Level 0's original behavior (so Level 0 is
unaffected if left unset):
- `StationSetup.contentProvider` / `quizProvider` / `onReviewed` — a level's
  `SceneFactory` wires these per station instead of `StationSetup` hardcoding
  `Level0Content`/`Level0Quiz`/`Level0Manager`.
- `GuardNPC.Build(pos, rot, displayName, signText, linesProvider)` — the NPC
  shell (model, facing, breathing) is shared; each level supplies its own name
  and phase-aware dialogue. Level 1 reuses it as the "SOC Lead."
- `ResultsScreen.Show(..., headerText, grantedLine, nextMissionText, replaySuffix)`
  — same results card, level-specific copy.

`StationSetup.Topic` is one shared enum across all levels (`IAM/CIA/NICE` for
Level 0, `SIEM/EDR/INCIDENT` for Level 1) so the Quiz and Glossary systems
never need per-level duplication — extend this enum for future levels.
**Glossary entries are always appended, never inserted**, because
`GlossaryProgress` persists unlocked entries by raw array index; reordering
would corrupt returning players' saved progress.

This is a **blockout**: same room footprint and prop set as Level 0 (by
design — those coordinates are already playtested for clear sightlines),
distinguished mainly by palette (a "SOC Red" alert theme vs. Level 0's cyan)
and content. Treat art pass, unique layout, and custom furnishings as the
next iteration once the content and flow are validated.

## Controls

| Action            | Key             |
| ----------------- | --------------- |
| Move              | `W A S D`       |
| Look              | Mouse           |
| Interact          | `E`             |
| Advance dialogue  | `Space`         |
| Scrub video       | `←` / `→` (near a Briefing Screen) |
| Glossary          | `G`             |
| Settings / Pause  | `Esc`           |

In the settings menu: **Up/Down** select a row, **Left/Right** adjust the value.
Adjustable: master volume, voice volume, caption text size, look sensitivity.
Settings persist between sessions (PlayerPrefs).

## How it's built

Level 0 is assembled **in code at Play time** by `Level0Bootstrap` so the team
has a working slice without fighting scene merge-conflicts early on. It creates:

- A lit placeholder room (floor + 4 walls).
- A first-person player (CharacterController + camera).
- The HUD (captions, interaction prompt, objective banner, crosshair).
- The managers (dialogue, accessibility settings, level flow).
- Three learning stations: **I/AM**, **CIA Triad**, **NICE Roles**.

Reviewing all three stations triggers the **"Access Granted — Level: Employee"**
completion, matching the CyVerse Script.

### Script map (`Assets/Scripts/`)

| File                                   | Responsibility                                  |
| -------------------------------------- | ----------------------------------------------- |
| `Core/GameState.cs`                    | Global "are we busy?" flags                     |
| `Core/ScoreSystem.cs`                  | Points + quiz tallies (HUD subscribes)          |
| `Quiz/QuizSystem.cs`                   | Multiple-choice knowledge-check card (keys 1–3) |
| `Level/Level0Quiz.cs`                  | Question bank per topic (edit copy here)        |
| `Interaction/FaceScanner.cs`           | Security Scanner: face-scan completion gate     |
| `Level/Billboard.cs`                   | World-space signage faces the player            |
| `UI/ResultsScreen.cs`                  | End card: score, accuracy, time, [R] replay     |
| `Player/FirstPersonController.cs`      | WASD + mouse look                               |
| `Player/PlayerInteractor.cs`          | Raycast + interact prompt                       |
| `Interaction/IInteractable.cs`         | Interactable contract                           |
| `Interaction/InteractableStation.cs`   | A learning station                              |
| `Dialogue/DialogueManager.cs`          | Plays captioned (+ optional voiceover) beats    |
| `UI/HudUI.cs`                          | Builds the HUD in code                          |
| `Settings/AccessibilitySettings.cs`    | Esc menu: audio, caption scale, sensitivity     |
| `Level/BuildKit.cs`                    | **Shared foundation**: room shell, materials, signage, stations, player, common systems — used by every level |
| `Level/Level0Content.cs`               | All Level 0 narration text (edit copy here)     |
| `Level/Level0Manager.cs`               | Intro, station tracking, completion             |
| `Level/SceneFactory.cs`                | Level 0-specific construction (on top of BuildKit) |
| `Level/Level0Bootstrap.cs`            | Runtime entry point (calls SceneFactory)        |
| `Level/StationSetup.cs`                | Per-station topic/content + completion feedback  |
| `Editor/Level0SceneBuilder.cs`         | Menu: CyVerse > Build Level 0 Scene             |
| `Level/Level1Content.cs`               | Level 1 narration text (SIEM/EDR/Incident Response) |
| `Level/Level1Quiz.cs`                  | Level 1 knowledge-check question bank           |
| `Level/Level1Manager.cs`               | Level 1 phase flow (mirrors Level0Manager)      |
| `Level/Level1SceneFactory.cs`          | Level 1-specific construction (on top of BuildKit) |
| `Level/Level1Bootstrap.cs`            | Runtime entry point (calls Level1SceneFactory)  |
| `Interaction/Level1Gate.cs`            | Threat Response Console (Level 1's completion gate) |
| `Editor/Level1SceneBuilder.cs`         | Menu: CyVerse > Build Level 1 Scene             |
| `Level/Rotator.cs`                     | Slow spin for holograms / centerpiece           |
| `Level/PropFactory.cs`                 | Furniture/props: desks, lounge, racks, drones   |
| `Level/Hoverer.cs`                     | Drone bob/yaw/rotor motion (Reduce Motion aware)|
| `Level/VisualDirector.cs`              | Fog, glow sprites, dust, vignette (self-adds)   |
| `Player/FirstPersonHands.cs`           | Procedural gloved hands: bob, hover, reach      |
| `Level/SignFX.cs`                      | Sign bob + pulse + holo-glitch (self-adds)      |
| `Interaction/GuardNPC.cs`              | Security guard: faces player, phase-aware talk  |
| `UI/GlossaryPanel.cs`                  | G-key glossary card (pauses, keyboard-driven)   |
| `Level/GlossaryContent.cs`             | Glossary terms/definitions + topic tags         |
| `Level/GlossaryProgress.cs`            | Tracks unlocked/discovered glossary entries     |
| `UI/MainMenu.cs`                       | Title screen: callsign entry, ENTER to begin    |
| `Core/PlayerIdentity.cs`               | Persisted player callsign                       |
| `Core/ScoreSystem.cs`                  | Score + answer-streak combo multiplier          |
| `Audio/AmbientHum.cs`                  | Seamless procedural room tone loop              |
| `Audio/ProceduralAudio.cs`             | Generates footstep/click/confirm SFX at runtime |
| `Audio/Sfx.cs`                         | One-shot SFX, scaled by the SFX volume channel  |
| `UI/ScreenFader.cs`                    | Fade-from-black on start / fade-to-black        |
| `UI/ControlsOverlay.cs`                | First-time controls card (fades on first move)  |
| `Resources/Shaders/GridFloor.shader`   | Glowing tech-grid floor (`Cyverse/GridFloor`)   |
| `Resources/Shaders/Hologram.shader`    | Holographic panels (`Cyverse/Hologram`)         |
| `Core/LevelProgress.cs`                | Persisted per-level completion + unlock order   |
| `UI/PasswordLockController.cs`         | Entry scene: password terminal + memo lesson    |
| `Level/HubSceneFactory.cs`             | Hub construction (doors, concierge, centerpiece)|
| `Level/HubManager.cs` / `HubBootstrap.cs` | Hub flow / runtime entry point               |
| `Interaction/HubDoor.cs`               | Level-select door: status sign + scene loading  |
| `Level/MissionBoard.cs`                | Hub status board: callsign, clearance, per-level state |
| `Level/AmbientScreen.cs`               | Wall TV driver: headline ticker + animated chart |
| `Interaction/LockedDoor.cs`            | Sliding door, unlocked by an event              |
| `Interaction/VideoStation.cs`          | Briefing Screen: slides or real video, scrubbing|
| `Level/Level1IamContent.cs`            | I/AM briefing slides, station lines, quiz       |
| `Level/Level1IamSceneFactory.cs`       | Level 1 (I/AM) two-room construction            |
| `Level/Level1IamManager.cs` / `Level1IamBootstrap.cs` | Watch→Task→Complete flow / entry |
| `Editor/CyverseSceneTools.cs`          | Menus: Build Hub / Build L1 I-AM / Add Scenes To Build Settings |

## Gameplay loop (feature-complete Level 0)

0. **Title screen** — "CYVERSE / Press ENTER to Begin"; type to set your
   **callsign** (pre-filled with the script's default, replaced on first
   keystroke, persists between sessions). Gameplay and the controls card hold
   until dismissed. Room tone (a generated facility hum) plays under everything.
1. **Arrive** — fade in, controls card, security-guard intro (captioned).
2. **Review** — visit the three signed stations (I/AM Kiosk, CIA Triad, NICE
   Roles). Each plays its lesson, then asks a **knowledge-check** question
   (answer with `1`/`2`/`3`; a wrong answer shows the explanation). Reviewing a
   station also **unlocks its glossary terms** — press `G` any time to browse
   what you've discovered ("X/21 discovered"); undiscovered terms show as
   "??? (locked)" until you visit the right station.
3. **Authenticate** — once all stations are reviewed, the **Security Scanner**
   activates. Press `E` there for the face scan → *"Access Granted — Level:
   Employee"*, per the CyVerse Script.
4. **Results** — score, security clearance grade, rank title, percentile vs.
   other recruits, quiz accuracy, best streak, time, and your persistent
   **best score** (gold "NEW BEST!" when beaten), with `[R]` to replay.

Scoring: station review **+50**, knowledge check **+100** correct / **+25**
attempted, face scan **+100** (max **550**, before combos). The score counter
(top right) pops when points land, and a **progress ring** (top left) tracks
station completion — it starts pre-filled at 15% ("orientation complete,
you're already underway") rather than empty, so the visible gap to 100%
always looks smaller. Questions are drawn from a per-topic pool in
`Level0Quiz.cs` so replays vary; educators can edit copy there without
touching gameplay code.

**Answer streaks:** consecutive correct answers build a combo multiplier —
2 in a row is x1.5, 3+ is x2 — with an ascending confirm chime and a gold
"COMBO x2!" toast. A wrong answer resets the streak (but still awards partial
credit). Your best streak is shown on the results screen.

## UI exclusivity standard (one menu at a time)

`GameState.AnyMenuOpen` is the single source of truth for "a full-screen
menu/modal owns the screen" (title, settings, glossary, quiz, results —
dialogue captions are gameplay, not a menu). The rules every UI element
follows:

1. **Modals never stack.** Anything that wants to open checks first: the
   settings menu's Esc only toggles when settings is already open or nothing
   owns the screen; the glossary's G is gated on `!GameState.Busy`; quizzes
   can only trigger through interaction (blocked while busy).
2. **Passive overlays hide under modals.** The controls card fully hides
   (and freezes its timers) while `AnyMenuOpen` is true, and resumes when
   the screen is free — it can never overlap the title screen or any menu.
3. **Shared keys can't double-fire.** `GameState.MenuTransitionFrame` records
   the frame a menu opened/closed; the Esc press that closes the glossary is
   ignored by settings that same frame instead of immediately reopening a
   different menu.

Follow the same three rules when adding any new menu or overlay.

## UX & accessibility features

Game-feel: animated crosshair (grows + tints on a target, kicks on press),
a popping "E" key badge, typewriter dialogue (Space completes, then advances),
hover-glow on targeted station holograms, floating "+points" popups, particle
bursts on station review / scan / completion, an animated scan-bar during the
face scan, pitch-varied footsteps, ambient server blips, drifting patrol
drones, a fade-from-black on start, and a green ✓ checkmark (a *shape*, not
colour alone) when a station is reviewed. The results screen awards a themed
Security Clearance grade (S/A/B/C) and teases Level 1 — Cyber Defense.

Accessibility (Esc menu, all persisted): separate **Master / Voice / SFX**
volume channels, a **Voiceover (TTS)** toggle, **caption text scaling**,
**look sensitivity**, **field of view** (motion-sickness comfort), and
**Reduce Motion** (freezes shader animation, hologram spin, HUD pulses, and
screen fades for photosensitivity).

**Text-to-speech:** in WebGL builds, any dialogue line without a recorded
clip is read aloud through the browser's Web Speech API
(`Assets/Plugins/WebGL/WebSpeech.jslib`) — free, key-less, and
offline-capable via the player's local OS voices, with per-speaker pitch
(guard lower, System brighter). Recorded `AudioClip`s always take priority,
captions always remain on, and skipping a line cancels its speech. In the
editor and desktop builds TTS is unavailable and silently skipped.
Captions cover all narration, the menu is keyboard-navigable, and opening it
pauses the game. Reduce Motion uses a global shader float `_CyMotion`.

## Extending Level 0

- **Edit the narration:** all copy lives in `Level/Level0Content.cs`.
- **Add voiceover:** drop `AudioClip`s in and pass them to the `DialogueLine`
  constructor (the 3rd argument). Captions already display regardless.
- **Use real art:** replace the primitives spawned in `Level0Bootstrap` with
  prefabs. Each station just needs a Collider + the `InteractableStation`
  component configured with its dialogue.
- **Move to a hand-built scene:** once the layout is settled, place the player,
  HUD systems, and station prefabs directly in the scene and delete the
  bootstrap. The components are designed to work either way.

## Building for WebGL

1. `File → Build Settings → WebGL → Switch Platform`.
2. **Select the SJSU-branded page**: `Player Settings → Resolution and
   Presentation → WebGL Template → CyVerse`. The built game then ships inside
   a branded shell (SJSU Blue `#0055A2` header, Spartan Gold `#E5A823`
   loading bar and accents, fullscreen button, offline-safe system fonts).
3. Run **CyVerse → Add Scenes To Build Settings** so all five scenes are in
   the build, PasswordLock first (it's the entry scene).
4. **Player Settings → Publishing Settings → Compression Format:** `Brotli`
   (smaller downloads; needs HTTPS hosting) or `Gzip`.
5. Keep textures compressed and the build lean for low-bandwidth / remote users.
6. `Build` and host the output folder on any static web server.

## Web deployment (updating the live web version)

The repo ships a GitHub Actions workflow —
`.github/workflows/webgl-deploy.yml` — that builds WebGL in the cloud
([game-ci](https://game.ci)) and publishes to **GitHub Pages**, so nobody
needs a local WebGL module to update the site.

**One-time setup:**

1. **Unity license secrets** (game-ci needs to activate Unity headlessly).
   Follow https://game.ci/docs/github/activation to obtain your personal
   `.ulf` license file, then add three repository secrets under
   *Settings → Secrets and variables → Actions*:
   `UNITY_LICENSE` (the file's full contents), `UNITY_EMAIL`, `UNITY_PASSWORD`.
2. **Enable Pages**: *Settings → Pages → Source:* deploy from the `gh-pages`
   branch (created by the first successful run).
3. In Unity, run **CyVerse → Add Scenes To Build Settings** and select the
   **CyVerse** WebGL template, then commit `ProjectSettings/` — the cloud
   build uses whatever is committed.

**Updating the site after that:** push your changes to `main`, then open the
**Actions** tab → *WebGL Build & Deploy* → **Run workflow**. A few minutes
later the game is live at `https://<owner>.github.io/<repo>/`. (The workflow
is manual-trigger by design — Unity cloud builds take ~15–30 min and you
usually don't want one per commit; the first run is slowest, later runs reuse
the cached `Library/`.)

**Manual alternative** (no Actions setup): build locally per the section
above and drag the output folder onto any static host — itch.io (create an
HTML5 project, upload a zip of the build), Netlify Drop, or SJSU web space.
For itch.io/Netlify set compression to `Gzip` unless the host serves the
`Content-Encoding: br` header.

## Visual style / shaders

Level 0 ships a procedural "high-tech, lived-in working space" look, built in
`Level0Bootstrap`:

- **Grid floor** — `Cyverse/GridFloor` surface shader: dark, glossy, with
  emissive cyan grid lines driven by world position. Tune `_LineColor`,
  `_GridScale`, `_Emission`, `_Metallic`, `_Smoothness` on the material.
- **Holograms** — `Cyverse/Hologram` transparent/additive shader: Fresnel rim,
  scrolling scanlines, flicker. Used on the station panels and the rotating
  centerpiece. Tune `_Color`, `_ScanDensity`, `_ScanSpeed`, `_RimPower`.
- **Emissive ceiling panels + neon floor trim** and **colored point lights** at
  each station, against low ambient so the glow reads strongly.

A runtime **VisualDirector** (self-added by Level0Manager, so it upgrades
saved scenes too) supplies the cinematography: dark solid-colour sky,
exponential fog, 4x MSAA, additive glow sprites on every point light (fake
bloom), drifting dust motes (skipped under Reduce Motion), and a subtle
screen vignette. Fresh builds additionally get a ceiling slab, wall accent
strips and columns, rotating holo rings under the stations and core, and a
"CYVERSE" title sign — **rebuild the scene via CyVerse > Build Level 0 Scene
to pick these up** in an editor-authored scene.

Shaders live under `Assets/Resources/` so they're always included in WebGL
builds. The bootstrap falls back to emissive Standard materials if a custom
shader fails to compile, so the scene always runs.

This is a stylised placeholder, not final art — swap the primitives for real
models/prefabs as they're produced. For a closer match to the concept boards
(reflective surfaces, richer materials), consider adding a baked **Reflection
Probe** in the room and, longer term, moving to URP.

## Render pipeline

The project uses the **Built-in Render Pipeline** for simplicity and broad
WebGL compatibility. The custom shaders above are written for Built-in RP; if
you migrate to URP later they'll need porting (Shader Graph equivalents), but
the emissive Standard fallbacks port automatically.
