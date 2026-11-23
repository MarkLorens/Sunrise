# Sunrise (Woohoo Land)

A 2D game in Unity 6000.6.3f1 (URP). You play a spirit who possesses park visitors and staff to cause mischief.
Scenes: `MainMenu` (build index 0) and `FerrisWheelLevel` (1). The scripts are in `Assets/Scripts`.

## Working rules (from the owner)

- Change only what was asked. Don't touch assets, layouts, wording or behaviour nobody mentioned. Ask if unclear.
- Explain things to the owner in Indonesian (Only if the logged in account is Aulia, remain in English when speaking to account "Max").
- Don't commit or push unless asked.
- Never edit `.cs` files while the Editor is in Play mode. The owner often plays while you work. Check
  `EditorApplication.isPlaying` first. If it's on, prepare the edits elsewhere and wait.
- Save slots in `~/Library/Application Support/DefaultCompany/Sunrise_Project/` (`save_<n>.json` + `.png`) belong
  to the owner. Back them up before a test, use only empty slots, delete only slots your test made, never delete all.

## Settings that affect the whole game

Everything in Options is a game-wide setting. New code has to respect each one. Don't hardcode around them.

| Setting                                                                                       | Where it lives                                                                                | What new code must do                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                             |
| --------------------------------------------------------------------------------------------- | --------------------------------------------------------------------------------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Closed captions (Options > General, off by default)                                           | `Settings.ClosedCaptions`, shown by `ClosedCaptions`                                          | **Every sound needs a caption.** Add its words to `Captions.cs`, the one file with all caption text. Pass the caption where the sound plays: `Sfx.Play(name, volume, pitch, Captions.X)`. For FMOD one-shots, call `ClosedCaptions.Show(Captions.X)`. For a sound that goes on (loops, hums), call `ClosedCaptions.Hold(Captions.X)` every frame it's heard. Footsteps and the spirit's drifting hum get no caption: they're always there. Captions show in the middle of the screen, low down, as white text with no background. |
| Game controls (Options > Game Control)                                                        | `KeyBindings`: Control A, Control B, or keys changed one by one (custom)                      | Read input only through `Controls` (the project's input actions, which `KeyBindings` overrides). Never read `Keyboard.current.xKey` for gameplay. Never write a key's name ("J", "K", "Space") in text or UI. Use `KeyBindings.Name(bind)` for keycaps, `KeyBindings.Words(bind)` for sentences and pills, and the `[J] [K] [WASD] [Tab] [Space] [Esc]` tokens in `GameText` (resolved by `KeyBindings.Token`). A new action means a new `KeyBindings.Bind`, a row in the Game Control page, and a line in `Apply()`.             |
| Window mode, graphics, screen shake, resolution, depth of field, vertical sync, anti-aliasing | `Settings` (PlayerPrefs)                                                                      | Camera shake goes through `CameraRig.Shake` (scaled by `Settings.ShakeStrength`). Resolutions come from the screen's own list (`Settings.Resolutions`). Anti-aliasing is the park camera's TAA (`CameraRig.ApplyAntiAliasing`), which also smooths the see-through dither.                                                                                                                                                                                                                                                        |
| Master, Background Music and SFX volume                                                       | `Settings.Volume` (FMOD master bus and the listener), `MusicVolume`, `SfxVolume` (`AudioMix`) | FMOD events whose path contains "Music" follow Background Music; every other event follows SFX automatically. Unity clips go through `Sfx.Play`, which applies SFX volume. Don't add an AudioSource that skips these.                                                                                                                                                                                                                                                                                                             |
| Language                                                                                      | English only for now                                                                          | Keep player-facing text in `GameText` (and captions in `Captions`), not scattered in scripts, so it can be translated later.                                                                                                                                                                                                                                                                                                                                                                                                      |

## UI conventions

- UI is built at runtime with uGUI. `MainMenu.Screen(name, sortingOrder)` makes a 1920x1080 canvas.
- Lay out from the Figma designs with `Figma.Place / Picture / Words` (design frame 1512x982).
- Use only the type scale in `Figma.Type`:
  - Atma (`ParkArt.TitleFont`) for page titles, headings and special text.
  - Arvo (`ParkArt.Font`) for titles, selections, subtitles and body text.
  - Don't invent font sizes.
- The artist's UI art lives in `Assets/UI Asset`. Load it with `UiArt.Get("Folder/Name")`. After adding art, run
  Tools > Build UI Art List.
- Show keyboard keys as `KeyCap`s (the artist's keycap pictures).
- Sorting orders: HUD 10, tutorial 15, captions 18, pause and menus 20 and above.

## Sound

- FMOD events are in `FMODEvents`, played through `AudioManager` or emitters.
- Small one-shot clips go through `Sfx.Play`.
- Either way, add a caption (see above).
