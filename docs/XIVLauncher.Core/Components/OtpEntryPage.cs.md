# OtpEntryPage.cs — Specification

**File:** `src/XIVLauncher.Core/Components/OtpEntryPage.cs` · 185 lines · 6,243 bytes
**Namespace:** `XIVLauncher.Core.Components` · **Declaration:** `public class OtpEntryPage : Page`
**Role:** OTP entry screen — centered entry box, paste button, OK/Cancel, plus the optional OTP HTTP listener and Steam Deck gamepad input

---

## 1. Purpose

`OtpEntryPage` is shown when login needs a 6-digit OTP code. It collects the code from three sources: typing, the clipboard (paste button), and — on Steam Deck — the Steam gamepad keyboard; plus, when configured, an HTTP listener that receives codes from an external OTP server. The result is delivered through a `TaskCompletionSource` that `LauncherApp.WaitForOtpAsync` awaits.

The immediate-mode version drew a real 300×200 child window with 40% alpha background and used an ImGui keyboard-focus trick; the Eto version is a centered fixed-width panel with the same tint, and `Focus()` on the entry (`Components/Common/Input.cs.md`).

Usings: `Eto.Drawing`, `Eto.Forms`, `Serilog`, `XIVLauncher.Common.Http` (`OtpListener`), `XIVLauncher.Core.Resources.Localization`.

## 2. Surface

| Member | Kind | Notes |
| --- | --- | --- |
| `otp` | `private string` (line 22) | last accepted code, written by `TryAcceptOtp` |
| `otpListener` | `private OtpListener?` (line 23) | HTTP listener, created on demand (lines 165–173) |
| `resultTcs` | `private TaskCompletionSource<string?>` (line 24) | recreated on `Reset` (line 118) |
| `steamDeckInputActive` | `private bool` (line 25) | set when `ShowGamepadTextInput` succeeds (line 131) |
| `otpInput` | `private readonly Common.Input` (line 27) | built in ctor (lines 43–46) |
| `Result` | `public string? { get; private set; }` (line 29) | the accepted code |
| `Cancelled` | `public bool { get; private set; }` (line 31) | set by the Cancel button |
| `GetResultAsync()` | `public Task<string?>` (line 33) | returns `resultTcs.Task` |
| `SteamOnOnGamepadTextInputDismissed(bool)` | `private` (lines 102–111) | Steam gamepad callback |
| `Reset()` | `public` (lines 113–148) | clears state, focuses input, starts gamepad input / OTP server |
| `TryAcceptOtp(string)` | `private` (lines 150–163) | validates and resolves the task |
| `StartOtpListener()` | `public` (lines 165–173) | creates and starts the listener |
| `StopOtpListener()` | `public` (lines 175–183) | stops listener, unsubscribes, clears field |

## 3. Layout

`DynamicLayout { Padding = new Padding(0, 200) }` with `layout.AddCentered(box)` (lines 97–99) — a 200 px top/bottom padding band, box centered in the remaining width. The box:

- `Width = 300` (line 71), `BackgroundColor = EtoColors.OtpChildBackground` (line 72) — `Color.FromArgb(102, 15, 15, 15)`, the old `WindowBg` 0.06 grey at 40% alpha, `EtoColors.cs.md`.
- Content: `StackLayout` with `Spacing = 10` (lines 73–95), items in HEAD order:
  1. `Label { Strings.EnterYourOTP, TextAlignment = Center }` (line 78)
  2. horizontal `StackLayout` with `this.otpInput` and `pasteButton`, both `VerticalAlignment.Center` (lines 79–87)
  3. horizontal `StackLayout` with `okButton`, `cancelButton` (lines 88–92)

Control sizes reproduce HEAD's pixel sizes: entry `InputWidth = 150` (line 45), paste button `28x28` (line 49), OK and Cancel `89x30` (lines 57, 60). `InputWidth` targets the entry itself, not the label+entry wrapper (`Components/Common/Input.cs.md`).

The OTP entry uses `Common.Input(string.Empty, string.Empty, null, 7, flags: Common.InputFlags.Decimal | Common.InputFlags.EnterReturnsTrue)` (line 43): no label (hidden), no hint, `MaxLength = 7`, digits only, Enter accepted. `MaxLength = 7` with `TryAcceptOtp` requiring exactly 6 is HEAD's behaviour.

## 4. Accepting a code

`TryAcceptOtp(string otp)` (lines 150–163):

```csharp
if (string.IsNullOrEmpty(otp) || otp.Length != 6) { Log.Error("Invalid OTP: {Otp}", otp); return; }
this.otp = otp;
Log.Verbose("Received OTP: {Otp}", otp);
this.Result = otp;
this.resultTcs.TrySetResult(otp);
```

Call sites:
- `otpInput.Enter` → `TryAcceptOtp(this.otpInput.Value)` (line 47) — `InputFlags.EnterReturnsTrue` fires `Enter` (`Common/Input.cs.md`).
- paste button → sets `otpInput.Value = Clipboard.Instance.Text ?? string.Empty` then accepts (lines 51–55).
- OK button → accepts current value (line 58).
- Steam gamepad dismissal → accepts `Program.Steam.GetEnteredGamepadText()` (line 110).
- OTP listener → `otpListener.OnOtpReceived += this.TryAcceptOtp` (line 170).

**Thread note:** the listener runs on a background task (`Task.Run(this.otpListener.Start)`, line 171), so `TryAcceptOtp` is invoked off the UI thread. It only mutates `otp`, `Result`, and the `TaskCompletionSource` — no Eto controls — so no marshalling is required. The paste and OK handlers do touch `otpInput.Value`, and they run on the UI thread.

