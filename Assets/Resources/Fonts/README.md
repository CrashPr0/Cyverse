# Fonts

`CyverseSans.ttf` is the font for all HUD text and 3D signs that are not
TextMesh Pro. It is loaded by `HudUI.LoadFont()`.

It exists because Unity's built-in font (`LegacyRuntime.ttf`) ships in WebGL
builds as a 332-character subset: no em dash, arrows, bullet, ellipsis, curly
double quotes or box and triangle shapes. Text using those characters showed
gaps or empty boxes in the browser, while the editor looked fine because it
falls back to system fonts.

The file is a subset of Liberation Sans (the same typeface as the built-in
font, so sizes and line heights do not change) covering Latin, punctuation,
arrows, maths signs, box drawing and geometric shapes: 437 characters, 64 KB.
It has no check mark (U+2713) and no play triangle (U+25B6); write "OK" or
use U+25BA instead.

Liberation Sans is licensed under the SIL Open Font License 1.1 (see
`OFL.txt`). The licence reserves the name "Liberation" for the original, so
this modified copy is named "Cyverse Sans".

To rebuild it with fontTools from `Assets/TextMesh Pro/Fonts/LiberationSans.ttf`:
subset to U+0020-007E, U+00A0-017F, U+2010-2027, U+2030-203A, U+2044, U+20AC,
U+2122, U+2190-2199, U+21B5, U+2202-2265, U+2500-257F, U+25A0-25FF and
U+2600-266F, then set name IDs 1, 4 and 16 to "Cyverse Sans".
