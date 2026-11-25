# CyVerse — Setup

A web-based (WebGL), desktop, first-person cybersecurity learning game. No VR.
This document covers opening, running, and extending the levels.

## Game flow

The game boots into the Hub and moves through a scene chain:

```
Hub  →  Level 1 (I/AM) → Level 2 (Cyber Defense) → Level 3 → Level 4
 (entry scene, level select)           (unlock in order)
```

- **PasswordLock** — an optional access-code gate, **off in release builds**:
  the game now opens in the Hub. The scene still ships, so to gate a build
  (a private beta, say) move it to the top of *File → Build Settings*. When
  it is the entry scene it is the first thing a player sees, fully **diegetic**:
  a 3D security vestibule (fixed camera with idle sway) where the login
  prompt renders on a physical terminal monitor, the memo is a plaque
  beside it, and the gate to the Hub is a real door that slides open on
  ACCESS GRANTED before the camera fades through. Two modes on the
  `PasswordLockController` component:
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
- **Hub** — a level-select room. Four doors on the east wall (Level 1
  Identity & Access, Level 2 Cyber Defense, Level 3 Digital Forensics,
  Level 4 Cyber Attack) plus an optional **Orientation** door on the west
  wall that loads Level 0. Doors unlock in order: finishing Level *N*
  unlocks Level *N+1* (`Core/LevelProgress.cs`). A concierge NPC comments on
  your progress, and a first-visit onboarding asks for a callsign and walks
  the player through look, walk and talk.
- **Fresh run on every load.** Each page reload (or entering Play mode)
  wipes progress, unlocks, scores, the callsign and the glossary
  (`Core/PlaythroughReset.cs`); settings are kept. Settings (Esc) →
  **Start Over** does the same without a reload, for study sessions.
- **Levels** — every level connects back: each has a gold **Return to Hub**
  door, the results card's **[Enter] Continue** goes to the Hub (Level 2
  goes straight on to the Forensics Lab), and all scene changes fade to
  black instead of hard-cutting.

### Visual-pass scenes

`Level0 Visual Pass`, `Level1_IAM_VisualPass` and
`Level2_CyberDefense_VisualPass` are earlier hand-dressed copies of their
levels. **They no longer ship**: `Core/SceneCatalog.cs` now maps every
variant (and the legacy `Level1` name) back to the maintained scene, and
**CyVerse → Add Scenes To Build Settings** leaves them out. Level art is
added by load-time polish passes (`HubPolish`, `Level0Polish`,
`Level2SocPolish`, `Level3ForensicsPolish`, `LevelCohesion` last) and by the
prefab hook below, not by editing these copies.

### Art prefabs (preferred over baking art into a scene)

Baking models into a saved scene means the art serves that one scene: new
levels start ugly, and gameplay changes have to be hand-merged into a huge
`.unity` file. Prefabs avoid both — the factory picks them up in **every**
level, including ones not built yet.

Drop a prefab at `Assets/Resources/Props/<Name>.prefab` and the matching
builder uses it automatically. If it's absent the builder falls back to its
primitive version, so props can be migrated **one at a time** and a missing
prefab can never break a build.

Names the factory looks for:

| Prefab name | Replaces | Footprint (m, W×D×H) |
| ----------- | -------- | -------------------- |
| `Desk`        | desk + monitor + keyboard | 1.5 × 0.75 × 0.8 |
| `Chair`       | office chair              | 0.5 × 0.5 × 1.1 |
| `Couch`       | lounge couch              | 2.1 × 0.85 × 1.0 |
| `CoffeeTable` | lounge table              | 1.15 × 0.62 × 0.45 |
| `Rug`         | lounge rug                | (flat) |
| `Plant`       | potted plant              | 0.6 × 0.6 × 1.3 |
| `ServerRack`  | server cabinet            | 0.85 × 0.7 × 2.2 |
| `LockerBank`  | 4-door lockers            | 2.0 × 0.5 × 1.9 |
| `Crate`       | one stackable crate       | 0.62 cube |
| `Reception`   | reception counter         | 2.8 × 1.5 × 1.1 |
| `Drone`       | patrol drone              | ~1.0 × 1.0 × 0.3 |