Cancel (lines 60–65): `Cancelled = true`, `resultTcs.TrySetResult(null)`. `TrySetResult` (not `SetResult`) keeps double-resolution from throwing, so paste + Enter + Cancel + listener races resolve once and log nothing.

## 5. `Reset()`

Called by `LauncherApp.AskForOtp()` (`LauncherApp.cs:246`) **before** the state switches to `OtpEntry` (`LauncherApp.cs.md` §7). Sequence (lines 113–148):

1. `otp = string.Empty`, `Result = null`, `Cancelled = false` (lines 115–117).
2. `resultTcs = new TaskCompletionSource<string?>()` (line 118) — a fresh task, so a stale `TrySetResult` from a previous session targets the discarded task.
3. `Ui.Post(() => { otpInput.Value = string.Empty; otpInput.Focus(); })` (lines 120–124) — UI mutation is posted, so `Reset()` is safe from the login background task.
4. Steam Deck gamepad keyboard, when `Program.Steam != null && Program.Steam.IsValid && Program.IsSteamDeckHardware && App.Settings.IsOtpServer is false` (line 128): `ShowGamepadTextInput(false, false, Strings.EnterYourOTP, 6, string.Empty)`, `steamDeckInputActive = success`, verbose log (lines 130–132). `App.Settings.IsOtpServer is false` (identity comparison against `false`) skips the keyboard when the setting is `null` **and** when it is `true` — `is false` matches only boxed `false`, so `IsOtpServer == null` also suppresses the keyboard.
5. When `App.Settings.IsOtpServer ?? false` (line 135): `StartOtpListener()` inside try/catch; failures logged as `Log.Error(ex, "Could not start OTP HTTP listener")` (lines 137–146).

The two `TODO`/`BUG` comments in the file are HEAD's: the gamepad keyboard does not work when `Reset` runs right after startup (Steam initializes slowly), and the keyboard must be disabled for the OTP server because there is no way to dismiss an open keyboard.

`SteamOnOnGamepadTextInputDismissed` (lines 102–111) is subscribed in the constructor (line 39) and **never unsubscribed**; it guards on `steamDeckInputActive`, so it is inert outside a gamepad-input session. The subscription lives for the process lifetime, and `steamDeckInputActive` is cleared before use (line 107) to prevent double-accept.

## 6. OTP listener lifecycle

- `StartOtpListener()` (lines 165–173): only acts when `otpListener is null`; creates `new OtpListener("core-" + AppUtil.GetAssemblyVersion())`, subscribes `OnOtpReceived += TryAcceptOtp`, then `Task.Run(this.otpListener.Start)` — start is not awaited, so listener failures surface on the background task.
- `StopOtpListener()` (lines 175–183): `otpListener.Stop()`, unsubscribe, `otpListener = null`. Called by `LauncherApp.WaitForOtpAsync` after the result is obtained (`LauncherApp.cs.md` §7).
- The listener name embeds the assembly version (`AppUtil.GetAssemblyVersion()` — `AppUtil.cs.md`), which the external OTP server matches.

## 7. Consumers

| Consumer | Site | Use |
| --- | --- | --- |
| `LauncherApp.AskForOtp` | `LauncherApp.cs:246` | `otpEntryPage.Reset()` then `State = OtpEntry` |
| `LauncherApp.WaitForOtpAsync` | `LauncherApp.cs:252` | `await otpEntryPage.GetResultAsync()` then `StopOtpListener()` |
| `MainPage` login path | `MainPage.cs:222`, `MainPage.cs:223` | asks for OTP when login needs it, awaits the code |
| `LauncherApp` ctor | `LauncherApp.cs:149` | page built once on the UI thread |

`MainPage` reads neither `Result` nor `Cancelled`; the awaited string *is* the code, and `null` (cancel) flows through the login path (`MainPage.cs.md`).

## 8. Quirks

- **`Cancelled` is write-only inside this file.** Nothing in `src/XIVLauncher.Core` reads `OtpEntryPage.Cancelled` (grep: no consumer), so the cancel signal is conveyed only by the `null` result.
- **`Result` is write-only too**: consumers use `GetResultAsync()`, not `Result`.
- The page keeps no `UITimer` and no background work of its own; the only long-lived objects are the `OtpListener` and the Steam event subscription.
- `MaxLength = 7` lets a 7th digit be typed, and `Input`'s decimal sanitization (`Common/Input.cs.md`) allows it; validation rejects it at accept time, logging `Invalid OTP`.
- Clipboard paste takes `Clipboard.Instance.Text` wholesale, including non-digits; the decimal filter in `Common.Input` sanitizes `Value` on `TextChanged` (`Common/Input.cs.md`), so a pasted string with letters is filtered by the entry, not by `TryAcceptOtp`.
- `Program.Steam` is captured at construction only when non-null (line 38); Steam is created in `Program.Main` before `LauncherApp`, so this is normally true, and Steam failure leaves the gamepad path disabled.

## 9. Referenced / referenced-by

Uses: `Page` (`Page.cs.md`), `Common.Input` (`Components/Common/Input.cs.md`), `Common.Button` (`Components/Common/Button.cs.md`), `EtoColors` (`EtoColors.cs.md`), `Ui` (`Ui.cs.md`), `Clipboard`, `OtpListener` (`XIVLauncher.Common.Http`), `AppUtil.GetAssemblyVersion` (`AppUtil.cs.md`), `Strings`, `Log`, `Program.Steam`, `Program.IsSteamDeckHardware`, `FontAwesomeIcon.Paste`.
Referenced by: `LauncherApp` (construction, `AskForOtp`, `WaitForOtpAsync`), `MainPage` (login path).
