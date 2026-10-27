# OtpEntryPage.cs — Specification

**File:** `src/XIVLauncher.Core/Components/OtpEntryPage.cs` · 211 lines · 8,184 bytes
**Namespace:** `XIVLauncher.Core.Components` · **Declaration:** `public class OtpEntryPage : Page`
**Role:** OTP entry screen — centered entry box, paste button, OK/Cancel, plus the optional OTP HTTP listener and Steam Deck gamepad input

---

## 1. Purpose

`OtpEntryPage` is shown when login needs a 6-digit OTP code. It collects the code from three sources: typing, the clipboard (paste button), and — on Steam Deck — the Steam gamepad keyboard; plus, when configured, an HTTP listener that receives codes from an external OTP server. The result is delivered through a `TaskCompletionSource` that `LauncherApp.WaitForOtpAsync` awaits.

The immediate-mode version drew a real 300×200 child window with 40% alpha background and used an ImGui
keyboard-focus trick; the Eto version is a centered fixed-width box with an opaque bitmap tint, and
`Focus()` on the entry (`Components/Common/Input.cs.md`).

Usings: `Eto.Drawing`, `Eto.Forms`, `Serilog`, `XIVLauncher.Common.Http` (`OtpListener`),
`XIVLauncher.Core.Resources.Localization`, plus `Gtk`/`Gdk` (GtkSharp, transitively via
`Eto.Platform.Gtk`) inside the `#if LINUX` clipboard branch (lines 59–66); the `#else`
branch (lines 63–66) uses Eto's `Clipboard.Instance.Text`, which reads correctly on the
WinForms platform.

## 2. Surface

| Member | Kind | Notes |
| --- | --- | --- |
| `ChildWidth` | `private const int` (line 25) | HEAD child window width (300) |
| `ChildHeight` | `private const int` (line 26) | HEAD child window height (200) |
| `otp` | `private string` (line 28) | last accepted code, written by `TryAcceptOtp` |
| `otpListener` | `private OtpListener?` (line 29) | HTTP listener, created on demand (lines 191–199) |
| `resultTcs` | `private TaskCompletionSource<string?>` (line 30) | recreated on `Reset` (line 144) |
| `steamDeckInputActive` | `private bool` (line 31) | set when `ShowGamepadTextInput` succeeds (line 157) |
| `otpInput` | `private readonly Common.Input` (line 33) | built in ctor (lines 49–52) |
| `Result` | `public string? { get; private set; }` (line 35) | the accepted code |
| `Cancelled` | `public bool { get; private set; }` (line 37) | set by the Cancel button |
| `GetResultAsync()` | `public Task<string?>` (line 39) | returns `resultTcs.Task` |
| `CreateTintView()` | `private static ImageView` (lines 118–126) | opaque bitmap fill used as the box background |
| `SteamOnOnGamepadTextInputDismissed(bool)` | `private` (lines 128–137) | Steam gamepad callback |
| `Reset()` | `public` (lines 139–174) | clears state, focuses input, starts gamepad input / OTP server |
| `TryAcceptOtp(string)` | `private` (lines 176–189) | validates and resolves the task |
| `StartOtpListener()` | `public` (lines 191–199) | creates and starts the listener |
| `StopOtpListener()` | `public` (lines 201–210) | stops listener, unsubscribes, clears field |

## 3. Layout

`DynamicLayout { Padding = new Padding(0, 200) }` with `layout.AddCentered(...)` (lines 111–113) — a 200 px top/bottom padding band, box centered in the remaining width. The box is `Panel { Size = new Size(ChildWidth, ChildHeight) }` (line 112) whose content is a `PixelLayout` (lines 107–109) with both children at `(0, 0)`:

1. `CreateTintView()` (lines 118–126) — a 300×200 `ImageView` filled with `EtoColors.OtpChildOpaqueBackground`, drawn as the background layer. GTK renders no alpha and never paints a `Panel` background, so HEAD's 40% alpha `WindowBg` is baked into one opaque fill.
2. `rows` — `StackLayout { Padding(0, 40, 0, 0), Spacing = 10, Width = ChildWidth }` (lines 86–105), items in HEAD order:
   1. `Label { Strings.EnterYourOTP, TextAlignment = Center }` (line 93)
   2. horizontal `StackLayout` with `this.otpInput` and `pasteButton` (lines 94–98)
   3. horizontal `StackLayout` with `okButton`, `cancelButton` (lines 99–103)

