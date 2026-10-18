# LoginAction.cs — Specification

**File:** `src/XIVLauncher.Core/Components/MainPage/LoginAction.cs` · 12 lines · 187 bytes
**Namespace:** `XIVLauncher.Core.Components.MainPage` · **Declaration:** `public enum LoginAction` (7 members)
**Role:** The vocabulary of "what the user asked for": one enum shared between the login UI and the login/patch/launch pipeline

---

## 1. Purpose

`LoginAction` is the smallest type in the package and the hinge of its UI↔logic seam: the UI (`MainPage/LoginFrame.cs.md`'s right-click popup and its `OnLogin` event) names an action, and the pipeline (`MainPage.cs.md`) interprets it. There is no class, no methods, no `using` directives — the file is the namespace line plus the enum.

The enum is **unscoped in its own file** (`public enum LoginAction` in `XIVLauncher.Core.Components.MainPage`), so anything in that namespace or importing it can name it; `MainPage/LoginFrame.cs.md` uses the bare name because that file shares the namespace, while `MainPage.cs.md` does the same.

## 2. Surface — member, producer, consumer

| Member | UI producer (`LoginFrame.cs`) | Meaning / consumer (`MainPage.cs`) |
| --- | --- | --- |
| `Game` | `TriggerLogin` via `OnLogin` (`LoginFrame.cs` ctor: `loginInput.Enter`, `passwordInput.Enter`, `loginButton.Click`) | default path: login → patches → launch |
| `GameNoDalamud` | menu item `Strings.LaunchWithoutDalamudButton` | → `StartGameAndAddon(..., forceNoDalamud: true)` at `MainPage.cs:368`; skips the Dalamud install wait (`MainPage.cs.md` §`DalamudEnabled`/`forceNoDalamud` branch) |
| `GameNoPlugins` | `Strings.LaunchWithoutPluginsButton` | → `noPlugins` argument to `DalamudLauncher` (`MainPage.cs.md` §`StartGameAndAddon` construction) |
| `GameNoThirdparty` | `Strings.LaunchWithNoCustomPluginsButton` | → `noThird` argument to the same ctor |
| `GameNoLaunch` | `Strings.PatchWithoutLaunchingButton` | patches, then `App.ShowMessageBlocking(Strings.UpdateCheckFinished, "XIVLauncher")` and `return false` (`MainPage.cs:331`) — login/patch **without** launch |
| `Repair` | `Strings.RepairGameFilesButton` | `MainPage.cs:126` refuses it while the game is open; `TryLoginToGame` (`:247`) logs in with the "repair" shape; `TryProcessLoginResult` (`:292`) calls `RepairGame(loginResult)` |
| `Fake` | `ImGui.MenuItem("Fake Login")`, shown only under `if (LauncherApp.IsDebug)` (`MainPage/LoginFrame.cs.md` §Draw's `LauncherApp.IsDebug` block) | test path: `MainPage.cs:176` returns `false` right after a dummy `LaunchGame` — never a shipping path |

Two facts worth preserving: the label strings are `Strings.*` keys (see `src/XIVLauncher.Core/Resources/Localization/Strings.Designer.cs`) except `"Fake Login"`, which is a **literal** because the debug menu item is not localized; and `GameNoLaunch` is the only action whose success signal is deliberately `false` (the launcher is expected to come back to the Main page), which is why `MainPage.cs.md` §`Reactivate` treats `Login() != true` as "reactivate", not as "error".

## 3. Invariants when editing this enum

- **The UI must produce every member.** `MainPage/LoginFrame.cs.md`'s popup is the single place that enumerates the variants; a member with no producer is unreachable from the UI (auto-login only ever produces `Game`, `MainPage.cs:71` `ProcessLogin(LoginAction.Game)` via `DoAutoLoginIfApplicable`).
- **The pipeline must interpret every member.** `MainPage.cs` tests `action` with `==`, never `switch`, and never exhaustively: an unhandled member silently behaves as `Game`. That is why `Fake` is debug-gated in the *UI* rather than filtered in the pipeline (`MainPage.cs:176`'s `if (action == LoginAction.Fake)` is live in release builds and reachable only if something hands it the value). Do not add a member without auditing both sides of this table.
- **Ordinal values are not persisted.** The enum is never written to config (grep: no `LoginAction` in `src/XIVLauncher.Core/Configuration/`), so inserting a member is safe for saved state — but the values *are* part of `MainPage.cs`'s local comparisons and the `LoginFrame.cs` menu order, both of which are source-level only.
- **`sealed` is not used here**, so the enum is extensible by convention only; there is no underlying-type declaration and no explicit ordinals, so `Fake` currently has value `6`. Nothing depends on that number.

## 4. Referenced / referenced-by

Produced by: `MainPage/LoginFrame.cs.md` (`event Action<LoginAction>? OnLogin` — the event's payload type is this enum), `MainPage.cs.md` (`ProcessLogin(LoginAction)` at `:114`, `Login(..., LoginAction action)` at `:174`).
Consumed by: `MainPage.cs.md` (`:126`, `:247`, `:292`, `:331`, `:368`), `MainPage/LoginFrame.cs.md` (popup menu items), `MainPage/AccountSwitcher.cs.md` indirectly (switching accounts re-copies the login frame's fields, not the action).
Related: `LauncherApp.cs.md` §loading/page transitions (each action produces a different loading-page sequence), `src/XIVLauncher.Core/Resources/Localization/Strings.Designer.cs` for the menu labels.
