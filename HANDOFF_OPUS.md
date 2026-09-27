# HANDOFF — Cyverse fix-up / polish pass (for Cloud Opus 5.5)

You are picking up a Unity project that ships to **WebGL**, called **Cyverse**. This is a
zero-context handoff: read this whole file before touching anything. Items are ordered by
value and risk — do the top item first.

**Standing rules (non-negotiable):**
- Commit author is **Christopher Anthony Velez <lilvelezcav@gmail.com>**.
- **Never push to `main` without explicit permission.**
- Stage files by **explicit path**, never `git add .` — there is a large uncommitted refactor
  in the working tree (see the co-mingle item) that must not be swept into an unrelated commit.
- **Run `Tools/compile-check.sh` before every push.** It compiles the runtime scripts and the
  PlayMode tests without Unity (details at the bottom). Main went red once already because
  nobody could compile in a cloud session.

---

## Status — 2026-09-27 pass (branch `claude/eloquent-edison-itnih2`)

| Item | State |
| --- | --- |
| 0. Main does not compile | **Fixed on branch.** Needs merge to main. |
| 1. Placeholder model quality | Not started. Needs the Unity editor / a DCC tool; a cloud session can't do it. |
| 2. Co-mingle conflict | Partly defused: `TypingChallenge.cs` no longer depends on the refactor. Reconcile steps below. |
| 3. Diegetic phone render | 3 code bugs fixed. **Still not render-verified**, and players currently never see the phone (see 3b). |
| 4. L3 up-arrow leak | Still held. Exact patch written out below, ready to apply. |

---

## 0. DONE ON BRANCH — main did not compile (CI red)

`61ad959` committed `TypingChallenge.cs` implementing `IGameplayActionTarget` and calling
`GameplayActions.TryApply(...)`, `GameplayAction.*` and `GameplayActionKind.*`. **None of
those types exist in the repo.** They live only in the uncommitted refactor (item 2). Unity
compiles every runtime script into one assembly, so the whole project failed to compile:
Play mode, the TAS runners and the WebGL build were all dead on main.

