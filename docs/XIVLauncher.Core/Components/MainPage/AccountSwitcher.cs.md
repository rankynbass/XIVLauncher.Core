# AccountSwitcher.cs — Specification

**File:** `src/XIVLauncher.Core/Components/MainPage/AccountSwitcher.cs` · 75 lines · 1798 bytes
**Namespace:** `XIVLauncher.Core.Components.MainPage` · **Declaration:** `public class AccountSwitcher : Component`
**Role:** A context menu of saved accounts (`AccountManager`'s list), whose selection is reported as an event — the state copy happens in `MainPage.cs.md`

---

## 1. Purpose

`AccountSwitcher` renders *no visible content of its own* except a popup window. It exists to (a) turn a click into `AccountChanged`, and (b) offer `Open()` as the imperative "show this menu now" trigger used by `MainPage/LoginFrame.cs.md` (`LoginFrame.cs` Draw's icon-button block: `this.mainPage.AccountSwitcher.Open()`).

Ownership: constructed by `MainPage.cs:44` as `new AccountSwitcher(app.Accounts)` (the `AccountManager` handle — `docs/XIVLauncher.Core/Accounts/`), and its only handler is bound at `MainPage.cs:45`:

```csharp
this.AccountSwitcher.AccountChanged += this.AccountSwitcherOnAccountChanged;   // MainPage.cs:45
private void AccountSwitcherOnAccountChanged(object? sender, XivAccount e) { SwitchAccount(e, true); }   // MainPage.cs:109
```

So the class's contract is "click → `AccountChanged(this, account)`"; the *meaning* of switching (copying name/OTP/free-trial/Steam/auto-login/password into the login frame and persisting `CurrentAccount`) lives entirely in `MainPage.cs.md` §`SwitchAccount` (`MainPage.cs:92-107`, reached from ctor `:56` for the saved account and from `:45`'s handler for a manual pick).

## 2. Surface

| Member | Kind | Notes |
| --- | --- | --- |
| `ACCOUNT_SWITCHER_POPUP_ID` | `private const string = "accountSwitcher"` | popup window id — **no `###` prefix**, §3 |
| `manager` | `private readonly AccountManager` | ctor argument (`app.Accounts`) |
| `doOpen` | `private bool` | one-frame trigger, set by `Open()`, cleared in `Draw()` |
| `AccountChanged` | `public event EventHandler<XivAccount>?` | fired as `AccountChanged?.Invoke(this, account)` |
| `Open()` | `public void` | sets `doOpen = true` |
| `Draw()` | `override` | popup + `OpenPopup` + `base.Draw()` |

Usings: `System.Numerics`, `Hexa.NET.ImGui`, `XIVLauncher.Core.Accounts` (`XivAccount`, `AccountManager`), `XIVLauncher.Core.Resources.Localization` (`Strings`). Base is `Component` (`Component.cs.md`), not `Page` — the popup is not a page, so `Padding`/`OnShow` do not apply.

## 3. The popup id and the anchor-item rule

```csharp
if (ImGui.BeginPopupContextItem(ACCOUNT_SWITCHER_POPUP_ID)) { …ImGui.EndPopup(); }
…
if (doOpen) { doOpen = false; ImGui.OpenPopup(ACCOUNT_SWITCHER_POPUP_ID); }
```

- **`BeginPopupContextItem` uses its argument as *both* the popup id and the anchoring item name**, and the id here is `"accountSwitcher"` — a bare word, not the `###`-prefixed id convention used by `Component.cs.md` §5, `OtpEntryPage.cs.md` (`"###otp"`), `MainPage/LoginFrame.cs.md` (`POPUP_ID_LOGINACTION = "popup_loginaction"`), and the settings entries (`$"###{Id}"`). Consequence: it occupies the same namespace as any *displayed label*, so a label string literally `"accountSwitcher"` elsewhere would collide, and the value is a package-wide constant — a second `AccountSwitcher` instance shares it (`MainPage.cs.md` creates exactly one).
- **The anchor is "the previously added item" in the current window**, i.e. whatever `MainPage.cs.md`'s Draw left last before `AccountSwitcher.Draw()` runs. In `MainPage.cs.md` §Draw the order is `newsFrame.Draw()` → `SameLine` → `loginFrame.Draw()` → `AccountSwitcher.Draw()` → `actionButtons.Draw()`, and LoginFrame's last items are its caret-down and account-friends icon buttons (`LoginFrame.cs.md` §Draw tail) — so the account menu also opens on **right-clicking LoginFrame's friends icon**. Reordering MainPage's `Draw()` children changes that anchor: this is why the class cannot rely on "the item I drew", and why `Open()` (an explicit `ImGui.OpenPopup`, which takes no anchor) exists. Keep both mechanisms working together rather than picking one.
- **`doOpen` is consumed in the same frame it is set, but only if `Draw()` runs after `Open()`** — which is the ordering contract between `LoginFrame.cs.md` (calls `Open()` during its `Draw()`) and `MainPage.cs.md` §Draw (calls `AccountSwitcher.Draw()` after `loginFrame.Draw()`). If MainPage ever drew the switcher before the login frame, `Open()`'s effect would be deferred to the *next* frame instead — visible as a one-frame delay, not a lost menu. The clear-then-open order inside this `Draw()` (clear flag, then call `OpenPopup`) is deliberate: it makes the trigger exactly-one-frame even if `OpenPopup` were to fail.
- **`ImGui.OpenPopup` must come after the popup's creation in the same frame**, which is why the `BeginPopupContextItem` block is *above* the `doOpen` block inside this `Draw()` — moving the `doOpen` block to the top of `Draw()` yields an empty popup. This ordering is a hard ImGui rule; see `MainPage/LoginFrame.cs.md` §popup for the same-shape pattern in that class.

## 4. The menu content — and the button-width trick

```csharp
foreach (XivAccount account in this.manager.Accounts)
{
    var name = account.UserName;
    if (account.UseSteamServiceAccount) name += " (Steam)";
    if (account.UseOtp)                 name += " (OTP)";
    if (account.IsFreeTrial)            name += " (Trial)";

    var textLength = ImGui.CalcTextSize(name).X;
    if (ImGui.Button(name + $"###{account.Id}", new Vector2(textLength + 15, 40)))
        this.AccountChanged?.Invoke(this, account);
}
```

- **The three suffixes are hardcoded English literals** (`" (Steam)"`, `" (OTP)"`, `" (Trial)"`) — not `Strings.*` keys. This is the one place in the MainPage cluster with un-localized UI text; `Strings.cs`-style keys exist for the rest (`Strings.NoSavedAccounts`). Keep that in mind when adding a suffix (and prefer a `Strings` key for anything new).
- **The id suffix rule is the `###` convention**: `name + $"###{account.Id}"` shows `name` while the id is `name + account.Id`, so two saved accounts with the same user name (e.g. same account, one OTP/one not) still get distinct ImGui ids — the `(OTP)`/`(Trial)` suffixes usually differentiate them, but the Guid suffix is what guarantees it. Contrast `Common/Button.cs.md` §"Label as ImGui id", which uses the label *as* the id and therefore collides for repeated labels. `account.Id` is the `XivAccount` Guid (`docs/XIVLauncher.Core/Accounts/`).
- **`ImGui.CalcTextSize(name).X` drives the width** (`textLength + 15` padding, fixed 40 height) so menu items look like a flush list rather than ImGui's default full-row buttons. `name` for the measure and `name` for the label are the same string, so the measure is exact; the `15` is the horizontal padding, and changing the height alone (`40`) does not reflow the text.
- **The empty case** prints `Strings.NoSavedAccounts` (`AccountManager` with zero entries) — the popup is created, opened, and shows that one line; `AccountManager`'s list is read as `manager.Accounts` and `.Count` (`docs/XIVLauncher.Core/Accounts/` §`AccountManager`).
- **Clicking a row invokes the event for *that* row only.** There is no "confirm" or "are you sure" step, and no way to dismiss a row: closing the popup is the cancel path.

## 5. Style scoping

`PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(5))` wraps the whole popup body and is popped **before** the `doOpen` block (and before `base.Draw()`), so the popup window gets a 5 px padding while the rest of MainPage keeps its own padding (`MainPage.cs.md` §Draw pushes `WindowPadding 32`). Two consequences worth keeping: the `5` is a *window* padding, not the row height (rows are the `40` above), and pushing it unconditionally — even when the popup is closed — is harmless because `BeginPopupContextItem` returned false and nothing was added under it.

## 6. Invariants and extension points

- **State is exactly one bit** (`doOpen`), so the class is safe to draw every frame; the account list itself is `manager.Accounts`, i.e. live data owned by `AccountManager` (`docs/XIVLauncher.Core/Accounts/`) — this class never copies or caches accounts. Any new state (e.g. "the row the user last chose") must be reset in the same one-frame idiom as `doOpen` (`if (this.someFlag) { this.someFlag = false; … }`), or it will persist across frames and across page changes.
- **`AccountChanged` carries the full `XivAccount`** and is invoked with `(this, account)` — the classic `EventHandler.Invoke(sender, payload)` shape (`event EventHandler<XivAccount>?`), unlike the `event Action?` fields used by `Common/Button.cs.md`, `Common/Checkbox.cs.md`, `MainPage/ActionButtons.cs.md`, and `MainPage/LoginFrame.cs.md` (`Action<LoginAction>?`). `MainPage.cs:45` composes it with `+=` from outside the class, and the handler signature must stay `(object? sender, XivAccount e)` (see `MainPage.cs.md` §`AccountSwitcherOnAccountChanged` at `:109`) — a payload-shape change touches both files together.
- **The switch is applied by the *owner*, not here.** `SwitchAccount(…, saveAsCurrent: true)` (`MainPage.cs:92`) also sets `App.Accounts.CurrentAccount`, which `MainPage.cs:54` reads back in the ctor (`savedAccount = App.Accounts.CurrentAccount`) — so the "which account am I on" record is `AccountManager.CurrentAccount`, not a field here. Do not add an "active account" field to this class: it would duplicate that record and desync on the ctor path (`:56` passes `saveAsCurrent = false`).
- **`Open()` is public and idempotent-ish** (it only sets a flag), so any component can request the menu; `MainPage/LoginFrame.cs.md` is the only caller today. A keyboard trigger (e.g. pressing a shortcut) belongs in that caller, not here — the class has no input channel.
- **`base.Draw()` last** (`Component.cs.md` §Draw chain): the class has no children; keep the call so `Enabled`/`Children` plumbing (`Component.cs.md`) still applies if rows ever become components.

## 7. Referenced / referenced-by

Constructed and wired by: `MainPage.cs.md` (`:43` ctor with `app.Accounts`, `:44` `AccountChanged +=`, `:83` handler, `:87` `SwitchAccount`).
Triggered by: `MainPage/LoginFrame.cs.md` (`LoginFrame.cs` Draw's account-friends icon → `Open()`).
Data source: `AccountManager`/`XivAccount` (`docs/XIVLauncher.Core/Accounts/`, `src/XIVLauncher.Core/Accounts/`).
Uses: `Component` (`Component.cs.md`), `Strings` (`src/XIVLauncher.Core/Resources/Localization/Strings.Designer.cs` — `NoSavedAccounts`), `ImGui.CalcTextSize` (Hexa binding), `ImGuiHelpers.cs.md` (not used here — this class does not centre or scale its rows).
Related: `LoginAction.cs.md` (the sibling MainPage vocabulary), `MainPage.cs.md` §`SwitchAccount` (the side effects).