**Authoring rules:**
- **Pivot at floor level**, centred on the footprint (the factory positions
  props by their base, not their middle).
- **+Z is the front** — the side a person faces, sits at, or reads.
- Keep roughly to the footprint above; layouts are spaced to avoid collisions.
- Put **colliders on anything solid**; the factory does not add them.
- `Drone` is special: it gets animated at runtime. Name any spinning children
  `Rotor*` and `Hoverer` will drive them.

Not prefab-swappable yet (they own generated text/behaviour): wall TVs and
the gameplay stations (badge kiosk, MFA vault, audit board, forensic desk).

`PropLibrary.ClearCache()` forgets lookups if you add prefabs mid-session.

**Required once per machine:** run the menu **CyVerse → Add Scenes To Build
Settings**. Scene loading (`SceneManager.LoadScene`) only works for scenes
registered in Build Settings; the menu registers the seven shipping scenes
in order (Hub first, so it is the entry scene of a build, then PasswordLock,
Level0, Level1_IAM, Level2_CyberDefense, Level3_Forensics,
Level4_CyberAttack). If you skip this, doors show a toast telling you to run
it instead of failing silently.

Feature status: all five levels (Orientation and Levels 1–4) are playable
end to end. Most props and all level architecture are still primitive
block-outs built in code; see `HANDOFF_OPUS.md`.

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
4. To play the full flow, open `Assets/Scenes/Hub.unity` and press
   **Play**. To jump straight into a level, open its scene instead
   (`Level0.unity`, `Level1_IAM.unity`, `Level2_CyberDefense.unity`,
   `Level3_Forensics.unity`, `Level4_CyberAttack.unity`).

> **If `Level0.unity` won't open** in your Unity version, you don't need it:
> create a new empty scene (`File → New Scene → Empty`), add an empty
> GameObject, and attach the **`Level0Bootstrap`** script
> (`Assets/Scripts/Level/Level0Bootstrap.cs`). Press Play — the level builds
> itself. This is the only object the scene needs.

### Playtest metrics

Every play session has a lightweight flight recorder (`Core/PlaytestMetrics`)
that emits one JSON line per meaningful event with the prefix
`[PLAYTEST_METRIC]`. It records scene loads, objective changes, interaction
attempts, score awards, level results, and runtime errors; it never records the
player's callsign or typed answers.

- In the Unity Editor and development builds, filter the Console/browser
  Console for **`[PLAYTEST_METRIC]`** and save the lines with the test report.
  Release builds do not print them; the events are still stored locally.
- Desktop builds also append to
  `Application.persistentDataPath/cyverse-playtest-metrics.jsonl`.
- WebGL keeps a bounded JSON snapshot in PlayerPrefs under
  `cv_playtest_metrics_json`, so it survives a scene change and browser reload
  on the same profile.

**Export Data** (Esc → Export Data) saves everything kept in this browser
as a file: ← / → pick CSV or JSON, Enter or a click saves. It reads the full
`.jsonl` log (every session on this browser, oldest first) and falls back to
the current session's trail. On WebGL it is a normal browser download built in
the page; nothing is uploaded. In the editor it is written to
`persistentDataPath/exports/`. The CSV columns are `sessionId, playthrough,
utc, seconds, scene, eventName, detail`.

Each session has a `sessionId`, and every event includes a `playthrough`
number (1 at page load, +1 at each Start Over, so participants sharing one tab
stay separate), the UTC time, the active scene, elapsed seconds, event name,
and compact detail text. This makes a manual
playtest easy to correlate with a browser crash or a Unity test artifact
without requiring a telemetry server.

### Automated visual QA

The editor-side visual QA harness scans every scene under `Assets/Scenes`
without saving or changing it. It reports machine-readable JSON plus a short
text summary in `Build/VisualQA` and checks for missing cameras/lights,
out-of-envelope renderers, suspicious world-text scale/material/depth and
text/text overlap, missing interactable colliders, and disabled/degenerate aim
volumes. The asset reference scene (`PropsAndAssets`) is treated as a library,
not as a gameplay room.

