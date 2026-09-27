# HANDOFF — Cyverse fix-up / polish pass (for Cloud Opus 5.5)

You are picking up a Unity project that ships to **WebGL**, called **Cyverse**. This is a
zero-context handoff: read this whole file before touching anything. Items are ordered by
value and risk — do the top item first.

**Standing rules (non-negotiable):**
- Commit author is **Christopher Anthony Velez <lilvelezcav@gmail.com>**.
- **Never push to `main` without explicit permission.**
- Stage files by **explicit path**, never `git add .` — there is a large uncommitted refactor
  in the working tree (see the co-mingle item) that must not be swept into an unrelated commit.

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

---

## 4. HELD — L3 up-arrow input-leak fix

On WebGL, the **Up-arrow keypress leaks into QueryTerminal's typed string**: the history
handler fires AND `Input.inputString` appends the arrow char in the **same frame**, so pressing
Up to recall history also types a stray glyph.

A patch is **diagnosed but HELD** because `QueryTerminal.cs` is inside the uncommitted refactor
in item 2 — patching it now would collide. **Apply once the refactor lands:**
- Consume the arrow keypress so the text loop can't re-add it that frame.
- Add an `IsArrowGlyph` filter so any leaked arrow char is dropped before it reaches the string.

---

## Context / conventions worth knowing

- **Stack:** Unity, ships to **WebGL**.
- **Level generation:** `Hub`, `Level0`, `Level1`, `Level2` are **baked scenes**;
  `Level3`, `Level4`, `Level1_IAM` are generated **100% in code at runtime**.
- **`PropFactory.BuildFurnishings` is shared across ~6 factories** — editing it hits **every**
  code-gen scene, so treat changes to it as high-blast-radius.
- Later levels spawn couches / furniture **programmatically**, so a disk-only scene scan is
  blind to their content — check the bootstrap / procedural code, not just `.unity` files.
- Commit author: **Christopher Anthony Velez <lilvelezcav@gmail.com>**.
- **Never push to `main` without explicit permission.**
