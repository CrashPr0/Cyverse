# Forensics Room (Level 3) — Rework Inventory + Edit Plan

**Status:** READ-ONLY inventory + edit plan. No gameplay/scene/prop code was changed this round.
**HEAD at time of writing:** `f41a18e` (origin/main).
**Scope note:** The working tree carries a large uncommitted `GameplayActions` refactor (~48 files) from another
session. This document does not touch, stage, or describe changes to that drift. Only this notes file is committed.

The Forensics room is **code-generated at runtime** — there is no `.unity` scene file for the layout. The room and its
stations are built entirely in C# under `Assets/Scripts/Level/` and `Assets/Scripts/Interaction/`.

---

## 1. Files that build the Forensics room, its stations, and screens

The room is built by a small chain of files. Entry point → factory → realization → polish:

| File | Role |
|---|---|
| `Assets/Scripts/Level/Level3ForensicsBootstrap.cs` | Runtime entry. `Awake()` calls `Level3ForensicsSceneFactory.BuildAll()`. (14 lines) |
| `Assets/Scripts/Level/Level3ForensicsSceneFactory.cs` | Builds the room shell + rooms + systems. `BuildAll()` (L18–33), `BuildVideoRoom()` (L44–63), `BuildTaskRoom()` (L65–94), `BuildSystems()` (L35–42). |
| `Assets/Scripts/Level/Level3ForensicsSceneRealization.cs` | `Realize()` (L26–48) ensures the intake form + station exist and wires bindings for the manager. |
| `Assets/Scripts/Level/Level3ForensicsPolish.cs` | The "finish layer": builds the workflow zones, the **REPORT station**, floor pads, path beams, lights, and rewrites station headings. `BuildLab()` (L96–107), `BuildReportingZone()` (L163–187). (397 lines) |
| `Assets/Scripts/Level/Level3ForensicsManager.cs` | Flow/phase state machine (Watch → Investigate → Report → Complete); subscribes to station events. (266 lines) |

### Stations the room spawns today

| Station | Built by | Position | Screen surface today |
|---|---|---|---|
| **Briefing TV** (analyst briefing) | `Level3ForensicsSceneFactory.BuildVideoRoom()` L46–47 → `VideoStation.Build(new Vector3(0,0,-6), 0f, …)` | (0, 0, -6) | **World-space Quad** (`VideoStation` — diegetic-style, uses a VideoPlayer RenderTexture OR slide text on a `Screen` quad). This is already a world surface, not a HUD. |
| **LEFT — 01 Evidence Intake / Chain of Custody** | `ChainOfCustodyStation.Build(new Vector3(-11.5f,0,8.15f), 0f, …)` (via `Ensure()` L42–46, called from `Level3ForensicsSceneRealization.Realize()` L29) | (-11.5, 0, 8.15) | Physical plinth/tablet props, BUT the form + phone open as a **2D screen-space HUD overlay** (see §2). |
| **MIDDLE — 02 Analyze / Forensic Terminal** | `ForensicsConsole.Build(new Vector3(0,0,10), 0f, …)` in `Level3ForensicsSceneFactory.BuildTaskRoom()` L68 | (0, 0, 10) | Three world-space **Quad** monitors (`MonScreen_-1/0/1`, `ForensicsConsole.cs` L166–169) that are just static emissive quads; the actual terminal UI (`QueryTerminal`) opens as a **2D HUD modal**. |
| **RIGHT — 04 Forensic Report** | `Level3ForensicsPolish.BuildReportingZone()` L163–187 → `reportDesk.AddComponent<ForensicsReportStation>()` L177; monitor/screen cubes L178–187 | (9.2, 0, ~11) | `DF_ReportScreen` is a static emissive **Cube** (L181–182); status text is world `TextMeshPro`. No interactive screen — pressing E just submits the report. |
| Evidence pinboard | `EvidenceBoard.Build(new Vector3(-8,0,15), …)` L71 (dressing, "03 Correlate") | (-8, 0, 15) | World board, not a rework target. |

