# SettingsTabWine.cs — Specification

**File:** `src/XIVLauncher.Core/Components/SettingsPage/Tabs/SettingsTabWine.cs` · 145 lines · 5798 bytes
**Namespace:** `XIVLauncher.Core.Components.SettingsPage.Tabs` · **Declaration:** `public class SettingsTabWine : SettingsTab`
**Role:** The Unix-only Wine/DXVK/GameMode tab — nine rows with sibling-dependent visibility, a memoised native probe, prefix/utility buttons, and the one tab-level `Save()` hook

---

## 1. Purpose

The largest tab, and the most interconnected: rows gate other rows (`startupTypeSetting` gates two, `dxvkVersionSetting` gates one), one row's validity depends on a native library probe, and the tab itself is the only `SettingsTab` that overrides `Save()`. Everything the Unix launch path needs to pick a compatibility tool is exposed here — see `docs/XIVLauncher.Core/Program.cs.md` §`CreateCompatToolsInstance` for the consuming constructor and `lib/FFXIVQuickLauncher/src/XIVLauncher.Common.Unix/Compatibility/{Wine,Dxvk,CompatibilityTools}.cs` for the machinery.

## 2. Structure

```
private SettingsEntry<WineStartupType> startupTypeSetting;   // :15
private SettingsEntry<DxvkVersion>     dxvkVersionSetting;   // :17
public  SettingsTabWine()                                    // :19  Entries = new SettingsEntry[] { … }  :21-:75
public  override SettingsEntry[] Entries { get; }             // :77  (slot declared AFTER the ctor body assigns it)
public  override bool IsUnixExclusive => true;                // :79
public  override string Title => Strings.WineTitle;           // :81
private bool? feralGameModeFound = null;                      // :83
private bool FeralGameModeFound { get }                       // :85-:96
public  override void Draw()                                  // :98-:138
public  override void Save()                                  // :140-:144
```

The `Entries` slot-after-ctor layout is the same shape as `Tabs/SettingsTabDalamud.cs.md` §2 (`public override SettingsEntry[] Entries { get; }` with the assignment in the ctor) — `SettingsTab.cs.md` §Entries calls the two shapes out ("field initializer in most tabs, assigned in a ctor in two"). The two sibling fields are assigned **inside** the array literal (`startupTypeSetting = new …` at `:24`, `dxvkVersionSetting = new …` at `:56`), which is what makes them non-null for the closures below.

## 3. Rows

| Row (label key) | Anchor | `T` | `Program.Config.*` | Hooks |
| --- | --- | --- | --- | --- |
| `WineInstallSetting` | `:24-:25` | `WineStartupType` | `WineStartupType ?? Managed` | — |
| `WineVersionSetting` | `:26-:30` | `WineManagedVersion` | `?? WineManagedVersion.Stable` | `CheckVisibility` (`:29`) |
| `WineBinaryPathSetting` | `:31-:36` | `string` | `WineBinaryPath` (nullable, no fallback) | `CheckVisibility` (`:35`) |
| `WineSyncMethodSetting` | `:37-:51` | `WineSyncType` | `?? WineSyncType.FSync` | `CheckValidity` (`:39-:51`) |
| `WineDebugAdditionalVarSetting` | `:53` | `string` | `WineDebugVars ?? ""` | — |
| `DXVKVersionSetting` | `:56` | `DxvkVersion` | `?? DxvkVersion.Stable` | — |
| `EnableDXVKOverlaySetting` | `:57` | `DxvkHudType` | `DxvkHudType` (**no** `??`) | — |
| `DXVKEnableAsyncSetting` | `:58-:61` | `bool` | `?? true` | `CheckVisibility` (`:60`) |
| `EnableFeralGameModeSetting` | `:64-:73` | `bool` | `?? true` | `CheckVisibility` (`:66`), `CheckValidity` (`:67-:72`) |

Grouping comments in the literal (`// WINE` `:23`, `// DXVK` `:55`, `// GameMode` `:63`) are the tab's own sections — display order is literal order (`SettingsTab.cs.md` §Draw), so those comments mark the boundaries the user sees.

## 4. The dependency chain

