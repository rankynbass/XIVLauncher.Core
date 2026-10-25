# Input.cs (Common) — Specification

**File:** `src/XIVLauncher.Core/Components/Common/Input.cs` · 175 lines · 5,176 bytes
**Namespace:** `XIVLauncher.Core.Components.Common` · **Declarations:** `public enum InputFlags` (line 11), `public class Input : Component` (line 34)
**Role:** Labelled text entry — native Eto `TextBox`/`PasswordBox` wrapped with a caption label, plus Steam Deck gamepad input hooks

---

## 1. Purpose

Replaces the immediate-mode ImGui text input, which kept its own text buffer and re-drew the entry every frame. Here typing is handled by the platform entry, `Value` reads `TextControl.Text` directly, and Enter is detected through `KeyDown` when `InputFlags.EnterReturnsTrue` is set.

Usings: `System.Numerics` (`Vector2` spacing), `Eto.Forms`, `Serilog` (digit-stripping log).

## 2. `InputFlags`

`[Flags]` enum (lines 11–23):

| Flag | Value | Effect |
| --- | --- | --- |
| `None` | 0 | plain text entry |
| `Password` | 1 | entry is a `PasswordBox` (masked) — **selects the control type** at construction (line 59) |
| `Decimal` | 2 | subscribes `SanitizeDecimal` to `TextChanged` (lines 79–81) |
| `EnterReturnsTrue` | 4 | `Keys.Enter` in `KeyDown` raises `Enter` (lines 84–89) |

`Password` is honoured by creating `PasswordBox` (lines 60–64) instead of `TextBox` (lines 65–70). `TextBox` gets `PlaceholderText = hint` (line 68); `PasswordBox` gets no placeholder.

## 3. Surface

| Member | Kind | Notes |
| --- | --- | --- |
| `input` | `private readonly TextControl` (line 36) | the native entry |
| `label` | `private readonly Label` (line 37) | caption, `Visible = !string.IsNullOrEmpty(label)` (lines 51–55) |
| `steamDeckInputActive` | `private bool` (line 39) | set when `ShowGamepadTextInput` succeeds (line 93) |
| `sanitizing` | `private bool` (line 40) | re-entrancy guard for `SanitizeDecimal` |
| `Input(string label, string hint, Vector2? spacing, uint maxLength = 255, bool isEnabled = true, InputFlags flags = InputFlags.None)` | ctor (lines 42–112) | |
| `Label`, `Hint`, `MaxLength`, `Flags`, `Spacing` | `public { get; }` (lines 114, 116, 118, 120, 128) | read-only metadata, assigned once (lines 72–76) |
| `IsEnabled` | `public bool` (lines 122–126) | forwards `input.Enabled` |
| `HasSteamDeckInput` | `public bool { get; set; }` (line 130) | gates the gamepad keyboard |
| `SteamDeckPrompt` | `public string { get; set; }` (line 132) | prompt text for the gamepad keyboard |
| `TakeKeyboardFocus` | `public bool { get; set; }` (line 135) | declared, **never read** |
| `InputWidth` | `public int` (lines 141–145) | width of the entry itself, forwarded to `Control.Width` |
| `Enter` | `public event Action?` (line 148) | raised on Enter when flagged |
| `Value` | `public string` (lines 150–154) | forwards `input.Text` |
| `Focus()` | `public new void` (line 157) | forwards `input.Focus()`; opening focus also triggers the gamepad path |
| `SanitizeDecimal()` | `private` (lines 159–174) | digit filter |

`MaxLength` is `uint` in the launcher API and cast to `int` for the native control (lines 63, 69). Eto exposes `MaxLength` on `TextBox` and `PasswordBox`, **not** on `TextControl` — hence the constructor sets it inside each concrete initializer, and the wrapper's `MaxLength` property is metadata only (line 118).

`InputWidth` targets the entry widget; the wrapper's own `Control.Width` sizes the label+entry panel. HEAD's `SetNextItemWidth` equivalent is `InputWidth`, used by the OTP entry (`OtpEntryPage.cs:45`, 150 px).

## 4. Layout

`this.Content = new StackLayout { Spacing = (int)(spacing?.X ?? 10), Items = { this.label, this.input } }` (lines 107–112) — vertical stack: label above entry. The `spacing` parameter is a `Vector2` from HEAD's margin API, and only `X` is used as the stack spacing; `spacing?.Y` is ignored (`spacing` is stored as metadata, line 76, and never read).

Consumers pass `new Vector2(12f, 0f)` for the login/password entries (`LoginFrame.cs:78`, `81`) → 12 px spacing, and `null` for the OTP entry (`OtpEntryPage.cs:43`) → 10 px spacing.

An empty label is hidden (line 53), so the OTP box (label `string.Empty`) reserves no vertical space.

## 5. Steam Deck gamepad input

`GotFocus` (lines 90–94): when `Program.Steam is { IsValid: true }` and `HasSteamDeckInput` is true, calls

```csharp
Program.Steam.ShowGamepadTextInput(flags.HasFlag(InputFlags.Password), false, this.SteamDeckPrompt, (int)this.MaxLength, this.Value)
```

and records success in `steamDeckInputActive`. Note `flags` here is the **constructor parameter**, captured by the closure, so the password flag passed to Steam is the flag set at construction.

