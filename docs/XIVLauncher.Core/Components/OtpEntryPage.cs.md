# OtpEntryPage.cs — Specification

**File:** `src/XIVLauncher.Core/Components/OtpEntryPage.cs` · 161 lines · 5181 bytes
**Namespace:** `XIVLauncher.Core.Components` · **Declaration:** `public class OtpEntryPage : Page`
**Role:** Six-digit OTP entry screen that can be filled by keyboard, clipboard paste, Steam Deck gamepad overlay, or the local OTP HTTP server — and reports completion as an awaitable `Task`

---

## 1. Purpose and the entry/exit choreography

This page is a *modal input request*: `MainPage.cs.md`'s login path suspends until an OTP arrives.

```
MainPage.cs:202  App.AskForOtp();                                             // LauncherApp.cs:220
MainPage.cs:203  otp = await App.WaitForOtpAsync().ConfigureAwait(false);     // LauncherApp.cs:226
```

`LauncherApp.AskForOtp` (`LauncherApp.cs:220-224`) = `otpEntryPage.Reset()` **then** `State = LauncherState.OtpEntry` (which fires `otpEntryPage.OnShow()` at `LauncherApp.cs:79` — `Page`'s empty hook, see `Page.cs.md` §"OnShow contract"). `LauncherApp.WaitForOtpAsync` (`LauncherApp.cs:226-231`) = `await otpEntryPage.GetResultAsync()` then **`otpEntryPage.StopOtpListener()`** — the teardown counterpart of this page's own `StartOtpListener()`. `MainPage.cs:197/200` gate the whole flow (`isOtp && !App.UniqueIdCache.HasValidCache(username)`, with the uid-cache bypass in `MainPage.cs.md` §login).

This file's contract, therefore: `Reset()` arms the page, `GetResultAsync()` is the **only** output channel (grep: `Result` and `Cancelled` have zero readers outside this class), and the page must call `TrySetResult` exactly once per `Reset()` for the awaiting login to resume.

## 2. Surface

| Member | Kind | Notes |
| --- | --- | --- |
| `otp` | `private string = string.Empty` | the ImGui text buffer, passed `ref` into `InputText` — §4 |
| `appearing` | `private bool` (no init) | `true` only after `Reset()`; consumed once in `Draw()` — §4 |
| `otpListener` | `private OtpListener?` | the local HTTP OTP server — §6 |
| `resultTcs` | `private TaskCompletionSource<string?> = new()` | replaced by `Reset()`; the completion channel — §3 |
| `Result` | `public string? get`, `private set` | last accepted OTP (no external reader — §3) |
| `Cancelled` | `public bool get`, `private set` | cancel was requested (no external reader — §3) |
| `GetResultAsync()` | `public Task<string?>` | `=> this.resultTcs.Task`, expression-bodied |
| `SteamOnOnGamepadTextInputDismissed(bool)` | `private` | Steam overlay-dismissed callback → `TryAcceptOtp(GetEnteredGamepadText())` |
| `Reset()` | `public void` | clears state, re-arms focus, may open deck keyboard, may start HTTP listener |
| `TryAcceptOtp(string otp)` | `private void` | the single accept gate — §4 |
| `StartOtpListener()` / `StopOtpListener()` | `public void` | HTTP listener lifecycle — §6 |
| `Draw()` | `override` | centred child window with input + paste + OK + cancel |

No `OnShow()` override — the entry work is in `Reset()`, which `LauncherApp` calls explicitly *before* the state change. Usings: `System.Numerics`, `Hexa.NET.ImGui`, `Serilog`, `XIVLauncher.Common.Http` (`OtpListener`), `XIVLauncher.Core.Resources.Localization` (`Strings`).

## 3. `TaskCompletionSource` is the completion protocol

```csharp
private TaskCompletionSource<string?> resultTcs = new();
public  Task<string?> GetResultAsync() => this.resultTcs.Task;
```

- **Three result shapes**, all delivered through `resultTcs.TrySetResult`: the accepted 6-digit OTP (a `string`), an explicit cancel (`TrySetResult(null)` from the Cancel button), and *never completing* (the await hangs the login task forever — see §4). `MainPage.cs:203`'s caller treats `null` as "no OTP" and continues login with `otp` still `null`, so a cancelled OTP is **not** an error signal to the login path; `Cancelled` exists to make that distinction visible to the UI, but nothing reads it today.
- **`TrySetResult` is first-write-wins** (TCS semantics), which is what makes the multiple producers safe: keyboard OK, Enter (`doEnter`), clipboard paste, the deck callback, and the HTTP listener can all fire in one frame and only the first is delivered. Do not replace it with a plain field + `Task.Of`.
- **`Reset()` allocates a fresh TCS**, so the awaited `Task` object identity changes per entry: `GetResultAsync()` is called *after* `Reset()` in the current choreography (`LauncherApp.cs:222` → `:228`), which is the required order. Any code that captured `GetResultAsync()`'s `Task` before a `Reset()` would await an already-orphaned task that can never complete — an ordering rule this file cannot enforce.
- **`Result` is `get`-only from outside** (`private set`), so a page-side "show the OTP we got" affordance must be rendered from within this class or via a new accessor; the field is written in `TryAcceptOtp` alongside `TrySetResult`, so `Result` and the delivered task value are always the same object.

## 4. The accept gate and the 7-vs-6 buffer

```csharp
private void TryAcceptOtp(string otp)
{
    if (string.IsNullOrEmpty(otp) || otp.Length != 6) { Log.Error("Invalid OTP: {Otp}", otp); return; }
    Log.Verbose("Received OTP: {Otp}", otp);
    this.Result = otp; this.resultTcs.TrySetResult(otp);
}
```

`var doEnter = ImGui.InputText("###otpInput", ref this.otp, 7, CharsDecimal | EnterReturnsTrue);`

- **Exactly six characters are accepted**, while the ImGui buffer is **7** wide (`MaxLength` = 7): the extra slot exists so the `EnterReturnsTrue` press can be recorded — the accepted-length rule and the buffer length differ *by design*, so neither number may be edited alone. `CharsDecimal` blocks non-digits but does not bound the count, so a 7-char entry is refused by `otp.Length != 6` and, critically, **stays in `this.otp`**: nothing clears the buffer, so a later OK-button press re-submits the same bad value. The user-visible fix is the Cancel button, not a retype — see §7 for the flow.
- **Rejection is silent to the UI**: `Log.Error` only. No `ImGui.SetTextColor` flash, no `Progress`-style error marker (contrast `Common/Checkbox.cs.md` §validation, which paints the id). Any new "why did my OTP fail" feedback belongs in this class's `Draw()` (which owns the buffer and the accept result), not in the accept gate.
- **`otp` is the shared model/widget buffer** (`ref this.otp`), same hazard class as `Common/Input.cs.md` §"`inputBacking` passed `ref`": ImGui's per-id store exchanges text with it every frame, and the paste branch (`this.otp = ImGui.GetClipboardTextS()`) overwrites it *outside* ImGui's own length bound — a 6-char rule downstream is what keeps an over-length paste from being accepted, but ImGui may re-truncate what it displays. Treat `this.otp` as "last exchanged text", never as a validated OTP; only `TryAcceptOtp`'s argument checks constitute validity.
- **`appearing` is a one-shot focus armer**: `Reset()` sets `appearing = true`, the first `Draw()` after that does `SetKeyboardFocusHere(0); this.appearing = false;`. The guard is needed because `ImGui.IsWindowAppearing()`-style detection is unavailable in a `BeginChild` block whose id is a package constant (`"###otp"`, §7) — ImGui keeps the window alive across entries, so "this is the first frame of this OTP request" must be tracked here. Consequence: focus is armed exactly once per `Reset()`, i.e. once per `AskForOtp`; if the user's keyboard focus is stolen by another window (`ImGui.BindPopup`/focus loss), nothing re-arms it and the Enter path stops working while the OK button still does.
- **`TryAcceptOtp` is called from three draw-side sites and one off-draw site**: OK button, `doEnter`, the paste button (indirectly, by forcing `doEnter = true`), and the Steam `OnGamepadTextInputDismissed` callback (`§5`). It is `private`, so a new producer must be added inside this class.

## 5. Steam Deck branches

```csharp
ctor:      if (Program.Steam is not null) Program.Steam.OnGamepadTextInputDismissed += this.SteamOnOnGamepadTextInputDismissed;
callback:  if (success && Program.Steam is not null) TryAcceptOtp(Program.Steam.GetEnteredGamepadText());
Reset():   if (Program.Steam != null && Program.Steam.IsValid && Program.IsSteamDeckHardware && App.Settings.IsOtpServer is false)
               Program.Steam.ShowGamepadTextInput(false, false, Strings.EnterYourOTP, 6, string.Empty);
```

- **The ctor binds a Steam callback per instance and never unbinds it** — same shape as `Common/Input.cs.md` §handler leak (`Common/Input.cs.md` documents the counter-pressure; here the count is one, since `LauncherApp.cs:136` constructs a single instance). `StopOtpListener()` unbinds only `otpListener.OnOtpReceived`, so the Steam handler stays attached for the process lifetime. Keep the ctor-only, long-lived assumption; if a second `OtpEntryPage` is ever constructed, this line needs the unbind counterpart.
- **`GetEnteredGamepadText()` returns `string`** (`ISteam`/`UnixSteam.cs` in `lib/FFXIVQuickLauncher/src/XIVLauncher.Common.Unix/`, `:59`), and the deck flow bypasses the ImGui buffer entirely: the accepted OTP goes straight to `TryAcceptOtp`, leaving `this.otp` stale. That is why `Result` exists as a separate record — the deck path's truth is in `Result`, the keyboard path's truth is in `otp`. Read `Result`, never `otp`, when comparing submissions.
- **`ShowGamepadTextInput(..., maxChars = 6, existingText = string.Empty)`** opens the deck overlay keyboard at the *top* of `Reset()`, before the listener branch. Its `6` is a third OTP-length literal (accept gate `6`, ImGui buffer `7`), and `false, false` means "not password, single line" — this OTP screen shows the digits on screen, unlike the password input in `MainPage/LoginFrame.cs.md`.
- **The `is false` pattern is the three-state trap to know about**: `App.Settings.IsOtpServer` is `bool?` (`src/XIVLauncher.Core/Configuration/ILauncherConfig.cs:41`), and `x is false` is true **only** for an explicit `false`. So the deck keyboard is opened *only* when `IsOtpServer` is explicitly `false`; when it is `null` (or `true`) the keyboard is skipped. In practice `Program.cs:120` runs `Config.IsOtpServer ??= false` at startup, so `null` cannot reach this check in the shipped flow — but the source-level rule is "explicit false", not "not true", and the two differ for any caller that reads config before that defaulting line. Use `!= true` if the intent is "not opted in"; keep `is false` if it is "explicitly opted out".
- The source's own comment on that branch records the known limitation: `// BUG: We have to turn this off when using OTP server, because there's no way to dismiss open keyboards` (and a `TODO(goat)` about `Reset()` being called too early for Steam to be ready). Both are deck-only caveats; do not "fix" the deck keyboard unconditionally — the server flow and the overlay keyboard are mutually exclusive on the Deck, which is why the guard is config-driven rather than capability-driven.
- **The UI that sets `IsOtpServer`** is `SettingsPage/Tabs/SettingsTabGame.cs.md` (entry at `:37`, labelled `Strings.UseXLAuthMacrosSetting`), and `Program.cs:121` also derives the neighbouring `IsIgnoringSteam` from env — see `FtsPage.cs.md` §3 for the env/config interplay. Keep the three sites (`ILauncherConfig.cs`, that settings entry, this check) in step.

## 6. The OTP HTTP listener

```csharp
private void StartOtpListener() → if (otpListener is null) { otpListener = new OtpListener("core-" + AppUtil.GetAssemblyVersion());
                                                   otpListener.OnOtpReceived += this.TryAcceptOtp;
                                                   Task.Run(this.otpListener.Start); }
public void StopOtpListener()  → if (otpListener is not null) { otpListener.Stop(); otpListener.OnOtpReceived -= this.TryAcceptOtp; }
                                  otpListener = null;   // unconditional
```

- `OtpListener.cs` (lib, `lib/FFXIVQuickLauncher/src/XIVLauncher.Common/Http/OtpListener.cs`) is a **`public` wrapper around an `internal HttpServer`** on port **4646** (`:18 HTTP_PORT`), whose ctor builds the thread but does **not** start it — `Start()` (`:36`) calls `serverThread.Start()`, and `OtpEntryPage` runs that inside `Task.Run(...)` so `Reset()` never blocks on server setup. Consequence: the listener is live a moment *after* `Reset()` returns, and a page drawn immediately can be older than the server — harmless here (the server only *receives*).
- Accepted OTPs arrive as `GET /ffxivlauncher/{otp}` (`OtpListener.cs:24`, path prefix `"/ffxivlauncher/"`, substring at 15), which is the desktop-browser flow: the user's browser hits the local server, and the page receives it without any UI interaction. `version` string = `"core-" + AppUtil.GetAssemblyVersion()` (`AppUtil.cs.md` §GetAssemblyVersion) is passed so the served page can match launcher builds.
- **`OnOtpReceived` is an instance-level event** (declared `public event LoginEvent OnOtpReceived` inside a non-`static` class), so the `+=`/`-=` pair here is per-instance bookkeeping; `StopOtpListener` still keeps the pair symmetric — and `OtpEntryPage.cs:29`'s field is what keeps the listener (and its thread) reachable until `Stop()` runs. `HttpServer.Stop()` (`HttpServer.cs:84`) is the real teardown; `StopOtpListener` nulls the field unconditionally afterwards, so a stray call on a never-started page is a cheap no-op.
- **`StopOtpListener()` is called by `LauncherApp.WaitForOtpAsync`** (`LauncherApp.cs:229`) — i.e. after the await resolves, on **every** OTP wait, whether a listener existed or not. Do not add a `StartOtpListener`-conditional around that call in `LauncherApp`: this method is already null-safe, and `LauncherApp.cs.md` §otp-await covers the await/teardown pairing.
- **`Reset()` can start the listener more than once per process** (each `AskForOtp`), guarded by `otpListener is null`. After a cancelled OTP, `WaitForOtpAsync` stopped it, so a re-entry restarts it — that is the intended lifecycle; do not move the start to the ctor (it must not run when `IsOtpServer` is off), and do not remove the null guard (a double start would leak a second server thread).
- `Reset()`'s listener start is wrapped in `try { StartOtpListener(); } catch (Exception ex) { Log.Error(ex, "Could not start OTP HTTP listener"); }` — a local swallow that leaves the page usable via keyboard. Keep that scope: a listener failure must not abort login, and the only fallback is on-screen typing.

## 7. `Draw()` — geometry and the four input routes

```
PushStyleVar(ChildRounding, 7f)
childSize = (300, 200); vpSize = ImGuiHelpers.ViewportSize
SetNextWindowPos((vpSize.X/2 - childSize.X/2, vpSize.Y/2 - childSize.Y/2), ImGuiCond.Always)
SetNextWindowBgAlpha(0.4f)
BeginChild("###otp", childSize, AlwaysAutoResize | AutoResizeY | AutoResizeX)
    Dummy(40)
    CenteredText(Strings.EnterYourOTP)
    const PASTE_ICON_WIDTH = 28; const INPUT_WIDTH = 150
    SetNextItemWidth(INPUT_WIDTH); CenterCursorFor(INPUT_WIDTH + PASTE_ICON_WIDTH)
    if (appearing) { SetKeyboardFocusHere(0); appearing = false; }
    doEnter = InputText("###otpInput", ref otp, 7, CharsDecimal | EnterReturnsTrue)
    SameLine(); SetNextItemWidth(28); PushFont(IconFont, 0.0f)
        if (Button(FontAwesomeIcon.Paste.ToIconString(), (28, 28))) { otp = GetClipboardTextS(); doEnter = true; }
    PopFont()
    buttonSize = ((INPUT_WIDTH + 28)/2, 30)
    CenterCursorFor(INPUT_WIDTH + 28)
    if (Button(Strings.OKLabel, buttonSize) || doEnter) TryAcceptOtp(otp)
    SameLine()
    if (Button(Strings.CancelLabel, buttonSize)) { Cancelled = true; resultTcs.TrySetResult(null); }
EndChild(); PopStyleVar(); base.Draw()
```

- **The centring model is "total width = input + paste icon"** (`CenterCursorFor(INPUT_WIDTH + PASTE_ICON_WIDTH)` twice, once before the input, once before the buttons): the row is centred as a pair, so the input box and the paste button read as one control line. Editing `INPUT_WIDTH` alone re-flows both, which is why they are `const` locals (`const int PASTE_ICON_WIDTH = 28; const int INPUT_WIDTH = 150;` — see `Spinner.cs.md` §`const` idiom) and there is no field for them: geometry lives in one place, in one frame.
- **`buttonSize` is shared by OK and Cancel**, so both hit boxes are `89×30`; `ImGui.SameLine()` before Cancel puts them side by side. This is the page's only *guaranteed* exit (`Cancelled = true; TrySetResult(null)`) and the only exit from a stuck over-length buffer (§4) — keep it clickable and unconditioned (unlike `FtsPage.cs.md` §5, whose finish button is mode-gated).
- **`BeginChild("###otp", ...)` with `AlwaysAutoResize | AutoResizeX | AutoResizeY`** and `SetNextWindowPos(..., ImGuiCond.Always)`: position is re-applied every frame while size is content-driven. The id is the package-level constant `"###otp"` (not `Component.Id`-derived, contrast `Component.cs.md` §5 and `SettingsEntry.cs.md` §id-usage) so the window persists across OTP requests and ImGui's per-id data (`InputText`'s text for `"###otpInput"`) **survives across entries**. Consequence: the persisted text is *not* cleared by `Reset()` (which clears `this.otp`, not ImGui's store) — the id pair here is the one place where a `Reset()` that forgets ImGui's own state can show a stale OTP on the first frame of a new request. If that ever bites, the fix is in this `Draw()` (clear the buffer or use a per-entry id), not in the accept gate.
- **The paste button is the clipboard route** and needs no `ImGui.SaveTextInputs` (contrast `SettingsEntry.cs.md` §SaveTextInputs): `ImGui.GetClipboardTextS()` reads the *system* clipboard, so an out-of-app OTP (phone screenshot, SMS text) can be pasted without entering the text box at all. The `|| doEnter = true` assignment reuses the OK path, so a paste is a *submit*, not an edit. It also runs **inside `PushFont(FontManager.IconFont, 0.0f)` / `PopFont()`**: the `0.0f` size argument means "keep the current size" while switching the font (`ImGuiHelpers.cs.md` §CenteredText uses the same `0.0f` convention; `FontAwesomeIcon.cs.md` for the glyph). `Button(Strings.OKLabel, buttonSize)` and `Button(Strings.CancelLabel, buttonSize)` are outside that font scope, correctly.
- **`ImGui.PushStyleVar(ImGuiStyleVar.ChildRounding, 7f)` / `PopStyleVar()`** wrap the whole child window (rounding the *window* corners, not the buttons) and are balanced; `base.Draw()` runs **after** the pop, so this page's rounding cannot leak into sibling components (`Component.cs.md` §"push/pop discipline" for the discipline rules).
- **`ImGuiHelpers.ViewportSize`** (`ImGuiHelpers.cs.md`) is read for centring; the `childSize` `(300, 200)` is only a *hint* (`AlwaysAutoResize` overrides it) and the `200` height is effectively unused — keep that in mind when editing: changing `childSize` alone changes nothing visible, whereas changing `INPUT_WIDTH`/`PASTE_ICON_WIDTH` does.
- **No `#if !DEBUG` demo mode exists in this file** (grep: no `#if` anywhere in `OtpEntryPage.cs`) — all four input routes are live in both build configs.

## 8. Invariants and failure modes worth preserving

- **The await hangs on "nothing accepted":** if the user neither submits, nor clicks OK, nor cancels (e.g. keyboard focus lost, §4), `resultTcs` never resolves and `MainPage.cs:203` stays parked on the OTP page. That is the *intended* UX (the launcher sits on the OTP screen) — do not add a timeout `TrySetResult` in `Draw()`; `LoadingPage.cs.md` §cancel is the project's abort channel for long waits, and it is not wired here.
- **`Cancelled` is not a completion signal**: the completion signal is `TrySetResult(null)`; the flag is decoration for future UI. `MainPage.cs` cannot distinguish "cancelled" from "no OTP" — see `MainPage.cs.md` §login for how `otp` being `null` flows into login (login continues with an empty OTP, which the server rejects). Any change to that flow must go through `LauncherApp.cs.md` §otp-await, not by reading `Cancelled` in `MainPage`.
- **`Result` and `otp` are two different truths** (§3, §4, §5): `Result` only ever holds an accepted value, `otp` holds whatever ImGui last exchanged, including rejected and over-length text. When adding an accessor, mirror that split (e.g. `AcceptedOtp` → `Result`, `LastTypedText` → `otp`) rather than reconciling them in one field.
- **No style-var or font stack is left open** past the `Draw()` (see the balance above): `PushStyleVar`/`PopStyleVar` pair once, `PushFont`/`PopFont` pair once (`FontManager.IconFont`, `FontManager.cs.md`), and the `FontAwesomeShared`/`FontAwesomeIconFont` aliases are `Resources/FontConfig.cs`'s, not this class's — see `ImGuiColors.cs.md`/`ImGuiHelpers.cs.md` for the app-level font pushes.
- **`const` locals (`PASTE_ICON_WIDTH`, `INPUT_WIDTH`) are compile-time constants**, so they cannot become user-scalable; if a DPI/viewport scaling is added, the natural shape is one scale factor multiplied into these locals plus `buttonSize`, inside this `Draw()` — keeping `INPUT_WIDTH + PASTE_ICON_WIDTH` as a single derived value in both `CenterCursorFor` calls.

## 9. Referenced / referenced-by

Constructed by: `LauncherApp.cs.md` ctor (`LauncherApp.cs:136`), selected via `LauncherApp.CurrentPage` (`LauncherApp.cs:101`), entered by `LauncherApp.AskForOtp` (`:220`) and torn down by `LauncherApp.WaitForOtpAsync` → `StopOtpListener()` (`:229`).
Driven by: `MainPage.cs.md` login (`MainPage.cs:202/203`, gated at `:197/:200`, `IsOtp` fed from the account at `MainPage.cs:95`), `MainPage/LoginFrame.cs.md` (`oneTimePasswordCheckbox` sets `IsOtp` in that page's `Draw()`), `MainPage/LoginAction.cs.md`.
Uses: `Page` (`Page.cs.md`), `Component` (`Component.cs.md`), `ImGuiHelpers.cs.md` (`ViewportSize`, `CenteredText`, `CenterCursorFor`), `OtpListener.cs` (`lib/FFXIVQuickLauncher/src/XIVLauncher.Common/Http/`) + `HttpServer.cs`, `AppUtil.cs.md`, `Program.cs.md` (`Steam`, `IsSteamDeckHardware`), `Strings` (`src/XIVLauncher.Core/Resources/Localization/Strings.Designer.cs`), `FontAwesomeIcon.cs.md`, `FontManager.cs.md`, `UnixSteam.cs` (`lib/.../XIVLauncher.Common.Unix/`).
Related: `Common/Input.cs.md` (the parallel text-input component — this page deliberately uses raw `ImGui.InputText` instead, because it needs `CharsDecimal | EnterReturnsTrue`), `LoadingPage.cs.md` (spinner page used during OTP-less waits), `SettingsPage/Tabs/SettingsTabGame.cs.md` (`IsOtpServer` toggle).