- **`startupTypeSetting` gates both alternative Wine rows**: `CheckVisibility = () => startupTypeSetting.Value == WineStartupType.Managed` (`:29`) for the managed-version picker, `== WineStartupType.Custom` (`:35`) for the custom-binary path. Exactly one of the two is visible per startup type (both hidden if a third `WineStartupType` member is added to the library enum — see that spec's base, `lib/FFXIVQuickLauncher/src/XIVLauncher.Common.Unix/Compatibility/Wine.cs` `WineStartupType`).
- **`dxvkVersionSetting` gates the async toggle**: `CheckVisibility = () => dxvkVersionSetting.Value != DxvkVersion.Disabled` (`:60`) — a *negative* predicate, so a new `DxvkVersion` member keeps the async row visible. The overlay row (`:57`) is *not* gated by it, so a disabled-DXVK user still sees the HUD picker (deliberate: the value is harmless-but-unused, not invalid).
- **Order-inside-the-literal is irrelevant to these closures** (they read lazily, per `SettingsEntry{T}.cs.md` §hooks: "a row needing dynamic hooks must implement them as closures reading live state"), but the **fields must be non-null when the closure runs**, which is why the sibling-field assignments are in the ctor (`SettingsTab.cs.md` §ctor-assignment). Do not restructure this into the accessor+initialiser shape of `Tabs/SettingsTabPatching.cs.md` while keeping sibling references: a field initializer referencing another `private` field of the same class reads it *before* assignment (declaration order) — the ctor assignment in `Tabs/SettingsTabDalamud.cs.md` and here exists precisely to avoid that ordering trap.
- **`Value` (not `InternalValue`) is what these gates read** — see `SettingsEntry{T}.cs.md` §Value and its null-box bullet: the enum gates compare a possibly-`default` value, so an unset `Config.WineStartupType` gates as `Managed` (`?? Managed` in the getter at `:24`), matching what `docs/XIVLauncher.Core/Program.cs.md` §`CreateCompatToolsInstance` defaults to. Keep getters and gates consistent if either default changes.

## 5. Per-frame filesystem/native cost

- **`WineSyncMethodSetting`'s `CheckValidity` runs `WineUtility.SystemFsyncSupport()` every frame** (`:39-:51` switch: `UnsupportedPlatform` → `…UnsupportedPlatformValidation`, `OutdatedKernel` → `…MinKernelValidation`, `Supported`/`default` → `null`). It is the one validity hook on the Settings page that does real work per frame (`SettingsEntry{T}.cs.md` §cheap-predicates names this bullet). It also runs on every `Load()` (`SettingsTab.cs.md` §Load) → per page entry. The `default:` fall-through next to `case FsyncSupport.Supported:` (both return `null`) is deliberate: an unknown answer must *not* invalidate the row.
- **`EnableFeralGameModeSetting`'s validity** (`:69`): `if (b == true && FeralGameModeFound == false) return Strings.EnableFeralGameModeNotFoundValidation;` — i.e. it only complains when the user *turned on* GameMode and the library isn't present. Note `b == true` (three-state `bool?` `T?` parameter, `SettingsEntry{T}.cs.md` §`Check*` signature) and `FeralGameModeFound == false`: neither test tolerates "unknown", which is why the accessor below never returns unknown.
- **`FeralGameModeFound` is a memoised accessor** (`:85-:96`, backed by `private bool? feralGameModeFound = null` `:83`): non-Linux ⇒ `false` (and does **not** cache — re-evaluated per call); otherwise first call does `NativeLibrary.TryLoad("libgamemodeauto.so.0", out handle)` (`:92`) and caches the result, later calls return it (`:90`). The cache is why this is the *tab's* pattern rather than the generic rows' (`SettingsEntry{T}.cs.md` §cheap-predicates: "cache in a `private` accessor on the tab"). `NativeLibrary` is `System.Runtime.InteropServices.NativeLibrary` (`:2 using`), the .NET dlopen-style API — the probe is "can the gamemode autoloader be loaded", not "is the gamemode binary installed", so a present-but-broken setup passes.
- **`NativeLibrary.Free(handle)` (`:93`) runs unconditionally**, including when `TryLoad` returned `false` with `handle == 0`. The handle is also deliberately never reused (only freed), so the probe leaks nothing but also never opens the library for use — keep the free-if-you-keep-the-handle pairing if this probe is ever expanded into an actual load.

## 6. `Draw()` — rows, then the compat-tool buttons

```csharp
base.Draw();                                                        // :100  (entries loop first)
if (!Program.CompatibilityTools.IsToolDownloaded)                   // :102
{ ImGui.BeginDisabled(); ImGui.Text(Strings.CompatibilityToolNotSetup); ImGui.Dummy(new Vector2(10)); }
if (Button(Strings.OpenWINEPrefix))            PlatformHelpers.OpenBrowser(Program.CompatibilityTools.Settings.Prefix.FullName);   // :110-:113
ImGui.SameLine();                                                   // :115
if (Button(Strings.OpenWINEConfiguration))     Program.CompatibilityTools.RunInPrefix("winecfg");                              // :117-:120
ImGui.SameLine();                                                   // :122
if (Button(Strings.OpenWINEExplorer))          Program.CompatibilityTools.RunInPrefix("explorer");                             // :124-:127
if (Button(Strings.KillAllWINEProcesses))      Program.CompatibilityTools.Kill();                                              // :129-:132
if (!Program.CompatibilityTools.IsToolDownloaded) ImGui.EndDisabled();                        // :134-:136
```

- **`base.Draw()` first** (`SettingsTab.cs.md` §base) puts buttons *below* the rows — the same choice as `Tabs/SettingsTabGame.cs.md` §Draw, and the opposite of `Tabs/SettingsTabAbout.cs.md`/`Tabs/SettingsTabDebug.cs.md`. Here it matters: the disabled-state block must not precede the rows, or the rows themselves would be greyed.
- **The disabled block is a state wrapper over the four buttons**, opened at `:104` and closed at `:136` under the *same* condition re-read later. ImGui's disabled stack is positional, so the two reads of `Program.CompatibilityTools.IsToolDownloaded` must agree within a frame — they do, since nothing between them downloads a tool (`RunInPrefix`/`Kill` are only reachable when the buttons are *not* disabled). If a future button in this block changed `IsToolDownloaded`, the balance breaks; the invariant to preserve is "nothing inside the wrapper mutates the gate".
- **The three `SameLine()`-adjacent buttons share a row; `KillAllWINEProcesses` does not** (no `SameLine` before `:129`), which is what keeps the destructive action from being a tap-extension of the three window-openers. Keep that separation if a fourth opener is added — and note the contrast with `Tabs/SettingsTabTroubleshooting.cs.md` §Cleanup, where destructive actions are a list separated only by a red warning line.
- **Actions here are immediate** (button click → effect), like `Tabs/SettingsTabGame.cs.md` §UID-cache button, unlike the tab-level `Save()` hook below. `PlatformHelpers.OpenBrowser` (`lib/FFXIVQuickLauncher/src/XIVLauncher.Common/Util/PlatformHelpers.cs`) — *not* `AppUtil.OpenBrowser` (`AppUtil.cs.md` §`OpenBrowser`, the Steam-Deck overlay variant) — is used on a **local directory path** here (`Settings.Prefix.FullName`); that split is the same one recorded in `Tabs/SettingsTabAbout.cs.md` §two browser helpers, and the Deck-aware wrapper is the wrong call for a path.
- **`Program.CompatibilityTools` is dereferenced without a null check** — safe only because (a) `docs/XIVLauncher.Core/Program.cs.md:263 CreateCompatToolsInstance();` runs during `Program` init before `LauncherApp`/`SettingsPage` construction, and (b) `IsUnixExclusive => true` (`:79`) means `SettingsPage.cs.md` §Draw/§search never call this tab's `Draw()` on Windows, which is exactly the case where `CreateCompatToolsInstance` early-returns (`Program.cs:360`). The two facts are coupled: dropping `IsUnixExclusive` would put an NRE in `Draw()` on Windows. `CompatibilityTools.cs.md` (§`IsToolDownloaded`, §`Settings.Prefix`, §`RunInPrefix`, §`Kill`) are the members touched here.

## 7. `Save()` — the only tab-level save hook

```csharp
public override void Save() { base.Save(); Program.CreateCompatToolsInstance(); }   // :141 / :143
```

- **`base.Save()` first, then re-create the tool** (`SettingsTab.cs.md` §Save asymmetry: `Load()` is non-virtual, `Save()` is virtual, so this override is the hook). Effect: pressing the finish button writes every row (`SettingsEntry.cs.md` §Save contract, each row's `save` lambda) and then rebuilds `Program.CompatibilityTools` from the freshly-written `Config.*` values (`docs/XIVLauncher.Core/Program.cs.md` §`CreateCompatToolsInstance`, which reads `WineStartupType`, `WineManagedVersion`, `WineBinaryPath`, `WineDebugVars`, `WineSyncType`, `DxvkVersion`, `DxvkHudType`, `GameModeEnabled`, `DxvkAsyncEnabled`). That is the mechanism by which editing Wine settings takes effect without relaunching — and it is the *only* place in `Components/` where the instance is refreshed after `Program` init, so `MainPage.cs.md` §launch pipeline reads whatever the last Settings exit installed (`docs/XIVLauncher.Core/Program.cs.md` §`CompatibilityTools`).
- **Order matters**: `base.Save()` must precede `CreateCompatToolsInstance()`, because the rebuild reads config that the rows' `Save()` lambdas wrote (`SettingsEntry{T}.cs.md` §Save bullet). Inverting them re-creates the tool from the pre-edit values.
- **This hook runs once per finish-button press**, whereas the buttons in §6 run per click. Anything added to this override inherits "runs on exit, not on click" — the place to add tab-level, config-dependent re-creation; the place for immediate actions is a button in `Draw()`.

## 8. Invariants and extension points

- **`IsUnixExclusive => true` is the tab-level platform gate** (`SettingsTab.cs.md` §`IsUnixExclusive`), while `EnableFeralGameModeSetting`'s `CheckVisibility = () => RuntimeInformation.IsOSPlatform(OSPlatform.Linux)` (`:66`) is a *second, narrower* gate: the tab hides on any non-Unix platform, and inside the tab the GameMode row additionally hides on non-Linux Unix (e.g. FreeBSD/macOS-class targets). Keep the two spellings distinct — `SettingsPage.cs.md` §Draw tests `Environment.OSVersion.Platform != PlatformID.Unix` while this row tests `IsOSPlatform(Linux)`; the difference is intentional and recorded in `SettingsTab.cs.md` §`IsUnixExclusive` bullet.
- **`WineBinaryPathSetting` is the only row with no validity check** among the path rows here (contrast `Tabs/SettingsTabGame.cs.md` §`GamePathSetting`, `Tabs/SettingsTabPatching.cs.md` §patch path): a bogus custom Wine path is accepted at the UI level and fails later — see `lib/FFXIVQuickLauncher/src/XIVLauncher.Common.Unix/Compatibility/Wine.cs` `WineUtility`/`WineStartupType.Custom` handling for where it actually fails. That asymmetry is current state, not an invitation to leave it alone: if a check is added, it must return `null`-for-OK (`SettingsEntry{T}.cs.md` §validators) and stay cheap (§5 cost bullets).
- **`WineDebugAdditionalVarSetting` (`:53`) is a free-text Wine debug-vars row** whose getter defaults to `string.Empty` — the value is consumed by `WineSettings` (`Program.cs.md` §`CreateCompatToolsInstance` argument list). No validation exists; it's an expert surface, deliberately not gated.
- **Every enum here is a library enum** (`WineStartupType`, `WineManagedVersion`, `WineSyncType`, `DxvkVersion`, `DxvkHudType`) declared under `lib/FFXIVQuickLauncher/src/XIVLauncher.Common.Unix/Compatibility/`; several carry `SettingsDescriptionAttribute` annotations, which is what gives their combo rows friendly labels (`SettingsEntry{T}.cs.md` §enum rows + the `"dummy"` filter bullet — `lib/FFXIVQuickLauncher/src/XIVLauncher.Common/Dalamud/DalamudLoadMethod.cs` is the `"dummy"` example, and the Wine/Dxvk enums are the annotated ones). Adding an enum member is a library edit; the row picks it up automatically (`SettingsEntry{T}.cs.md` §enum rows uses `Enum.GetValues`).

## 9. Referenced / referenced-by

Instantiates: `SettingsEntry{T}.cs.md` (×9 rows: startup type, managed version, binary path, sync type, debug vars, DXVK version, DXVK HUD type, DXVK async, GameMode); the two `private` row fields are assigned inside the array literal (`:24`, `:56`) — see `SettingsEntry{T}.cs.md` §2 for the ctor's four arguments.
Base of: `SettingsTab.cs.md` (`IsUnixExclusive` override, `Save()` override, ctor-built `Entries`).
Consumed by: `SettingsPage.cs.md` §Draw (`IsUnixExclusive` filter, tab bar), §search mode (`Entries`), §finish button (`IsValid` sweep), `docs/XIVLauncher.Core/Program.cs.md` §`CreateCompatToolsInstance` (all nine config fields above) and §`CompatibilityTools`.
Library: `lib/FFXIVQuickLauncher/src/XIVLauncher.Common.Unix/Compatibility/{Wine,Dxvk}.cs` (enums + `SettingsDescriptionAttribute` annotations + `WineUtility.SystemFsyncSupport`/`FsyncSupport`), `lib/FFXIVQuickLauncher/src/XIVLauncher.Common.Unix/Compatibility/CompatibilityTools.cs` (`IsToolDownloaded`, `Settings.Prefix`, `RunInPrefix`, `Kill`), `lib/FFXIVQuickLauncher/src/XIVLauncher.Common/Util/PlatformHelpers.cs` (`OpenBrowser`).
