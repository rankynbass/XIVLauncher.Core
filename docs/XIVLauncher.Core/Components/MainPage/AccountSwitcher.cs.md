# AccountSwitcher.cs — Specification

**File:** `src/XIVLauncher.Core/Components/MainPage/AccountSwitcher.cs` · 57 lines · 1,763 bytes
**Namespace:** `XIVLauncher.Core.Components.MainPage` · **Declaration:** `public class AccountSwitcher : Component`
**Role:** The saved-account picker, opened from the friends button in the login frame

---

## 1. Purpose

Replaces the immediate-mode popup: the ImGui version registered a popup id, drew it when open, and re-opened it when `Open()` set a flag. Eto builds a `ContextMenu` **on demand** inside `Open()` and shows it anchored to a control, so there is no popup-id machinery, no per-frame draw, and no state to reset.

Usings: `Eto.Forms`, `XIVLauncher.Core.Accounts`, `XIVLauncher.Core.Resources.Localization`.

## 2. Surface

| Member | Kind | Notes |
| --- | --- | --- |
| `manager` | `private readonly AccountManager` (line 18) | the account store, passed in |
| `AccountSwitcher(AccountManager manager)` | ctor (lines 20–23) | stores the manager; builds no UI |
| `AccountChanged` | `public event EventHandler<XivAccount>?` (line 26) | raised with `this` as sender and the picked account |
| `Open(Control anchor)` | `public` (lines 29–56) | builds the menu and shows it at `anchor` |

The class has no child controls, no `Content`, and no `OnShow()` override — it is a menu builder that inherits `Component` (`Components/Component.cs.md`) purely for API-shape continuity.

## 3. `Open(Control anchor)`

1. `new ContextMenu()` (line 31) — fresh menu on every call.
2. If `manager.Accounts.Count == 0`: add a disabled `ButtonMenuItem { Text = Strings.NoSavedAccounts }` (lines 33–34).
3. For each `account in manager.Accounts` (lines 36–53):
   - `name = account.UserName` (line 38)
   - append `" (Steam)"` if `account.UseSteamServiceAccount` (lines 40–41)
   - append `" (OTP)"` if `account.UseOtp` (lines 43–44)
   - append `" (Trial)"` if `account.IsFreeTrial` (lines 46–47)
   - `new ButtonMenuItem { Text = name }` (line 49)
   - `item.Click += (_, _) => this.AccountChanged?.Invoke(this, account)` (line 50)
   - `menu.Items.Add(item)` (line 52)
4. `menu.Show(anchor)` (line 55) — GTK shows the menu at the anchor control.

`ButtonMenuItem` is the concrete Eto menu item type (`MenuItem` is abstract, so the launcher constructs `ButtonMenuItem`; `MainMenu.cs.md` uses the same type).

`Strings.NoSavedAccounts` is the only localized string in this file; account names and the suffixes (`" (Steam)"`, `" (OTP)"`, `" (Trial)"`) are literal.

## 4. Consumers

| Consumer | Site | Use |
| --- | --- | --- |
| `MainPage` ctor | `MainPage.cs:55` | `new AccountSwitcher(app.Accounts)` — `app.Accounts` is `LauncherApp.Accounts` (`LauncherApp.cs.md` §5) |
| `MainPage` ctor | `MainPage.cs:56` | `AccountSwitcher.AccountChanged += this.AccountSwitcherOnAccountChanged` |
| `MainPage.AccountSwitcherOnAccountChanged` | `MainPage.cs:135-138` | `SwitchAccount(e, true)` |
| `MainPage.SwitchAccount` | `MainPage.cs:118-132` | writes `loginFrame.Username/IsOtp/IsFreeTrial/IsSteam/IsAutoLogin`, sets `Password` when `account.SavePassword`, and sets `App.Accounts.CurrentAccount` when `saveAsCurrent` |
| `LoginFrame` friends button | `LoginFrame.cs:117` | `this.mainPage.AccountSwitcher.Open(this.loginButton)` |

`MainPage.AccountSwitcher` is a public property (`MainPage.cs:45`), so `LoginFrame` reaches it through `mainPage`.

**Menu content is live:** each `Open()` reads `manager.Accounts` at that moment, so accounts added/removed since construction are reflected on the next open (`AccountManager.cs.md`).

## 5. Thread / UI rules

`Open()` builds GTK menu items and calls `menu.Show(anchor)`, so it must run on the UI thread. Its only caller is the friends button click handler (`LoginFrame.cs:117`), which is UI-thread. `AccountChanged` is raised from a menu item click (UI thread); `MainPage`'s handler writes `LoginFrame` properties, which are UI-thread widget writes (`Components/Common/Checkbox.cs.md` §5).

`AccountManager.Accounts` is read here; `AccountManager` is also mutated by the login pipeline (`MainPage.PersistAccount`), so menu construction and account persistence are UI-thread vs background-thread reads of the same list. `MainMenu.cs.md` documents the same kind of read (`MainMenu` snapshots accounts when the menu opens).

## 6. Quirks

- **`Component` is used only as a base**: no `Content`, no `OnShow()`, `Component.Id` is present and unused, and `Component.Enabled` has nothing to disable (the menu items' `Enabled` is set only for the no-accounts placeholder, line 34).
- The menu is not cached: building it per open means account names are rebuilt each time, and the lambdas capture `account`, so each menu item keeps its account alive until the menu is discarded.
- `AccountChanged` uses `EventHandler<T>` (sender + account) while most launcher events in this rewrite are `Action`-style; the sender argument is `this` and is ignored by the consumer (`MainPage.cs:135`).
- No profile pictures: HEAD's `AccountSwitcherEntry` loaded `profilePictures/<id>.png` thumbnails via `TextureWrap` (`docs` for `Accounts/AccountSwitcherEntry.cs.md` deleted), so menu items are text-only, and the trailing ` (Steam)/(OTP)/(Trial)` suffixes replace HEAD's icon markers.
- Menu items have no keyboard shortcut, no icons, and no radio/check state to mark the current account; HEAD's popup had the same limitation.
- `Open(Control anchor)` requires a live control; `LoginFrame` passes `this.loginButton`, so the menu positions at the login button.

## 7. Referenced / referenced-by

Uses: `Component` (`Components/Component.cs.md`), `AccountManager` (`Accounts/AccountManager.cs.md`), `XivAccount` (`Accounts/XivAccount.cs.md`), `Strings`, `Eto.Forms.ContextMenu`/`ButtonMenuItem`/`Control`.
Referenced by: `MainPage.cs` (lines 45, 55, 56, 135), `LoginFrame.cs` (line 117).
