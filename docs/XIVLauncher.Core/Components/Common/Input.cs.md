# Input.cs — Specification

**File:** `src/XIVLauncher.Core/Components/Common/Input.cs` · 123 lines · 3490 bytes
**Namespace:** `XIVLauncher.Core.Components.Common` · **Declaration:** `public class Input : Component`
**Role:** Text field with label, hint, keyboard-focus hookup and a Steam-Deck gamepad-overlay fallback path

---

## 1. Purpose

`Input` is the launcher's text-entry primitive: it draws its label, then an ImGui text box that spans the containing window, and fires an `Enter` event when the user submits. It also carries the *Steam Deck* branch: when the deck is active, submission is delegated to Steam's gamepad text input overlay instead of ImGui's own box.

Instances (all outside this file): exactly **two** — `loginInput` and `passwordInput`, both in `MainPage/LoginFrame.cs.md` (`LoginFrame.cs:72` and `:75`). Other text fields in the package bypass this component and call ImGui directly: `OtpEntryPage.cs.md` uses `ImGui.InputText("###otpInput", ref this.otp, 7, …)` and `SettingsPage/SettingsEntry{T}.cs.md` uses `ImGui.InputText($"###{Id}", …)`. Usings: `System.Numerics`, `Hexa.NET.ImGui`, `Serilog`.

## 2. Surface

| Member | Kind | Notes |
| --- | --- | --- |
| `inputBacking` | `private string = ""` | the only storage; passed **`ref`** to ImGui |
| `isSteamDeckInputActive` | `private volatile bool` | deck-overlay flag, written by two threads — see §6 |
| `Label` | `public string`, **`get` only** | drawn as `ImGui.Text`, *not* as the box's id |
| `Hint` | `public string`, **`get` only** | placeholder text inside the box |
| `MaxLength` | `public uint`, **`get` only** | ImGui buffer capacity for the value |
| `Flags` | `public ImGuiInputTextFlags`, **`get` only** | e.g. `Password`, `NoUndoRedo`, `CharsDecimal` |
| `IsEnabled` | `public bool`, `get`/`set`, `= true` | honoured — see §5 |
| `Spacing` | `public Vector2`, **`get` only** | trailing `ImGui.Dummy` gap, default `Vector2.Zero` |
| `HasSteamDeckInput` | `public bool`, `get`/`set` | set by ctor from `Program.IsSteamDeckHardware` |
| `SteamDeckPrompt` | `public string`, `get`/`set` | defaults to `Hint`; shown in the Steam overlay |
| `TakeKeyboardFocus` | `public bool`, `get`/`set` | opt-in keyboard focus (default `false`) |
| `Enter` | `public event Action?` | fired on submit — see §5 |
| `Value` | `public string`, `get`/`set` | explicit proxy for `inputBacking` |
| `Input(label, hint, spacing, maxLength = 255, isEnabled = true, flags = None)` | ctor | multi-line parameter list; also binds the Steam handler |
| `SteamOnOnGamepadTextInputDismissed(bool success)` | `private` | plain method bound by hand in the ctor (not a feature hook) |

## 3. Constructor contract

```csharp
Label = label;  Hint = hint;  MaxLength = maxLength;  Flags = flags;
IsEnabled = isEnabled;  Spacing = spacing ?? Vector2.Zero;
SteamDeckPrompt = hint;

if (Program.Steam != null) {
    Program.Steam.OnGamepadTextInputDismissed += this.SteamOnOnGamepadTextInputDismissed;
    HasSteamDeckInput = Program.IsSteamDeckHardware;
}
```

- **Steam handler registration happens in the constructor.** `Program.Steam.OnGamepadTextInputDismissed += this.SteamOnOnGamepadTextInputDismissed` is added per constructed instance and never removed, so the count of live handlers tracks the number of `Input` instances ever created. Today that is exactly two (`LoginFrame.cs:72/75`), which is bounded and intentional; the constraint is that `Input` must stay a long-lived, constructor-only object — building one per frame or per refresh would accumulate Steam callbacks. The unbinding idiom, where it is needed at all, is shown in `OtpEntryPage.cs.md` §listener teardown.
- `Program.Steam` is a nullable process handle (`Program.cs.md`), so the null test is required; `HasSteamDeckInput` is then `false`, which keeps the deck branch inert on ordinary desktops.
- `MaxLength` defaults to **255**; both login instances pass `128` (`MainPage/LoginFrame.cs.md` §ctor). It is `get`-only, so it cannot be retuned after construction.
- `Flags` is `get`-only too: `passwordInput` passes `ImGuiInputTextFlags.Password | ImGuiInputTextFlags.NoUndoRedo` and `loginInput` takes the default `None` (see `MainPage/LoginFrame.cs.md` §ctor). Components that want `CharsDecimal | EnterReturnsTrue` submit semantics do not use this class (see `OtpEntryPage.cs.md`).
- `SteamDeckPrompt = hint` is an initial value only: it is `get`/`set`, so pages may re-word the deck prompt without touching the box hint.

