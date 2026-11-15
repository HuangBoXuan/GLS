# Chinese UI font

The game embeds the unmodified **Noto Sans SC Medium** OpenType font so Chinese labels work on Android as well as in the editor. It replaces the previous Latin-only Inter font.

- Asset: `Assets/LoopSort/Resources/LoopSort/Fonts/NotoSansSC-Medium.otf`
- Source on this development machine: `C:/Windows/Fonts/Noto Sans SC Medium (TrueType).otf`
- Copyright recorded in the font: © 2014–2020 Adobe (http://www.adobe.com/).
- SHA-256: `9c62ceb174d7529ae4a7f2071f6531991cfadbc2f1897910b48ba951a580ac57`
- License: SIL Open Font License 1.1, also recorded in the font's name table.
- Bundled license: `Assets/LoopSort/Resources/LoopSort/Fonts/OFL-NotoSansSC.txt`, copied from https://raw.githubusercontent.com/google/fonts/main/ofl/notosanssc/OFL.txt.

The full character set is retained, rather than relying on a font installed on the player's device. The APK-reference importer intentionally does not reintroduce the unused Inter font. This font is a separate licensed resource, outside the APK resource hash manifest.
