# SettingsTabGame.cs — Specification

**File:** `src/XIVLauncher.Core/Components/SettingsPage/Tabs/SettingsTabGame.cs` · 60 lines · 2967 bytes
**Namespace:** `XIVLauncher.Core.Components.SettingsPage.Tabs` · **Declaration:** `public class SettingsTabGame : SettingsTab`
**Role:** The "Game" tab — game/config directory, extra args, language, DPI, OTP-macros toggle, ignore-Steam toggle, UID-cache toggle plus a reset button

---

## 1. Purpose

The row-heavy, mostly-data tab: eight rows in an `Entries` **field initializer** and a `Draw()` that adds exactly one conditional action. Everything else is inherited from `SettingsTab.cs.md` (§Draw entries loop, §Load, §Save), so this file is the reference example for "how a row is written" (`SettingsEntry{T}.cs.md` §config bridge + §`init` hooks) and for the per-row platform filter style that `SettingsPage.cs.md` §`IsUnixExclusive` deliberately does *not* use.

## 2. Entries

Rows are `new SettingsEntry<T>(label, description, () => Program.Config.X, x => Program.Config.X = x)` (`SettingsEntry{T}.cs.md` §2) with `T` choosing the widget (`SettingsEntry{T}.cs.md` §3: `DirectoryInfo` → text field, `string` → text field, `bool` → checkbox, enum → combo with `SettingsDescriptionAttribute` labels — `Tabs/SettingsTabGame.cs.md` §enum rows).

| Row (label key) | Anchor | `T` | Config field (`Program.Config.*`) | Hooks |
| --- | --- | --- | --- | --- |
| `Strings.GamePathSetting` | `:12-:22` | `DirectoryInfo` | `GamePath` | `CheckValidity` (`:14-:21`) |
| `Strings.GameConfigurationPathSetting` | `:24-:30` | `DirectoryInfo` | `GameConfigPath` | `CheckValidity` (`:26`, ternary), `CheckVisibility` (`:29`) |
| `Strings.AdditionalGameArgsSetting` | `:34` | `string` | `AdditionalArgs` | — |
| `Strings.GameLanguageSetting` | `:35` | `ClientLanguage` (enum) | `ClientLanguage` (`?? ClientLanguage.English`) | — |
| `Strings.GameDPIAwarenessSetting` | `:36` | `DpiAwareness` (enum) | `DpiAwareness` (`?? DpiAwareness.Unaware`) | — |
| `Strings.UseXLAuthMacrosSetting` | `:37` | `bool` | **`IsOtpServer`** | — |
| `Strings.IgnoreSteamSetting` | `:38-:41` | `bool` | `IsIgnoringSteam` (`?? false`) | `CheckVisibility` (`:40`) |
| `Strings.UseUIDCacheSetting` | `:42` | `bool` | `IsUidCacheEnabled` (`?? false`) | — |

`Title` is `:45 Strings.GameTitle`; `Draw()` is `:47-:59` with `base.Draw()` at `:49` (§4).

## 3. The rows' platform and validation logic

