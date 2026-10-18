# LoginFrame.cs — Specification

**File:** `src/XIVLauncher.Core/Components/MainPage/LoginFrame.cs` · 200 lines · 5932 bytes
**Namespace:** `XIVLauncher.Core.Components.MainPage` · **Declaration:** `public class LoginFrame : Component`
**Role:** The login form: two inputs, four checkboxes, one button, one right-click popup of `LoginAction` variants — and the six public accessors `MainPage.cs.md` reads/writes

---

## 1. Purpose

`LoginFrame` is the UI half of the MainPage seam: it collects *intent* (username, password, OTP/Steam/free-trial flags, auto-login) and emits `OnLogin(LoginAction)`; `MainPage.cs.md` (§`ProcessLogin` at `MainPage.cs:114`) is the consumer. It owns no login logic, no state machine, and no knowledge of the pipeline beyond the enum.

The design point worth keeping: the six public accessors are **storage**, not presentation — `MainPage.cs.md` writes them (`SwitchAccount`, `MainPage.cs:94-98` and `:101`) and reads them (`MainPage.cs:70`, `MainPage.cs:149`), while the *presentation* is whatever the ImGui frame looked like on the previous frame. So the class is simultaneously a component and a plain data record.

## 2. Surface

| Member | Kind | Backed by | Notes |
| --- | --- | --- | --- |
| `Username` | `public string { get; set; }` | `loginInput.Value` | accessor pair, §3 |
| `Password` | same | `passwordInput.Value` | |
| `IsOtp` | `public bool` | `oneTimePasswordCheckbox.Value` | |
| `IsSteam` | same | `useSteamServiceCheckbox.Value` | |
| `IsAutoLogin` | same | `autoLoginCheckbox.Value` | §7 two-truths invariant |
| `IsFreeTrial` | same | `freeTrialCheckbox.Value` | |
| `OnLogin` | `public event Action<LoginAction>?` | — | bound by `MainPage.cs:47` (`this.loginFrame.OnLogin += this.ProcessLogin;`) |
| `POPUP_ID_LOGINACTION` | `private const string = "popup_loginaction"` | — | popup id, §5 |
| `mainPage` | `private readonly MainPage` | ctor arg | back-reference, §3 |
| 7 private readonly children | `Input`×2, `Checkbox`×4, `Button`×1 | ctor | §4 |
| `GetSize()` | `private Vector2` | — | `(-1, vp.Y - 128f)`, §6 |
| `Draw()` | `override` | child `"###loginFrame"` | §6–§8 |

Usings: `System.Numerics`, `Hexa.NET.ImGui`, `XIVLauncher.Core.Accounts.Secrets.Providers` (`DummySecretProvider`, §8), `XIVLauncher.Core.Components.Common` (`Input`, `Checkbox`, `Button`), `XIVLauncher.Core.Resources.Localization` (`Strings`); also resolves `LoginAction` (same namespace, `LoginAction.cs.md`), `LauncherApp.IsDebug`, `Program.Secrets` (`docs/XIVLauncher.Core/Program.cs.md` §`Secrets`), `ImGuiHelpers`/`FontManager`/`FontAwesomeIcon`/`ImGuiColors` (same-package).

## 3. The accessor-over-backing-component idiom

```csharp
public string Username { get => this.loginInput.Value; set => this.loginInput.Value = value; }
```

