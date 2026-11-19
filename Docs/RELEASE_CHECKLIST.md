# CyVerse release checklist

Running list for the release pass. Each fix lands as its own small pull
request; the pull request ticks its line here.

How things were checked: the published build (`gh-pages`, built from
`a353e99`) was served locally and driven in headless Chromium; code and
scenes were read in the repository. Unity is not installed in the audit
environment, so Play Mode tests run in CI (`PR Checks` workflow).

Not checked yet, and needing a person with a real browser and speakers:
frame rate on real graphics hardware, how the audio actually sounds, pointer
lock and fullscreen behaviour, and a full hands-on playthrough of each level.

## Audit results

| Area | Result |
| --- | --- |
| Boot and first screen | Loads with no errors or failed requests. |
| Every level loads (0–4, Hub, password gate) | Yes, no exceptions in the browser console. |
| Level load time | 2.2–3.5 s from Hub door to level, software rendering. |
| Download size | 28.4 MB compressed (data 21.2, code 7.1). |
| Console | No errors or warnings; one expected warning when Level 3 is opened without Level 2 evidence. |
| Automated coverage | The Play Mode suite passes on `main` (run 92). |
| Text rendering | **Broken**: see blocker 1. |

## Release blockers

- [x] **1. Missing characters in the web build.** The built-in font ships as
      a 332-character subset with no em dash, arrows, bullet, ellipsis, curly
      double quotes or box/triangle shapes. Text drawn with it showed gaps:
      the Settings key hints for "Select" and "Change" were empty boxes, and
      "Onboarding — Level 0" on the title card read "Onboarding  Level 0".
      Fixed by shipping the game's own font (`Assets/Resources/Fonts`).
      Confirmed on the deployed build in headless Chromium: the em dash,
      arrows and bullets now draw.
- [x] **2. Play Mode tests did not run on pull requests**, only after merge
      to `main`. Fixed by the `PR Checks` workflow
      (`.github/workflows/pr-checks.yml`): every pull request that touches
      `Assets/`, `Packages/` or `ProjectSettings/` runs the Play Mode tests,
      then a WebGL build attached as the `webgl-build` artifact.
- [x] **3. Web page text was left over from an earlier version.** The loading
      screen said "Loading Level 0 — Onboarding" (the game opens on the
      access terminal) and the tab title was "CyVerse — Cyverse". The landing
      page listed Level 4 as "in development" and described an older Level 3.

## Owner decisions

- [x] **Access code gate: removed.** The game opens in the Hub. The
      access-code scene still ships and can be made the entry scene again
      in Build Settings to gate a private build.
- [x] **Progress reset: kept, plus Start Over.** Every page load starts a
      fresh run, which is what study sessions need between participants.
      Settings (Esc) → Start Over does the same without a reload: it erases
      progress, scores and the callsign, picks the next culprit profile,
      and returns to the title card. Settings are kept, and a
      `playthrough_reset` event is written to the playtest telemetry.
- [x] **Browser text-to-speech: off by default.** Recorded narration and
      captions still play; Settings → Voiceover turns the computer voice on.
      A browser that already saved the old setting keeps it until Restore
      Defaults is used.
- [x] **`CyverseVer1.0.zip` removed.** It was a 7.5 MB WebGL build from July
      in the repository root. It is still in git history.
- [x] **Company name: San Jose State University.** Product name is now
      "CyVerse".

## Polish

- [x] Progress ring and "STEP n OF m" disagreed in Level 3 (0/14 beside
      "STEP 1 OF 6") and Level 4 (0/4 beside "STEP 1 OF 5"). The ring now
      counts the same steps as the banner. Level 1's seven-line list beside
      six steps is unchanged: the MFA Specialist talk is a line but not a
      step.
- [x] Task list style differed by level: Level 2 numbered its lines, the
      others did not. Level 2 now matches.
- [x] Level 0's title card called it "Cybersecurity Onboarding — Level 0"
      while the Hub door calls it Orientation. The card now says Orientation.
      It no longer asks for the callsign again (see visual pass item 10).
- [x] Briefings: "E for the next slide" shows from the spawn point, but E
      only works once the player has walked up to the screen. The first
      objective in Levels 1, 2 and 4 now says "Walk up to the screen and
      press E to watch the ... briefing". Note for study analysis: this
      changes the objective text recorded in playtest telemetry.
- [x] Leftover "in development" wording in defaults (`ResultsScreen.Show`,
      legacy `Level1Manager`).
