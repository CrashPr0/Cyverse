# Footstep sounds

Drop audio files here — no code changes needed.

- **`Default/`** — used in every level. Put as many variations as you like
  (6–10 short steps sounds natural). Any file names work.
- **`<SceneName>/`** — optional. A folder named exactly after a scene
  (`Hub`, `Level1_IAM`, `Level2_CyberDefense`, `Level3_Forensics`,
  `Level4_CyberAttack`, `Level0`) replaces `Default/` in that level, e.g.
  carpet in the Hub, hard floor in the SOC.

Each step picks a random clip (never the same one twice in a row) with a
little pitch and volume variation. If a folder is empty, the game falls back
to the built-in synthesized step.

**Clip tips**
- One footfall per file, trimmed tight (about 0.15–0.4 s), no silence at the start.
- Mono, WAV or OGG; match loudness across the set.
- In Unity's import settings, *Load Type: Decompress On Load* suits short clips.

**Tuning** (no art changes needed)
- Loudness: `Sfx.footstepVolume` (GameSystems object), then the player's SFX volume setting.
- Step spacing: `FirstPersonController.stepInterval` (metres walked per step) on the Player.