- **All six are accessor pairs with `get;`-style implicit params and a `set` that writes the backing component's `Value`** (see `Common/Input.cs.md` §value and `Common/Checkbox.cs.md` §value). The backing components are the *only* storage: there is no `string username` field, so the value is exactly "what the user typed last frame" for the inputs, and "last committed checkbox state" for the checkboxes.
- **Writing from code does not simulate the click path.** `MainPage.cs:94-98` (`SwitchAccount`) copies a saved `XivAccount` into these six setters; that path never touches the ImGui boxes' click handlers, so the checkbox visuals and their `OnChange` events follow the component implementations (`Common/Checkbox.cs.md`), not this class.
- **Reading is safe from outside the frame's `Draw()`**: `MainPage.cs:70` (`DoAutoLoginIfApplicable`) reads `Username`/`Password`, and `MainPage.cs:149` reads five of the six inside `ProcessLogin`'s task — both read the *field-backed* value, which persists across frames, so reads are stable regardless of whether `loginFrame.Draw()` ran this frame.
- **`Password` is not stored in a separate field either**, so the typed password lives in `Input`'s `std::string` backing (`Common/Input.cs.md` §value, the `volatile`-adjacent discussion there) — the redaction of the box is done by the ImGui flag (§4), not by this class clearing the value. Do not add "clear password after use" logic here without reading `Common/Input.cs.md` §buffer lifetime.
- **Ownership asymmetry**: `mainPage` is a `private readonly MainPage` back-reference (ctor `new LoginFrame(this)` at `MainPage.cs:39`), which exists solely for `this.mainPage.AccountSwitcher.Open()` (§7). `NewsFrame.cs.md` §3 records the opposite choice (`NewsFrame` takes `LauncherApp`). Consequence: `LoginFrame` cannot reach `App.Settings` directly — the only two places it touches app state are via `mainPage`, and the only app-state write in this file is the secrets warning (a *read* of `Program.Secrets`).

## 4. Construction of the seven children (ctor)

| Component | Ctor line | Shape |
| --- | --- | --- |
| `loginInput` | `new Input(Strings.UsernameInput, Strings.UsernameInputHint, new Vector2(12f, 0f), 128)` | label = id? no — `Common/Input.cs.md` uses `$"###{Id}"`; see §4 note. Max length 128, extra padding `(12, 0)` |
| `passwordInput` | `new Input(Strings.PasswordInput, Strings.PasswordInputHint, new Vector2(12f, 0f), 128, flags: ImGuiInputTextFlags.Password \| ImGuiInputTextFlags.NoUndoRedo)` | `Password` = dots instead of glyphs; `NoUndoRedo` disables the undo/redo keyboard behaviour |
| `oneTimePasswordCheckbox`, `useSteamServiceCheckbox`, `freeTrialCheckbox`, `autoLoginCheckbox` | `new Checkbox(Strings.…)` ×4 | one label each; `Common/Checkbox.cs.md` §label/id |
| `loginButton` | `new Button(Strings.LoginButton)` | `Common/Button.cs.md` (label-as-id, no size) |

- **`TriggerLogin` is a local function declared at the top of the ctor** and attached to three sources:
  `this.loginInput.Enter += TriggerLogin;`, `this.passwordInput.Enter += TriggerLogin;`, `this.loginButton.Click += TriggerLogin;` — so Enter-in-username, Enter-in-password, and clicking the button are *three* independent submissions, all invoking `OnLogin?.Invoke(LoginAction.Game)`. `TriggerLogin` hardcodes `LoginAction.Game`; the other variants come only from the popup MenuItems (§5). Keep the trio: it is the documented affordance "type it and press Enter, or click".
- **`Input`'s `Enter` event** is what `Common/Input.cs.md` §surface documents; `Common/Input.cs.md` also records that only *two* `Input` instances exist in the whole package — these two. So everything that `Input.cs.md` says about Steam-Deck handler binding and the `ref` backing hazard applies here and only here.
- **`flags:` uses a named argument** to pass the ImGui bitmask; the parameter is defaulted otherwise (max length 128 is the 4th positional). Adding a flag to `loginInput` (e.g. disabling paste) must keep the positional order — the binding has several numeric parameters.
- **`Button.IsEnabled` is dead in this file's context** (`Common/Button.cs.md` §dead `IsEnabled`): nothing here disables the login button.
- **The checkboxes' `OnChange(bool)` is not consumed by this class** — the four values are read programmatically by `MainPage.cs.md` instead. Do not add per-checkbox side effects here without checking `Common/Checkbox.cs.md` §`OnChange` (that hook exists for exactly this).

