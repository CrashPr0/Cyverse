# Palette materials

These files are the colours of the props the game builds in code: stations,
kiosks, server cabinets, lockers, ceiling lights, the Level 4 desks, and the
office furniture. Change a colour here and every prop that uses it changes, in
every level and in the web build.

## How to edit

- **In the Project window:** select a file here and change `Albedo` (the
  colour), `Metallic` or `Smoothness` in the Inspector. Lights and LEDs also
  have an `Emission` colour; that is how bright they glow.
- **While playing:** press Play, click a prop in the Scene view or Hierarchy,
  and edit its material in the Inspector. The material shown is the file in
  this folder, so the change stays after you stop Play.

Most props are built when a level loads, so they only appear in Play mode.
Editing a material on one of the bought furniture models (wood, leather,
chrome) has no effect in the game; the Furniture files below replace those
at load.

If a file is deleted, the game falls back to the original colour in code.

## What each file colours

| File | Used for |
|---|---|
| Body | Main panels of every station, kiosk, vault, desk and sign plate |
| Trim | Frames, bezels, edges, posts and rods |
| Recess | Inset panels, vents, kick plates, keypads |
| RecessDeep | The darkest insets: server bays and vents, the enrollment kiosk |
| Chrome | Bolts, handles, hinges |
| CaseBody | Data Triage crate cases and IR playbook binders |
| CabinetBody | Server cabinet and locker bodies |
| ServerBlade | Server faceplates inside the cabinets |
| ServerBlank | Blanking panels between servers |
| LockerDoor | Locker doors |
| LockerPlate | Locker number plates |
| Cable / PatchCable | Cable bundles and blue patch cords on the cable trays |
| CabinetGlow | Lit strips on cabinets and lockers |
| LedGreen / LedCyan / LedAmber / LedRed | Server lights and locker lamps |
| CeilingHousing / CeilingDivider | Ceiling light housings and the dividers along them |
| FurnitureFabric | Sofas, chairs and small desk props (was leather and plastic) |
| FurnitureTop | Desk and table tops (was wood) |
| FurnitureMetal | Chair legs and frames (was chrome) |
| FurnitureDark | Black glass and dark plastic on the furniture |
| AttackDeskBody / AttackDeskTrim / AttackDeskRecess | Level 4 workstation desks and monitors |
| AttackBoard / AttackBoardFrame | Level 4 hanging station boards and room header |

Level accent colours (the cyan, blue, coral, green and orange glows) are set
per level in code and are not in this folder yet.