Every row is wrapped in `StackLayoutItem(HorizontalAlignment.Center)`, and the stack is pinned to `Width = ChildWidth` (line 90). GTK ignores `Label.TextAlignment`, and an unpinned stack sizes to its natural 188 px, so centring would happen inside the row width and the content would sit at the left of the box. Items *inside* a row stay plain: `VerticalAlignment.Center` pushes the fixed-size 28 px paste button below the entry's top edge.

Control sizes reproduce HEAD's pixel sizes: entry `InputWidth = 150` (line 51), paste button `28x28` (line 55), OK and Cancel `89x30` (lines 70, 73). `InputWidth` targets the entry itself, not the label+entry wrapper (`Components/Common/Input.cs.md`).

The OTP entry uses `Common.Input(string.Empty, string.Empty, null, 7, flags: Common.InputFlags.Decimal | Common.InputFlags.EnterReturnsTrue)` (line 49): no label (hidden), no hint, `MaxLength = 7`, digits only, Enter accepted. `MaxLength = 7` with `TryAcceptOtp` requiring exactly 6 is HEAD's behaviour.

## 4. Accepting a code

`TryAcceptOtp(string otp)` (lines 176–189):

```csharp
if (string.IsNullOrEmpty(otp) || otp.Length != 6) { Log.Error("Invalid OTP: {Otp}", otp); return; }
this.otp = otp;
Log.Verbose("Received OTP: {Otp}", otp);
this.Result = otp;
this.resultTcs.TrySetResult(otp);
```

Call sites:
- `otpInput.Enter` → `TryAcceptOtp(this.otpInput.Value)` (line 53) — `InputFlags.EnterReturnsTrue` fires `Enter` (`Common/Input.cs.md`).
- paste button → sets `otpInput.Value` from `Gtk.Clipboard.Get(Gdk.Selection.Clipboard).WaitForText()` (line 62, `#if LINUX`) then accepts (line 67). Eto.Gtk binds `Clipboard.Text` to a write-only GtkSharp property, so `Clipboard.Instance.Text` is always `null` on GTK — the GtkSharp request is the only read that sees the clipboard. On Windows the `#else` branch (lines 63–66) reads `Clipboard.Instance.Text`, which works on the WinForms platform.
- OK button → accepts current value (line 71).
- Steam gamepad dismissal → accepts `Program.Steam.GetEnteredGamepadText()` (line 136).
- OTP listener → `otpListener.OnOtpReceived += this.TryAcceptOtp` (line 196).

Pasting 6 digits submits immediately, without OK: the paste handler calls `TryAcceptOtp` itself. HEAD did the same — the paste button set `doEnter = true` (`git show 93a7a22`).

**Thread note:** the listener runs on a background task (`Task.Run(this.otpListener.Start)`, line 197), so `TryAcceptOtp` is invoked off the UI thread. It only mutates `otp`, `Result`, and the `TaskCompletionSource` — no Eto controls — so no marshalling is required. The paste and OK handlers do touch `otpInput.Value`, and they run on the UI thread.

Cancel (lines 74–78): `Cancelled = true`, `resultTcs.TrySetResult(null)`. `TrySetResult` (not `SetResult`) keeps double-resolution from throwing, so paste + Enter + Cancel + listener races resolve once and log nothing.

## 5. `Reset()`

Called by `LauncherApp.AskForOtp()` (`LauncherApp.cs:248`) **before** the state switches to `OtpEntry` (`LauncherApp.cs.md` §7). Sequence (lines 139–174):

1. `otp = string.Empty`, `Result = null`, `Cancelled = false` (lines 141–143).
2. `resultTcs = new TaskCompletionSource<string?>()` (line 144) — a fresh task, so a stale `TrySetResult` from a previous session targets the discarded task.
3. `Ui.Post(() => { otpInput.Value = string.Empty; otpInput.Focus(); })` (lines 146–150) — UI mutation is posted, so `Reset()` is safe from the login background task.
4. Steam Deck gamepad keyboard, when `Program.Steam != null && Program.Steam.IsValid && Program.IsSteamDeckHardware && App.Settings.IsOtpServer is false` (line 154): `ShowGamepadTextInput(false, false, Strings.EnterYourOTP, 6, string.Empty)`, `steamDeckInputActive = success`, verbose log (lines 156–158). `App.Settings.IsOtpServer is false` (identity comparison against `false`) skips the keyboard when the setting is `null` **and** when it is `true` — `is false` matches only boxed `false`, so `IsOtpServer == null` also suppresses the keyboard.
5. When `App.Settings.IsOtpServer ?? false` (line 161): `StartOtpListener()` inside try/catch; failures logged as `Log.Error(ex, "Could not start OTP HTTP listener")` (lines 163–172).