## 5. Popup: three trigger registrations, one id, five/six menu items

```csharp
ImGui.NewLines();                                    // adds no item
ImGui.OpenPopupOnItemClick(POPUP_ID_LOGINACTION, ImGuiPopupFlags.MouseButtonRight);
ImGui.PushStyleColor(ImGuiCol.PopupBg, ImGuiColors.BlueShade1);
if (ImGui.BeginPopupContextItem(POPUP_ID_LOGINACTION)) { …MenuItems…; ImGui.EndPopup(); }
ImGui.PopStyleColor();
```

- **`NewLines()` does not add an item**, so at both registration calls the "current item" is still `loginButton`'s button (`Common/Button.cs.md` §draw). Net effect: right-clicking the login button opens this popup (registered twice — once via `OpenPopupOnItemClick`, once via `BeginPopupContextItem`; the second is redundant-but-harmless because the popup is already open by then). The *visible* affordance is the caret-down icon button (§7) which calls `ImGui.OpenPopup(POPUP_ID_LOGINACTION)` directly. Keep all three working: the invisible right-click affordance is a documented UX feature, and removing `BeginPopupContextItem` while keeping `OpenPopupOnItemClick` changes which trigger works, not how it looks.
- **Popup id is a plain string `"popup_loginaction"`** — the `###`-prefixed convention of `Component.cs.md` §5, `OtpEntryPage.cs.md`, and `MainPage/AccountSwitcher.cs.md` is *not* used here; the popup id namespace is separate from label ids, but a display label elsewhere that literally equals `popup_loginaction` would collide (see `Component.cs.md` §5 for the id survey).
- **Menu order and payload** (`LoginAction.cs.md` §2 table is the consumer side): `Strings.LaunchWithoutDalamudButton` → `GameNoDalamud`; `Strings.LaunchWithoutPluginsButton` → `GameNoPlugins`; `Strings.LaunchWithNoCustomPluginsButton` → `GameNoThirdparty`; `Strings.PatchWithoutLaunchingButton` → `GameNoLaunch`; `Strings.RepairGameFilesButton` → `Repair`; and under `if (LauncherApp.IsDebug)` a `ImGui.MenuItem("Fake Login")` → `Fake`. `ImGui.Separator()` divides the five shipping items; the debug branch emits its own `Separator()` *inside* the `IsDebug` guard, so the separators stay correct in release builds (the trailing separator does not appear). Keep new items in this block, not elsewhere: `LoginAction.cs.md` §"the UI must produce every member" is enforced by this being the only enumerating site.
- **`MenuItem` labels are ids** (no `##` split): two items sharing a localized string would collide in the popup's id space; the six labels are distinct `Strings` keys except `"Fake Login"`, which is a literal (unlocalized on purpose — see `LoginAction.cs.md` §2 last row).
- **`PushStyleColor(ImGuiCol.PopupBg, ImGuiColors.BlueShade1)`** wraps the popup body and is popped after `EndPopup()` — a balanced pair (`Component.cs.md` §push/pop discipline); `ImGuiColors.cs.md` §palette names `BlueShade1`. Because it sits *inside* the child window, the popup window inherits it only for its own background — that is what makes the popup legible over the news frame.
- **The popup contents are evaluated only when the popup opens**, and each item invokes `OnLogin` as soon as it is clicked — i.e. an action fires while the popup is still open. `MainPage.cs.md` §`ProcessLogin` then starts a loading page while this popup window is still live that frame; that ordering is the pipeline's contract (`MainPage.cs.md` §`StartLoading`), not something this class should try to guard.

## 6. Child window geometry

```csharp
if (ImGui.BeginChild("###loginFrame", this.GetSize())) { … }
ImGui.EndChild();
base.Draw();
```

