# SettingsTabTroubleshooting.cs — Specification

**File:** `src/XIVLauncher.Core/Components/SettingsPage/Tabs/SettingsTabTroubleshooting.cs` · 102 lines · 4536 bytes
**Namespace:** `XIVLauncher.Core.Components.SettingsPage.Tabs` · **Declaration:** `public class SettingsTabTroubleshooting : SettingsTab`
**Role:** Five Unix env-hack rows plus the tab's biggest `Draw()`: a log-pack lever and six immediately-destructive cleanup buttons

---

## 1. Purpose

The tab is two halves with opposite risk profiles. The top half is data — five persisted environment-hack switches (`Program.Config.FixLDP`/`FixIM`/`FixError127`/`FixLocale`/`DontUseSystemTz`) whose whole point is to change how the game process is launched (`lib/FFXIVQuickLauncher/src/XIVLauncher.Common.Unix/Compatibility/Wine.cs` `WineSettings` consumes them; see `docs/XIVLauncher.Core/Program.cs.md` §`CreateCompatToolsInstance` argument list for the wiring). The bottom half is actions that take effect the moment they're clicked, including `ClearAll` which deletes the launcher's own config and re-enters the Settings state.

## 2. Structure

```csharp
public override SettingsEntry[] Entries { get; } = { … };   // :11-:42  (accessor WITH initialiser, not ctor-assigned)
public override string Title => Strings.TroubleshootingTitle; // :43
public override void Draw()                                   // :45-:101
```

The `{ get; } = { … }` form (`SettingsTab.cs.md` §Entries: "field initializer in most tabs, assigned in a ctor in two") is the third shape: an accessor with an initialiser. It has no sibling-row cross-references, which is why it doesn't need the ctor form that `Tabs/SettingsTabDalamud.cs.md` §2 and `Tabs/SettingsTabWine.cs.md` §2 use. No `IsUnixExclusive` — instead, **every row here has its own `CheckVisibility = () => Environment.OSVersion.Platform == PlatformID.Unix`** (see §3), i.e. this tab is visible on Windows as an almost-empty section plus its action list. That's deliberate: the Logs/Cleanup half is cross-platform, the Fixes half is not, and the tab-level gate would hide both.

## 3. The five hack rows

| Row (label key) | Anchor | `T` | `Program.Config.*` (`??` default) | Unix gate |
| --- | --- | --- | --- | --- |
| `Strings.DisableGameoverlayRendererHack` | `:13-:16` | `bool` | `FixLDP ?? false` | `:15` |
| `Strings.XModifiersHack` | `:17-:20` | `bool` | `FixIM ?? false` | `:19` |
| `Strings.FixLibICUUCErrorHack` | `:21-:24` | `bool` | `FixError127 ?? false` | `:23` |
| `Strings.ForceLocaleHack` + `Program.CType` | `:25-:37` | `bool` | `FixLocale ?? false` | `:29`; `CheckWarning` `:30-:36` |
| `Strings.ForceDontUseSystemTZ` | `:38-:41` | `bool` | `DontUseSystemTz ?? true` | `:40` |

**Four of the five gates are the identical lambda**; the fifth row's gate is at `:29`. All five use the `PlatformID.Unix` spelling (`SettingsTab.cs.md` §`IsUnixExclusive` records the two spellings and which one `SettingsPage.cs.md` §Draw tests; `Tabs/SettingsTabWine.cs.md` §invariants contrasts `IsOSPlatform(Linux)`). The launcher-side reads of the config fields are `docs/XIVLauncher.Core/Program.cs.md` §`CreateCompatToolsInstance` — each field appears as a named argument, so a renamed or removed row must be tracked there too.

### The ForceLocale row (`:25-:37`) — the one row whose identity is dynamic

