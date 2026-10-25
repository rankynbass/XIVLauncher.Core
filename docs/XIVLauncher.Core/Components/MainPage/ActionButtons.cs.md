# ActionButtons.cs — Specification

**File:** `src/XIVLauncher.Core/Components/MainPage/ActionButtons.cs` · 61 lines · 2,278 bytes
**Namespace:** `XIVLauncher.Core.Components.MainPage` · **Declaration:** `public class ActionButtons : Component`
**Role:** The row of three icon buttons under the login frame — account page, service status, launcher settings

---

## 1. Purpose

Replaces the three immediate-mode icon buttons, whose tooltips were drawn with style pushes and whose icons came from the FontAwesome font atlas. Here each button is a `Common.Button` (`Components/Common/Button.cs.md`) with `ShowIcon(icon)` applied and `ToolTip` set directly, so GTK renders the tooltip and the click.

Usings: `Eto.Forms`, `XIVLauncher.Core.Resources.Localization`.

## 2. Surface

| Member | Kind | Notes |
| --- | --- | --- |
| `IconButtonSize` | `private const int` (line 19) | 80 — HEAD drew each icon button as an 80x80 square |
| `accountButton`, `statusButton`, `settingsButton` | `private readonly Common.Button` (lines 21–23) | built in ctor (lines 25–27) |
| `ActionButtons()` | ctor (lines 25–36) | horizontal `StackLayout` with the three buttons (lines 29–33) |
| `OnAccountButtonClicked` | `public event Action?` (line 40) | |
| `OnStatusButtonClicked` | `public event Action?` (line 43) | |
| `OnSettingsButtonClicked` | `public event Action?` (line 46) | |
| `MakeIconButton(FontAwesomeIcon icon, string toolTip, Action clicked)` | `private static` (lines 49–60) | `new Common.Button(string.Empty) { Width = IconButtonSize, Height = IconButtonSize }` (lines 51–54), `ShowIcon(icon)` (line 56), `ToolTip = toolTip` (line 57), `Click += clicked` (line 58) |

`MakeIconButton` is static: it does not touch instance state, and each event is wired by the ctor's lambda (`() => this.OnAccountButtonClicked?.Invoke()`, lines 25–27), so `MakeIconButton` takes `Action` and the instance event raising lives in the ctor.

## 3. Buttons

| Button | Icon | Tooltip string | Event |
| --- | --- | --- | --- |
| `accountButton` | `FontAwesomeIcon.User` | `Strings.MyAccount` | `OnAccountButtonClicked` |
| `statusButton` | `FontAwesomeIcon.Heartbeat` | `Strings.ServiceStatus` | `OnStatusButtonClicked` |
| `settingsButton` | `FontAwesomeIcon.Cog` | `Strings.LauncherSettings` | `OnSettingsButtonClicked` |

All three are `Width = IconButtonSize, Height = IconButtonSize` (80x80, matching HEAD's square buttons) and start with an empty label, which `ShowIcon` replaces with the glyph — so the visible content is glyph only (no text; §3 of `Components/Common/Button.cs.md`). GTK's theme minimum width for a button is above 45 px, so `ActionButtons` is the only icon row that reaches a true square: the height request reaches 80 px.

## 4. Consumers

`MainPage` builds and wires the row (`MainPage.cs.md`):

| Site | Behaviour |
| --- | --- |
| `MainPage.cs:53` | `new ActionButtons()` on the UI thread |
| `MainPage.cs:59` | `OnSettingsButtonClicked += () => this.App.State = LauncherApp.LauncherState.Settings` |
| `MainPage.cs:60` | `OnStatusButtonClicked += () => AppUtil.OpenBrowser("https://is.xivup.com/")` |
| `MainPage.cs:61` | `OnAccountButtonClicked += () => AppUtil.OpenBrowser("https://sqex.to/Msp")` |
| `MainPage.cs:105` | `layout.Add(this.actionButtons, 0, 1, xscale: false, yscale: true)` — the row is the second cell of the page's table; the column row above it scales (`MainPage.cs:103`), so the row sits under the columns |

`OnAccountButtonClicked` opens the Square Enix account management page in the browser; the local account picker is the **friends** button in `LoginFrame`, which opens `AccountSwitcher` (`AccountSwitcher.cs.md`, `LoginFrame.cs:117`).

## 5. Thread / UI rules

Construction and `ShowIcon`/`ToolTip`/`Click` wiring are UI-thread (native widgets). Click handlers fire on the UI thread; the two `AppUtil.OpenBrowser` calls start a process, and the settings handler changes `App.State`, which marshals via `Ui.Post` in `LauncherApp.SetState` (`LauncherApp.cs.md`). No handler blocks.

## 6. Quirks

- The row is `Orientation.Horizontal` with default spacing (0), so the three 80 px buttons sit adjacent; HEAD spaced icon buttons with an item-width/spacing trick that has no direct Eto equivalent here.
- `Width = 80` is fixed regardless of glyph width; GTK centers the glyph in the button. The glyph is drawn with the launcher font size (`AppFonts.IconFont`), which GTK uses to size the button's height; the 80 px height request dominates, so the button stays square.
- The wrapper's `Click` is `Action?` (`Components/Common/Button.cs.md` §7), so handlers cannot distinguish keyboard activation from a mouse click.
- `ActionButtons` has a parameterless ctor: it does not take `LauncherApp` (unlike most components), because the events are wired by `MainPage`.
- `Component.Id` exists on this wrapper (`Components/Component.cs.md` §2) and is unused.
- Buttons have no `Name`/`AccessibleName` beyond the label glyph, so screen readers announce an icon glyph; the tooltip is GTK's hover text, not an accessibility label.

## 7. Referenced / referenced-by

Uses: `Component` (`Component.cs.md`), `Common.Button` (`Components/Common/Button.cs.md`), `FontAwesomeIcon`, `Strings`.
Referenced by: `MainPage.cs` (lines 41, 53, 59–61, 105).