**Screens the room has today:** Briefing TV screen (world Quad), 3 console monitor quads (world, static), report monitor
(world cube, static), plus the intake **phone** which is a 2D HUD panel. The three rework targets are LEFT, MIDDLE, RIGHT
as mapped above.

---

## 2. LEFT screen: the 2D phone UI and the SHA-256 field

All in **`Assets/Scripts/Forensics/ChainOfCustodyForm.cs`** (540 lines). The whole form is a **2D screen-space HUD** — it
is parented to `HudUI.Instance.Canvas` (L108–115 in `Build()`), i.e. a full-screen overlay, **not** a world/RenderTexture
surface.

### The 2D phone UI ("Forensic Acquisition Phone")

- **Built by `BuildAcquisitionPhone()` — L356–419.** Creates a `GameObject "EvidenceAcquisitionPhone"` as a `RectTransform`
  panel anchored to the top-right of the card (`phoneRect`, L362–367), with title, source line, a transfer progress
  track/fill, status, and a fingerprint line — all `TMP_Text` / `Image` UI elements on the HUD canvas.
- **Animated by `DownloadRoutine()` — L438–470** (a coroutine that slides the phone panel in and fills the transfer bar),
  kicked off by `BeginEvidenceDownload()` — L421–436. `ShowDownloadedPhone()` — L472–479 restores the finished state.
- The phone panel fields (`phonePanel`, `phoneRect`, `transferFillRect`, `phoneSource`, `phoneStatus`, `phoneFingerprint`)
  are declared at **L52–54**.

### The SHA-256 / hash references

1. **`ChainOfCustodyForm.cs:198`** — the `INTEGRITY CHECK` form field, one of its dropdown options:
   ```csharp
   fields.Add(MakeField("INTEGRITY CHECK",
       new[] { "Filename visually checked", "SHA-256 hash verified", "No hash required" }, 1));
   ```
   (This field is defined in `ConfigureFields()`, L182–200. Correct answer index `1` = "SHA-256 hash verified".)

2. **`ChainOfCustodyForm.cs:432`** — the phone "IMAGE FINGERPRINT" readout, computed with Unity's `Hash128`:
   ```csharp
   phoneFingerprint.text = "IMAGE FINGERPRINT\n" + Hash128.Compute(payload).ToString().ToUpperInvariant();
   ```
   (Inside `BeginEvidenceDownload()`; `payload` is `source + "|" + activity`.)

> Note: there is a third, unrelated hash mention in `Assets/Scripts/Forensics/QueryTerminal.cs:540`
> (`"… disk image · SHA-256 verified · chain of custody intact"` flavour string on the terminal). It is **not** part of
> the custody form and is out of scope for the LEFT rework, but flagged here so the follow-up round can decide whether to
> keep that flavour text consistent.

---

## 3. How station screens are rendered today + the reusable diegetic pattern

### Current rendering

- **Chain-of-custody form + phone (LEFT):** 2D **screen-space HUD** overlay on `HudUI.Instance.Canvas`. Not diegetic.
- **Forensic Terminal (MIDDLE):** the console builds **world-space Quad monitors** (`ForensicsConsole.cs` L160–169) but
  they are **static emissive quads** — the actual `QueryTerminal` interface is a **2D HUD modal**. So the *props* are
  world-space; the *screen content* is not diegetic.
- **Report (RIGHT):** static emissive **cube** screen + world `TextMeshPro` status. No live screen; not diegetic.
- **Briefing TV:** genuinely world-space via `VideoStation` (`Screen` Quad, RenderTexture from a `VideoPlayer`, or slide
  text). This is the closest existing example of a world screen for full-motion / text content.

### The reusable DIEGETIC-SCREEN pattern (what we reuse)

**`Assets/Scripts/Interaction/DiegeticPhone.cs`** (406 lines) is the canonical RenderTexture diegetic-screen pattern in
the repo. The shape to copy:

1. **Physical prop** — `BuildProp()` L106–150: a dock + upright body + a `Screen` **Quad** slightly proud of the shell
   front. The screen quad sits on the body's **local -Z** face; identity rotation faces a viewer standing on -Z (see the
   VideoStation convention below).
2. **Offscreen UI render pipeline** — `BuildOffscreenUi()` L156–246:
   - A `RenderTexture` (`RtWidth` 256 × `RtHeight` 384, L64–65) bound to the screen quad via an **`Unlit/Texture`**
     material so it reads as self-lit (L187–192).
   - A **WorldSpace `Canvas`** holding the UI widgets, plus a **private orthographic `Camera`**, both parented under an
     `offscreenRoot` that is **unparented and pinned thousands of units away** (`OffscreenOrigin = (10000,-1000,10000)`,
     L44) so no other geometry is ever in the render camera's shot.
   - A **dedicated layer** for the offscreen canvas: the RT camera's `cullingMask` renders **only that layer**
     (L235–236), and `ExcludeLayerFromMainCamera()` (L307–312) removes that layer from the **main camera's** culling mask
     so the WorldSpace canvas is never drawn into the game view. Layer is auto-picked from a free slot
     (`ResolvePhoneUiLayer()` L286–299) so no TagManager edit is needed.
   - `uiCamera.enabled = false` — renders **on demand** via `RenderNow()` (L378–382), called only when content changes.
     Chosen deliberately because Cyverse ships to WebGL (zero per-frame cost).
3. **Drive API** — `ShowCode()`/`SetCode()`/`SetStatus()`/`Hide()` (L349–372) update the canvas widgets and re-render.
4. **Lifecycle** — a `static DiegeticPhone Active` (L37) lets a shared UI find the current phone; `OnDestroy()`
   (L392–405) releases the RT and destroys the unparented offscreen root (no per-reload leak).

**`VideoStation.cs` quad-facing convention** (referenced repeatedly in `DiegeticPhone` and `VideoStation.Build()`
L360–366): **Unity's `Quad` renders on its LOCAL -Z face**, so **identity rotation already faces a viewer standing on
-Z**. A 180° yaw backface-culls the surface (invisible screen). Every diegetic screen in this room should therefore keep
the screen quad at identity rotation and place the viewer/approach on -Z (all Level3 stations are approached from the
south / -Z; `Level3ForensicsPolish.CreateWorldText()` L349–351 documents the same "-Z is the readable side" rule).

**Reuse strategy:** generalize the `DiegeticPhone` RT-screen recipe into station screens. The cheapest path is a small
shared helper (e.g. `DiegeticScreen` builder, or lifting the offscreen-canvas/RT/camera/layer wiring out of
`DiegeticPhone` into a reusable component) that each station's screen quad binds to. The MIDDLE terminal can render its
`QueryTerminal` content into that RT; LEFT and RIGHT render their own custody/upload canvases.

---

## 4. Where the diegetic phone lives today + docking it at a Forensics station

- **Current home (I/AM MFA vault):** `Assets/Scripts/Interaction/MfaGauntlet.cs:211`:
  ```csharp
  DiegeticPhone.Build(terminalPos + new Vector3(-0.9f, 0f, -0.2f), 0f, accent);
  ```
  It sits on a dock just left of the KNOW passcode terminal in the MFA vault. It is registered as `DiegeticPhone.Active`.
- **Who drives it:** `Assets/Scripts/UI/TypingChallenge.cs:93` grabs `DiegeticPhone.Active` and calls
  `ShowCode()`/`SetStatus()` to light the OTP on the phone screen during the auto-type; falls back to a HUD panel if no
  diegetic phone exists (L98–106). The phone is **display-only** — no in-world tap interaction.
