# EtoColors.cs — Specification

**File:** `src/XIVLauncher.Core/EtoColors.cs` · 46 lines · 2404 bytes
**TargetFramework:** net10.0 (Linux/GTK3 build)
**Role:** the launcher palette as Eto `Color` values, kept from the old ImGui style; the replacement for
`imgui.net` style colors (`ImGuiColors.cs`, deleted).

---

## 1. Purpose

`EtoColors` provides the explicit-text colors the launcher draws on top of native Eto controls
(`Eto.Forms`, https://github.com/picoe/Eto). Native widgets take their colors from the active GTK
theme; these values are used only where text is explicitly coloured — errors, descriptions, warnings,
the spinner, and the OTP tint.

Every color is stored as RGB bytes; `From` converts the old ImGui 0..1 floats with
`(int)Math.Round(r * 255)` (line 40).

Members:

| Member | Old ImGui source | Notes |
| --- | --- | --- |
| `Red` | `1,0,0` | errors, destructive labels |
| `Grey`, `Grey2` | `0.7,0.7,0.7` | duplicate values, both kept |
| `Grey3` | `0.5,0.5,0.5` | section headers, captions |
| `White` | `1,1,1` | unused |
| `Orange` | `1,0.709,0` | unused |
| `Yellow` | `1,1,0.4` | unused |
| `Violet` | `0.770,0.700,0.965` | unused |
| `Blue` | launcher button color | spinner color |
| `BlueShade0..3` | window/frame shades | unused (theme provides them) |
| `Text` | ImGui text color | unused (theme provides it) |
| `OtpChildBackground` | `WindowBg` 0.06 grey at 40% alpha | `Color.FromArgb(102, 15, 15, 15)`, alpha = 102/255 ≈ 40%; no longer consumed (GTK renders no alpha) |
| `OtpChildOpaqueBackground` | the same tint, composited | `Color.FromArgb(28, 28, 28)` — 40% grey(15) over the dark theme window background, drawn as an opaque bitmap fill |

## 2. Consumers

| Consumer | Site | Member |
| --- | --- | --- |
| `Components/LoadingPage/LoadingPage.cs` | line 65 | `EtoColors.Blue` — `Spinner` color |
| `Components/MainPage/LoginFrame.cs` | line 122 | `EtoColors.Red` — dummy-secrets warning label |
| `Components/MainPage/NewsFrame.cs` | lines 63, 70 | `EtoColors.Grey3` — caption labels |
| `Components/SettingsPage/SettingsEntry{T}.cs` | line 67 (description), lines 73, 80 (validity/warning labels) | `EtoColors.Grey3`, `EtoColors.Red` |
| `Components/SettingsPage/SettingsPage.cs` | lines 133, 141 | `EtoColors.Grey3` — search results headers / no-results label |
| `Components/SettingsPage/Tabs/SettingsTabTroubleshooting.cs` | lines 61, 90, 99, 103, 136 (`Grey3`), line 107 (`Red`) | header/explanation labels, destructive warning |
| `Components/SettingsPage/Tabs/SettingsTabDebug.cs` | lines 40, 47 | `EtoColors.Grey3` section headers |
| `Components/OtpEntryPage.cs` | line 118 | `EtoColors.OtpChildOpaqueBackground` — the OTP child box tint, filled into a 300×200 bitmap |

## 3. Quirks

- The unused members (`White`, `Orange`, `Yellow`, `Violet`, `BlueShade0..3`, `Text`, `Grey`) exist
  only to keep the palette values from the deleted `ImGuiColors.cs` retrievable; the active theme,
  not these, paints native widgets, so removing them does not change rendering.
- `Grey2` duplicates `Grey`; the old style stored them as separate fields.
- Alpha is available only through `Color.FromArgb(a, r, g, b)`; `From` has no alpha parameter, so
  `OtpChildBackground` is the only translucent color. **GTK renders no alpha, and Eto.Gtk never paints
  a `Panel` background**, so translucent palette entries cannot be used as widget fills: verified on
  Eto 2.13/GTK3 that `Panel.BackgroundColor` (opaque and alpha) paints nothing, `Label.BackgroundColor`
  paints opaque colors only, and an `ImageView` shows opaque bitmaps only. Any tint must be baked to an
  opaque colour, as `OtpChildOpaqueBackground` is.

## 4. Cross-references

- `docs/XIVLauncher.Core/Components/LoadingPage/Spinner.cs.md` — color drawn by `Graphics.DrawArc`.
- `docs/XIVLauncher.Core/Components/OtpEntryPage.cs.md` — the opaque bitmap tint behind the centered
  box replacing the ImGui 300×200 child window.
- `docs/XIVLauncher.Core/EtoTheme.cs.md` — dark GTK theme selection that supplies the widget colors.
