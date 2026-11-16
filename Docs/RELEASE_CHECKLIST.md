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
      **Still to confirm in a browser build**: the editor never showed the
      bug, so the tests prove the font is loaded and complete, not how it
      looks on screen.
- [ ] **2. Play Mode tests did not run on pull requests**, only after merge
      to `main`. Fixed by the `PR Checks` workflow (tests, then a WebGL
      build attached to the run).
- [x] **3. Web page text was left over from an earlier version.** The loading
      screen said "Loading Level 0 — Onboarding" (the game opens on the
      access terminal) and the tab title was "CyVerse — Cyverse". The landing
      page listed Level 4 as "in development" and described an older Level 3.
      Still open: the build is stamped company "DefaultCompany" (Player
      Settings → Company Name), which the owner should set.

## Decisions needed from the owner

- [ ] **Access code gate.** The game opens on a "CLOSED BETA" passcode
      screen. Keep it for release, or open the game to everyone?
- [ ] **Progress is wiped on every page load** (by design: "Play in one
      sitting"). A reload or a browser crash mid-campaign loses everything.
      Keep, or save progress between visits?
- [ ] **Browser text-to-speech is on by default** and reads briefing slides
      and unvoiced dialogue in the computer's synthetic voice. Only the MFA
      and NICE lines have recorded narration. This is the one synthetic
      sound left in the game. Default it to off, or keep it?
- [ ] **`CyverseVer1.0.zip`** (7.5 MB, an old WebGL build from July) sits in
      the repository root. Remove it?

## Polish

- [ ] Progress ring and "STEP n OF m" disagree: Level 1 shows 0/6 beside a
      seven-line task list, Level 3 shows 0/14 beside "STEP 1 OF 6", Level 4
      shows 0/4 beside "STEP 1 OF 5".
- [ ] Task list style differs by level: Level 2 numbers its lines, the
      others do not.
- [ ] Level 0 shows the full title card and asks for the callsign again when
      entered from the Hub.
- [ ] Leftover "in development" wording in defaults (`ResultsScreen.Show`,
      legacy `Level1Manager`).
- [ ] The legacy `Level1` scene still ships in the build although nothing
      can load it.
- [ ] Playtest telemetry prints a line to the browser console for every
      event in release builds.
- [ ] `README.md` and `TESTING.md` describe an older game (Level 0 only;
      "Level 4 doorway remains in development").
- [ ] Audio: sound effects are already soft, low mallet tones with a
      low-pass and reverb, and the room tone is low brown noise. Needs a
      listen on real speakers before sign-off; no code change planned
      beyond the text-to-speech decision above.

## Done

Nothing merged yet.