The two `TODO`/`BUG` comments in the file are HEAD's: the gamepad keyboard does not work when `Reset` runs right after startup (Steam initializes slowly), and the keyboard must be disabled for the OTP server because there is no way to dismiss an open keyboard.

`SteamOnOnGamepadTextInputDismissed` (lines 128–137) is subscribed in the constructor (line 45) and **never unsubscribed**; it guards on `steamDeckInputActive`, so it is inert outside a gamepad-input session. The subscription lives for the process lifetime, and `steamDeckInputActive` is cleared before use (line 133) to prevent double-accept.

## 6. OTP listener lifecycle

- `StartOtpListener()` (lines 191–199): only acts when `otpListener is null`; creates `new OtpListener("core-" + AppUtil.GetAssemblyVersion())`, subscribes `OnOtpReceived += TryAcceptOtp`, then `Task.Run(this.otpListener.Start)` — start is not awaited, so listener failures surface on the background task.
- `StopOtpListener()` (lines 201–210): `otpListener.Stop()`, unsubscribe, `otpListener = null`. Called by `LauncherApp.WaitForOtpAsync` after the result is obtained (`LauncherApp.cs.md` §7).
- The listener name embeds the assembly version (`AppUtil.GetAssemblyVersion()` — `AppUtil.cs.md`), which the external OTP server matches.

## 7. Consumers

| Consumer | Site | Use |
| --- | --- | --- |
| `LauncherApp.AskForOtp` | `LauncherApp.cs:248` | `otpEntryPage.Reset()` then `State = OtpEntry` |
| `LauncherApp.WaitForOtpAsync` | `LauncherApp.cs:254` | `await otpEntryPage.GetResultAsync()` then `StopOtpListener()` (lines 254–255) |
| `MainPage` login path | `MainPage.cs:222`, `MainPage.cs:223` | asks for OTP when login needs it, awaits the code |
| `LauncherApp` ctor | `LauncherApp.cs:148` | page built once on the UI thread |

`MainPage` reads neither `Result` nor `Cancelled`; the awaited string *is* the code, and `null` (cancel) flows through the login path (`MainPage.cs.md`).

## 8. Quirks

- **`Cancelled` is write-only inside this file.** Nothing in `src/XIVLauncher.Core` reads `OtpEntryPage.Cancelled` (grep: no consumer), so the cancel signal is conveyed only by the `null` result.
- **`Result` is write-only too**: consumers use `GetResultAsync()`, not `Result`.
- The page keeps no `UITimer` and no background work of its own; the only long-lived objects are the `OtpListener` and the Steam event subscription.
- `MaxLength = 7` lets a 7th digit be typed, and `Input`'s decimal sanitization (`Common/Input.cs.md`) allows it; validation rejects it at accept time, logging `Invalid OTP`.
- Clipboard paste takes the clipboard text wholesale, including non-digits; the decimal filter in `Common.Input` sanitizes `Value` on `TextChanged` (`Common/Input.cs.md`), so a pasted string with letters is filtered by the entry, not by `TryAcceptOtp`.
- **Clipboard paste is `#if LINUX`/`#else` split (lines 59–66).** `Gtk.Clipboard.WaitForText()` is a synchronous X selection request on the UI thread; it returns `null` when nothing is on the clipboard, and can block briefly when the clipboard owner is another process. On Windows, `Clipboard.Instance.Text` (Eto's clipboard) is the read.
- `EtoColors.OtpChildBackground` (the 40% alpha tint) is no longer consumed here: GTK renders no alpha, so `CreateTintView` fills with `EtoColors.OtpChildOpaqueBackground` (`EtoColors.cs.md`).
- `Program.Steam` is captured at construction only when non-null (line 44); Steam is created in `Program.Main` before `LauncherApp`, so this is normally true, and Steam failure leaves the gamepad path disabled.

## 9. Referenced / referenced-by

Uses: `Page` (`Page.cs.md`), `Common.Input` (`Components/Common/Input.cs.md`), `Common.Button` (`Components/Common/Button.cs.md`), `EtoColors` (`EtoColors.cs.md`), `Ui` (`Ui.cs.md`), `Gtk.Clipboard` + `Gdk.Selection.Clipboard` (GtkSharp, `#if LINUX`), `Eto.Forms` `Clipboard.Instance` (Windows), `OtpListener` (`XIVLauncher.Common.Http`), `AppUtil.GetAssemblyVersion` (`AppUtil.cs.md`), `Strings`, `Log`, `Program.Steam`, `Program.IsSteamDeckHardware`, `FontAwesomeIcon.Paste`.
Referenced by: `LauncherApp` (construction, `AskForOtp`, `WaitForOtpAsync`), `MainPage` (login path).
