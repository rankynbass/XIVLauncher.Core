# Button.cs (Common) — Specification

**File:** `src/XIVLauncher.Core/Components/Common/Button.cs` · 68 lines · 2,014 bytes
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
| `ShowIcon(FontAwesomeIcon icon)` | `public` (lines 53–56) | `button.Font = AppFonts.IconFont`, `button.Text = AppFonts.Glyph(icon)` |
| `ToolTip` | `public new string` (lines 60–64) | forwards `Control.ToolTip` |
| `Focus()` | `public new void` (line 67) | forwards `Control.Focus()` |

`public new` on `Width`, `ToolTip`, `Focus` hides the inherited `Control` members (the base declares `Width` as `int`, `ToolTip` as `string`), which is why the declarations use `new` — the wrapper changes the nullability of `Width`.

## 3. `ShowIcon` semantics

`ShowIcon` **replaces** the button's text with a Font Awesome glyph and switches the button font to `AppFonts.IconFont` (`AppFonts.cs.md`). There is no icon/text combination: after `ShowIcon` the label is the glyph, and `Label` (line 39) reads/writes the glyph string. Consumers that need text + icon must set `Label` after `ShowIcon`, which overwrites the glyph.

`FontAwesomeIcon` comes from the submodule's FontAwesome enum (`XIVLauncher.Common`), so `ShowIcon`'s parameter type is the launcher's icon vocabulary, not Eto's `Image`.

## 4. `Width` semantics

The nullable wrapper encodes Eto's "unset width" sentinel: `button.Width` is `int` and `-1` means unset, so:

- `Width` getter → `null` when `button.Width < 0`
- `Width` setter → `value ?? -1`

This lets callers write `button.Width = 45` (fixed) and leave the natural size (`Width = null`). Consumers use both forms: `Width = 45` for the login caret/friends buttons (`LoginFrame.cs:111`, `115`), `Width = 40` for the settings finish button (`SettingsPage.cs:56`), `Width = 28/89` for the OTP buttons (`OtpEntryPage.cs:49`, `57`, `60`), `Width = 320` for the loading-page buttons (`LoadingPage.cs:83`, `87`).

## 5. Consumers

| Consumer | Site | Use |
| --- | --- | --- |
| `ActionButtons` | `ActionButtons.cs:43-53` | `MakeIconButton(FontAwesomeIcon, string toolTip, Action clicked)` builds `new Common.Button(string.Empty)` with `Width = 80` (line 47), then `ShowIcon(icon)` (line 50), `ToolTip` (line 51), `Click += clicked` (line 52) |
| `LoginFrame` | `LoginFrame.cs:107`, `111`, `115` | login button (`Strings.LoginButton`), caret button (`Width = 45`), friends button (`Width = 45`) |
| `LoadingPage` | `LoadingPage.cs:82`, `86` | cancel (`Strings.CancelLabel`), disable-auto-login, both `Width = 320` |
| `SettingsPage` | `SettingsPage.cs:56` | finish button, `Width = 40`, `ShowIcon(FontAwesomeIcon.Check/Ban)` in `UpdateFinishButton` |
| `OtpEntryPage` | `OtpEntryPage.cs:49`, `57`, `60` | paste (`28x28`), OK, Cancel (`89x30`) |

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
