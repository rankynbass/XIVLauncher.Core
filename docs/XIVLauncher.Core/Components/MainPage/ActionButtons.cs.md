# ActionButtons.cs — Specification

**File:** `src/XIVLauncher.Core/Components/MainPage/ActionButtons.cs` · 64 lines · 2,362 bytes
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
| `accountButton`, `statusButton`, `settingsButton` | `private readonly Common.Button` (lines 21–23) | built in ctor (lines 28–30) |
| `ActionButtons()` | ctor (lines 26–39) | horizontal `StackLayout` with `Spacing = 10` (line 35) and `Padding = new Padding(10)` (line 36), items at lines 32–38 |
| `OnAccountButtonClicked` | `public event Action?` (line 42) | |
| `OnStatusButtonClicked` | `public event Action?` (line 45) | |
| `OnSettingsButtonClicked` | `public event Action?` (line 48) | |
| `MakeIconButton(FontAwesomeIcon icon, string toolTip, Action clicked)` | `private static` (lines 50–61) | `new Common.Button(string.Empty) { Width = IconButtonSize, Height = IconButtonSize }` (lines 53–56), `ShowIcon(icon)` (line 58), `ToolTip = toolTip` (line 59), `Click += clicked` (line 60) |

`MakeIconButton` is static: it does not touch instance state, and each event is wired by the ctor's lambda (`() => this.OnAccountButtonClicked?.Invoke()`, lines 28–30), so `MakeIconButton` takes `Action` and the instance event raising lives in the ctor.

## 3. Buttons

| Button | Icon | Tooltip string | Event |
| --- | --- | --- | --- |
| `accountButton` | `FontAwesomeIcon.User` | `Strings.MyAccount` | `OnAccountButtonClicked` |
| `statusButton` | `FontAwesomeIcon.Heartbeat` | `Strings.ServiceStatus` | `OnStatusButtonClicked` |
| `settingsButton` | `FontAwesomeIcon.Cog` | `Strings.LauncherSettings` | `OnSettingsButtonClicked` |

All three are `Width = IconButtonSize, Height = IconButtonSize` (80x80, matching HEAD's square buttons) and start with an empty label, which `ShowIcon` replaces with the glyph — so the visible content is glyph only (no text; §3 of `Components/Common/Button.cs.md`). The stack's `Spacing = 10` (line 35) puts 10 px between buttons, and its single-value `Padding = new Padding(10)` (line 36) applies 10 px on all four edges, so the row's natural size is 280 × 100 px (3 × 80 + 2 × 10 + 2 × 10 wide, 80 + 2 × 10 tall). Combined with `MainPage`'s own `Padding(10, 32, 0, 10)` (left, top, right, bottom) and the layout's 16 px spacing, the buttons end up 20 px from the window left and bottom edges and 26 px below the news scrollable. GTK's theme minimum width for a button is above 45 px, so `ActionButtons` is the only icon row that reaches a true square: the height request reaches 80 px.

## 4. Consumers

`MainPage` builds and wires the row (`MainPage.cs.md`):

| Site | Behaviour |
| --- | --- |
| `MainPage.cs:54` | `new ActionButtons()` on the UI thread |
| `MainPage.cs:60` | `OnSettingsButtonClicked += () => this.App.State = LauncherApp.LauncherState.Settings` |
| `MainPage.cs:61` | `OnStatusButtonClicked += () => AppUtil.OpenBrowser("https://is.xivup.com/")` |
| `MainPage.cs:62` | `OnAccountButtonClicked += () => AppUtil.OpenBrowser("https://sqex.to/Msp")` |
| `MainPage.cs:105` | `layout.Add(this.actionButtons, xscale: false, yscale: false)` inside `BeginVertical(xscale: true, yscale: true)` (line 101) — added after `EndScrollable` (line 104), so the row sits at the bottom of the scaled news column, below the scrollable news frame, with its own 10 px padding around the buttons |

`OnAccountButtonClicked` opens the Square Enix account management page in the browser; the local account picker is the **friends** button in `LoginFrame`, which opens `AccountSwitcher` (`AccountSwitcher.cs.md`, `LoginFrame.cs:117`).

## 5. Thread / UI rules

Construction and `ShowIcon`/`ToolTip`/`Click` wiring are UI-thread (native widgets). Click handlers fire on the UI thread; the two `AppUtil.OpenBrowser` calls start a process, and the settings handler changes `App.State`, which marshals via `Ui.Post` in `LauncherApp.SetState` (`LauncherApp.cs.md`). No handler blocks.

## 6. Quirks

- The row is `Orientation.Horizontal` with `Spacing = 10` (line 35) and `Padding = new Padding(10)` (line 36): 10 px between the three 80 px buttons, 10 px inset on all edges, 280 × 100 px natural size. HEAD spaced icon buttons with an item-width/spacing trick that has no direct Eto equivalent here.
- `Width = 80` is fixed regardless of glyph width; GTK centers the glyph in the button. The glyph is drawn with the launcher font size (`AppFonts.IconFont`), which GTK uses to size the button's height; the 80 px height request dominates, so the button stays square.
- The wrapper's `Click` is `Action?` (`Components/Common/Button.cs.md` §7), so handlers cannot distinguish keyboard activation from a mouse click.
- `ActionButtons` has a parameterless ctor: it does not take `LauncherApp` (unlike most components), because the events are wired by `MainPage`.
- `Component.Id` exists on this wrapper (`Components/Component.cs.md` §2) and is unused.
- Buttons have no `Name`/`AccessibleName` beyond the label glyph, so screen readers announce an icon glyph; the tooltip is GTK's hover text, not an accessibility label.

## 7. Referenced / referenced-by

Uses: `Component` (`Component.cs.md`), `Common.Button` (`Components/Common/Button.cs.md`), `FontAwesomeIcon`, `Strings`.
Referenced by: `MainPage.cs` (lines 42, 54, 60–62, 105).
