# Level 4 — Cyber Attack: gameplay and learning specification

**Status:** design pass for implementation

**Design authority:** the supplied `CyVerse Tables.docx` (especially Tables 2–4)
and the Level 4 brief supplied in the task. This document describes a fictional,
authorized training simulation. It must not expose real credentials, live
targets, real network addresses, or executable attack instructions.

## Player promise

The player enters the **CyVerse Red Team Simulator** as an attacker in a
contained exercise. The target is the fictional **Northstar Cooperative** and
all accounts, endpoints, alerts, and records are synthetic. The player makes
high-level decisions against deliberately simplified control panels; they do
not type shell commands, exploit real software, contact a network, or handle
real personal data.

The learning loop is deliberately inverted from Levels 1–3:

```text
Level 1 authentication  →  choose the weak control in Bypass MFA
Level 2 IAM / SOC         →  follow an over-permissioned role in Escalate Privileges
Level 3 evidence / audit  →  move a fictional record in Extract Data and Cover Tracks
                           →  debrief with the control that would have stopped it
```

The attack is a simulation of risk, not a tutorial for carrying out an attack.
Every station gives the defensive explanation immediately after a choice, and
the final debrief names the corresponding control from the earlier rooms.

## Room flow

The four required stations run in a fixed narrative order. The player may
replay completed stations, but the next station remains the prominent
objective so the route is clear in WebGL.

1. **Recon Brief** — a short briefing overlay and a fictional org card. It is
   context for the four stations, not a fifth completion gate. The player
   identifies the target role, asset class, and authorized exercise boundary
   from three selectable cards. It teaches threat actors, scope, and ethics.
2. **Bypass MFA** — obtain an initial foothold by recognizing a bad
   authentication configuration.
3. **Escalate Privileges** — pivot through a permission graph and identify the
   smallest misconfiguration that reaches the chosen training asset.
4. **Extract Data** — select a synthetic data package and a simulated transfer
   route while balancing value, exposure, and policy controls.
5. **Cover Tracks** — review the generated event timeline, choose what a
   careless actor might try to conceal, and see why immutable audit and EDR
   telemetry still expose the attempt.
6. **Exfiltration Debrief** — show the final meters, replayable mistakes, and
   the defensive countermeasure that maps back to Levels 1–3.

The room should spawn with a clearly lit **RECON BRIEF** station, then lead the
player clockwise with four distinct accent colors: amber (access), red
(privilege), violet (data), and blue-white (audit). Each completed station
changes its status to `COMPLETE` and illuminates the next route. No station
should require a hidden trigger or a blind raycast.

## Shared simulation model

### Exercise state

The manager should own one run-scoped state object. The state is reset when a
new Level 4 run begins and is never allowed to persist real user input.

Suggested fields:

```text
phase                  Recon, BypassMfa, Escalate, Extract, Cover, Complete
scenarioIndex          0..2 (rotates on a new run)
stationCompleted[4]    one flag per station
timeRemaining          seconds in the active station
exfilValue              0..100 synthetic data value acquired
detectionPressure       0..100 likelihood the simulator noticed the run
mistakes                count of incorrect decisions / expired timers
defensiveLessons        ordered list of controls surfaced in the debrief
```

Use a serializable DTO only if the existing project needs a results export;
otherwise keep it in memory and let `LevelProgress` persist only the existing
level completion bit. If a best result is saved, store aggregate values only
(score, exfil value, detection pressure, time), never typed strings or
credentials.

### Exfiltration and detection meters

The HUD should display both meters continuously during a station:

| Meter | Meaning | Increases when | Player-facing goal |
|---|---|---|---|
| `DATA ACQUIRED` | fictional value recovered from the sandbox | a valid data package is selected and delivered | maximize useful data |
| `DETECTION` | how strongly the training controls noticed the activity | risky choice, wrong target, noisy route, timeout, or attempted concealment | stay below the scenario threshold |

Recommended presentation is two horizontal TMP bars in a compact top-right
card, below the existing score and never over the crosshair. Use an accessible
text readout as well: `DATA 60/100 · DETECTION 35/100`. Do not rely on red/green
color alone.

Each decision returns a `DecisionResult` with `dataDelta`, `detectionDelta`,
`scoreDelta`, `lessonKey`, and a short explanation. Clamp both meters to 0–100.
The meter should animate with unscaled time and remain legible at small browser
sizes.

