# FtsPage.cs — Specification

**File:** `src/XIVLauncher.Core/Components/FtsPage.cs` · 110 lines · 3,222 bytes
**Namespace:** `XIVLauncher.Core.Components` · **Declaration:** `public class FtsPage : Page`
**Role:** Steam Deck first-time-setup screen: full-window artwork with two invisible click regions over the painted buttons

---

## 1. Purpose

`FtsPage` shows one of two Steam Deck artworks (`steamdeck_fts.png`, `steamdeck_fterror.png`) at fixed coordinates, with two invisible `ClickArea` rectangles standing in for the transparent ImGui buttons that used to overlay the painted "Open the Guide" and "I'm done, launch the game" buttons (`Components/Common/ClickArea.cs.md`).

Usings: `Eto.Drawing`, `Eto.Forms`, `XIVLauncher.Core.Components.Common` (for `ClickArea`).

## 2. Surface

| Member | Kind | Notes |
| --- | --- | --- |
| `steamdeckFtsImage` | `private readonly ImageView` (line 18) | `steamdeck_fts.png`, 1280×800, `Visible = false` (lines 26–31) |
| `steamdeckAppIdErrorImage` | `private readonly ImageView` (line 19) | `steamdeck_fterror.png`, 1280×800, `Visible = false` (lines 33–38) |
| `isSteamDeckAppIdError` | `private bool` (line 21) | selects which artwork and which button semantics are active |
| `FtsPage(LauncherApp app)` | ctor (lines 23–66) | builds the `PixelLayout`, images, and the two click areas |
| `OpenFtsIfNeeded()` | `public` (lines 68–95) | decides whether to show FTS, the FTS-error screen, or nothing |
| `FinishFts(bool save)` | `private` (lines 97–103) | returns to `Main`, optionally persists `CompletedFts` |
| `OnShow()` | `public override` (lines 105–109) | flips the two images' `Visible` from `isSteamDeckAppIdError` |

Layout (lines 40–65): `PixelLayout` with the two images at `(0, 0)` (lines 42–43) and the click areas at `(316, 481)` (line 54) and `(316, 598)` (line 63), each `649x101`.

## 3. Click areas

| Region | Position | Size | Normal behaviour | App-ID-error behaviour |
| --- | --- | --- | --- | --- |
| `openGuideButton` (lines 45–53) | `316, 481` (line 54) | `649x101` | `AppUtil.OpenBrowser("https://goatcorp.github.io/faq/steamdeck")` | `Environment.Exit(0)` (line 51) |
| `finishFtsButton` (lines 56–62) | `316, 598` (line 63) | `649x101` | `FinishFts(true)` | **nothing** (guarded by `if (!this.isSteamDeckAppIdError)`) |

Both areas are added at the same size, mirroring the two painted buttons in the artwork. The coordinates are hard-coded for a 1280×800 window; they are absolute positions inside the page, and `PixelLayout` does not scale them.

The click areas paint nothing, so the visible UI is entirely the artwork (`ClickArea.cs.md`).

## 4. `OpenFtsIfNeeded()` decision table

Order matters (lines 70–95):

1. `CoreEnvironmentSettings.IsDeckFirstRun.HasValue` — if the env var is present:
   - `IsDeckFirstRun.Value == true` → `App.State = Fts`, return (lines 74–76);
   - otherwise return without showing FTS (line 78).
   **The env var short-circuits everything below it**, including the App-ID error check.
2. `!(App.Settings.CompletedFts ?? false) && Program.IsSteamDeckHardware` → `App.State = Fts`, return (lines 81–84).
3. `Program.IsSteamDeckHardware && (Program.Steam == null || !Program.Steam.IsValid)`:
   - `App.Settings.IsIgnoringSteam ?? false` → return, skipping the error screen (line 90; the comment records that this fixes the deck always showing the FTS error screen),
   - otherwise `App.State = Fts` **and** `isSteamDeckAppIdError = true` (lines 92–93).

Note the third case sets state and flag **without returning**, so it is the end of the method.

`App.State` is the posted setter (`LauncherApp.cs.md` §2), so `OpenFtsIfNeeded()` is safe from any thread; `isSteamDeckAppIdError` is written here and read by `OnShow()` on the UI thread.

## 5. Consumers

| Consumer | Site | Use |
| --- | --- | --- |
| `LauncherApp.RunStartupTasks()` | `LauncherApp.cs:280` | `this.ftsPage.OpenFtsIfNeeded()` — **only under `#if FLATPAK`** |
| `LauncherApp.SetState` | `LauncherApp.cs:92` | `this.ftsPage.OnShow()` when entering `LauncherState.Fts` |
| `LauncherApp` construction | `LauncherApp.cs:150` | page built once at startup, on the UI thread |

The state `LauncherState.Fts` is reachable **only** through `OpenFtsIfNeeded()` — nothing else sets it. On non-Flatpak builds the page is therefore never shown, so `OnShow()`'s visibility flip and the click areas are inert in the standard Linux build. (`MainPage`'s Steam Deck handling uses `LauncherState.SteamDeckPrompt`, `MainPage.cs.md`.)

## 6. `FinishFts`

`FinishFts(bool save)` (lines 97–103): sets `App.State = LauncherState.Main` first, then `App.Settings.CompletedFts = true` when `save`. Order matters: the state change is posted, the setting write is synchronous, so the setting is persisted before the main page's `OnShow()` runs on the UI thread.

Only one call site: `finishFtsButton`'s click handler, with `save: true`.

## 7. `OnShow()`

Lines 105–109 set:

- `steamdeckFtsImage.Visible = !isSteamDeckAppIdError`
- `steamdeckAppIdErrorImage.Visible = isSteamDeckAppIdError`

Both images start `Visible = false` in the constructor, so visibility is decided only when the page is shown. Because both images occupy the same `PixelLayout` slot `(0, 0)`, the visible one is the full window; the hidden one takes no space.

## 8. Quirks

- **`Environment.Exit(0)` from a click handler** (line 51) terminates the process without Eto shutdown, Eto form close, or `Patcher.StartCancellation()` (`Program.cs.md`). The App-ID-error "open guide" region is therefore a *quit* button — the artwork paints it as such.
- `openGuideButton` and `finishFtsButton` are local variables in the constructor; the click lambdas capture nothing that keeps them alive beyond the layout's own references, so there is no way for a caller to trigger them programmatically.
- The click areas have no keyboard focus, no accessibility, and no visual feedback: clicking is the only interaction, and the GTK `DrawingArea` shows no hover/pressed state.
- `steamdeck_fts.png` is 1280×800 8-bit **RGB** (no alpha); `steamdeck_fterror.png` is 1280×800 RGBA. The `Visible = false` initial state is what makes the pair mutually exclusive.
- `isSteamDeckAppIdError` is never reset, so once the App-ID error path is taken, the page is stuck in error mode for the process lifetime.

## 9. Referenced / referenced-by

Uses: `Page` (`Page.cs.md`), `ClickArea` (`Components/Common/ClickArea.cs.md`), `AppImages` (`AppImages.cs.md`), `AppUtil.OpenBrowser` (`AppUtil.cs.md`), `CoreEnvironmentSettings`, `Program.Steam`, `Program.IsSteamDeckHardware`, `App.Settings`.
Referenced by: `LauncherApp` (construction, `RunStartupTasks`, `SetState`).