- [x] The legacy `Level1` scene no longer ships in the build.
- [x] Playtest telemetry printed a line to the browser console for every
      event in release builds. Now only the editor and development builds
      print them (`PlaytestMetrics.EchoToConsole`); the events are still
      stored in the browser.
- [x] `README.md` rewritten for the current game; stale lines in
      `TESTING.md` and `SETUP.md` corrected.
- [x] `SETUP.md` reviewed against the current game (2026-11-16). Fixed:
      Hub doors and unlocks, the fresh-run reset and Start Over, the
      visual-pass scenes (no longer shipped), scene order with Hub first,
      the Level 3 section (still described the removed KQL terminal; now
      the Evidence Lab, pointing at `Docs/Level3-Forensics-Guide.md`), a new
      Level 4 section, the Level 2 alert board, Level 1's MFA passcode,
      pars, controls and settings, results keys, the PR Checks workflow and
      the web font. Not rewritten: the Level 0 deep-dive and the shader and
      script-map sections, which still match the code.
- [ ] Audio: sound effects are already soft, low mallet tones with a
      low-pass and reverb, and the room tone is low brown noise. Needs a
      listen on real speakers before sign-off.

- [x] **Level 1 MFA phone seen running** (`DiegeticPhone`, never looked at
      before). Rendered in Unity on 2026-11-16 with the new
      `DiegeticPhonePlayModeTests` (`CYVERSE_PHONE_CAPTURE=1` writes the
      views): it stands upright on its dock left of the passcode terminal,
      faces the player, and the code is legible from the terminal, from the
      phone and close up. The always-on half of the test now guards its
      orientation, size and screen. Notes, not changed: the phone is about
      20 x 40 cm (large for a phone, chosen for legibility), and from the
      terminal the dock's "MY PHONE" plate is partly hidden by the dock edge.
- [x] **`HANDOFF_OPUS.md` brought up to date.** Its co-mingled refactor
      warning and the held Level 3 `QueryTerminal` up-arrow patch were
      stale: the refactor was committed on 2026-09-28 and the terminal was
      removed in PR #11. Placeholder-art notes kept and corrected.