## 4. Draw sequence

```
PushStyleVar(FramePadding, (12,10));   PushStyleVar(FrameRounding, 0)
PushStyleColor(FrameBg, BlueShade1);   PushStyleColor(FrameBgActive, BlueShade2)
PushStyleColor(FrameBgHovered, BlueShade2); PushStyleColor(TextDisabled, TextDisabled)
PushStyleColor(Text, Text)
if (TakeKeyboardFocus && ImGui.IsWindowAppearing()) SetKeyboardFocusHere()
ImGui.Text(Label)
if (!IsEnabled || isSteamDeckInputActive) { BeginDisabled(); isDisabled = true; }
SetNextItemWidth(GetWindowWidth());
PopStyleColor();                       // <-- pops exactly one colour, mid-function
ImGui.InputTextWithHint($"###{Id}", Hint, ref inputBacking, MaxLength, Flags)
if (IsItemFocused() && IsKeyPressed(Enter)) Enter?.Invoke()
if (IsItemActivated() && HasSteamDeckInput && Steam != null && Steam.IsValid)
    isSteamDeckInputActive = Steam.ShowGamepadTextInput(Flags.HasFlag(Password), false, SteamDeckPrompt, (int)MaxLength, inputBacking) ?? false
ImGui.Dummy(Spacing)
if (isDisabled) EndDisabled()
PopStyleVar(2); PopStyleColor(4)
```

Balance check: 5 colour pushes ↔ 1 mid-function pop + 4 at the end; 2 style-var pushes ↔ `PopStyleVar(2)`. The single mid-function `PopStyleColor()` pops **`Text`** (last pushed), so the *label* is drawn with the local colours while the input box itself is drawn with the enclosing window's colours. Style vars stay pushed across the whole body, so the box's frame padding is 12×10.

