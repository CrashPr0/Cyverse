# Level 3: Digital Forensics Lab — Player & Instructor Guide

This guide covers the Level 3 forensics room in CyVerse: what the player is
trying to do, how to play it start to finish, what every station and tool does,
the correct answers and scoring for instructors, and what to do when something
goes wrong.

Play it at <https://crashpr0.github.io/Cyverse/play/>. Level 3 unlocks in the Hub
after Level 2 (SOC), but it can also be entered with no Level 2 handoff, in
which case the lab uses a training device.

---

## 1. Learning goals

By the end of the level a student should be able to:

1. **Explain chain of custody** and fill in a custody entry correctly: the same
   item number at every handoff, who released it, who received it, and why.
2. **Describe forensic acquisition**: evidence is imaged through a write blocker,
   the copy is hash-verified (SHA-256), and analysis only ever touches the copy.
3. **Set up a forensic tool** the way an examiner does before looking at any data:
   case type, evidence source (platform), acquisition method, optional passes.
4. **Examine a phone image**: browse artifact categories, read every field of a
   record, search by keyword, and tag only the records that prove a finding.
5. **Pivot between artifacts**: text → link → download → app → IP → a second
   domain on the same IP.
6. **Use metadata as evidence**: Wi-Fi history and photo metadata establish where
   a phone was and when.
7. **Treat automated flags as leads, not proof**: one AI flag is wrong on purpose.

Typical play time: 15–25 minutes.

---

## 2. Controls