CI confirmed it: [WebGL Build & Deploy run #20](https://github.com/CrashPr0/Cyverse/actions/runs/36285926001)
failed in `playthrough-test` with exactly these two errors and nothing else:

```
TypingChallenge.cs(21,51): error CS0246: 'IGameplayActionTarget' could not be found
TypingChallenge.cs(151,30): error CS0246: 'GameplayAction' could not be found
```

**Fix (chosen by the project owner, "strip the plumbing"):** `TypingChallenge.Update` now
calls its own `Close` / `Submit` / `Append` / backspace logic directly, and the class no longer
implements `IGameplayActionTarget`. All phone-OTP behavior from `61ad959` is kept: auto-typing,
Esc-cancels-during-auto-type, the diegetic screen and the HUD fallback. Nothing outside the file
called `TryApply`. `Tools/compile-check.sh` passes for release and development, runtime and
tests.

---

## 1. TOP PRIORITY — Blockout / placeholder model quality

The whole recent effort replaced greybox primitive block-outs with real prefabs, but **many
props and ALL level architecture are still low-fidelity placeholders**. This is the
highest-value work: upgrading blockout model quality is what makes the project read as a real
environment instead of a greybox.

**Props still placeholder (procedural primitives / crude prefabs):**
- Couches / Corner-Rugs pods
- WorkPodA / WorkPodB desk pods
- The diegetic phone (procedural primitive body — see item 3)
- Coffee tables
- Plants
- Server racks
- Kiosk / pedestal terminals

**Level ARCHITECTURE — the biggest gap.** The generated rooms are currently just a **tiled
plane + a grid + floating text**. None of the built environment exists as real geometry:
- Doorway / portal frames (the Hub level has explicit portal slots)
- Modular wall panels
- Floor tiles / trim
- Ceiling + light strips
- Pillars / columns
- Podiums / plinths (these are the MFA authenticator pedestals)
- Vault / security doors
- Reception desks

### The swap mechanism (this is your leverage — understand it before modeling)
- **Code-gen levels** (Level3, Level4, Level1_IAM): real prefabs drop into
  `Assets/Resources/Props/` and are spawned by **`PropFactory` / `PropLibrary.TrySpawn`**.
  Add the prefab under that Resources path and the factory picks it up — no scene edit needed.
- **Baked scenes** (Hub, Level0, Level1, Level2): swap the placeholder directly in the scene.

### Modeler / prefab conventions (a violation here caused a real magenta bug)
- **Real-world scale** (metres).
- **Pivot at floor-center** (X/Z centered, Y at the base) so props drop cleanly onto the floor.
- **Consistent −Z forward.** Unity Quads render on their **local −Z** face; identity rotation
  faces a viewer standing on the −Z side. Author models so their front is −Z.
- **Materials must be ASSIGNED.** A null/missing material renders **magenta** in build — this
  was a real shipped bug. When checking a fixture, enumerate **all** child renderers, not just
  the one you expect; the magenta can be on a differently-named child.
- **Low-poly / WebGL-friendly** geometry and textures.
- **Screen-bearing objects need a SEPARATE screen-quad / mesh face** for RenderTexture or
  emissive binding (terminals, the phone, kiosks). Don't paint the screen onto the body mesh.

---

## 2. HIGH STAKES — Co-mingle conflict (resolve deliberately, not blindly)

Commit **`61ad959`** (diegetic phone) **co-mingled another session's UNCOMMITTED
GameplayActions / phone refactor** into:
- `TypingChallenge.cs`
- `MfaGauntlet.cs`
- `MfaFactor.cs`

There is **ALSO a large uncommitted refactor still sitting in the working tree right now**
(~30 files), including:
- `QueryTerminal.cs`
- `Level3ForensicsManager.cs`
- `Level4CyberAttackManager.cs`
- `GameplayAction.cs`
- `DeterministicGameplayAdapter.cs`

When that working-tree refactor is finally committed it **WILL conflict** with `61ad959`
(they touch overlapping GameplayActions / terminal code). Resolve this **deliberately** —
read both sides, understand which change is authoritative per method, and reconcile by hand.
Do **not** blindly accept-theirs / accept-ours, and do **not** `git add .` the refactor into
an unrelated commit.

**Update (2026-09-27):** item 0 removed the GameplayActions routing from `TypingChallenge.cs`,
so the committed tree no longer references any refactor type. That changes the reconcile:

1. **Commit `GameplayAction.cs`, `DeterministicGameplayAdapter.cs` and every file that uses
   them in ONE commit.** Splitting them is exactly how main broke. Run
   `Tools/compile-check.sh` on that commit before pushing.
2. Pulling this branch will **not** conflict on `TypingChallenge.cs` (the refactor's working
   tree doesn't modify it). It will silently leave TypingChallenge outside the action system.
   If the refactor needs TypingChallenge drivable by `DeterministicGameplayAdapter` / TAS,
   re-add `IGameplayActionTarget` and `TryApply`. The removed version is at
   `git show 61ad959:Assets/Scripts/UI/TypingChallenge.cs`, lines 119–190. Keep the
   `autoTyping` guard: only Cancel may apply while the phone is auto-typing.
3. `MfaGauntlet.cs` / `MfaFactor.cs` from `61ad959` compile standalone and need no change
   unless the refactor also edits them.

---

## 3. HIGH STAKES — Unverified diegetic phone render

The I/AM MFA "diegetic phone" (`Assets/Scripts/Interaction/DiegeticPhone.cs`) is
**CODE-VERIFIED but NOT render-verified** — no in-Editor screenshot was ever captured (the
worker sessions had no Unity MCP tools, so nobody saw it run).

Latest fix **`f415c68`** addressed orientation / scale / RT-camera-layer isolation:
- Offscreen UI canvas unparented to a far-away scene-root object; a dedicated free layer for
  the phone UI; RT camera `cullingMask` = only that layer; **main camera masked against that
  layer** so the world-space canvas no longer bleeds into the game view; phone body rotation
  set upright (was a −20° lean); `OnDestroy` cleans up the unparented objects.

**You (or a human in Play mode) must actually verify** the phone renders **upright,
phone-sized, with the OTP legible ON its screen** in the I/AM MFA vault. The level is
code-generated at runtime, so entering Play mode rebuilds the vault from the current code —
that is the verification. If it's still wrong, the render math in `DiegeticPhone.cs` is where
to look (RT camera framing, canvas layer, screen-quad orientation).

### 3a. Fixed on branch (static review, 2026-09-27)

A line-by-line review of `DiegeticPhone.cs` checked the geometry: the quad's visible face and
the terminal both face −Z, the RT camera looks +Z at an un-mirrored canvas, ortho framing
matches the canvas, and layer 8 is free in `TagManager.asset`. It also found three real bugs:

1. **Stale RT content.** `RenderNow()` called `uiCamera.Render()` right after changing text,
   but uGUI only rebuilds canvas geometry at end of frame. Every render showed the *previous*
   state: the OTP appeared one update late, and "CODE ACCEPTED BY DEVICE" never showed.
   Fixed with `Canvas.ForceUpdateCanvases()` before `Render()`.
2. **Idle screen never hid.** `Build()` called `Hide()` before setting `built = true`, and
   `SetVisible()` no-ops until `built`, so the idle phone showed a lit "----" screen. Fixed by
   reordering.
3. **Squashed UI.** The screen quad is 0.176 × 0.36 m (aspect 0.49) but the RT/canvas was
   256 × 384 (0.67), so everything drew ~27% too narrow. The RT is now 256 × 524 and the canvas
   220 × 450, with ortho size derived from the canvas height.

### 3b. Players never see the phone right now

The handoff above assumes I/AM is code-generated. **The shipped path is not.** The Hub's saved
door (`Hub.unity`, `sceneName: Level1_IAM_VisualPass`), Build Settings and `SceneCatalog`
all send players to the **baked `Level1_IAM_VisualPass.unity`**. That scene was saved before
the phone existed and contains no `DiegeticPhone`. There, `MfaGauntlet.Wire()` hides the old
`PasscodeMemo`, `DiegeticPhone.Active` is null, and `TypingChallenge` falls back to the HUD
corner panel. So:

- To verify the phone itself, open **`Assets/Scenes/Level1_IAM.unity`** (procedural)
  directly and press Play. Walk to the MFA vault (west wall) and press E on the PASSCODE
  terminal. Check that the phone is upright and phone-sized left of the terminal, the screen is
  dark before interaction, "SPARTAN AUTHENTICATOR" plus the 4-digit code appear immediately,
  the status goes DAILY OTP → ENTERING CODE… → CODE ACCEPTED, and the screen goes dark after.
  Also confirm there's no floating panel in the room.
- To verify what players get, play through the Hub into the visual pass. Expect the HUD
  corner phone, not the diegetic one.
- **Decision for the owner:** should the visual pass get the phone? Two options: place it in
  the scene by hand, or have `MfaGauntlet.Wire()` build one beside the Knowledge `MfaFactor`
  when `DiegeticPhone.Active == null`. The code option needs an in-editor look, since the
  visual pass has hand-placed art where the phone would spawn.

---

## 4. HELD — L3 up-arrow input-leak fix

On WebGL, the **Up-arrow keypress leaks into QueryTerminal's typed string**: the history
handler fires AND `Input.inputString` appends the arrow char in the **same frame**, so pressing
Up to recall history also types a stray glyph.

A patch is **diagnosed but HELD** because `QueryTerminal.cs` is inside the uncommitted refactor
in item 2 — patching it now would collide. **Apply once the refactor lands:**
- Consume the arrow keypress so the text loop can't re-add it that frame.
- Add an `IsArrowGlyph` filter so any leaked arrow char is dropped before it reaches the string.

**Exact location (main @ `f41a18e`):** `QueryTerminal.Update()`. History is handled at lines
100–110 (`typed = history[historyIndex]`), then the `foreach (char c in Input.inputString)`
loop at line 113 runs in the same frame. `char.IsControl` (line 125) doesn't catch the arrow:
browsers on macOS report arrow and function keys as **private-use glyphs U+F700–U+F8FF**
(Up = U+F700), not control characters, so it gets appended. Ready-to-apply patch:

```csharp
// class member
private static bool IsArrowGlyph(char c) => c >= '' && c <= ''; // Apple function-key range

// in Update(), around the existing history block
bool historyKey = false;
if (Input.GetKeyDown(KeyCode.UpArrow) && history.Count > 0)   { /* existing */ historyKey = true; }
else if (Input.GetKeyDown(KeyCode.DownArrow) && historyIndex >= 0) { /* existing */ historyKey = true; }

foreach (char c in Input.inputString)
{
    if (historyKey || IsArrowGlyph(c)) continue; // consume the arrow press this frame
    // ... existing loop body unchanged ...
}
```

`TypingChallenge` (line ~140) and `PasswordLockController` (lines 129 and 165) also read
`Input.inputString` with the same `!char.IsControl(c)` filter. Arrows don't do anything in
those, but the same stray glyph can be typed, so apply `IsArrowGlyph` there too. `MainMenu`
is safe because it only accepts letters and digits.

---

## Context / conventions worth knowing

- **Stack:** Unity, ships to **WebGL**.
- **Level generation:** `Hub`, `Level0`, `Level1`, `Level2` are **baked scenes**;
  `Level3`, `Level4`, `Level1_IAM` are generated **100% in code at runtime**. But the Hub
  sends players to the baked `*_VisualPass` scenes for I/AM and Cyber Defense
  (`SceneCatalog.Variants`), so the code-gen `Level1_IAM` is only a fallback. Code-only changes
  to I/AM builders don't reach players unless the visual pass picks them up (see 3b).
- **`PropFactory.BuildFurnishings` is shared across ~6 factories** — editing it hits **every**
  code-gen scene, so treat changes to it as high-blast-radius.
- Later levels spawn couches / furniture **programmatically**, so a disk-only scene scan is
  blind to their content — check the bootstrap / procedural code, not just `.unity` files.
- Commit author: **Christopher Anthony Velez <lilvelezcav@gmail.com>**.
- **Never push to `main` without explicit permission.**

## Verifying without Unity — `Tools/compile-check.sh`

Cloud sessions have no Unity editor. This script compiles the project the way the player
build does, using Roslyn on Mono against Unity's public reference assemblies:
uGUI → TextMeshPro 3.0.9 → `Assembly-CSharp` → `Cyverse.PlayModeTests`, each in release and
`DEVELOPMENT_BUILD` configurations with WebGL defines.

```sh
sudo apt-get install -y mono-devel   # once per container
Tools/compile-check.sh               # prints "compile-check: OK" or the csc errors
```

It downloads its toolchain from api.nuget.org and GitHub (needle-mirror) into
`~/.cache/cyverse-compile-check`. Calibration on 2026-09-27: 0 errors on `e9b157d` (last green
CI), and exactly CI's two errors on `f41a18e` (red CI).

**What it does NOT check:** Editor scripts (`Assets/Editor`, no `UnityEditor.dll`), code under
`#if UNITY_EDITOR`, anything runtime-only (null refs, rendering, scene wiring), and API
differences between its 2021.3 reference assemblies and the project's 2022.3. It is a
pre-push gate, not a substitute for Play mode or CI.
