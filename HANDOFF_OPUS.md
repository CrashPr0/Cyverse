# HANDOFF — Cyverse

Zero-context handoff for a new working session. Read this whole file before
touching anything. Last checked against `main` on 2026-11-16 (after PR #53,
squash `0111162`). The running release list is `Docs/RELEASE_CHECKLIST.md`;
setup and tooling are in `SETUP.md`; tests in `TESTING.md`.

**Standing rules:**
- Commit author is **Christopher Anthony Velez <lilvelezcav@gmail.com>**.
- **Never push to `main`.** Work on a branch and open a pull request only
  when the owner asks. Merging to `main` deploys to GitHub Pages.
- Every pull request needs green `playmode-tests` and `webgl-build` checks
  (`.github/workflows/pr-checks.yml`) before merge.
- Stage files by **explicit path**, never `git add .`.
- No real brand or trademark names in game text. Audio stays soft and warm.
- Ask the owner before any big design change.

---

## 1. Open — placeholder art

Many props and **all level architecture** are still low-fidelity
placeholders built from primitives in C# at load time. This needs real 3D
models and is waiting on the owner.

**Props with a prefab hook but primitive stand-ins:** `Chair`, `CoffeeTable`,
`Couch`, `Crate`, `Desk`, `Drone`, `LockerBank`, `Plant`, `Reception`, `Rug`,
`ServerRack`. Only `CornerRugs`, `WorkPodA` and `WorkPodB` have prefabs in
`Assets/Resources/Props/` today. Kiosk and pedestal terminals and the MFA
phone are built in code with no prefab hook.

**Architecture with no real geometry:** doorway and portal frames, modular
wall panels, floor trim, ceiling and light strips, pillars, plinths (the MFA
pedestals), vault doors.

### The swap mechanism
- **Code-generated scenes** (`Level1_IAM`, `Level3_Forensics`,
  `Level4_CyberAttack`, `PasswordLock`; each `.unity` file is a few KB):
  drop a prefab named after the prop into `Assets/Resources/Props/` and
  `PropLibrary.TrySpawn` uses it instead of the primitives. No scene edit.
- **Baked scenes** (`Hub`, `Level0`, `Level2_CyberDefense`): swap the
  placeholder in the scene, or add a `TrySpawn` call in the polish pass
  (`HubPolish`, `Level0Polish`, `Level2SocPolish`) that builds that prop.
- `PropFactory.BuildFurnishings` is shared by several factories, so a change
  there hits every code-generated scene.
- Load-time polish passes restyle scenes without editing `.unity` files;
  `LevelCohesion` runs last. A disk-only scene scan misses everything these
  build, so read the bootstrap code too.

### Model conventions (a violation here caused a real magenta bug)
- Real-world scale (metres), pivot at floor centre, front facing **−Z**
  (Unity Quads draw on their local −Z face).
- **Every renderer needs an assigned material.** A missing one renders
  magenta in the build; check all child renderers, not just the main one.
- Low-poly, WebGL-friendly meshes and textures.
- Screens need a **separate quad or face** for RenderTexture or emissive
  binding; don't paint the screen onto the body mesh.

## 2. Check — Level 1 MFA phone

`Assets/Scripts/Interaction/DiegeticPhone.cs` builds the in-world
authenticator phone beside the MFA vault's passcode terminal. Its screen is a
RenderTexture drawn on demand by a private camera that sees only a dedicated
layer, far from the level. `DiegeticPhonePlayModeTests` checks that it stands
upright, is phone-sized, faces the player and shows the code; set
`CYVERSE_PHONE_CAPTURE=1` to write screenshots of it from the player's
views (see the release checklist for the result).

## Resolved since the first handoff (2026-09-26)

- **Co-mingled refactor (old item 2).** The GameplayActions refactor was
  committed on 2026-09-28 (`8701a81`) and everything since builds on it.
  The working tree on `main` is clean; there is no uncommitted refactor.
- **Up-arrow leak in Level 3's `QueryTerminal` (old item 4).** The KQL
  terminal was removed in PR #11 (Evidence Explorer replaced it), so the
  held patch no longer applies. The Evidence Explorer uses ← and → to change
  category while it also reads typed text; the release checklist tracks a
  browser check that arrows type nothing there.
- The legacy `Level1` scene no longer ships; `Level1_IAM` is Level 1.