- [x] **CI actions moved off Node 20** (GitHub is retiring it):
      `actions/checkout` v5, `actions/cache` v5, `actions/upload-artifact`
      v6, `game-ci/unity-builder` v5 (v5 only dropped the cloud-runner
      inputs, which these workflows don't use). `game-ci/unity-test-runner@v4`
      and `peaceiris/actions-gh-pages@v4` already run on Node 24. Left on
      Node 20: `game-ci/unity-request-activation-file@v2` in the single-use
      activation workflow (no newer release). **Not yet run**: the first pull
      request that touches `Assets/` proves the PR Checks half; the deploy
      half runs on the next merge.

## Visual pass (2026-11-16)

Sources: the deployed web build (`0111162`) in headless Chromium, entering
Hub, Orientation and Levels 2–4 from their doors; and Unity runtime
captures of every scene and station (`VisualQaRuntimeCaptureTests`).
Mouse look does not register in headless Chromium, so the web pass saw
each level from its spawn and the briefing screen; station close-ups come
from the Unity captures. Level 1 was not entered in the browser (its door
name contains "/", so it cannot be reached by script).

- [x] **1. The game is taller than the window on common laptops.** The
      page sizes the canvas by width only (1280 x 720 under a header), so
      at a 1366 x 768 screen 144 px (20%) of the game is below the fold, and
      71 px at 1536 x 864. Captions, the E prompt and the Settings footer
      sit at the bottom. Fix is page-only: cap the canvas by viewport
      height. **Fixed**: the canvas now fits the window height too (checked
      at 1366 x 657, 1536 x 730, 1280 x 600 and 800 x 500: nothing hidden).
- [x] **2. Briefing slide footer is unreadable** ("SLIDE 1 OF N · E for
      the next slide"): pale grey over the bright floor, and it is the
      instruction to progress (Levels 1, 2 and 4). **Fixed**: dark plate
      behind it, white text, gold E.
- [x] **3. Patrol drones fly through the briefing reading spot** in
      Levels 2 and 4 and cover the slide text while the player reads.
      It was the guide drone: standing at the board, the objective marker
      sits above the view, so after 30 s the drone flew in to point the
      way. **Fixed**: it stays away within 4.5 m of the objective.
- [x] **4. Hub and Orientation are very dark**, and from the Hub spawn the
      door banner captions and mission board rows are too small to read;
      the mission board is cut off at the left of the spawn view.
      **Fixed**: ambient raised to the other levels' level
      (`LightingDirector.RaiseAmbient`), banner words 50% bigger. The
      mission board was not moved (baked Hub scene).
- [x] **5. Level 1 MFA wording disagrees with itself.** The phone says
      "MY PASSCODE · SECRET PASSCODE, MEMORIZE IT", the wall board says
      "DAILY OTP · CHECK YOUR PHONE · code rotates every 24h", and the
      terminal says "CODE FROM YOUR PHONE". The factor is "something you
      know", but the signs describe a one-time code (something you have).
      **Fixed** (owner let us decide): wall board "YOUR PASSCODE · ONLY YOU
      KNOW IT · new here? your starter passcode is on your phone",
      terminal "ENTER YOUR PASSCODE", card "TYPE YOUR SECRET PASSCODE",
      phone status "STARTER PASSCODE".
- [ ] **6. Hanging signs collide with door headers.** Level 2:
      CERTIFICATION over the FORENSICS LAB door header (two frames
      overlap). Level 3: "03 FORENSIC REPORT" covers "RETURN TO HUB";
      RECEPTION hangs in front of the Evidence Locker; the station heading
      underlines cut through the letters. **Not changed**: these line up
      only from some spots (the signs are metres in front of the doors).
      Lowering CERTIFICATION breaks its wall-light clearance test, and
      Level 3's layout stays as is by owner decision.
- [x] **7. Level 1 station labels.** Data Triage crate labels pass through
      the podium and box lids, and the DATA TRIAGE table plate is
      unreadable; the vault's KNOW / HAVE / ARE captions and
      "LOCKED · 0 OF 3" are tiny. **Fixed**: vault captions about 40%
      bigger and brighter. Crate labels unchanged (the shared carry label
      also sits on the Level 2 card rack).
- [x] **8. Level 2 small text.** The IR playbook step list and card
      captions are too small, and the Unity capture shows gaps in
      "CONTAINMENT" and "ERADICATION" and em dashes drawn as "– –" on the
      workstation monitors. The web build draws em dashes correctly on the
      briefing board, so this may be editor-only; check at a workstation in
      a browser. **Fixed**: step list and rack caption bigger.
- [x] **9. HUD task list** (top left) is small, and unticked lines are grey
      on teal, low contrast at 1280 px wide. **Fixed**: brighter unticked
      lines on a darker backing.
- [x] **10. Orientation asked for the callsign again**, so a player who
      started walking typed "WWWEEE" into it. **Fixed**: Orientation skips
      its title card when the Hub title already took a callsign this
      playthrough (`Level0CallsignPlayModeTests`).
- [ ] **11. MFA phone** "MY PHONE" plate partly hidden from the terminal
      (see above).
- [ ] **12. PasswordLock** renders from inside a black slab in the Unity
      capture. Doesn't ship as the entry scene; matters only if the gate
      returns.

Not checked: whether ← / → in the Level 3 Evidence Explorer type a stray
character into its search box in the browser (the old `QueryTerminal`
bug); audio; frame rate on real hardware.

## Added for release

- [x] **Credits** in Settings (Esc) → Credits. PLACEHOLDER text with the
      developer's name; the full team list goes in
      `AccessibilitySettings.CreditsText`.
- [x] **Certificate can be saved** as a PNG from the browser ([S] or the
      Save Image button). Its "prototype" labels now read "training"; it
      still says it is not an official credential.
- [x] **Game page notice**: no WebGL 2 gets an explanation instead of a
      stuck loading bar; a touch-only device is told the game needs a
      keyboard and mouse and asked before the download starts. Tested
      locally on desktop, an emulated phone and with WebGL 2 disabled.

- [x] **Export Data** in Settings (Esc): saves the playtest log kept in
      this browser as CSV or JSON for the study. Nothing is uploaded. Events
      now carry a `playthrough` number (Start Over starts the next one) and
      a UTC time. Covered by `PlaytestExportPlayModeTests`.

- [x] **Look input is clamped per frame** (30 degrees), so a pointer-lock
      jump or a long frame can no longer snap the view to the ceiling.

**Seen running** on the deployed build (headless Chromium): the game
starts in the Hub, Credits opens, Start Over resets the run, no console
errors. **Not seen running**: the certificate save and the ring change.

## Done

- PR #53 (squash `0111162`) merged the release pass above and is deployed.