Run it from **CyVerse → Visual QA → Scan All Scenes**, or from a terminal:

```sh
CYVERSE_VISUAL_QA_OUTPUT=/tmp/cyverse-visual-qa \
  /Applications/Unity/Hub/Editor/2022.3.40f1/Unity.app/Contents/MacOS/Unity \
  -batchmode -nographics -projectPath . \
  -executeMethod Cyverse.Editor.VisualQaHarness.RunBatch -quit
```

Set `CYVERSE_VISUAL_QA_SCENES=Level2_CyberDefense_VisualPass,Level3_Forensics`
to narrow a run, `CYVERSE_VISUAL_QA_STRICT=1` to fail on hard findings, or
`CYVERSE_VISUAL_QA_CAPTURE=1` (without `-nographics`) to render PNGs for
serialized scenes. Captures are best-effort and never modify the scenes.

Procedural room builders are checked after startup by the optional
`VisualQaRuntimeCaptureTests` PlayMode test. It is a no-op during normal CI;
enable it when you want post-bootstrap frames for representative scenes:

```sh
CYVERSE_VISUAL_QA_RUNTIME_CAPTURE=1 \
  CYVERSE_VISUAL_QA_RUNTIME_OUTPUT=/tmp/cyverse-runtime-captures \
  /Applications/Unity/Hub/Editor/2022.3.40f1/Unity.app/Contents/MacOS/Unity \
  -batchmode -projectPath . -runTests -testPlatform PlayMode \
  -testFilter Cyverse.Tests.VisualQaRuntimeCaptureTests
```

The runtime capture test waits for each bootstrap to settle, verifies that a
`MainCamera` exists, disables player drift, and writes a deterministic room
overview. Levels 1–3 also produce focused station frames (briefing; SOC alert,
south/north workstation views, playbook and overview; Forensics console,
report and custody)
so text and puzzle spacing can be reviewed without walking every room.
Use the normal Development WebGL screenshot tour for focused views of a board,
workstation row, briefing screen, or custody form; the harness is intended to
surface candidates for human review rather than decide art direction itself.

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
   - **MFA Vault** (Authentication) — first hear the MFA Specialist's
     briefing, then clear three factors physically: type the secret
     passcode (something you KNOW; a new player's starter passcode is on
     the in-world phone beside the terminal, `Interaction/DiegeticPhone.cs`), fetch the
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
8. Completion is recorded (`LevelProgress.MarkCompleted(1)`) for the rest
   of the run, the results card shows, and **[Enter] Continue** returns to
   the Hub, where the next level's door is now open.

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
shows its status on the sign (green READY / red LOCKED / gold COMPLETE) and
loads its scene on `E`. The Hub names and marks the next story door.

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
drives the doors, the wayfinding, *and* the mission board — to add a level,
point a new entry at its scene name and add the scene in
`CyverseSceneTools.AddScenesToBuildSettings`; everything else follows.

## Level 3: Digital Forensics Lab