- **`GetSize()` returns `(-1, vp.Y - 128f)`**: `-1` width means "extend to the parent's right edge" — the login frame consumes whatever horizontal space `NewsFrame.cs.md` §7's fractional width left over. The `128f` subtraction must match that file's (`NewsFrame.cs.md` §"the two frames must agree on the 128 px band"); `ImGuiHelpers.cs.md` §viewport size is the source of `vp`.
- **Id is `"###loginFrame"`** (`###` = empty display text + id, `Component.cs.md` §5), and MainPage draws this frame *after* `newsFrame.Draw()` (`MainPage.cs:79` → `:83`) inside MainPage's own `PushStyleVar(WindowPadding, 32)` — so the padding applies to the *parent* window, and this file pushes its own `WindowPadding 32` again inside the child (§7); the child's padding is not inherited from the parent's push.
- **`base.Draw()` runs after `EndChild()`** — same `Component.cs.md` §Draw chain shape as `NewsFrame.cs.md`; this class has no children in `Children`, so the call is a chain-passthrough.

## 7. Draw tail — the secrets warning and the two icon buttons

```csharp
if (Program.Secrets is DummySecretProvider) { PushStyleColor(Text, DalamudRed); TextWrapped(Strings.NoSecretsProviderWarning); PopStyleColor(); Dummy(15); }
PushFont(FontManager.IconFont, 0.0f);
var extraButtonSize = new Vector2(45) * ImGuiHelpers.GlobalScale;
if (ImGui.Button(FontAwesomeIcon.CaretDown.ToIconString(), extraButtonSize)) ImGui.OpenPopup(POPUP_ID_LOGINACTION);
ImGui.SameLine();
if (ImGui.Button(FontAwesomeIcon.UserFriends.ToIconString(), extraButtonSize)) this.mainPage.AccountSwitcher.Open();
PopFont();
```