- **`GameConfigPath` is Unix-only per row**, not per tab: `CheckVisibility = () => Environment.OSVersion.Platform == PlatformID.Unix` (`:29`) with a `// TODO: We should also support this on Windows` comment (`:28`) marking it as *current state*, not intent. Contrast `Tabs/SettingsTabWine.cs.md` (`IsUnixExclusive => true`, whole tab hidden) — the granularity choice is recorded in `SettingsTab.cs.md` §`IsUnixExclusive`.
- **`IgnoreSteamSetting` is *hidden* under a Steam compat tool**, not "Steam Deck only": `CheckVisibility = () => !CoreEnvironmentSettings.IsSteamCompatTool` (`:40`). Read `CoreEnvironmentSettings.cs.md` §`IsSteamCompatTool` for what that tests (the steam-virtuous / steamcompat launcher flavour): the row is offered when the launcher is *not* running under a compat tool, because under one, Steam is the thing being sidestepped. Keep the polarity — inverting it is what makes "can't launch without Steam" unconfigurable on Deck-class systems.
- **`GamePath` validity is name-shape, not existence**: `string.IsNullOrWhiteSpace(x?.FullName)` → `Strings.GamePathSettingNotSetValidation`; `x.Name is "game" or "boot"` (C# 13 `or` pattern on `DirectoryInfo.Name`, the last path component) → `Strings.GamePathSettingInvalidValidationj`; else `null`. The name test is the guard against pointing at the folder *inside* a prefix layout (`…/Final Fantasy XIV Online/game` or `…/drive_c/game`) instead of the prefix-ish directory the launcher expects — see `docs/XIVLauncher.Core/Configuration/ILauncherConfig.cs.md` for what the launcher derives from `GamePath`, and `MainPage.cs.md` §patch machinery for what it then scans. Nothing here touches the filesystem, so a set-but-missing path is valid at the UI level.
- **The `…InvalidValidationj` key name really ends in a stray `j`** in `src/XIVLauncher.Core/Resources/Localization/Strings.Designer.cs:474` (a typo carried into the `.resx`); the code must keep the identifier that exists, so do not "fix" one side only — a rename is a `Designer.cs` + every `.resx` pair edit (`SettingsTab.cs.md` §2.1).
- **The `bool` row named `UseXLAuthMacrosSetting` writes `Program.Config.IsOtpServer`** (`:37`) — the label and the field disagree in vocabulary, and the field is the one `OtpEntryPage.cs.md` / `MainPage.cs.md` §OTP server (`HttpServer`, port 4646) read for the in-game OTP macro path. The mapping is the row's contract: rename the *label* (in the `.resx`) if the vocabulary must change, keep the field.

## 4. `Draw()` — rows first, then the UID-cache affordance

```csharp
base.Draw();                                              // :49 — SettingsTab entries loop
if (Program.Config.IsUidCacheEnabled == true)              // :51
{
    ImGui.Text(Strings.ResetUIDCacheInfo);                 // :53
    if (ImGui.Button(Strings.ResetUIDCacheButton))          // :55
        Program.ResetUIDCache();                            // :57
}
```

- **`base.Draw()` first** puts the action block *below* the rows, i.e. the affordance reads as "after the setting it acts on" — the position choice `SettingsTab.cs.md` §base.Draw() lists; see that spec for which tabs choose first vs last (`Tabs/SettingsTabWine.cs.md`, `Tabs/SettingsTabTroubleshooting.cs.md`, `Tabs/SettingsTabPatching.cs.md` §no Draw, `Tabs/SettingsTabAbout.cs.md`, `Tabs/SettingsTabDebug.cs.md`).
- **The gate is `== true`, three-state** (`Config.IsUidCacheEnabled` is `bool?`): a never-set row shows the row itself (`?? false` getter ⇒ row reads `false`) but draws no affordance, and `Save()` on the row writes `true` (not "unset"). That distinction is why the button block is gated on the literal-`true` comparison against `Config` rather than on `UseUIDCacheSetting.Value`: config keeps the unset/`false` split, whereas `Value` collapses them (`SettingsEntry{T}.cs.md` §null boxes read as default).
- **`Program.ResetUIDCache()` fires on click, immediately** (`:57`) — see `docs/XIVLauncher.Core/Program.cs.md:484` (`public static void ResetUIDCache(bool tsbutton = false) => launcherApp.UniqueIdCache.Reset();`). It is **not** deferred to the finish-button save path (`SettingsPage.cs.md` §finish button), unlike `Tabs/SettingsTabWine.cs.md` §Save override: the tab's button is an instant action, `Tabs/SettingsTabGame.cs.md` §`UseUIDCacheSetting` is the persisted toggle. The launcher-side lever on the same switch (`MainPage.cs.md:198` calling `Program.ResetUIDCache()` before a login, gated by `IsUidCacheEnabled == false` — see that spec's UID-cache gating note) is what makes clicking the button equivalent to a login-cycle reset — see that spec for the gate details.
- **The row's `CheckVisibility` is absent**, so the row is visible on every platform; the tab is not `IsUnixExclusive`. That is the shape `SettingsTab.cs.md` §`IsUnixExclusive` bullet records for this tab.

## 5. Invariants and extension points

- **Rows are added in the `Entries` field initializer**, hooks via designated initializers (`SettingsEntry{T}.cs.md` §`init` accessors — the restriction is compile-enforced, so a hook can only be set here, never later; a dynamic hook is a closure reading live state, which is exactly what `:29`/`:40` do).
- **The order of rows is the tab's visual order** (`SettingsTab.cs.md` §Draw iterates in `Entries` order), and `SettingsPage.cs.md` §search mode groups matches by tab in the same order.
- **No entry here holds state that the launcher re-reads mid-session** — `SettingsEntry{T}.cs.md` §config bridge bullet (lambdas re-read `Program.Config`) is why `MainPage.cs.md` §`DoAutoLoginIfApplicable` (which consults `Config.ClientLanguage` and friends at login time) sees a fresh value after a Settings visit without any reload. Do not add per-row caching to "make it stick"; the launcher-side reads are the live path.
- **Enums here are library types** (`ClientLanguage`, `DpiAwareness` — see `lib/FFXIVQuickLauncher/src/XIVLauncher.Common/` for their declarations) whose combo labels come from `SettingsDescriptionAttribute` annotations (`SettingsEntry{T}.cs.md` §enum rows): adding an `Enum.WithAnnotations`-style member is an enum-edit, adding a *friendly label* is an annotation edit, both outside this file. Neither of these two enums is annotated today, which is why their combo rows show raw member names (`SettingsEntry{T}.cs.md` §enum rows fallback `x.ToString()`).

## 6. Referenced / referenced-by

Instantiates: `SettingsEntry{T}.cs.md` (×8). Base of `SettingsTab.cs.md`; iterated by `SettingsPage.cs.md` §Draw/§search/§finish button; loaded on show by `SettingsTab.cs.md` §Load.
Config vocabulary: `docs/XIVLauncher.Core/Program.cs.md` §`Config` + `docs/XIVLauncher.Core/Configuration/ILauncherConfig.cs.md`; consumed at launch time by `MainPage.cs.md` §login pipeline (`ClientLanguage`, `AdditionalArgs`, `GamePath`, `IsOtpServer`, `IsUidCacheEnabled`, `IsIgnoringSteam`).
Action: `docs/XIVLauncher.Core/Program.cs.md` §`ResetUIDCache`.
Platform probe: `CoreEnvironmentSettings.cs.md` §`IsSteamCompatTool`.