- **Name is `string.Format(Strings.ForceLocaleHack, Program.CType)`** (`:25`), and the *description* is a ternary over the same emptiness test (`:26`): when `Program.CType` is empty the description becomes the English literal `"Hack Disabled. Could not find a UTF-8 C locale. You may have to set LC_ALL manually if LANG is not a UTF-8 type."`. That literal is inline in this file — it's the only non-localized text on the Settings page besides the two `TextDisabled` headers (§4). If a translation is ever wanted, that string moves to `src/XIVLauncher.Core/Resources/Localization/Strings.Designer.cs` (the `.resx`/`Designer.cs` pair, see `SettingsTab.cs.md` §2.1) — and the reason it's not now is that the format argument is a runtime locale value that no `.resx` key can carry.
- **`Program.CType`** (see `docs/XIVLauncher.Core/Program.cs.md` §`CType`) is the discovered UTF-8 C locale; it is empty when none was found, so the row's *label* changes between "Force locale to en_US.UTF-8"-style text and `ForceLocaleHack` formatted with an empty argument (`string.Format` leaves the `{}` placeholder empty rather than failing). Keep the empty test in both places (`:25` argument and `:26` ternary) in sync — they read the same source twice, unlike the `Tabs/SettingsTabWine.cs.md` §dependency-chain rows where a second reference would be a closure over a sibling row.
- **`CheckWarning`** (`:30-:36`): reads `CoreEnvironmentSettings.GetCleanEnvironmentVariable("LANG")` (`:32`), and warns when `lang.ToUpper().Contains("UTF")` **and** the row is on (`:33`). Message is `string.Format(Strings.ForceLocaleHackUTFValidation, lang)` (`:34`). Semantics: turning the hack on when `LANG` already names a UTF-8 locale is redundant/possibly harmful, so it's surfaced as a *warning* — which does **not** gate the finish button (`SettingsEntry{T}.cs.md` §CheckWarning bullet: warnings exist only in `Draw()`, the button tests `IsValid`). Consequence for editors: if this condition should block saving, it must move to `CheckValidity`; keep the current split if the intent is "inform, don't forbid".
- The row is *not* named by a `Strings` key alone, so the `SettingsPage.cs.md` §search mode string-match hits the formatted label (which embeds the locale value) — searching "locale" matches the format's literal text, not the value. That is current state; anything changing the label scheme should keep "a search for the user-visible label finds the row" as the invariant.

## 4. `Draw()` — the layout, and what each line means

```csharp
ImGui.TextDisabled("Fixes"); ImGui.Spacing();               // :47-:48   header for the entries block
base.Draw();                                                 // :49       (AFTER the header — SettingsTab.cs.md §Draw)
ImGui.Separator(); ImGui.TextDisabled("Logs"); ImGui.Spacing();   // :51-:53
if (Button(Strings.GenerateTSPackTroubleshootingButton))     // :54
{ PackGenerator.SavePack(Program.storage);                    // :56
  PlatformHelpers.OpenBrowser(Program.storage.GetFolder("logs").FullName); }   // :57
ImGui.TextColored(ImGuiColors.DalamudGrey, Strings.GenerateTSPackTroubleshooting);   // :59
ImGui.Separator(); ImGui.TextDisabled("Cleanup");            // :61-:62
ImGui.TextColored(ImGuiColors.DalamudRed, Strings.TroubleshootingDestructiveActionWarning);   // :63
ImGui.Spacing();                                             // :64
… six button+explanation pairs, :66-:100                      // see §5
```