- **What it would take to also dock/animate it at a Forensics "plug-in" station (RIGHT):**
  - The phone is built per-scene and registered as `Active`; Level 3 does **not** build one today. The RIGHT station rework
    would build/relocate a phone prop at the plug-in dock (reuse `DiegeticPhone.BuildProp()` geometry, or a slimmer
    docked variant) at the RIGHT desk (~(9.2, 0, 11)).
  - Because `DiegeticPhone` currently exposes only `ShowCode/SetCode/SetStatus/Hide` (no move/dock animation), the docking
    animation is **new**: a coroutine that lerps the phone prop's transform from a "handheld"/approach pose into the dock
    slot, then plays an "upload" progress on its RT screen (reuse the transfer-bar idea from `ChainOfCustodyForm`'s
    `DownloadRoutine()`, but rendered onto the diegetic RT instead of the HUD). No literal connect/tap — the animation
    plays automatically after custody is accepted.
  - Trigger: hook the animation to `ChainOfCustodyForm.Completed` (already an event, `ChainOfCustodyForm.cs:37`) or the
    manager's `OnCustodyCompleted()` (`Level3ForensicsManager.cs:120–129`), which fires exactly once when custody is
    accepted — the natural "after chain of custody" moment.

---

## Concrete per-station EDIT PLAN

### LEFT — Evidence Intake / Chain of Custody: drop the 2D phone + SHA-256, go location/handler-based + diegetic

Target file: **`Assets/Scripts/Forensics/ChainOfCustodyForm.cs`**, plus station prop in
`Assets/Scripts/Interaction/ChainOfCustodyStation.cs`.

1. **Remove the 2D phone UI.**
   - Delete the phone panel construction: `BuildAcquisitionPhone()` (L356–419) and its call site at the end of `Build()`
     (`BuildAcquisitionPhone();` L153).
   - Delete the phone animation: `BeginEvidenceDownload()` (L421–436), `DownloadRoutine()` (L438–470),
     `ShowDownloadedPhone()` (L472–479), and their call sites in `Open()` (L94–97).
   - Delete the phone fields: `phonePanel, phoneRect, transferFillRect, phoneSource, phoneStatus, phoneFingerprint`
     (L52–54), `evidenceDownloaded` (L57), `transferRoutine` (L58), and the `IsEvidenceDownloaded` property (L47).
   - Remove the `evidenceDownloaded` gate in `Validate()` (L262–269) — the form no longer waits on a phone download.
   - Update the intro text at L128–130 (currently "The Evidence Intake phone downloads the workstation image first…") to
     describe a location/handler custody log instead.
2. **Remove SHA-256 / device-contents framing.** In `ConfigureFields()` (L182–200), replace the current device-contents
   fields with **WHO handled / WHERE it came from / custody transfers** fields. Concretely:
   - Delete the `Hash128.Compute` fingerprint use (was L432, removed with the phone).
   - Replace the `INTEGRITY CHECK` field (L197–198, the SHA-256 option) and reframe the others. Proposed field set
     (keep 4 fields so `FieldCount`/objective text at `Level3ForensicsManager.cs` L184, L246 stay correct):
     - `COLLECTED FROM (LOCATION)` — e.g. "SOC evidence locker", "User's desk, Bldg 2", "Unknown / undocumented" (correct = a documented location).
     - `RECEIVING HANDLER` — e.g. "Named on-call analyst (signed)", "Left on desk, unsigned", "Anonymous drop" (correct = named + signed).
     - `CUSTODY TRANSFER LOGGED` — e.g. "Transfer signed by both parties", "Verbal handoff only", "Not logged" (correct = signed transfer).
     - `CUSTODY ACTION` — keep the existing "Seal, log, and transfer" answer (currently L199–200, correct index 1).
   - Keep `MakeField(label, options, correctIndex)` shape (L202–203) — only the content changes.
3. **Make the screen diegetic.** Move the custody form off `HudUI.Instance.Canvas` (L108–115) onto a world-space
   RenderTexture surface using the §3 pattern:
   - Add a screen **Quad** to `ChainOfCustodyStation.Build()` (`ChainOfCustodyStation.cs` L48–67) at the intake plinth,
     identity rotation (faces -Z approach), replacing/augmenting the `CustodyTablet` cube (L57–59).
   - Render the custody form canvas into an RT via the reusable diegetic-screen helper (see §3 "reuse strategy"), instead
     of parenting the card to the HUD canvas. Interaction stays as the existing `ChainOfCustodyForm.Open()` modal flow;
     only the render target changes.

