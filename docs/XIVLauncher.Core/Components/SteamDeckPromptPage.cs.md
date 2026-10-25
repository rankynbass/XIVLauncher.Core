# SteamDeckPromptPage.cs — Specification

**File:** `src/XIVLauncher.Core/Components/SteamDeckPromptPage.cs` · 30 lines · 799 bytes
**Namespace:** `XIVLauncher.Core.Components` · **Declaration:** `public class SteamDeckPromptPage : Page`
**Role:** The full-window "switch to Steam" prompt shown after launching the game on a Steam Deck

---

## 1. Purpose

`SteamDeckPromptPage` displays `steamdeck_switchprompt.png` filling the window, with no interactive regions at all. The immediate-mode version drew the same full-window image with a transparent full-window button for Steam Deck's touch-to-continue gesture; the Eto version has no click area, so the page is display-only.

Usings: `Eto.Drawing`, `Eto.Forms`.

## 2. Surface

| Member | Kind | Notes |
| --- | --- | --- |
| `switchPromptImage` | `private readonly ImageView` (line 14) | `steamdeck_switchprompt.png`, `Size = new Size(1280, 800)` (lines 19–23) |
| `SteamDeckPromptPage(LauncherApp app)` | ctor (lines 16–29) | builds a `PixelLayout`, adds the image at `(0, 0)` (line 26), assigns `Content` (line 28) |

`OnShow()` is **not overridden**: entering `LauncherState.SteamDeckPrompt` runs `Page.OnShow()`'s empty body (`LauncherApp.cs.md` §2, `Page.cs.md`), and the page stays as constructed.

## 3. Consumers

| Consumer | Site | Use |
| --- | --- | --- |
| `LauncherApp` ctor | `LauncherApp.cs:151` | page built once, on the UI thread |
| `LauncherApp.SetState` | `LauncherApp.cs:95` | calls `OnShow()` on entering `LauncherState.SteamDeckPrompt` |
| `LauncherApp.CurrentPage` | `LauncherApp.cs:117` | state → page instance |
| `MainPage.StartGameAndAddon` | `MainPage.cs:787` | `App.State = LauncherApp.LauncherState.SteamDeckPrompt` |

`MainPage.cs:787` is the **only** place that enters this state: after the game is launched, inside the `if (!Program.IsSteamDeckHardware || CoreEnvironmentSettings.IsSteamCompatTool)` check at `MainPage.cs:781` — the true branch calls `this.Hide()` (`MainPage.cs:783`), the `else` branch shows this prompt.

## 4. Artwork

`AppImages.FromResource("steamdeck_switchprompt.png")` (`AppImages.cs.md`); the embedded PNG is measured as `1280 x 800`, 8-bit/color RGBA, non-interlaced. Widget size equals bitmap size, so Eto's `ImageViewHandler` scales by `Math.Min(scaleWidth, scaleHeight) == 1` and the centring offset is zero — the image is drawn 1:1 at the top-left of its widget, which is the window's top-left (`Eto.Gtk/Forms/Controls/ImageViewHandler.cs`).

`PixelLayout` (`Gtk.Fixed`) positions the image at literal `(0, 0)` and does not resize children, so the page assumes a 1280×800 client area, the size `LauncherForm` sets (`LauncherForm.cs.md`).

## 5. Quirks

- **No interaction, no dismissal:** nothing dismisses this page. It is left when the launcher process exits with the game, or when other code sets `App.State`. `MainPage`'s post-launch path (`MainPage.cs` game-exit wait) is what eventually returns to `Main` (`MainPage.cs.md`).
- **No `ClickArea`** here, unlike `FtsPage.cs.md`; if the artwork's painted button needs to be clickable, a `ClickArea` must be added.
- The page never spins the spinner, never stops timers, and holds no `UITimer`, so it has no background activity.
- The image is cached in `AppImages`, so showing the page repeatedly does not re-decode the PNG (`AppImages.cs.md`).

## 6. Referenced / referenced-by

Uses: `Page` (`Page.cs.md`), `AppImages` (`AppImages.cs.md`), `Eto.Forms.ImageView`, `Eto.Forms.PixelLayout`.
Referenced by: `LauncherApp` (construction, `SetState`, `CurrentPage`), `MainPage.cs:787`.
