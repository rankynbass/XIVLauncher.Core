# ImGuiColors.cs — Specification

**File:** `src/XIVLauncher.Core/ImGuiColors.cs` · 112 lines · 3,952 bytes
**Namespace:** `XIVLauncher.Core` · **Declaration:** `public static class ImGuiColors`
**Role:** Named palette constants for the UI (immutable design tokens)

---

## 1. Purpose

Centralises the RGBA colours used across the launcher's ImGui drawing code so pages reference semantics (`ImGuiColors.BlueShade1`) instead of magic floats, and so the game-side role colours (tank/healer/dps) and the "parsed log" colours used by Dalamud-style overlays stay consistent everywhere.

All colours are `Vector4` in **RGBA, 0–1, straight alpha**, the form ImGui's style API consumes directly (`ImGui.PushStyleColor(ImGuiCol.X, color)`).

## 2. Palette

### 2.1 Dalamud-derived colours (shared with the plugin overlay's palette)

| Member | RGBA | Notes |
| --- | --- | --- |
| `DalamudRed` | `1.0, 0.0, 0.0, 1` | |
| `DalamudGrey` | `0.7, 0.7, 0.7, 1` | |
| `DalamudGrey2` | `0.7, 0.7, 0.7, 1` | **duplicate of `DalamudGrey`** (kept for name compatibility with the Dalamud palette) |
| `DalamudGrey3` | `0.5, 0.5, 0.5, 1` | |
| `DalamudWhite` | `1.0, 1.0, 1.0, 1` | |
| `DalamudWhite2` | `0.878, 0.878, 0.878, 1` | |
| `DalamudOrange` | `1.0, 0.709, 0.0, 1` | |
| `DalamudYellow` | `1.0, 1.0, 0.4, 1` | |
| `DalamudViolet` | `0.770, 0.700, 0.965, 1.0` | |

### 2.2 Game role colours (class-role palette)

| Member | RGBA | Source annotation |
| --- | --- | --- |
| `TankBlue` | `0.0, 0.6, 1.0, 1` | `UIColor37` |
| `HealerGreen` | `0.0, 0.8, 0.1333333, 1` | `UIColor504` |
| `DpsRed` | `0.7058824, 0.0, 0.0, 1` | `UIColor545` |

### 2.3 Parsed-log severity colours

| Member | RGBA |
| --- | --- |
| `ParsedGrey` | `0.4, 0.4, 0.4, 1` |
| `ParsedGreen` | `0.117, 1.0, 0.0, 1` |
| `ParsedBlue` | `0.0, 0.439, 1.0, 1` |
| `ParsedPurple` | `0.639, 0.207, 0.933, 1` |
| `ParsedOrange` | `1.0, 0.501, 0.0, 1` |
| `ParsedPink` | `0.886, 0.407, 0.658, 1` |
| `ParsedGold` | `0.898, 0.8, 0.501, 1` |

### 2.4 Launcher theme blues (used by the window chrome / background)

| Member | RGBA |
| --- | --- |
| `Blue` | `0.3647059, 0.4549020, 0.6745098, 1` |
| `BlueShade0` | `0.1686275, 0.1882353, 0.2313725, 1` |
| `BlueShade1` | `0.2, 0.2235294, 0.2784314, 1` |
| `BlueShade2` | `0.2705882, 0.3019608, 0.3725490, 1` |
| `BlueShade3` | `0.2980392, 0.3843137, 0.5803922, 1` |

`BlueShade0` is the main window background colour — `LauncherApp.Draw()` pushes it as `ImGuiCol.WindowBg` (with `SetNextWindowBgAlpha(0.75f)` applied on top). The `x/255`-style repeating decimals indicate the palette was authored as 8-bit values (`43/255`, `48/255`, `59/255`, …).

### 2.5 Text colours

| Member | RGBA | Notes |
| --- | --- | --- |
| `Text` | `0.7843137, 0.7960784, 0.8196078, 1` | near-white, slightly cool |
| `TextDisabled` | `Text.X, Text.Y, Text.Z, 0.5` | derived from `Text`; alpha halved |

## 3. Mutability contract (deliberate, not uniform)

Two flavours of accessor coexist in the same class:

| Flavour | Members | Consequence |
| --- | --- | --- |
| `get` only | `DalamudYellow`, `DalamudViolet`, `Parsed*` (all 7), `Blue`, `BlueShade0–3`, `Text`, `TextDisabled` | compile-time constants: cannot be reassigned, and ImGui's `ImColor` conversion can be inlined at the call site |
| `get`, `internal set` | `DalamudRed`, `DalamudGrey`, `DalamudGrey2`, `DalamudGrey3`, `DalamudWhite`, `DalamudWhite2`, `DalamudOrange`, `TankBlue`, `HealerGreen`, `DpsRed` | writable **from inside `XIVLauncher.Core` only** |

The `internal set` members are therefore theme-able at runtime (only from code in this assembly), which is how a theme model would override the Dalamud/role colours without exposing mutation to consumers such as components or plugins. No code in this file performs that assignment; any theming must be added where the style model is applied.

Note `TextDisabled` is a *derived* `get`-only accessor: if `Text` were ever made writable, `TextDisabled` must become an accessor over it (which it already is) — order of evaluation is per-access, so it always tracks `Text`.

## 4. Non-contract / caveats

- These are `Vector4` colours, **not** `ImColor` handles: pushing the same constant repeatedly is cheap, but ImGui colour state must still be paired (`PushStyleColor`/`PopStyleColor`).
- No sRGB/gamma transform is applied: values are used as-is by the SDL3 GPU renderer path, so the perceived colour depends on the swapchain's SDR interpretation set in `ImGuiBindings` (`SDLGPUSwapchainComposition.Sdr`).
- `DalamudGrey` == `DalamudGrey2`: do not "fix" one of them when restyling, since the two names exist to match external palette names.
- There is no `ImGuiColors` mapping to ImGui style slots (`ImGuiCol.*`); that mapping is done by `Style/StyleModel*.cs` (see `StyleModelV1.cs`) — this file supplies values only.