| Action | Key |
| --- | --- |
| Move / look | WASD (or arrow keys) + mouse |
| Interact with a station | **E** |
| Close a screen | **Esc** or **Tab** (Tab keeps the mouse captured in a browser; Esc releases it) |
| Briefing: scrub slides | **←/→** while looking at the screen |
| Process steps: pick an option | Click it, or press **1–4** |
| Process steps: start / skip / continue | **Enter** or the green button |
| Evidence Explorer: change category | Click it, or **←/→** |
| Evidence Explorer: move through the list | Click a row, or **Up/Down** |
| Evidence Explorer: search | Just type (Backspace deletes) |
| Evidence Explorer: tag the selected item | **Enter** or TAG AS EVIDENCE |
| Evidence Explorer: hint | HINT button (halves that question's points) |

Settings (gear menu) include Reduce Motion, which keeps UI fades but drops the
sliding and scaling animations.

---

## 3. Walkthrough

The HUD always shows the current objective at the top, a task list on the side,
and a gold beacon over the next station. The lab has three numbered zones on the
floor and wall: **01 Evidence Intake → 02 Analyze Evidence → 03 Forensic
Report**.

### Step 1 — Retrieve the device (evidence locker)

You arrive facing the **evidence locker** in the south wall. Press **E** to take
out the seized phone. It stays in your hands until it is docked; it cannot be set
down loose, because evidence never leaves your custody.

If you came from Level 2 the toast names the SOC workstation the phone came from
(for example WS-03). With no handoff, the locker holds a training device and the
level plays the same way.

### Step 2 — Watch the analyst briefing

Walk to the TV and press **E**. The briefing covers chain of custody, hash
values, processing, the Evidence Explorer, pivoting and metadata. Use **←/→** to
scrub. Finishing it unlocks the **FORENSICS LAB** door.

### Step 3 — Evidence Intake: the chain-of-custody form (01)

At the **01 EVIDENCE INTAKE** plinth press **E**. The form already has entry 1,
signed in the SOC (client → SOC analyst). You write **entry 2**: the same phone
moving from the SOC to Digital Forensics. Click each blank and choose a value,
then **SIGN & SUBMIT ENTRY 2**.

- A wrong blank turns red and is marked **[X]**; the feedback line explains the
  first one. Fix it and submit again. There is no point penalty for a wrong submit.
- Once accepted, both signatures appear and the entry is locked.

### Step 4 — Dock and image the phone (02)

At the **02 ANALYZE EVIDENCE** desk press **E** once. The phone goes into the
cradle behind a write blocker. The left monitor shows the imaging progress and
the SHA-256 hash being verified (about five seconds). Press **E** again at any
time to finish instantly and open the Evidence Explorer.

### Step 5 — Process the image (Evidence Explorer, Process)

The first time the Explorer opens it shows **CASE SETUP**, five steps on a left
rail:

1. **Case details** — what kind of case is this?
2. **Evidence source** — what kind of device is in the cradle?
3. **Acquisition** — how much should the tool copy?
4. **Processing options** — optional passes; none is required.
5. **Process** — the run: copy, verify hash, parse, carve deleted records, run
   the chosen passes. Enter skips the animation.

A wrong pick on steps 1–3 explains why it is wrong and costs nothing. The
evidence card on the left (owner, case number, platform, custody status) is the
"evidence label" the steps refer to.

What the optional passes add in Examine:

| Pass | Adds |
| --- | --- |
| Keyword search | `[HIT]` badge on records containing the campaign's terms (spartan-rewards, 45.133.7.22) |
| Hash match | `[BAD]` badge on files matching the known-bad list (the Gold Claim app) |
| Text in photos (OCR) | Reads the words in photos; the search box then finds them |
| AI triage | `[AI]` badge with a reason. Flags are leads, not proof |

### Step 6 — Examine: work Case 1 and Case 2

The Explorer has three panes: **categories** on the left (with counts), the
**item list** in the middle (with a search box), and **artifact details** on the
right (every recovered field, any processing findings, and the source file on
the phone). The question sits in the gold banner on top.

Select the item that proves the answer and press **TAG AS EVIDENCE**. Correct
tags turn gold (`[EVIDENCE]`) and the desk's centre monitor counts them.
A wrong tag tells you which category holds the proof (or to re-read the fields
if you are already in the right one).

When Case 1 closes, press **Esc**, then **E** at the desk again to open Case 2.
The phone stays processed; Case 2 opens straight into Examine.

### Step 7 — Submit the forensic report (03)

With both cases closed, go to the **03 FORENSIC REPORT** desk and press **E**.

### Step 8 — Knowledge check

Filing the report opens a five-question knowledge check, one question at a
time. Answer with **1**, **2** or **3**. A wrong answer shows why and still
moves on. After the last question the level completes, the results screen
shows your score, and Level 4 unlocks. Leave through either **Return to Hub**
door.

There is no certificate for Level 3 on its own. A single prototype certificate
is offered on the Level 4 results screen once all four story levels are
complete (press **C** or VIEW CERTIFICATE).

### Optional — the witness phone

East of the report desk, a phone lies on a stand marked **WITNESS PHONE**. It
belongs to Jamie Park, a coworker of the phone's owner, who shared it by
consent. Press **E** to open it as a messages app: click a contact to open the
conversation, scroll with the mouse wheel, by dragging, or with **Up/Down**,
and press **Esc** to go back to the list (and Esc again to close). It is not
needed to finish the level.

What it shows: the same fake "CyVerse IT Support" account tried Jamie first;
Jamie asked for a ticket number, checked with the real help desk (ticket
#4471) and blocked it. Jamie also tried to warn the owner, whose replies show
the after-hours return to the office from Case 2. Jamie's copy of that
conversation is longer than what the image holds from the owner's side, a
reminder that a witness's device can hold messages the other party no longer
has.

---

## 4. Stations and tools at a glance

| Station / tool | Where | What it does |
| --- | --- | --- |
| Evidence locker | South wall, by the entrance | Holds the phone handed off from the SOC (or a training device). Must be emptied before intake. |
| Briefing TV | First room | 11 slides on custody, hashing, processing and examination. Unlocks the lab door. |
| 01 Evidence Intake | Lab, left | Chain-of-custody form (entry 2). Its plinth screen mirrors progress, e.g. `CUSTODY 2 / 4`. |
| 02 Analyze Evidence | Lab, centre | Three monitors: left = acquisition and hash, centre = live case readout (question, progress, score), right = the working copy. Opens the Evidence Explorer. |
| Write blocker + cradle | On the 02 desk | The phone docks here; the blocker's amber LED shows no writes reach the evidence. |
| Evidence Explorer | Full-screen at 02 | Process (case setup) then Examine (categories, list, details, tagging). |
| 03 Forensic Report | Lab, right | Locked until intake and both cases are done; its monitor shows `CASEWORK n / 11`. Filing it starts the knowledge check. |
| Witness phone | Stand east of the report desk | Optional. A coworker's phone shared by consent: contact list, scrollable conversations. |

---

## 5. Instructor answer key and rubric

### 5.1 Chain-of-custody entry 2

| Blank | Correct answer | Why |
| --- | --- | --- |
| ITEM # | **#1** | Same phone as entry 1; the item number never changes between handoffs. |
| RELEASED BY | **L. Torres / SOC Analyst** | Whoever had custody before the lab: the analyst who received it in entry 1. |
| RECEIVED BY | **You / Digital Forensics** | Whoever takes custody now. |
| REASON | **For analysis** | Every handoff needs a reason, even inside one department. |

Common wrong answers: "#2" (treating a handoff as a new item), "R. Alvarez /
Client IT" as releaser (skipping the SOC's custody), and "None: same department".

### 5.2 Process (case setup)

| Step | Correct pick | Teaching point |
| --- | --- | --- |
| 1 Case details | **Phishing / malware** | The briefing describes a phishing campaign; the SOC caught unexplained activity on the owner's workstation. |
| 2 Evidence source | **Phone** | The evidence label says item #1 is a work phone (it runs the invented NovaOS). The tool needs the device type to know where data lives. Wrong picks: Vehicle Operating System (a car's infotainment unit), Tablet, Laptop. |
| 3 Acquisition | **Full file system** | Quick (logical) skips chat databases, deleted records and photo GPS, which Case 2 needs. Lock bypass isn't needed (the owner gave the passcode) and needs legal authority. |
| 4 Options | Any combination | None is required. Hash match and AI triage give the most visible clues. |

### 5.3 Case 1: Spartan Gold (the phishing kill chain)

| # | Question (short) | Tag this item | Category | Points |
| --- | --- | --- | --- | --- |
| 1 | Text that delivered the scam | SMS from +1 408 555 0199, 07-14 08:02 ("You've won a Spartan Gold reward…") | Text messages | 120 |
| 2 | Phishing email | "Claim Your Spartan Gold Reward" from prizes@spartan-rewards.com, 07-14 08:03 | Email | 120 |
| 3 | Did the owner click? | spartan-rewards.com/claim-now, 07-14 08:15 (opened from the SMS link) | Web history | 120 |
| 4 | App not from the app store | Gold Claim, 07-14 08:16 (installed from a browser download) | Installed apps | 120 |
| 5 | App traffic to the attacker | Gold Claim → 45.133.7.22, 07-14 08:17 (2.4 MB sent) | Network | 120 |
| 6 | The pivot: a new domain on the same IP | DNS gold-updates.net → 45.133.7.22, 07-14 08:18 | Network | 140 |

Distractors to discuss: the legitimate "Spartan Pride Week" email from
news@cyverse.edu (Q2), the APK download record next to the claim-now visit (Q3),
and the DNS lookup of spartan-rewards.com, which is the domain already known (Q6).

### 5.4 Case 2: Midnight Exfil (insider exfiltration by social engineering)

| # | Question (short) | Tag this item | Category | Points |
| --- | --- | --- | --- | --- |
| 1 | Chat asking for confidential files | From "CyVerse IT Support", 07-15 18:30 ("…payroll sheet, client list, board salaries and the merger draft TONIGHT…") | Chats | 140 |
| 2 | Phone back in the office after hours | Joined CyVerse-Staff, 07-15 23:40 | Wi-Fi & location | 140 |
| 3 | First photo of a confidential document | IMG_0412.jpg (payroll_2026.xlsx), taken 07-15 23:52 in the office | Photos | 140 |
| 4 | Chat that sent the photos out | To CyVerse IT Support, 07-16 00:11 ("sent all 4…", 4 attachments) | Chats | 140 |
| 5 | Proof "IT Support" isn't CyVerse, tied to Case 1 | Chat contact added: CyVerse IT Support, 07-15 09:12 (freechat account, not in directory, last seen from 45.133.7.22) | Chats | 160 |

Discussion points: the morning CyVerse-Staff join at 08:20 is normal (Q2); the
sunset photo's AI "possible deepfake" flag is wrong (its metadata is ordinary:
taken at home at 20:15), which is the lesson that AI flags are leads; the "delete
this chat" message was recovered from deleted space, which only a full file
system acquisition finds.

### 5.5 Knowledge check

Asked after the report, in this order; the options are shuffled on screen.

| # | Question (short) | Correct answer | Teaching point |
| --- | --- | --- | --- |
| 1 | Which item number did entry 2 keep? | **#1, the same number at every handoff** | Each handoff adds a row, not a new item. |
| 2 | Why a write blocker and a SHA-256 check? | **To prove the copy matches the phone and nothing changed it** | Wrong picks: unlocking the phone, speeding up processing. |
| 3 | Which acquisition finds deleted chats and photo GPS? | **Full file system** | Quick (logical) skips them; SIM only holds contacts and the number. |
| 4 | App → IP → second domain on the same IP is called? | **Pivoting** | Wrong picks: hashing, chain of custody. |
| 5 | What to do with the AI "possible deepfake" flag? | **Treat it as a lead and check the metadata** | Wrong picks: report it as proof, delete the photo. |

Each answer goes through the shared quiz card: 100 points for a correct answer
(more on a streak, with the combo multiplier), 25 for a wrong one. The results
screen's Knowledge Check line counts these together with first-try tags.

### 5.6 Scoring

| Item | Points |
| --- | --- |
| Custody form accepted | 150 |
| Each question, tagged first try with no hint | 120–160 (as listed above) |
| Hint used | half of that question's points |
| Any wrong tag on that question first | one third of its points (minimum 20); this replaces the hint halving |
| Maximum for the level's tasks | **1,610** (150 + 740 + 720) |
| Knowledge check | 100 per correct answer (streak bonus on top), 25 per wrong one; 500+ for a clean run |

The results screen also tracks quiz accuracy (first-try tags / questions),
streaks, and time. Hard mode multiplies the par score.

**Suggested classroom rubric** (if you grade the run):

| Grade | Evidence |
| --- | --- |
| Excellent | Level complete, ≥ 1,300 points, no more than 2 hints, can explain the pivot (IP → second domain) and why the deepfake flag is wrong. |
| Proficient | Level complete, ≥ 1,000 points, can explain why entry 2 keeps item #1 and why the copy is hash-verified. |
| Developing | Level complete with heavy hint use, or cannot explain custody or hashing in their own words. |

Discussion prompts: Why does every handoff need its own row? What would a
defence attorney ask if entry 2 were missing? Why image through a write blocker
instead of browsing the phone? What else would you check before trusting an AI
flag?

---

## 6. Troubleshooting

| Symptom | Cause / fix |
| --- | --- |
| Evidence Intake says "retrieve the device first" | Take the phone out of the evidence locker on the south wall by the entrance. |
| The lab door won't open | Finish the briefing (scrub to the end with →). |
| "Hands are full" at the locker | Put the carried item down with Q, then take the device. |
| The desk says "Complete the chain-of-custody form first" | Intake isn't accepted yet; go back to 01 and fix the red [X] blanks. |
| Pressing E at the desk didn't open the Explorer | The first E docks the phone. Press E again (this also skips the imaging). |
| The case closed but there are more questions on the task list | Case 2 is a new file: press Esc, then E at the desk again. |
| The report desk says "locked" | Both cases (11 findings) and intake must be done first; check the task list. |
| Number keys don't answer the knowledge check | Wait for "Press 1, 2 or 3 to answer" under the question (a short read delay), and click the game first so it has keyboard focus. |
| The witness phone won't scroll | Open a conversation first (the contact list fits on one screen), then use the wheel, drag, or Up/Down. |
| Keys don't type into the search box | Click inside the browser window first so the game has keyboard focus. |
| Pressing Esc drops out of mouse look / fullscreen | That's the browser. Use **Tab** to close screens instead, then click to re-capture the mouse. |
| Left the level halfway | Progress inside a level isn't saved; re-entering from the Hub starts Level 3 fresh. Completing it is saved. |
| Text is small on a small window | Make the browser window larger or use fullscreen; the UI scales with the window. |
| Animations are distracting | Settings → Reduce Motion. |

For developers: Level 3 is generated in code at load time
(`Level3ForensicsSceneFactory`, `Level3ForensicsPolish`). The flow lives in
`Level3ForensicsManager`, the custody form in `ChainOfCustodyForm`, the Explorer
in `EvidenceExplorer` and `EvidenceExplorer.Process`, and the case content in
`InvestigationCase` and `PhoneImage`. The knowledge check questions are in
`Level3KnowledgeCheck`, the witness phone in `WitnessPhone` and
`WitnessPhoneContent`, and the certificate in `CertificateCard`. The PlayMode tests in
`SocHandoffAcceptanceTests` play the level end to end and gate the deploy.