`Assets/Scenes/Level3_Forensics.unity` (generated in code) is a forensics
lab in three stations: **01 Evidence Intake** (fill in the chain-of-custody
entry for the seized phone), **02 Analyze Evidence** (dock the phone, image
it through a write blocker, then the **Evidence Explorer**: a Process step
that sets up the case, then browsing and tagging artifacts on the phone
image, including a witness's phone under OTHER SOURCES) and **03 Forensic
Report**. A five-question knowledge check follows the report. Played after
Level 2, it investigates that level's culprit; entered directly, it uses a
training device.

The full walkthrough, answer key and scoring for instructors are in
[`Docs/Level3-Forensics-Guide.md`](Docs/Level3-Forensics-Guide.md). Code:
`Forensics/EvidenceExplorer*.cs`, `Forensics/PhoneImage.cs`,
`Forensics/InvestigationCase.cs`, `Level/Level3Forensics*.cs`.

## Level 4: Cyber Attack

`Assets/Scenes/Level4_CyberAttack.unity` (generated in code) is a big-tech
employee game room where each attack step is an arcade machine: Spot the
Phish, Phish Frenzy, the claw machine (Access Grabber) and Network Attack.
A correct pick plays a short bonus round; the normal run is untimed. Spec:
[`Docs/LEVEL4_CYBER_ATTACK_GAMEPLAY_SPEC.md`](Docs/LEVEL4_CYBER_ATTACK_GAMEPLAY_SPEC.md).
Finishing Levels 1–4 offers an end-of-game certificate on the Level 4
results card, which can be saved as a PNG.

## Level 2: Cyber Defense — gamified

`Assets/Scenes/Level2_CyberDefense.unity` — **SOC Analyst / Protection &
Defense**, on the two-room template, with one hands-on mechanic per concept
instead of review stations:

- **SOC alert board** (`Interaction/SiemConsole.cs`) — flag one of four
  event rows, walk to that computer, compare the alert with its live
  activity, and give a verdict: **[1] IT MATCHES — FALSE ALARM** or
  **[2] NO MATCH — POSSIBLY REAL**. App names in the data are generic (no
  real products).
- **EDR containment** (`Interaction/EndpointStation.cs`) — five workstations
  whose screens always show their running processes. Read them, press `E` to
  isolate the compromised ones (a double-extension `invoice_2026.pdf.exe`, an
  encoded `powershell.exe -enc`). Isolating a *healthy* machine is allowed but
  warns about service disruption and costs the clean-sweep bonus — the real
  trade-off analysts face.
- **IR playbook** (`Interaction/PlaybookStation.cs`) — carry six response cards
  onto a sequence board **in order** (Preparation → Detection → Containment →
  Eradication → Recovery → Lessons Learned). Slots fill strictly left to right
  and only accept the genuine next step, because doing Eradication before
  Containment reinfects the network and Recovery before Eradication restores
  the malware with the backup.
- **Certification Exam** — unlocks after all three; four knowledge checks with
  streaks/combos.

Par score **1280** (`Level2Content.ParScore`). Content lives in
`Level/Level2Content.cs` — alerts, endpoints, playbook steps and exam
questions are all editable data.

**Open it:** `Level2_CyberDefense.unity` and press Play, or build an editable
copy via **CyVerse → Build Level 2 (Cyber Defense) Scene**.

> The old `Level1.unity` (the first SOC level) no longer ships;
> `SceneCatalog` routes any door that still names it to
> `Level2_CyberDefense`.

### Grading

Grades are relative to each level's **par score**, not a fixed number — an
`S` needs 95% of par, `A` 82%, `B` 68%. Pars: Level 0 **550**, Level 1 (I/AM)
**1100**, Level 2 **1280**, Level 3 (Forensics) **2300** (1800 plus 100 per
knowledge-check question), Level 4 **1280**. Hard runs scale par by the
Hard score multiplier. The best score is kept per level. Pass `parScore:` to
`ResultsScreen.Show()` when adding a level, or every run will grade as an S.

**How it shares Level 0's foundation — the `BuildKit` refactor:** the parts of
`SceneFactory` that had nothing to do with Level 0's story (materials,
signage, the room shell, the player rig, the common systems bundle, the
generic learning-station geometry) were extracted into `Level/BuildKit.cs`.
`SceneFactory` (Level 0) and `Level1SceneFactory` (Level 1) both build on top
of `BuildKit`, so neither reinvents the floor shader wiring, the hologram
signage, or the station geometry — they only supply their own room palette,
station topics/content, centerpiece, and completion gate.

To keep `StationSetup`, `QuizSystem`, and the glossary reusable across levels
without duplicating them, the shared systems use serialized profiles/topics
with optional runtime delegates for custom overrides:
- `StationSetup.contentProvider` / `quizProvider` / `onReviewed` — a level's
  `SceneFactory` may wire these at runtime. Saved scenes fall back to the
  serialized `topic`, so Unity domain reloads and player builds retain the
  correct Level 0 or Cyber Defense content, quiz, and manager callback.