Suggested end bands:

| Result | Requirement | Debrief tone |
|---|---|---|
| `CLEAN SIMULATION` | exfil ≥ 70 and detection < 35 | controls were bypassed because several layers were weak |
| `DETECTED` | detection 35–69, or exfil < 70 | the SOC found useful signals before full impact |
| `CONTAINED` | detection ≥ 70 or timer expiry at a critical station | layered controls limited the fictional loss |

These are learning outcomes rather than moral judgments. A player can always
finish the room and inspect the explanation; a poor run should not soft-lock
the campaign.

### Timer policy

Use one timer per station, paused whenever its modal is closed, the player is
walking between stations, or a browser loses focus. This preserves the
existing game's WebGL-friendly pause behavior and prevents a background tab
from creating a false failure.

Recommended first-run timers:

| Station | Intro | Standard | Advanced |
|---|---:|---:|---:|
| Bypass MFA | 90 s | 75 s | 60 s |
| Escalate Privileges | 100 s | 80 s | 65 s |
| Extract Data | 90 s | 70 s | 55 s |
| Cover Tracks | 100 s | 80 s | 60 s |

The first run uses `Intro`. Completion unlocks `Standard`; a clean run unlocks
`Advanced`. The mode is a content multiplier, not a required difficulty spike.
On expiry, apply a visible consequence (`DETECTION +20`, a concise feedback
card, and a retry button) rather than silently ending the scene.

## Station designs

### 1. Bypass MFA — “Find the weak factor”

**World prop:** an amber access console with three factor tiles, a fictional
identity card, and an authorization-policy display.

**Player interaction:** the console presents four synthetic observations and
four high-level attack-vector cards. The player selects the observation that
represents the actual weakness, then selects the least noisy route in the
simulator. Choices are labels and diagrams only:

- `PHISHING LURE — SIMULATED`
- `MALWARE DELIVERY — SIMULATED`
- `CREDENTIAL REUSE — SIMULATED`
- `VULNERABLE SERVICE — SIMULATED`

No message templates, URLs, payloads, password lists, service names, or
procedures are shown. The correct card is determined by the scenario data and
the visible policy clue, not by guessing a real-world trick.

**Example Intro scenario:**

```text
Target role: Northstar payroll coordinator
Policy clue: push approval is enabled without number matching
Evidence clue: a training notification shows repeated unverified prompts
Correct insight: recognize the weak approval policy and report the missing
verification step in the simulator
```

Selecting the weak factor grants a small foothold (`DATA +5`, `DETECTION +5`)
and explains that phishing-resistant MFA, number matching, and user reporting
would have blocked or surfaced the attempt. Selecting a strong factor gives a
hint and increases detection, never a real bypass.

**Learning targets:** authentication purpose, access control, threat actors,
phishing/social engineering as an attack category, and legal/ethical scope.
Maps to prior Level 1 MFA and to CAE-CD `CSP`, `ISC`, `PLE`, `AIG`/`AIF`.

### 2. Escalate Privileges — “Follow the permission graph”

**World prop:** a red role graph showing five fictional principals and six
synthetic resources. Nodes are `Intern`, `Analyst`, `Service Account`,
`Operations Admin`, and `Vault Export`; edges are labeled with abstract roles
such as `READ`, `APPROVE`, or `ADMIN`.

**Player interaction:** click a starting account, then click one edge at a time
to trace a path. The player must identify an over-permissioned edge and choose a
safe remediation explanation from three cards. They are not asked to run a
command or edit a live policy.

**Example choices:**

- `SERVICE ACCOUNT → OPERATIONS ADMIN` (over-permissioned; intended clue)
- `ANALYST → INCIDENT NOTES` (expected access; noise)
- `INTERN → TRAINING WIKI` (expected access; noise)

The player may follow the wrong edge for partial progress, but it adds
detection pressure and a feedback card explains least privilege, role
separation, and periodic access review. A clean path increases synthetic
access without exposing secrets. A later scenario introduces a stale role and
a shared service identity so the player must compare ownership and scope.

