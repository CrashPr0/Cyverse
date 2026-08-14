# Automated Unity testing

## One-click Level 1 playthrough

Open the project in Unity 2022.3.40f1, then choose:

`CyVerse > Testing > Run Level 1 Automated Playthrough`

The runner saves any scene changes after asking, loads
`Level1_IAM_VisualPass`, enters Play Mode, and drives the complete end flow:

1. Complete the security briefing and verify the task door unlocks.
2. Enroll the badge.
3. Carry and insert the real MFA token, then clear the other factors.
4. Carry every Data Triage crate through its wired role `DropZone`.
5. Solve each Audit Hunt round through its normal flag path.
6. Unlock and finish the Certification Exam.
7. Verify the manager reaches `Complete`, the results flow opens, and Level 1
   completion is persisted.

Look for `[PLAYTHROUGH] PASS` or `[PLAYTHROUGH] FAIL` in the Console. The
runner restores the previous `cv_done_1` PlayerPrefs value when it finishes.

## Watchable TAS replay

Choose `CyVerse > Testing > Watch Level 1 TAS Replay` to watch the automated
route in the Game view. The replay smoothly moves the first-person camera to
each station and shows a bottom-center input panel containing the current
virtual keys and action, including walking, interaction, carrying, scrubbing,
audit selection, and exam answers. It uses the same station and DropZone event
wiring as the smoke test, but deliberately pauses between actions so the route
is readable.

For the full released route, choose
`CyVerse > Testing > Watch Full Campaign TAS (Password to Level 3)`. This
starts at the password vestibule, enters and crosses the Hub between missions,
then plays Levels 1, 2, and 3 in order. Level 3 Digital Forensics is currently
the newest playable level; the Level 4 Hub doorway remains in development.

## Test Runner and command line

The same flow is registered as a Play Mode test:

`Level1EndFlowPlayModeTests.VisualPass_CompletesEndFlowAndPersistsProgress`

Run it from `Window > General > Test Runner > PlayMode`, or from a terminal
after closing any Unity editor that has the project open:

```sh
/Applications/Unity/Hub/Editor/2022.3.40f1/Unity.app/Contents/MacOS/Unity \
  -batchmode \
  -projectPath /absolute/path/to/Cyverse \
  -runTests \
  -testPlatform PlayMode \
  -testResults /absolute/path/to/playmode-results.xml \
  -logFile /absolute/path/to/playmode-tests.log
```

GitHub Actions runs this Play Mode test before the WebGL build-and-deploy job.
If the end flow fails or times out, deployment is blocked and the test results
are uploaded as the `unity-playmode-results` artifact.

The same Play Mode run includes `SocHandoffAcceptanceTests`, which verifies:

- all three SOC cases contain four distinct WS-01..WS-04 rows;
- the final trigger is WS-03 at 02:13;
- structured evidence survives a JSON round-trip;
- Digital Forensics stays locked for every incomplete three-key combination;
- the saved Level 2 visual pass rewires to four workstations and builds the TMP Alert Board.

The complete Password → Hub → Levels 1–3 campaign can also run headlessly.
Do not pass `-quit`; the asynchronous runner exits Unity itself with status 0
on success or 1 on failure:

```sh
/Applications/Unity/Hub/Editor/2022.3.40f1/Unity.app/Contents/MacOS/Unity \
  -batchmode \
  -nographics \
  -projectPath /absolute/path/to/Cyverse \
  -executeMethod Cyverse.Editor.Level1PlaythroughAutomation.RunCampaignFromCommandLine \
  -logFile /absolute/path/to/campaign-e2e.log
```

Look for `[CAMPAIGN TAS] PASS — completed through Digital Forensics` in the
log. The runner preserves and restores the campaign progression PlayerPrefs.

## Local WebGL release build

Use the same build entry point locally and in release automation:

```sh
CYVERSE_WEBGL_BUILD_PATH=/absolute/path/to/output \
/Applications/Unity/Hub/Editor/2022.3.40f1/Unity.app/Contents/MacOS/Unity \
  -batchmode \
  -nographics \
  -projectPath /absolute/path/to/Cyverse \
  -buildTarget WebGL \
  -executeMethod Cyverse.Editor.WebGLBuildAutomation.BuildFromCommandLine \
  -logFile /absolute/path/to/webgl-build.log
```

If `CYVERSE_WEBGL_BUILD_PATH` is omitted, the build is written to
`Build/WebGL/WebGL`. The project emits `.unityweb` fallback-compressed files so
static hosts such as GitHub Pages do not need custom `Content-Encoding` headers.

## Development diagnostics

Editor and Development builds also emit `[END FLOW]` state snapshots whenever
the phase or station completion state changes. These logs are intentionally
excluded from non-development WebGL builds.