`Program.Steam?.OnGamepadTextInputDismissed += a => ...` (lines 96–106): if `steamDeckInputActive`, clear it, then on `a == true` and valid Steam, write `Program.Steam.GetEnteredGamepadText()` into `Value`.

Key facts:

- The subscription is made **per input instance**, in the constructor, on `Program.Steam` (the shared Steam object). Every `Input` constructed while Steam exists keeps a subscription for the process lifetime, and the lambda captures `this`, so all inputs stay alive.
- The handler writes `Value` (GTK `Text`) **from Steam's callback thread**; nothing marshals through `Ui.Post` (`Ui.cs.md`). `OtpEntryPage` has the same pattern with its own handler, marshalled (`OtpEntryPage.cs:110` uses `Ui.Post`).
- **`HasSteamDeckInput` is never set by any consumer** (grep in `src/XIVLauncher.Core`: no `HasSteamDeckInput =`), and `SteamDeckPrompt` is only assigned `string.Empty` inside the constructor (line 77). So this gamepad path is inert for all three consumer instances; it is dead code inherited from HEAD (`git grep` at HEAD shows the same).

## 6. Decimal sanitization

`SanitizeDecimal` (lines 159–174), subscribed only when `InputFlags.Decimal` is set:

1. Return early if `sanitizing` (re-entrancy guard: the filter writes `input.Text`, which fires `TextChanged` again).
2. Return if `text.All(char.IsDigit)`.
3. Set `sanitizing = true`, write `new string(text.Where(char.IsDigit).ToArray())`, clear the guard, then `Log.Debug("Stripped non-digit input from the {Label} entry", this.Label)`.

Consequences:

- Pasted text with letters is filtered to digits, and the caret position is not preserved (the whole string is replaced).
- Programmatic `Value = "abc1"` from any thread writes through GTK, and the filter fires on the UI thread; digits-only strings are untouched.
- The log message names the entry by `Label`, which is `string.Empty` for the OTP box.

## 7. Consumers

| Consumer | Site | Configuration |
| --- | --- | --- |
| `loginInput` | `LoginFrame.cs:78`, Enter → `TriggerLogin` (line 79) | label `Strings.UsernameInput`, hint `Strings.UsernameInputHint`, spacing `(12,0)`, `MaxLength = 128`, no flags |
| `passwordInput` | `LoginFrame.cs:81`, Enter → `TriggerLogin` (line 82) | `Strings.PasswordInput`, `Strings.PasswordInputHint`, spacing `(12,0)`, `MaxLength = 128`, `Password \| EnterReturnsTrue` |
| `otpInput` | `OtpEntryPage.cs:43-45`, Enter → `TryAcceptOtp` (line 47) | empty label/hint, `spacing = null`, `MaxLength = 7`, `Decimal \| EnterReturnsTrue`, `InputWidth = 150` |

`LoginFrame.Username`/`Password` forward to these inputs' `Value` (`LoginFrame.cs:32–42`), and `MainPage` reads them on the UI thread inside the login path (`MainPage.cs:175`). Settings entries use raw Eto `TextBox` (not this wrapper) — see `Components/SettingsPage/SettingsEntry{T}.cs.md`.

## 8. Thread / UI rules

Construction, `Value` reads/writes, `InputWidth`, `IsEnabled`, `Focus()` are UI-thread operations on GTK widgets. The `Enter` event fires on the UI thread (GTK key event). The decimal filter fires on the UI thread. Only the Steam dismissal callback (line 96) writes from a non-UI thread, and as noted that path is unreachable today.

## 9. Quirks

- **`TakeKeyboardFocus` is dead** — declared (line 135), never read, no consumer sets it. HEAD's "focus the entry when the page is shown" behaviour is not implemented in the Eto path; `OtpEntryPage.Reset` calls `Focus()` explicitly instead (`OtpEntryPage.cs:122`).
- **`HasSteamDeckInput` / `SteamDeckPrompt` are dead** as described in §5, so Steam Deck users cannot type into the username/password entries with the gamepad keyboard.
- The per-instance Steam subscription is never unsubscribed, so N inputs ⇒ N permanent handlers on the shared Steam object; a dismissal triggers all of them, each guarded by its own `steamDeckInputActive` flag.
- `MaxLength` metadata is `uint`, the native control's is `int`; `(int)this.MaxLength` is used for the gamepad call (line 93).
- `spacing` is stored but only `X` is meaningful; passing a `Vector2` with a `Y` component has no effect.
- The wrapper inherits `Component`'s `Panel` (`Component.cs.md`), so `Component.Enabled = false` disables the label and the entry together, while `IsEnabled = false` disables only the entry.
- `Value` never returns `null`: Eto `TextControl.Text` is non-null, so an empty entry reads as `string.Empty`.

## 10. Referenced / referenced-by

Uses: `Component` (`Component.cs.md`), `Eto.Forms.TextBox`/`PasswordBox`/`Label`/`StackLayout`, `Keys.Enter`, `Program.Steam` (`Program.cs.md`), `Log`.
Referenced by: `LoginFrame.cs` (lines 24–25, 78–82), `OtpEntryPage.cs` (lines 27, 43–47).