- **`is DummySecretProvider` is a C# pattern match against a type** (`is Type`, no binding), testing whether `Program.Secrets` (docs/XIVLauncher.Core/Program.cs.md §`Secrets`) is the null-provider — see `Accounts/Secrets/Providers/DummySecretProvider.cs.md` for when that happens (no usable secret provider, so passwords cannot be stored). The warning is drawn **every frame while the condition holds**, and is informational only: no interaction is disabled. Keep it as a draw-time check (the provider can be (re)selected at runtime from the settings page).
- **The red block must stay balanced** (`PushStyleColor`/`PopStyleColor(1)`) and the `Dummy(new Vector2(15))` after it exists to keep the following icon buttons from overlapping the warning text — removing the dummy changes layout only in the warning case.
- **Font scoping**: `PushFont(FontManager.IconFont, 0.0f)` … `PopFont()` around both buttons, with the `0.0f` size meaning "keep current size" (`FontManager.cs.md` §font roles; the same idiom is in `ActionButtons.cs.md` §font scoping). The news frame and the caption text drawn by other components must therefore still see the default face — see `ActionButtons.cs.md` §font scoping for the balance rule.
- **`Vector2(45)` scaled by `ImGuiHelpers.GlobalScale`** (`ImGuiHelpers.cs.md` §scale) — the single-vector form (square buttons). Note `ActionButtons.cs.md` §geometry uses `Vector2(80)` for the same kind of control: the two strips differ in size on purpose (the caret/switch buttons sit inside the login frame's bottom row).
- **`ImGui.SameLine()`** keeps the caret and the switcher buttons on one row; unlike `ActionButtons.cs.md` (three flush buttons), here the two are separated by the row width, so the pair's layout depends on `extraButtonSize` only.
- **Left-clicking the caret opens the login popup** (the visible affordance), **left-clicking the friends/user icon calls `AccountSwitcher.Open()`** → `MainPage/AccountSwitcher.cs.md` §"the popup id and the anchor-item rule" records what that does and why `Open()` exists. `AccountSwitcher.Draw()` runs *after* `loginFrame.Draw()` in `MainPage.cs.md` §Draw (`:83` then the switcher draw), which is the frame-ordering contract `AccountSwitcher.cs.md` §"doOpen is consumed in the same frame…" depends on; changing MainPage's child order reorders these two and delays the account menu by a frame.
- **Neither icon button is `BeginDisabled`-gated** (contrast `ActionButtons.cs.md` §3): the switcher popup shows `Strings.NoSavedAccounts` when empty, so there is nothing to disable here.

## 8. Invariants and extension points

- **`IsAutoLogin` and `App.Settings.IsAutologin` are two truths** synced at two points in opposite directions: `MainPage.cs:98` writes the checkbox *from* settings (`SwitchAccount`), `MainPage.cs:147` writes settings *from* the checkbox (every login). `LoadingPage.cs.md` §disable-auto-login (`LoadingPage.cs:42` `App.Settings.IsAutologin = false;`) sets the settings side only, so the checkbox can still read `true` and `MainPage.cs:147` restores auto-login on the next login. The fix belongs in one of the three sites, not in a new field: either `LoadingPage.cs.md` should clear the checkbox (it has no reference to it) or `MainPage.cs:147` should stop overwriting a deliberate disable. Document whichever you choose in both specs.
- **`MainPage.cs:70`'s auto-login precondition** requires both `Username` and `Password` non-empty — i.e. `DoAutoLoginIfApplicable` silently no-ops when the saved account's password was not kept (`SwitchAccount` at `MainPage.cs:101` only writes `Password` when `account.SavePassword`). Keep the two-condition shape; do not add a "prompt for password" path inside this class.
- **No `Reset()` and no per-frame state** in this class: all state is in the seven children (`Common/Input.cs.md`, `Common/Checkbox.cs.md`, `Common/Button.cs.md` own their state), so a page change (`MainPage.cs.md` §`Reactivate`) leaves typed values intact — that is what makes re-login after a failure possible without re-typing.
- **`OnLogin` is fired with `?.` everywhere** (`OnLogin?.Invoke(…)`), so the class works unsubscribed; `MainPage.cs:47` is the only binder. Any second consumer (e.g. a keyboard shortcut) belongs in that binder or in a new `MenuItem` inside §5's popup block — not in a new method here.
- **Nothing here is localized beyond `Strings`**: the popup labels are `Strings.*` keys (`src/XIVLauncher.Core/Resources/Localization/Strings.Designer.cs`); the only literal UI string is `"Fake Login"` (debug-only) and the warnings are `Strings` keys. Contrast `MainPage/AccountSwitcher.cs.md` §4, which has three hardcoded English suffixes — this file is the clean side of that comparison.

## 9. Referenced / referenced-by

Constructed by: `MainPage.cs:39` (`new LoginFrame(this)`); declared `MainPage.cs:30`; `OnLogin` bound at `MainPage.cs:47`; read/written at `MainPage.cs:70`, `:94-98`, `:101`, `:147`, `:149`; drawn at `MainPage.cs:83`.
Uses: `LoginAction.cs.md` (event payload and menu variants), `Common/Input.cs.md`, `Common/Checkbox.cs.md`, `Common/Button.cs.md`, `MainPage/AccountSwitcher.cs.md` (`Open()`), `MainPage.cs.md` (the pipeline and the accessors' consumers), `docs/XIVLauncher.Core/Program.cs.md` (`Secrets`), `Accounts/Secrets/Providers/DummySecretProvider.cs.md`, `docs/XIVLauncher.Core/FontManager.cs.md`, `docs/XIVLauncher.Core/FontAwesomeIcon.cs.md`, `docs/XIVLauncher.Core/ImGuiHelpers.cs.md`, `docs/XIVLauncher.Core/ImGuiColors.cs.md` (`BlueShade1`, `DalamudRed`), `src/XIVLauncher.Core/Resources/Localization/Strings.Designer.cs`.
Related: `NewsFrame.cs.md` §7 (the `128f` band both frames share), `Component.cs.md` §5 (id conventions), `docs/XIVLauncher.Core/LauncherApp.cs.md` (§page transitions that call `Reactivate`).
