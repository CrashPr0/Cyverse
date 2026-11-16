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

## Owner decisions

- [x] **Access code gate: removed.** The game opens in the Hub. The
      access-code scene still ships and can be made the entry scene again
      in Build Settings to gate a private build.
- [x] **Progress reset: kept.** Every page load starts a fresh run, which
      is what study sessions need between participants. Reloading the page
      is the reset; settings (volume, captions, graphics) are kept.
- [x] **Browser text-to-speech: off by default.** Recorded narration and
      captions still play; Settings → Voiceover turns the computer voice on.
      A browser that already saved the old setting keeps it until Restore
      Defaults is used.
- [x] **`CyverseVer1.0.zip` removed.** It was a 7.5 MB WebGL build from July
      in the repository root. It is still in git history.
- [x] **Company name: San Jose State University.** Product name is now
      "CyVerse".

## Polish

- [ ] Progress ring and "STEP n OF m" disagree: Level 1 shows 0/6 beside a
      seven-line task list, Level 3 shows 0/14 beside "STEP 1 OF 6", Level 4
      shows 0/4 beside "STEP 1 OF 5".
- [x] Task list style differed by level: Level 2 numbered its lines, the
      others did not. Level 2 now matches.
- [x] Level 0's title card called it "Cybersecurity Onboarding — Level 0"
      while the Hub door calls it Orientation. The card now says Orientation.
      It still asks for the callsign again; left as is.
- [x] Briefings: "E for the next slide" shows from the spawn point, but E
      only works once the player has walked up to the screen. The first
      objective in Levels 1, 2 and 4 now says "Walk up to the screen and
      press E to watch the ... briefing". Note for study analysis: this
      changes the objective text recorded in playtest telemetry.
- [x] Leftover "in development" wording in defaults (`ResultsScreen.Show`,
      legacy `Level1Manager`).
- [ ] The legacy `Level1` scene still ships in the build although nothing
      can load it.
- [ ] Playtest telemetry prints a line to the browser console for every
      event in release builds.
- [x] `README.md` rewritten for the current game; stale lines in
      `TESTING.md` and `SETUP.md` corrected. The rest of `SETUP.md` (800
      lines) has not been reviewed.
- [ ] Audio: sound effects are already soft, low mallet tones with a
      low-pass and reverb, and the room tone is low brown noise. Needs a
      listen on real speakers before sign-off.

## Done

Nothing merged yet.
