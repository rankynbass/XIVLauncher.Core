# ActionButtons.cs — Specification

**File:** `src/XIVLauncher.Core/Components/MainPage/ActionButtons.cs` · 56 lines · 2,014 bytes
**Namespace:** `XIVLauncher.Core.Components.MainPage` · **Declaration:** `public class ActionButtons : Component`
**Role:** The row of three icon buttons under the login frame — account page, service status, launcher settings

---

## 1. Purpose

Replaces the three immediate-mode icon buttons, whose tooltips were drawn with style pushes and whose icons came from the FontAwesome font atlas. Here each button is a `Common.Button` (`Components/Common/Button.cs.md`) with `ShowIcon(icon)` applied and `ToolTip` set directly, so GTK renders the tooltip and the click.

Usings: `Eto.Forms`, `XIVLauncher.Core.Resources.Localization`.

## 2. Surface

| Member | Kind | Notes |
| --- | --- | --- |
| `accountButton`, `statusButton`, `settingsButton` | `private readonly Common.Button` (lines 17–19) | built in ctor (lines 23–25) |
| `ActionButtons()` | ctor (lines 21–32) | horizontal `StackLayout` with the three buttons (lines 27–31) |
| `OnAccountButtonClicked` | `public event Action?` (line 35) | |
| `OnStatusButtonClicked` | `public event Action?` (line 38) | |
| `OnSettingsButtonClicked` | `public event Action?` (line 41) | |
| `MakeIconButton(FontAwesomeIcon icon, string toolTip, Action clicked)` | `private static` (lines 43–55) | `new Common.Button(string.Empty) { Width = 80 }` (lines 45–48), `ShowIcon(icon)` (line 50), `ToolTip = toolTip` (line 51), `Click += clicked` (line 52) |

`MakeIconButton` is static: it does not touch instance state, and each event is wired by the ctor's lambda (`() => this.OnAccountButtonClicked?.Invoke()`, lines 23–25), so `MakeIconButton` takes `Action` and the instance event raising lives in the ctor.

## 3. Buttons

| Button | Icon | Tooltip string | Event |
| --- | --- | --- | --- |
| `accountButton` | `FontAwesomeIcon.User` | `Strings.MyAccount` | `OnAccountButtonClicked` |
| `statusButton` | `FontAwesomeIcon.Heartbeat` | `Strings.ServiceStatus` | `OnStatusButtonClicked` |
| `settingsButton` | `FontAwesomeIcon.Cog` | `Strings.LauncherSettings` | `OnSettingsButtonClicked` |

All three are `Width = 80` and start with an empty label, which `ShowIcon` replaces with the glyph — so the visible content is glyph only (no text; §3 of `Components/Common/Button.cs.md`).

## 4. Consumers

`MainPage` builds and wires the row (`MainPage.cs.md`):

| Site | Behaviour |
| --- | --- |
| `MainPage.cs:53` | `new ActionButtons()` on the UI thread |
| `MainPage.cs:59` | `OnSettingsButtonClicked += () => this.App.State = LauncherApp.LauncherState.Settings` |
| `MainPage.cs:60` | `OnStatusButtonClicked += () => AppUtil.OpenBrowser("https://is.xivup.com/")` |
| `MainPage.cs:61` | `OnAccountButtonClicked += () => AppUtil.OpenBrowser("https://sqex.to/Msp")` |
| `MainPage.cs:103` | `layout.Add(this.actionButtons, 0, 1, xscale: false, yscale: true)` — the row is the second column cell and scales vertically |

`OnAccountButtonClicked` opens the Square Enix account management page in the browser; the local account picker is the **friends** button in `LoginFrame`, which opens `AccountSwitcher` (`AccountSwitcher.cs.md`, `LoginFrame.cs:117`).

## 5. Thread / UI rules

Construction and `ShowIcon`/`ToolTip`/`Click` wiring are UI-thread (native widgets). Click handlers fire on the UI thread; the two `AppUtil.OpenBrowser` calls start a process, and the settings handler changes `App.State`, which marshals via `Ui.Post` in `LauncherApp.SetState` (`LauncherApp.cs.md`). No handler blocks.

## 6. Quirks

- The row is `Orientation.Horizontal` with default spacing (0), so the three 80 px buttons sit adjacent; HEAD spaced icon buttons with an item-width/spacing trick that has no direct Eto equivalent here.
- `Width = 80` is fixed regardless of glyph width; GTK centers the glyph in the button.
- The wrapper's `Click` is `Action?` (`Components/Common/Button.cs.md` §7), so handlers cannot distinguish keyboard activation from a mouse click.
- `ActionButtons` has a parameterless ctor: it does not take `LauncherApp` (unlike most components), because the events are wired by `MainPage`.
- `Component.Id` exists on this wrapper (`Components/Component.cs.md` §2) and is unused.
- Buttons have no `Name`/`AccessibleName` beyond the label glyph, so screen readers announce an icon glyph; the tooltip is GTK's hover text, not an accessibility label.

## 7. Referenced / referenced-by

Uses: `Component` (`Component.cs.md`), `Common.Button` (`Components/Common/Button.cs.md`), `FontAwesomeIcon`, `Strings`.
Referenced by: `MainPage.cs` (lines 41, 53, 59–61, 103).