- **`base.Draw()` after a `TextDisabled("Fixes")` header** — the entries render *under their section title*, unlike `Tabs/SettingsTabWine.cs.md`/`Tabs/SettingsTabGame.cs.md` where `base.Draw()` is the first statement. The ordering rule that applies to this tab: section text before the rows it labels, actions in their own labelled sections after (`SettingsTab.cs.md` §Draw records the `base.Draw()`-first convention the other tabs pick; this tab is an exception because its `Draw()` adds two labelled action groups whose headers must come first).
- **The grey `TextColored(DalamudGrey, …)` line under each button** is the explanation of what was just done, drawn *after* the action ran (`SettingsEntry{T}.cs.md` §Draw bullet's red-then-grey validity layout is the analogous pair for rows). Because it's outside the `if`, it prints whether or not the button fired — i.e. it documents the button, it does not report the action's result. Keep the pairing (button first, description after) if adding a lever; and note that a section with no action (`Logs`) would need both its header and its button.
- **`TextDisabled` and `TextColored` are the section-label idiom** here: `("Fixes")` `:47`, `("Logs")` `:52`, `("Cleanup")` `:62` are English literals (they never go through `Strings`), while all button labels and explanations are `Strings.*` keys. That mix is the current state of the localization surface on this tab — recorded with `SettingsTab.cs.md` §2.1 (`Strings.X` must exist in `Designer.cs`/`.resx`).
- **`ImGuiColors.DalamudRed` at `:63` is the single danger marker** for the whole Cleanup group (`docs/XIVLauncher.Core/ImGuiColors.cs.md` §`DalamudRed`/`§DalamudGrey`). It's a *warning line*, not a per-button flag: the destructive buttons below inherit the warning from context. Adding a new lever to that group means adding it *below* `:63`, and anything that must read as safe belongs above `:61` (the `Separator`).

## 5. The pack lever and the six cleanup levers, and their Program-side effects

All are immediate (`if (ImGui.Button(…)) { … }`), each with `true` passed except `ClearPrefix`, which has no such parameter:

| Button (label key) | Anchor | Call | Effect (`docs/XIVLauncher.Core/Program.cs.md`) |
| --- | --- | --- | --- |
| `GenerateTSPackTroubleshootingButton` | `:54-:58` | `PackGenerator.SavePack(Program.storage)` then `PlatformHelpers.OpenBrowser(storage.GetFolder("logs"))` | writes the support zip (`docs/XIVLauncher.Core/PackGenerator.cs.md` §`SavePack`) then opens the logs folder |
| `ClearWINEPrefixTroubleshootingButton` | `:66-:69` | `Program.ClearPrefix()` (`:68`) | deletes `wineprefix` recursively, re-creates the empty folder (`Program.cs:430-:433`) — **no `tsbutton` parameter exists** |
| `ClearManagedCompatToolsTroubleshootingButton` | `:72-:76` | `Program.ClearTools(true)` (`:74`) | deletes `compatibilitytool`, re-creates `wine`/`dxvk` subfolders, **rebuilds `Program.CompatibilityTools`** (`Program.cs:456-:462`, `CreateCompatToolsInstance()`) |
| `ClearDalamudTroubleshootingButton` | `:78-:82` | `Program.ClearPlugins(true)` (`:80`) | deletes `dalamud`/`dalamudAssets`/`installedPlugins`/`runtime` + `dalamudUI.ini`/`dalamudConfig.json`, then **resets `DalamudLoadInfo`, re-creates and runs `DalamudUpdater`** (`Program.cs:436-:455`) |
| `ClearAllLogsTroubleshootingButton` | `:84-:88` | `Program.ClearLogs(true)` (`:86`) | deletes `logs` folder + the four Dalamud log files, then `SetupLogging(mainArgs)` re-opens the log handles (`Program.cs:464-:474`) |
| `ResetSettingsTroubleshootingButton` | `:90-:94` | `Program.ClearSettings(true)` (`:92`) | deletes `launcher.ini`, then `LoadConfig(storage)` re-reads it (absent ⇒ defaults) and **sets `launcherApp.State = LauncherApp.LauncherState.Settings`** (`Program.cs:420-:429`) |
| `ResetAllTroubleshootingButton` | `:96-:100` | `Program.ClearAll(true)` (`:98`) | runs all five in order: `ClearSettings(true)`, `ClearPrefix()`, `ClearPlugins(true)`, `ClearTools(true)`, `ClearLogs(true)` (`Program.cs:475-:482`) |

Ordering and state consequences:

- **Every lever except `ClearPrefix` is `Clear…(true)`**, which is what selects the "re-initialise after wiping" branch in each Program method above. `ClearPrefix()` has no such branch (nothing prefix-shaped needs rebuilding) — so the `true` argument is not uniform plumbing by accident: adding it to a method that doesn't take it is an edit to `Program.cs.md`, not to this file.
- **The same five names are also env-var levers** — `CoreEnvironmentSettings.cs.md` §`ClearTools`/etc. (`:13+`) gate the identical calls from `docs/XIVLauncher.Core/Program.cs.md:186-196` during `Program` init, before the UI exists. The UI buttons are the post-startup equivalent; keeping both routes in sync is the contract (the `bool tsbutton` default `false` on the Program methods exists for the env-var route).
- **`ResetSettings` mutates live app state mid-frame**: `LoadConfig(storage)` replaces `Config`'s values from a now-deleted ini, so every row on the *other* tabs now reads defaults (`SettingsEntry{T}.cs.md` §config bridge bullet: lambdas re-read `Program.Config` per call) while their `InternalValue` boxes still hold the user's earlier choices — and `Program.CreateCompatToolsInstance()` is **not** re-run by `ClearSettings`, only by `ClearTools`/`ClearAll` (§table, `Program.cs:456-:462`/`Program.cs:263` init). Consequence: a user who clicks `ResetSettings` and *not* `ClearTools` has a `CompatibilityTools` instance built from the pre-wipe config. If that asymmetry is ever "fixed", the place to do it is the Program method (single re-creation helper), not by adding `CreateCompatToolsInstance()` into this `Draw()` block.
- **`ResetAll` also deletes the ini, so the Settings page's persisted rows are gone for the *next* session** (`SettingsEntry{T}.cs.md` §`Load()` = "read whatever the user chose in a previous session") while the current frame's checkbox states stay as they were — i.e. this button is "wipe disk state", not "wipe UI state". `launcherApp.State = LauncherApp.LauncherState.Settings` inside it (`Program.cs:420-:429`) is the self-targeting part: the app is *already* in Settings, so re-entering Settings is a no-op visually — but for a `ClearAll` reached from the main page (§finish button is on the Settings page only, `SettingsPage.cs.md` §finish button) it re-asserts the state the buttons' host assumes. `docs/XIVLauncher.Core/LauncherApp.cs.md` §`LauncherState` is that state machine; `MainPage.cs.md` §13 §Hide/Reactivate records how a *different* button pair (`MainPage.cs.md:1137` `Hide()` / `:1142` `Reactivate()`) leaves it consistent.
- **`ClearPlugins(true)` re-runs `DalamudUpdater.Run(Config.DalamudBetaKind, Config.DalamudBetaKey)`** (`Program.cs:452`) — that's why the Dalamud row pair (`Tabs/SettingsTabDalamud.cs.md`) and this button interact: the branch row's persisted `Config.DalamudBetaKind` (`Tabs/SettingsTabDalamud.cs.md` §`DalamudBetaKind` getter) is the argument used, and `docs/XIVLauncher.Core/Program.cs.md:261` is the matching init-time call.

## 6. Invariants and extension points

- **No entry-level action here**: all mutation in `Draw()` is inside `if (ImGui.Button(…))`, i.e. click-gated (`Button.cs.md` §Click vs Hover / the `ImGui.Button` return contract). The row `save` lambdas in §3 only take the finish-button path (`SettingsPage.cs.md` §finish button → `SettingsTab.cs.md` §Save); nothing in this file overrides `Save()` (contrast `Tabs/SettingsTabWine.cs.md` §Save override) — so the five hack rows persist through the standard path and need no tab-level hook.
- **Adding a lever**: put it under the correct `TextDisabled` header, follow the button-then-`TextColored(DalamudGrey, …)` pairing, and use a `Strings.*Button`/`Strings.*` explanation key. Destructive ones go below the red warning at `:63`; anything whose effect is bounded to disk-wiping stays in the `Cleanup` group. The `Separator()` at `:51`/`:61` is the only visible divider between the three groups — keep one divider per group boundary.
- **`PlatformHelpers.OpenBrowser` (not `AppUtil.OpenBrowser`) at `:57`** — same split as `Tabs/SettingsTabAbout.cs.md` §two browser helpers: `AppUtil.cs.md` §`OpenBrowser` adds the Steam-Deck overlay path for **URLs**, and this call passes a **local directory**. Keep that helper choice here even though the tab is not Unix-gated.
- **The rows' `T` is `bool` for all five**, so `SettingsEntry{T}.cs.md` §`bool` rows (the checkbox path) applies, and §null-boxes bullet is why each getter carries `??` — note the defaults here are *not* uniform: four rows default `false` (hack off) and `DontUseSystemTz` defaults `true` (`:38`). A `null` box on that row therefore reads as "hack on" in the UI but as unset in config (`SettingsEntry{T}.cs.md` §null boxes read as default) — the one row where the `default`-collapse is user-visible. Keep the getter's `?? true` if that row is edited.

## 7. Referenced / referenced-by

Instantiates: `SettingsEntry{T}.cs.md` (×5 `bool` rows). Base of `SettingsTab.cs.md` (accessor+initialiser `Entries` — see `SettingsTab.cs.md` §3.1; `Load`/`Save` inherited unchanged, the tab-level-hook contrast being `Tabs/SettingsTabWine.cs.md` §Save).
Iterated by `SettingsPage.cs.md` §Draw (its `Entries` loop with per-row `IsVisible` filters) and §search mode; saved by `SettingsTab.cs.md` §Save via the finish button (`SettingsPage.cs.md` §finish button).
Program-side effects: `docs/XIVLauncher.Core/Program.cs.md` §`ClearSettings`/§`ClearPrefix`/§`ClearPlugins`/§`ClearTools`/§`ClearLogs`/§`ClearAll` (anchors listed in §5 table) + §`storage` + §`CreateCompatToolsInstance` and §`DalamudUpdater` as reached through them; `docs/XIVLauncher.Core/Program.cs.md:186-:196` is the env-var twin of the same levers (`CoreEnvironmentSettings.cs.md` §`ClearAll`/§`ClearSettings`/§`ClearTools`).
Helpers: `docs/XIVLauncher.Core/PackGenerator.cs.md` §`SavePack`, `docs/XIVLauncher.Core/ImGuiColors.cs.md` §`DalamudGrey`/§`DalamudRed`, `lib/FFXIVQuickLauncher/src/XIVLauncher.Common/Util/PlatformHelpers.cs` §`OpenBrowser` (wrapper split documented in `Tabs/SettingsTabAbout.cs.md` §two browser helpers), `CoreEnvironmentSettings.cs.md` §`GetCleanEnvironmentVariable` (the `LANG` read in §3).