**Learning targets:** authorization versus authentication, least privilege,
system architecture, security risk analysis, and how IAM misconfiguration
creates lateral movement. Maps to Level 1 role/access control, Level 2 EDR/SOC,
and CAE-CD `ISC`, `SRA`, `NDF`, `PLE`.

### 3. Extract Data — “Choose a bounded target”

**World prop:** a violet data-classification wall and a route board with three
fictional repositories: `PEOPLE OPS`, `LEDGER LAB`, and `NORTHSTAR R&D`.

**Player interaction:** select one synthetic package from a set of cards, then
select a simulated transfer route. Each package displays classification,
value, size, and monitoring level. Each route displays speed, noise, and
whether the sandbox DLP sensor sees it. The player is rewarded for comparing
classification and value, not for memorizing an operational method.

**Example packages:**

| Package | Value | Classification | Built-in lesson |
|---|---:|---|---|
| Public training slides | 10 | Public | low value, low detection |
| Synthetic employee roster | 55 | Confidential | PII handling and minimization |
| Mock ledger snapshot | 75 | Restricted | financial-data controls |
| Fictional prototype notes | 90 | Restricted | trade-secret protection |

The route board offers abstract options such as `APPROVED TEST CHANNEL`,
`UNMONITORED SIMULATOR CACHE`, and `OVERSIZED BULK TRANSFER`. The player can
complete the station with a low-value package, but the final result explains
the trade-off. A package is never copied to disk or sent anywhere; the UI only
updates `exfilValue`.

**Learning targets:** confidentiality and data classification, network
monitoring, DLP/egress controls, CIA trade-offs, and proportional response.
Maps to Level 2 SIEM/EDR and CAE-CD `BNW`, `NDF`, `SRA`, `PLE`.

### 4. Cover Tracks — “The audit remembers”

**World prop:** a blue-white audit wall with a timeline, EDR signal cards, and
an immutable-ledger indicator.

**Player interaction:** the station reconstructs the run's synthetic timeline
from the prior three stations. The player highlights which events a threat
actor would likely try to hide and chooses the simulated outcome from three
cards:

- `LEAVE TELEMETRY INTACT`
- `ATTEMPT TO SUPPRESS A SIGNAL`
- `CREATE A DISTRACTION`

The game does not describe how to delete, alter, or evade real logs. Every
choice displays the defender-side observation: immutable audit records,
endpoint telemetry, timestamp correlation, or anomaly detection. A concealment
attempt can lower the visible alert briefly but sharply raises `DETECTION` once
the correlation pass runs; leaving telemetry intact yields the strongest
lesson and a small score bonus.

**Example Advanced scenario:** a delayed event from the service account is
correlated with the earlier role transition and data selection. The player
must choose the earliest timestamp that the SOC would pivot on, not erase the
event.

**Learning targets:** auditability, evidence integrity, event correlation,
network monitoring, and why defense-in-depth survives an attempted cover-up.
Maps to Level 2 alert hunt, Level 3 digital forensics/chain of custody, and
CAE-CD `DFS`, `DVF`, `NDF`, `PLE`.

## Content rotation

The existing project already has `ScenarioRoster` for rotating people and
scenario names. Level 4 should use the same seeded rotation at startup so a
culprit, target, or answer does not look identical on every run. Keep the
rotation in data, not in scene text. A scenario seed should select from:

- three target roles (`Payroll`, `Support`, `Research`),
- three weak-control patterns,
- three privilege graphs,
- four synthetic data packages,
- three audit timelines.

All rotations must preserve one unambiguous answer per station. The test
harness should run every seed and assert that each station can be completed and
that the correct answer is discoverable from the scenario data.

## Scoring and debrief

Award points for reasoning, not just speed:

```text
correct station decision       +100
first-attempt correct          +40
valid data package              +value / 2
clean route                     +50
wrong decision                  -25, detection +10
timer expiry                    -50, detection +20
cover-up attempt                -25, detection +15 after correlation
all four stations               +150
```

The debrief card should show:

```text
SIMULATION COMPLETE
DATA ACQUIRED       75 / 100
DETECTION PRESSURE  28 / 100
TIME                04:12
DEFENSIVE LESSONS   MFA policy · least privilege · DLP · immutable audit
```

Below the meters, use four concise rows:

```text
1  BYPASS MFA        weak push approval        → phishing-resistant MFA
2  ESCALATE ACCESS   stale service role        → least privilege / review
3  EXTRACT DATA      restricted ledger package → DLP + egress monitoring
4  COVER TRACKS      correlated timeline       → immutable audit + EDR
```

This makes the edutainment loop explicit: the player sees that the defensive
controls from Levels 1–3 were not abstract checkboxes; they changed the
outcome of the attack simulation.

## Implementation plan in this repository

### New files recommended

```text
Assets/Scenes/Level4_CyberAttack.unity
Assets/Scripts/Level/Level4CyberAttackSceneFactory.cs
Assets/Scripts/Level/Level4CyberAttackBootstrap.cs
Assets/Scripts/Level/Level4CyberAttackManager.cs
Assets/Scripts/Level/Level4Content.cs
Assets/Scripts/Interaction/BypassMfaStation.cs
Assets/Scripts/Interaction/PrivilegeGraphStation.cs
Assets/Scripts/Interaction/DataExtractionStation.cs
Assets/Scripts/Interaction/AuditCoverStation.cs
Assets/Scripts/UI/ExfiltrationMeter.cs
Assets/Tests/PlayMode/Level4EndFlowPlayModeTests.cs
```

Implement the new stations as data-driven `IInteractable` components with a
single modal ownership path through `GameState`. Follow the existing patterns
in `Level3ForensicsManager`, `ForensicsConsole`, `ChainOfCustodyForm`, and
`ResultsScreen`; avoid adding new scene-specific input loops where the shared
HUD/modal path will work. Every station must expose a deterministic
`CompleteForAutomation()` method for the playthrough harness, but the normal
player path must still be exercised by at least one PlayMode test.

### Existing integration points

The Level 4 work will need these updates, coordinated in one integration pass:

1. Add `Level4_CyberAttack` to `SceneCatalog` as a visual/procedural variant
   chain when a visual pass exists.
2. Give the Level 4 Hub door a real scene name. It currently has an empty
   `sceneName`, so `HubDoor` correctly reports it as `IN DEVELOPMENT`.
3. Register the new scene in `CyverseSceneTools.AddScenesToBuildSettings` and
   `ProjectSettings/EditorBuildSettings.asset`.
4. Update the Level 3 results copy from “in development” once Level 4 is
   playable.
5. Extend `CampaignTasPlayback` and the command-line end-flow runner through
   Level 4, preserving and restoring PlayerPrefs like the existing campaign
   path.
6. Add focused Level 4 visual captures for the four stations and the final
   debrief. Verify at small and maximized WebGL aspect ratios.
7. Add a WebGL build smoke test. Use only managed in-memory state and
   `PlayerPrefs`; do not require a server, filesystem, browser clipboard, or
   external AI/network service.

### Current blockers

- There is no Level 4 scene, bootstrap, manager, content bank, station
  component, or meter in the repository as of this design pass.
- The Hub's Level 4 door is intentionally a placeholder (`sceneName == ""`).
- The current campaign TAS stops after Level 3 and has no Level 4 route.
- The existing HUD has a score and a single onboarding progress ring, but no
  dual data/detection meter. A small isolated UI component is preferable to
  overloading the level progress ring.
- No art/model assets are required for a safe first playable pass because the
  project already builds rooms procedurally with `BuildKit`. A dedicated 3D
  modeling pass can replace the primitive console shells later without
  changing the station contracts.

## Acceptance criteria

The first playable Level 4 pass is ready when all of the following are true:

- A player who completes Level 3 can enter Level 4 from the Hub; an incomplete
  player receives the existing gate explanation.
- Recon and all four stations are reachable, visibly labeled, aimable, and
  completable with mouse/keyboard in WebGL.
- Every station pauses its timer while closed or unfocused and never traps the
  player behind a modal.
- The four stations update the same two clamped meters and the final debrief
  reports the exact aggregate values.
- At least three rotated scenarios are solvable; no scenario reveals real
  credentials, targets, network details, or executable procedures.
- Wrong choices teach a defensive countermeasure and never crash the scene.
- `Level4EndFlowPlayModeTests` covers normal completion, timer pause, wrong
  choice recovery, all scenario seeds, and completion persistence.
- Full PlayMode tests pass, focused captures show no text/mesh overlap, and a
  non-development WebGL build completes without errors.