- `GuardNPC.Build(..., dialogueProfile, linesProvider)` — the NPC shell (model,
  facing, breathing) is shared. The serializable profile retains the Concierge
  or SOC Lead dialogue in editor-built scenes; a delegate can still override it
  at runtime.
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
| Next briefing slide | `E` (at the Briefing Screen) |
| Continue (quiz explanation, results card) | `Enter` |
| Glossary          | `G`             |
| Settings / Pause  | `Esc` (`Tab` when the browser is full screen) |

In the settings menu: **Up/Down** select a row, **Left/Right** adjust the value.
Adjustable: master, voice and SFX volume, voiceover (browser text-to-speech,
off by default), caption text size, look sensitivity, field of view and
Reduce Motion. The menu also has **Start Over** and **Credits**. Settings
persist between sessions (PlayerPrefs) and survive Start Over.

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
| `Level/SecurityPlusContent.cs`         | CompTIA Security+ practice bank (5 NICE categories, 294 Qs) |
| `UI/SecurityPlusTerminal.cs`           | Security+ Prep modal: role select + MCQ practice |
| `Interaction/SecurityPlusKiosk.cs`     | The physical Security+ Prep Terminal prop        |
| `Level/StationSetup.cs`                | Per-station topic/content + completion feedback  |
| `Editor/Level0SceneBuilder.cs`         | Menu: CyVerse > Build Level 0 Scene             |
| `Level/Level1*.cs` (no `Iam`), `Interaction/Level1Gate.cs` | The legacy first SOC level; its scene no longer ships |
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
| `Forensics/EvidenceExplorer*.cs`       | Level 3 Process step + artifact browser (modal) |
| `Forensics/PhoneImage.cs`              | The seized phone's artifacts                    |
| `Forensics/InvestigationCase.cs`       | Case findings, answers + briefing slides        |
| `Interaction/ForensicsConsole.cs`      | Investigation Desk prop; owns case + database    |
| `Level/Level3Forensics*.cs`            | Level 3 factory / manager / bootstrap            |
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
4. **Results** — score, security clearance grade, rank title, quiz
   accuracy, best streak, time, and the **best score** for this level in
   this run (gold "NEW BEST!" when beaten), with `[Enter]` to continue.

