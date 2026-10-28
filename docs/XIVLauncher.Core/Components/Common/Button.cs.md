# Button.cs (Common) — Specification

**File:** `src/XIVLauncher.Core/Components/Common/Button.cs` · 70 lines · 2,244 bytes
**Namespace:** `XIVLauncher.Core.Components.Common` · **Declaration:** `public class Button : Component`
**Role:** Launcher push button — a native Eto `Button` wrapped in the launcher's `Component` base

---

## 1. Purpose

Replaces the immediate-mode ImGui button, which drew itself every frame and implemented the "press Enter while focused" special case. This wrapper exposes the launcher's original API shape (`Click`, `IsEnabled`, `Label`, `ShowIcon`) over a native Eto button; click detection, keyboard focus, and default-button behaviour come from GTK, so the special case is gone.

Usings: `Eto.Forms`. The wrapper inherits `Component` (`Component.cs.md`), so it is a `Panel` whose `Content` is the real button.

## 2. Surface

| Member | Kind | Notes |
| --- | --- | --- |
| `button` | `private readonly Eto.Forms.Button` (line 15) | the native control |
| `Button(string label, bool isEnabled = true)` | ctor (lines 17–29) | `Text = label`, `Enabled = isEnabled` (lines 20–23), forwards `Click` (line 25), `Content = button` (line 27) |
| `Click` | `public event Action?` (line 31) | raised from the native `Click` handler, `(_, _) => this.Click?.Invoke()` |
| `IsEnabled` | `public bool` (lines 33–37) | forwards `button.Enabled` |
| `Label` | `public string` (lines 39–43) | forwards `button.Text` |
| `Width` | `public new int?` (lines 46–51) | `null` means "let the layout size the button"; getter returns `null` when `button.Width < 0`, setter writes `value ?? -1` |
| `ShowIcon(FontAwesomeIcon icon, float? fontSize = null)` | `public` (lines 55–59) | `button.Font = AppFonts.IconFont` (or `AppFonts.IconFontAt(fontSize)`), `button.Text = AppFonts.Glyph(icon)` |
| `ToolTip` | `public new string` (lines 63–67) | forwards `Control.ToolTip` |
| `Focus()` | `public new void` (line 69) | forwards `Control.Focus()` |

`public new` on `Width`, `ToolTip`, `Focus` hides the inherited `Control` members (the base declares `Width` as `int`, `ToolTip` as `string`), which is why the declarations use `new` — the wrapper changes the nullability of `Width`.

## 3. `ShowIcon` semantics

`ShowIcon` **replaces** the button's text with a Font Awesome glyph and switches the button font to `AppFonts.IconFont` (`AppFonts.cs.md`). There is no icon/text combination: after `ShowIcon` the label is the glyph, and `Label` (line 39) reads/writes the glyph string. Consumers that need text + icon must set `Label` after `ShowIcon`, which overwrites the glyph.

The optional `fontSize` selects `AppFonts.IconFontAt(fontSize)`: GTK sizes a button's height from its font, so a fixed-height icon button needs a smaller glyph. `LoginFrame` passes `14f` for the 45 px caret and account buttons; every other consumer uses the page font size.

`FontAwesomeIcon` comes from the submodule's FontAwesome enum (`XIVLauncher.Common`), so `ShowIcon`'s parameter type is the launcher's icon vocabulary, not Eto's `Image`.

## 4. `Width` semantics

The nullable wrapper encodes Eto's "unset width" sentinel: `button.Width` is `int` and `-1` means unset, so:

- `Width` getter → `null` when `button.Width < 0`
- `Width` setter → `value ?? -1`

This lets callers write `button.Width = 45` (fixed) and leave the natural size (`Width = null`). Consumers use both forms: `Width = 45, Height = 45` for the login caret/friends buttons (`LoginFrame.cs:122`, `126`), `Width = 40` for the settings finish button (`SettingsPage.cs:77`), `Width = 28, Height = 28` for the OTP paste button and `89x30` for OK/Cancel (`OtpEntryPage.cs:55`, `65`, `68`), `Width = 80, Height = 80` for the action icons (`ActionButtons.cs:49-52`), `Width = 320` for the loading-page buttons (`LoadingPage.cs:83`, `87`).

## 5. Consumers

| Consumer | Site | Use |
| --- | --- | --- |
| `ActionButtons` | `ActionButtons.cs:47-58` | `MakeIconButton(FontAwesomeIcon, string toolTip, Action clicked)` builds `new Common.Button(string.Empty)` with `Width = IconButtonSize, Height = IconButtonSize` (80x80, lines 49–52), then `ShowIcon(icon)` (line 55), `ToolTip` (line 56), `Click += clicked` (line 57) |
| `LoginFrame` | `LoginFrame.cs:116`, `122`, `126` | login button (`Strings.LoginButton`), caret button (`45x45`, `ShowIcon(CaretDown, 14f)`), friends button (`45x45`, `ShowIcon(UserFriends, 14f)`) |
| `LoadingPage` | `LoadingPage.cs:82`, `86` | cancel (`Strings.CancelLabel`), disable-auto-login, both `Width = 320` |
| `SettingsPage` | `SettingsPage.cs:77` | finish button, `Width = 40`, `ShowIcon(FontAwesomeIcon.Check/Ban)` (lines 78, 257) |
| `OtpEntryPage` | `OtpEntryPage.cs:55`, `65`, `68` | paste (`28x28`), OK, Cancel (`89x30`) |

Consumers that use raw Eto buttons instead of this wrapper: `ModalDialog` (the modal OK button) and the settings tab action panels (`SettingsTabAbout.cs:61`, `SettingsTabGame.cs:60`, `SettingsTabTroubleshooting.cs:63-85`, `SettingsTabWine.cs:92-101`) — those need only `Text` + `Click`, so the wrapper adds nothing.

## 6. Thread / UI rules

Construction and every property mutation must happen on the UI thread: `Component` is a GTK `Panel`, and `ShowIcon` mutates the native button's `Font`/`Text`. The wrapper does not marshal: `IsEnabled`, `Label`, `Width`, `ShowIcon`, `ToolTip` called from a background thread mutate GTK directly. Consumers that change button state from background work marshal themselves (e.g. `SettingsPage` finish-button updates, `MainPage` UI updates via `Ui.Post`; `Ui.cs.md`).

`Click` is raised by GTK, i.e. on the UI thread; handlers that start background work should not block.

## 7. Quirks

- `Click` is `Action?` (no sender/args), so handlers can't distinguish rapid multi-clicks, mouse buttons, or keyboard vs mouse activation.
- `isEnabled` is a constructor parameter only; toggling enablement later goes through `IsEnabled`.
- `ShowIcon` does not cache the icon, so a consumer cannot read which glyph is displayed.
- `Component`'s `Enabled` is `Panel.Enabled`, which propagates to children, so `Component.Enabled = false` disables the wrapped button as well as `IsEnabled = false`; the two paths coexist.
- `Component.Id` exists on this wrapper (`Component.cs.md` §2) and is unused.
- There is no `Height` wrapper: `Height = 28` / `Height = 30` in the OTP page use `Control.Height` directly, so the wrapper's nullable-`Width` convenience is not symmetrical.

## 8. Referenced / referenced-by

Uses: `Component` (`Component.cs.md`), `AppFonts.IconFont`/`Glyph` (`AppFonts.cs.md`), `FontAwesomeIcon`.
Referenced by: `ActionButtons.cs`, `LoginFrame.cs`, `LoadingPage.cs`, `SettingsPage.cs`, `OtpEntryPage.cs`.
