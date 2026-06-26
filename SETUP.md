# CyVerse — Level 0 Setup

A web-based (WebGL), desktop, first-person cybersecurity learning game. No VR.
This document covers opening, running, and extending **Level 0**.

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

## Controls

| Action            | Key             |
| ----------------- | --------------- |
| Move              | `W A S D`       |
| Look              | Mouse           |
| Interact          | `E`             |
| Advance dialogue  | `Space`         |
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
| `Player/FirstPersonController.cs`      | WASD + mouse look                               |
| `Player/PlayerInteractor.cs`          | Raycast + interact prompt                       |
| `Interaction/IInteractable.cs`         | Interactable contract                           |
| `Interaction/InteractableStation.cs`   | A learning station                              |
| `Dialogue/DialogueManager.cs`          | Plays captioned (+ optional voiceover) beats    |
| `UI/HudUI.cs`                          | Builds the HUD in code                          |
| `Settings/AccessibilitySettings.cs`    | Esc menu: audio, caption scale, sensitivity     |
| `Level/Level0Content.cs`               | All Level 0 narration text (edit copy here)     |
| `Level/Level0Manager.cs`               | Intro, station tracking, completion             |
| `Level/Level0Bootstrap.cs`            | Assembles the whole level + visual style        |
| `Level/Rotator.cs`                     | Slow spin for holograms / centerpiece           |
| `Audio/ProceduralAudio.cs`             | Generates footstep/click/confirm SFX at runtime |
| `Audio/Sfx.cs`                         | One-shot SFX, scaled by the SFX volume channel  |
| `UI/ScreenFader.cs`                    | Fade-from-black on start / fade-to-black        |
| `UI/ControlsOverlay.cs`                | First-time controls card (fades on first move)  |
| `Resources/Shaders/GridFloor.shader`   | Glowing tech-grid floor (`Cyverse/GridFloor`)   |
| `Resources/Shaders/Hologram.shader`    | Holographic panels (`Cyverse/Hologram`)         |

## UX & accessibility features

Game-feel: animated crosshair (grows + tints on a target, kicks on press),
a popping "E" key badge, footstep audio, UI/confirm sounds, a fade-from-black
on start, and a green ✓ checkmark (a *shape*, not colour alone) when a station
is reviewed.

Accessibility (Esc menu, all persisted): separate **Master / Voice / SFX**
volume channels, **caption text scaling**, **look sensitivity**, **field of
view** (motion-sickness comfort), and **Reduce Motion** (freezes shader
animation, hologram spin, HUD pulses, and screen fades for photosensitivity).
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
2. Add `Assets/Scenes/Level0.unity` to **Scenes In Build**.
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
