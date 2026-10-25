# Checkbox.cs (Common) — Specification

**File:** `src/XIVLauncher.Core/Components/Common/Checkbox.cs` · 54 lines · 1,535 bytes
**Namespace:** `XIVLauncher.Core.Components.Common` · **Declaration:** `public class Checkbox : Component`
**Role:** Launcher checkbox — a native Eto `CheckBox` wrapped in the launcher's `Component` base

---

## 1. Purpose

Replaces the immediate-mode ImGui checkbox. The wrapper keeps the launcher's API shape (`OnChange`, `Label`, `IsEnabled`, `Value`) over a native Eto checkbox, so the toggle is a real GTK widget with real focus, keyboard, and accessibility behaviour.

Usings: `Eto.Forms`. Inherits `Component` (`Component.cs.md`), i.e. a `Panel` whose `Content` is the native checkbox.

## 2. Surface

| Member | Kind | Notes |
| --- | --- | --- |
| `checkBox` | `private readonly Eto.Forms.CheckBox` (line 15) | the native control |
| `Checkbox(string label, bool value = false, bool isEnabled = true)` | ctor (lines 17–29) | `Text = label`, `Checked = value`, `Enabled = isEnabled` (lines 19–24); forwards `CheckedChanged` (line 26); `Content = checkBox` (line 28) |
| `OnChange` | `public event Action<bool>?` (line 32) | raised as `this.OnChange?.Invoke(this.Value)` |
| `Label` | `public string` (lines 34–38) | forwards `checkBox.Text` |
| `IsEnabled` | `public bool` (lines 40–44) | forwards `checkBox.Enabled` |
| `Value` | `public bool` (lines 46–50) | getter `checkBox.Checked == true`, setter `checkBox.Checked = value` |
| `Focus()` | `public new void` (line 53) | forwards `Control.Focus()` |

`Text`, not `Label`, is Eto's checkbox caption property; the wrapper's `Label` name preserves HEAD's naming. `Value`'s getter compares `bool? Checked` against `true`, so an unset/indeterminate `Checked` reads as `false`.

The wrapper does not expose `ThreeState`, `CheckedBinding`, `UseMnemonic`, or `AlwaysShowMnemonic` — Eto's `CheckBox` provides them, and the launcher never uses them.

## 3. Change propagation

`CheckedChanged` is forwarded once at construction (line 26) with `(_, _) => this.OnChange?.Invoke(this.Value)`, so:

- The handler reads `Value` **after** the native state has changed, i.e. the value passed to `OnChange` is the new value.
- Programmatic writes through `Value` (setter, line 48) also raise `CheckedChanged` in GTK, so a UI-thread `Value = true` fires `OnChange` as well. Consumers that both read and write settings through `Value` must tolerate the extra event.
- `OnChange` is `Action<bool>?` with no sender, so a handler cannot tell which checkbox fired — in `LoginFrame` the four checkboxes each get their own lambda.

## 4. Consumers

`LoginFrame` is the only consumer (four instances, fields at `LoginFrame.cs:26-29`, construction at `LoginFrame.cs:84-87`). None of them have `OnChange` handlers: the wrapper's state is read through `LoginFrame`'s properties (`IsOtp` line 44, `IsSteam` line 50, `IsAutoLogin` line 56, `IsFreeTrial` line 62; `LoginFrame.cs.md`), and `MainPage` is the code that persists them — `App.Settings.IsAutologin = this.loginFrame.IsAutoLogin` at `MainPage.cs:173`, and the read at `MainPage.cs:175` inside the `Login(...)` call. `MainPage.cs:120-124` writes the properties when an account is switched.

Settings-page checkboxes use raw Eto `CheckBox` inside `SettingsEntry{T}` (`Components/SettingsPage/SettingsEntry{T}.cs.md`), not this wrapper: the settings row builds its own label + control layout, and Eto's checkbox is enough.

## 5. Thread / UI rules

Construction and property mutation are UI-thread-only (GTK widget). `OnChange` fires on the UI thread. `LoginFrame`'s properties forward directly to `Value` (`LoginFrame.cs:44-66`), so `MainPage`'s reads (`MainPage.cs:175`) and writes (`MainPage.cs:120-124`, `173`) touch GTK; `MainPage` performs them on the UI thread, inside the login handler path that runs before `Task.Run` hands work to the background task (`MainPage.cs.md`).

## 6. Quirks

- `Value`'s setter fires `OnChange` — a programmatic set (e.g. loading settings) produces the same event as a user toggle. `LoginFrame`'s handlers are idempotent writes to `App.Settings`, so this is harmless; a consumer with side effects would double-fire.
- `Component.Enabled` (`Panel.Enabled`) propagates to the child, so disabling via the base and via `IsEnabled` are equivalent in effect.
- No `Height`/`Width` wrapper: consumers set `Control.Width`/`Height` directly when needed.
- `Component.Id` exists on this wrapper (`Component.cs.md` §2) and is unused.
- Eto's `CheckBox` supports `ThreeState`; the wrapper's `Value` collapses indeterminate to `false`, so a three-state checkbox cannot be represented.

## 7. Referenced / referenced-by

Uses: `Component` (`Component.cs.md`), `Eto.Forms.CheckBox`.
Referenced by: `LoginFrame.cs` (lines 26–29, 84–87).