### MIDDLE — Forensic Terminal: keep role, make screen diegetic

Target files: **`Assets/Scripts/Interaction/ForensicsConsole.cs`** + `Assets/Scripts/Forensics/QueryTerminal.cs`.

1. Keep `ForensicsConsole`'s role and the `QueryTerminal.Open(...)` flow (`ForensicsConsole.cs` L108–125) unchanged — the
   terminal still owns the two `InvestigationCase`s and the `LogDatabase`.
2. The three monitor quads already exist as world Quads (`ForensicsConsole.cs` L165–169, `MonScreen_-1/0/1`). Replace the
   **center** monitor's static emissive material with a **RenderTexture-backed diegetic screen** (the §3 pattern): render
   the `QueryTerminal` content canvas into an RT bound to `MonScreen_0`. Keep the two side monitors as ambient dressing
   (or render subordinate panels onto them in a later pass).
3. No gameplay change — `QueryTerminal` logic is untouched; only its presentation target moves from the HUD modal to the
   RT surface. (If the full terminal UX is too large to fit the RT this round, at minimum drive a live "case status /
   query result" readout onto the center monitor so it reads as a working terminal.)

### RIGHT — "Plug-in" station: docking animation (phone docks → uploads), diegetic screen

Target files: **`Assets/Scripts/Interaction/ForensicsReportStation.cs`** + `Assets/Scripts/Level/Level3ForensicsPolish.cs`
(`BuildReportingZone()` L163–187), reusing **`DiegeticPhone.cs`**.

1. **Add a plug-in dock + diegetic screen.** In `BuildReportingZone()` (L163–187), replace the static `DF_ReportScreen`
   emissive cube (L181–182) with a diegetic RT screen (Quad, identity rotation, §3 pattern) and add a small dock slot on
   the desk for the phone to land in.
2. **Build/relocate a phone prop here.** Reuse `DiegeticPhone.BuildProp()` geometry for the RIGHT desk (~(9.2, 0, 11)).
   Since the MFA vault phone is Level-1/IAM only and Level 3 builds none today, this is a fresh build for the Forensics
   scene (does not disturb `MfaGauntlet.cs:211`).
3. **Add a docking ANIMATION (new code).** Add a coroutine — either on `ForensicsReportStation` or a new small
   `PlugInStation` component — that:
   - lerps the phone prop transform from a raised/approach pose into the dock slot (reuse the `SmoothStep` slide pattern
     from `ChainOfCustodyForm.DownloadRoutine()` L440–450 as a reference, applied to a world transform);
   - then plays an "UPLOADING…" progress on the diegetic RT screen (reuse the transfer-fill idea, rendered to the RT).
   - **No literal connect/tap interaction** — the sequence plays automatically.
4. **Trigger point.** Fire the docking animation from the "after chain of custody" moment: subscribe to
   `ChainOfCustodyForm.Completed` (`ChainOfCustodyForm.cs:37`) or hook `Level3ForensicsManager.OnCustodyCompleted()`
   (`Level3ForensicsManager.cs` L120–129). Report submission (`ForensicsReportStation.Interact` L36–58 →
   `manager.SubmitReport()`) stays as-is; the animation is presentation layer only.

---

## Follow-up decomposition (suggested next-round work items)

1. **Shared diegetic-screen helper** — lift the offscreen-canvas + RT + private-camera + layer-isolation wiring out of
   `DiegeticPhone.cs` into a reusable `DiegeticScreen` component/builder. Prereq for all three station reworks.
2. **LEFT rework** — rip out the 2D phone + SHA-256, reframe fields to location/handler/transfer, render the custody form
   onto a diegetic station screen.
3. **MIDDLE rework** — render `QueryTerminal` (or a live status readout) onto the center console monitor RT.
4. **RIGHT rework** — plug-in dock + diegetic screen + phone docking/upload animation, triggered on custody accepted.