Scoring: station review **+50**, knowledge check **+100** correct / **+25**
attempted, face scan **+100** (max **550**, before combos). The score counter
(top right) pops when points land, and a **progress ring** (top left) tracks
station completion — it starts pre-filled at 15% ("orientation complete,
you're already underway") rather than empty, so the visible gap to 100%
always looks smaller. Questions are drawn from a per-topic pool in
`Level0Quiz.cs` so replays vary; educators can edit copy there without
touching gameplay code.

### Security+ Prep Terminal (optional study kiosk)

A fourth interactable near the centerpiece (0, 0, 16.5): the **Security+ Prep
Terminal**, contributed by Dr. Rocca as five spreadsheets of CompTIA
Security+ practice questions, one per **NICE Workforce Framework** category
(Design & Development, Implementation & Operations, Investigation, Oversight
& Governance, Protection & Defense — 294 questions total). Press `E`, pick a
category with `1`-`5`, then answer definition-matching questions with `1`-`4`;
`Tab` steps back a screen (category select, then closed). Questions are
served from a shuffled, no-repeat bag per category, and each category tracks
a **personal best accuracy %** (PlayerPrefs), shown next to it on the select
screen.

This is deliberately **separate from level scoring** — it never touches
`ScoreSystem.Score` and has no bearing on completing Level 0. It's a study
aid, not a gate. All content lives in `Level/SecurityPlusContent.cs`, which
is generated from the source spreadsheets rather than hand-typed; add a
category or more questions by re-running the extraction against updated
`.xlsx` files (same struct shape: prompt, 4 options, correct index, concept)
rather than editing the generated file directly.

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
   the frame a menu opened/closed; the key press that closes the glossary is
   ignored by settings that same frame instead of immediately reopening a
   different menu.

Follow the same three rules when adding any new menu or overlay.

## UX & accessibility features

Game-feel: animated crosshair (grows + tints on a target, kicks on press),
a popping "E" key badge, typewriter dialogue (Space completes, then advances),
hover-glow on targeted station holograms, floating "+points" popups, particle
bursts on station review / scan / completion, an animated scan-bar during the
face scan, pitch-varied footsteps, ambient server blips, drifting patrol
drones, and a fade-from-black on start. The results screen awards a themed
Security Clearance grade (S/A/B/C). The game ships its own font
(`Assets/Resources/Fonts/CyverseSans.ttf`, loaded by `HudUI.LoadFont`)
because Unity's built-in font is a 332-character subset in WebGL builds;
check new symbols render in a WebGL build before using them.

Accessibility (Esc menu, all persisted): separate **Master / Voice / SFX**
volume channels, a **Voiceover (TTS)** toggle, **caption text scaling**,
**look sensitivity**, **field of view** (motion-sickness comfort), and
**Reduce Motion** (freezes shader animation, hologram spin, HUD pulses, and
screen fades for photosensitivity).

**Text-to-speech:** off by default (Settings → Voiceover turns it on). When
on, in WebGL builds, any dialogue line without a recorded
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
3. Run **CyVerse → Add Scenes To Build Settings** so all seven scenes are
   in the build, Hub first (it's the entry scene).
4. **Player Settings → Publishing Settings → Compression Format:** `Gzip` with
   **Decompression Fallback** ON. (`Brotli` needs a host that sends
   `Content-Encoding: br`; GitHub Pages does not — see *Web deployment*.)
5. Keep textures compressed and the build lean for low-bandwidth / remote users.
6. `Build` and host the output folder on any static web server.

## Web deployment (GitHub Pages)

The site is published from the **`gh-pages`** branch by two workflows that
write to different folders, so neither erases the other:

| Workflow | Publishes | To | Needs secrets? |
| --- | --- | --- | --- |
| `pages.yml` | `web/` (landing page) | site root | **No** |
| `webgl-deploy.yml` | the Unity WebGL build | `/play/` | Yes (Unity licence) |

That split matters: the landing page goes live **without** Unity CI configured,
and stays up if a game build ever fails. The landing page checks whether
`/play/` exists and shows "Build not published yet" instead of a dead link.

**One-time setup (the only manual step):**

> **Settings → Pages → Source: "Deploy from a branch" → branch `gh-pages` →
> folder `/ (root)`**

The `gh-pages` branch is created by the first run of `pages.yml`, which fires
automatically on any push that touches `web/`. If the branch doesn't exist
yet, run it once from the **Actions** tab. The site then lives at
`https://<owner>.github.io/<repo>/`.

### Getting the Unity CI secrets

The cloud build needs to activate Unity headlessly, which takes three
repository secrets. For a **Unity Personal** licence (the free one, and what
a student account normally is):

1. **Actions** tab → **"Unity — Request Activation File"** → *Run workflow*.
   (That workflow exists only for this; delete it afterwards.)
2. Open the finished run, download the artifact, and unzip it — you'll get
   `Unity_v2022.x.alf`.
3. Go to <https://license.unity3d.com/manual>, sign in, upload the `.alf`,
   choose **Unity Personal** → it returns a `.ulf` file.
4. **Settings → Secrets and variables → Actions → New repository secret**:

   | Secret | Value |
   | --- | --- |
   | `UNITY_LICENSE` | the **entire contents** of the `.ulf` file (it's XML — open it in a text editor and paste everything) |
   | `UNITY_EMAIL` | your Unity account email |
   | `UNITY_PASSWORD` | your Unity account password |

5. Delete `.github/workflows/unity-activation.yml` — it's single-use.

Then run **WebGL Build & Deploy**.

**If SJSU gives you a Unity Pro / Education seat instead**, activation is by
serial rather than licence file: set `UNITY_SERIAL` (plus `UNITY_EMAIL` and
`UNITY_PASSWORD`) and swap `UNITY_LICENSE` for `UNITY_SERIAL` in
`webgl-deploy.yml`. Worth asking your department first — a Pro serial is
less fiddly than the `.alf`/`.ulf` round trip.

Notes:
- The `.ulf` is a licence credential. Keep it in Actions secrets; never commit
  it, and don't paste it into chat or an issue.
- A Personal licence covers a limited number of activations. CI consumes one
  seat, so if your local editor later asks you to reactivate, that's why —
  signing in again fixes it.
- The activation file is tied to the Unity version, so if the project moves
  off `2022.3.40f1` you'll need to redo this.

**Publishing the game.** Once the three secrets exist, builds are
**automatic**: any push to `main` that touches `Assets/`, `Packages/` or
`ProjectSettings/` builds WebGL and publishes it to `/play/`. Pushes that
only change docs or `web/` are skipped, so a typo fix never costs a
half-hour build. You can still trigger one by hand from the Actions tab
(*WebGL Build & Deploy → Run workflow*).

If several commits land quickly, the newer build cancels the older one
rather than queueing.

> **Before the first automated build, run CyVerse → Add Scenes To Build
> Settings and commit `ProjectSettings/`.** The cloud build ships exactly the
> scenes registered there — with a stale list it will happily publish a game
> missing levels, on every push.

**Pull requests** run `.github/workflows/pr-checks.yml` when they touch
`Assets/`, `Packages/` or `ProjectSettings/`: the Play Mode tests, then a
WebGL build attached to the run as the `webgl-build` artifact. Both must be
green before merging, since merging to `main` deploys.

Editing the landing page is just editing `web/index.html`; pushing to `main`
redeploys it.

**Public URLs** (project site, no backend): landing page
`https://crashpr0.github.io/Cyverse/`, game `https://crashpr0.github.io/Cyverse/play/`.

**Pages-specific rules.**
- Player Settings must stay: Compression `Gzip` + **Decompression Fallback ON**
  (Pages sends no `Content-Encoding` for `.br`/`.gz`; the workflow fails the
  build if it finds them), Threads Support OFF (needs COOP/COEP headers Pages
  can't send).
- `webgl-deploy.yml` publishes landing page + game as a single orphan commit on
  `gh-pages`, so the branch never accumulates history (builds are ~25 MB each).
- A failing PlayMode test blocks the deploy on push. To publish anyway:
  *Actions → WebGL Build & Deploy → Run workflow → skip_tests*.

### Embedding on another site (Cybers Learning)

Link button (most reliable — fullscreen and mouse-look work best in the
game's own tab):

```html
<a href="https://crashpr0.github.io/Cyverse/play/" target="_blank" rel="noopener"
   style="display:inline-block;padding:14px 32px;background:#0055A2;color:#fff;
          font:700 16px/1 system-ui,sans-serif;text-decoration:none;border-radius:8px">
  Play CyVerse
</a>
```

Inline `<iframe>` (the game page has a header and footer around a 16:9 canvas,
so give it a bit more height than 16:9):

```html
<iframe src="https://crashpr0.github.io/Cyverse/play/"
        title="CyVerse - cybersecurity training game"
        style="display:block;width:100%;max-width:1100px;height:760px;margin:0 auto;border:0;background:#000"
        allow="fullscreen; autoplay" allowfullscreen loading="lazy"></iframe>
<p><a href="https://crashpr0.github.io/Cyverse/play/" target="_blank" rel="noopener">Open in its own tab</a>
   for fullscreen and the best mouse-look.</p>
```

Notes: do not add a `sandbox` attribute (if the host requires one it needs at
least `allow-scripts allow-same-origin allow-pointer-lock`); if the host site
sets a Content-Security-Policy, allow `frame-src https://crashpr0.github.io`.
Click inside the game once so it takes keyboard focus. Safari/Firefox may
partition or block storage for cross-site iframes, so saved progress and the
asset cache can reset there — another reason to offer the link button.

**Manual alternative:** build WebGL locally and drag the output onto any
static host — itch.io (HTML5 project, upload a zip), Netlify Drop, or SJSU web
space. Use `Gzip` compression unless the host sends `Content-Encoding: br`.

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