- **`ImGui.Text(Label)` uses the raw label** (id derived from the string), while the box uses the `###` id-split form (`$"###{Id}"`, empty display text, instance-unique id) — `Component.cs.md` §5. The box can therefore never collide, and the *label* is the identity-bearing one (see §7 hazard).
- **`SetNextItemWidth(GetWindowWidth())`** makes the box span the full window width — `Input` has no width parameter; its width is whatever window it is drawn into (`LoginFrame`'s child window, `SettingsEntry`'s parent window, `OtpEntryPage`'s auto-resizing child window).
- No `base.Draw()` call: like `Button`/`Checkbox`, an `Input` is a strict leaf — its `Children` collection is never painted.

## 5. Submission: `Enter`

```csharp
if (ImGui.IsItemFocused() && ImGui.IsKeyPressed(ImGuiKey.Enter))
    Enter?.Invoke();
```

- Focus is only established when `TakeKeyboardFocus` is set **and** the containing window is appearing (`ImGui.IsWindowAppearing()`), i.e. on the first frame of that window. Neither login instance sets `TakeKeyboardFocus` (grep: no writer anywhere), so `SetKeyboardFocusHere()` is currently never called by this class and the `Enter` branch below is reachable only while some *other* component has set ImGui keyboard focus — see `MainPage/LoginFrame.cs.md` §ctor for what the two live instances actually leave as default.
- `IsKeyPressed` is a *held* query, so holding `Enter` re-fires `Enter` on every frame after the first; consumers must be idempotent or self-guarding (`MainPage/MainPage.cs.md` guards with `IsLoggingIn`, `OtpEntryPage.cs.md`'s accept path is monotonic through a `TaskCompletionSource`).
- The mouse path is separate: ImGui's own "clicked → text entered" state, plus `ImGui.IsItemActivated()` for the deck branch.
- `Enter` is an `event` (`+=`-composable, externally non-assignable); the only binding sites in the project are `this.loginInput.Enter += TriggerLogin` and `this.passwordInput.Enter += TriggerLogin` (`MainPage/LoginFrame.cs.md` §ctor) — the same handler is also bound to `loginButton.Click`, so submit has several routes.
- **Mouse-driven `InputTextWithHint` returns a bool that this code ignores**: the `Enter` event is keyboard-only (plus the deck branch). A user with no keyboard submits by clicking — see the per-caller specs for how they compensate (`OtpEntryPage.cs.md` adds an explicit OK button; `MainPage/LoginFrame.cs.md` adds a login `Button`).

## 6. The Steam Deck branch

```csharp
isSteamDeckInputActive = Program.Steam?.ShowGamepadTextInput(
    Flags.HasFlag(ImGuiInputTextFlags.Password), false, SteamDeckPrompt, (int)MaxLength, this.inputBacking) ?? false;
```

- `ShowGamepadTextInput(passcode, unlisted?, prompt, maxSize, currentText)` returns whether the Steam overlay input is active; the result is stored as `isSteamDeckInputActive`. Note the argument is **`this.inputBacking`** — the current text is passed as the overlay's starting value.
- Gate: `ImGui.IsItemActivated()` (the user clicked/activated the box) and `HasSteamDeckInput` and a valid Steam handle. The `?? false` keeps the assignment total even though the null check already happened (the `?.` re-narrows).
- While `isSteamDeckInputActive` is true, the whole component is wrapped in `BeginDisabled` (via the `|| this.isSteamDeckInputActive` term, §5 of the draw) **except** the label text and the deck call — ImGui's own editing is suppressed so the Steam overlay owns entry.
- Completion arrives asynchronously through `OnGamepadTextInputDismissed` → `SteamOnOnGamepadTextInputDismissed(success)`:
  ```csharp
  if (success && this.isSteamDeckInputActive) inputBacking = Program.Steam!.GetEnteredGamepadText();
  this.isSteamDeckInputActive = false;
  ```
  `inputBacking` is written by whatever thread delivers the Steam callback while the draw thread reads it — hence the field is `volatile`, and it is the *only* mutable field in this class shared across threads.
- On the deck the `Enter` event is **not** fired by the dismiss callback: the callback only refreshes text. Callers on deck therefore need the submit path to come from a subsequent frame's focus test or from their own button (`MainPage/LoginFrame.cs.md` §Draw, `OtpEntryPage.cs.md`) — a real behavioural difference between desktop and deck that this class encodes implicitly.
- `Log.Information("SteamDeck Input Active({Name}): {IsActive}", this.Label, this.isSteamDeckInputActive)` uses the project's `Log.*` logging entry point whose format names its arguments (Serilog-side convention; see `Program.cs.md` §logging and `AppUtil.VersionInfo.cs.md` §"logging use"). The label is the log's discriminator, so `Strings.*` keys double as log keys.

## 7. Invariants and hazards

- **`inputBacking` is passed `ref` into `InputTextWithHint`**: as with `Common/Checkbox.cs.md`, the widget exchanges state with ImGui's per-id store, so external `Value = "x"` writes live in the same channel as the widget's own text and can be republished/overwritten on the next draw. Read `Value` as "last value exchanged", not as a model field (`MainPage/LoginFrame.cs.md` §accessors proxies it, `MainPage/MainPage.cs.md` reads it immediately after a login trigger, which is the safe ordering).
- **`MaxLength` is a capacity, not a validation rule**: an over-typed value is truncated by ImGui; nothing here validates length or content. `OtpEntryPage.cs.md` §validation is the caller-side check that layers on top.
- **Label collision:** `ImGui.Text(Label)` makes the label an ImGui id. Two `Input`s sharing a `Label` string in one window share that item's identity, and the label containing `#` characters alters id splitting. The login inputs use distinct `Strings.UsernameInput`/`Strings.PasswordInput`; the settings entries use entry names — keep them unique per window.
- **No trimming, no case handling, no clipboard handling** here — e.g. `MainPage/LoginFrame.cs.md` §Draw applies `.Trim()`/`.ToLower()` to `Username` after reading it. Do not add such transforms to `Value`'s getter: they break the "text as typed" contract ImGui republishes into `inputBacking`.
- `isSteamDeckInputActive` is reset **only** by the dismiss callback; if Steam shows the overlay and never dismisses it, the input stays disabled forever until the containing page resets — there is no timeout or explicit cancel in this class (`OtpEntryPage.cs.md` §cancel button and `LoadingPage.cs.md` §cancel show how pages handle their own cancels around this).
- `TakeKeyboardFocus`, `HasSteamDeckInput`, `IsEnabled`, `SteamDeckPrompt` are `get`/`set` and are the intended per-frame knobs; `Label`, `Hint`, `MaxLength`, `Flags`, `Spacing` are construction-time only. Neither login instance sets `TakeKeyboardFocus` or `IsEnabled`, so both are currently default-valued plumbing (grep: no writer) — unlike `Common/Button.cs.md`, whose `IsEnabled` is additionally unread inside its own draw, whereas `Checkbox` and `Input` both honour their own copy (see `Common/Checkbox.cs.md`, this file §5 of the draw sequence).
- Nothing here pops/pushes around `ImGui.Dummy(Spacing)`: spacing after the box is the component's contribution to layout, and `Vector2.Zero` default means "no gap" — pages add their own `ImGui.NewLine()`/`ImGui.Dummy` (see `MainPage/LoginFrame.cs.md`, `SettingsPage/SettingsEntry{T}.cs.md`).

## 8. Referenced / referenced-by

Constructed by: `MainPage/LoginFrame.cs.md` (username/password), `SettingsPage/SettingsEntry{T}.cs.md` (one per settings entry), `OtpEntryPage.cs.md` (OTP box).
Uses: `Component`/`Component.Id` (`Component.cs.md`), `ImGuiColors.cs.md`, `Program.cs.md` (`Program.Steam`, `Program.IsSteamDeckHardware`), Hexa.NET ImGui input bindings.
Related: `Common/Button.cs.md`, `Common/Checkbox.cs.md` (the sibling controls: shared `IsEnabled` name, differing honouring), `ImGuiHelpers.cs.md`.
