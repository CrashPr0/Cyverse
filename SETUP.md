# CyVerse — Setup

A web-based (WebGL), desktop, first-person cybersecurity learning game. No VR.
This document covers opening, running, and extending the levels. **Level 0**
(Onboarding: I/AM, CIA Triad, NICE Roles) is feature-complete; **Level 1**
(Cyber Defense: SIEM, EDR, Incident Response) is a playable blockout built on
the same shared foundation — see [Level 1 (blockout)](#level-1-cyber-defense--blockout) below.

## Requirements

- **Unity 2022.3 LTS** (the project is pinned to `2022.3.40f1`; any `2022.3.x`
  works — Unity Hub will offer to open with your installed patch version).
- WebGL Build Support module (install via Unity Hub → Installs → Add Modules)
  when you're ready to build for the browser.

## Opening the project

1. Open **Unity Hub → Add → Add project from disk** and select this folder.
2. Open it with Unity 2022.3.x. Unity will import packages and generate the
   `Library/`, `ProjectSettings/` defaults, and `.meta` files on first launch.
3. Open the scene: `Assets/Scenes/Level0.unity`.
4. Press **Play**.

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

## Level 1: Cyber Defense (blockout)

A playable vertical-slice blockout of Room 2 from the concept tables — **SOC
Analyst / Protection & Defense** — teaching SIEM, EDR, and Incident Response,
gated by a **Threat Response Console** instead of the Security Scanner.

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
3. Add `Assets/Scenes/Level0.unity` to **Scenes In Build**.
3. **Player Settings → Publishing Settings → Compression Format:** `Brotli`
   (smaller downloads; needs HTTPS hosting) or `Gzip`.
4. Keep textures compressed and the build lean for low-bandwidth / remote users.
5. `Build` and host the output folder on any static web server.

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
