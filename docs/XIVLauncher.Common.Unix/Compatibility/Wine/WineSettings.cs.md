# WineSettings.cs — Specification

**File:** `src/XIVLauncher.Common.Unix/Compatibility/Wine/WineSettings.cs` · 67 lines · 2488 bytes · ASCII, LF, no BOM
**Namespace:** `XIVLauncher.Common.Unix.Compatibility.Wine` (`:6`)
**Declarations:** `public enum WineStartupType` (`:8`–`:15`), `public enum WineManagedVersion` (`:17`–`:27`),
`public enum WineSyncType` (`:29`–`:35`), `public readonly struct WineSettings` (`:37`, fields `:39`–`:45`,
constructor `:47`–`:66`)

**Role:** the immutable value object that carries the user's wine choices into `CompatibilityTools`,
plus the three UI-facing enums whose members `SettingsEntry<T>` rows render. Upstream's same-named
file was a *mutable reference class* with boolean flags; the fork re-cut it as a `struct` with
enum-typed sync mode and a release object resolved inside the constructor.

---

## 1. Enum vocabulary (config surface)

These types are the *declared types* of the ini-backed settings — see `ILauncherConfig.cs:57`–`:59`
(`docs/XIVLauncher.Core/Configuration/ILauncherConfig.cs.md` §2) — so changing a member name changes
the persisted ini value.

| Enum | Members (line) | `SettingsDescription` label / help |
| --- | --- | --- |
| `WineStartupType` | `Managed` (`:11`), `Custom` (`:14`) | "Managed by XIVLauncher" / "Custom" |
| `WineManagedVersion` | `Stable` (`:20`), `Beta` (`:23`), `Legacy` (`:26`) | "The most stable experience, based on WINE 10.8" / "newest wine changes … lsteamclient patches" / "older systems … WINE 8.5" |
| `WineSyncType` | `ESync` (`:32`), `FSync` (`:34`) | "Eventfd-based … recommended for older systems" / "futex2 … requires Linux Kernel 5.16+" |

`[SettingsDescription(caption, help)]` is the annotation consumed by `SettingsEntry{T}` rendering
(`docs/XIVLauncher.Core/Components/SettingsPage/SettingsEntry{T}.cs.md`) — it has no runtime effect here.

Consumers of the enum values themselves:
`SettingsTabWine.cs:25`/`:26` (`SettingsEntry<WineStartupType>`/`<WineManagedVersion>`), `:37`
(`<WineSyncType>`), `:29`/`:35` visibility predicates keyed on `WineStartupType`, and the defaulting
probes `Program.cs:134` (`??= Managed`), `:135` (`??= Stable`), `:136` (sync type from the
`WineUtility.SystemFsyncSupport()` probe).

## 2. Value-object shape

| Field | Line | Type | Written from |
| --- | --- | --- | --- |
| `StartupType` | `:39` | `WineStartupType` (`init`) | ctor param 1 |
| `Release` | `:40` | `IWineRelease` (`init`) | ctor, only in the `Managed` branch — **null when `Custom`** |
| `SyncType` | `:41` | `WineSyncType` (`init`) | ctor param **7** (`wineSyncType`) |
| `CustomBinPath` | `:42` | `string` (`init`) | param 3 |
| `DebugVars` | `:43` | `string` (`init`) | param 4 |
| `LogFile` | `:44` | `FileInfo` (`init`) | param 5 |
| `Prefix` | `:45` | `DirectoryInfo` (`init`) | param 6 |

Because it is a `public readonly struct` (not a class), it is a value type: every assignment copies
fields, `null!`-style "constructed later" patterns are impossible, and all seven must be initialised in
the constructor (`:49`–`:54`, `:58`–`:64`). No other type can hold one without copying — e.g.
`CompatibilityTools.Settings` (`CompatibilityTools.cs:40`) stores it by value.

## 3. Constructor contract

```
:47  public WineSettings(WineStartupType startupType, WineManagedVersion managedWine,
            string customBinPath, string debugVars, FileInfo logFile,
            DirectoryInfo prefix, WineSyncType wineSyncType)
```

* **Positional trap:** the parameter order does not match the field order — `wineSyncType` is last but
  `SyncType` is the second field. A transposed call compiles silently when both arguments are the same
  type; only `startupType`/`managedWine` (`WineStartupType`/`WineManagedVersion`) and
  `logFile` (`FileInfo`) are type-distinguishable, the three `string`-adjacent slots are not. The sole
  construction site, `Program.cs:363`, passes them in declaration order and is correct.
* **Release resolution (`:55`–`:64`):** inside `if (startupType == WineStartupType.Managed)` the ctor calls
  `CompatUtil.GetWineIdForDistro()` (`:57`) — *disk/CPU work inside a constructor*, and the only place
  the distro is probed — then switches `managedWine` to `new WineStableRelease(wineDistroId)` (`:60`),
  `WineBetaRelease` (`:61`), `WineLegacyRelease` (`:62`).
* **Default arm (`:63`):** `throw new ArgumentOutOfRangeException(managedWine.ToString())`. Unreachable today:
  `WineManagedVersion` has exactly three members and all three are listed, so the switch is exhaustive;
  the arm only fires if a fourth version is added without a release class. Note the exception's *argument*
  is the message — `ToString()` on an enum yields the member name (e.g. `"Stable"`), which is the whole
  diagnostic.
* **Custom branch:** when `startupType != Managed`, `Release` gets no assignment → default `null`.
  Every later `Settings.Release.*` read (`CompatibilityTools.cs:28`, `:41`, `:85`, `:99`) then
  null-derefs. The fork's launch path pre-empts that by validating the custom binary before the tool is
  used (`MainPage.cs:686`/`:688`/`:691` in `docs/XIVLauncher.Core/Components/MainPage/MainPage.cs.md`), but
  the type itself carries no `NonNull` guarantee.

## 4. Defaults applied by callers (not by this file)

The struct has no defaulting of its own; `Program.cs:363` supplies it:

```
new WineSettings(Config.WineStartupType   ?? WineStartupType.Custom,
                 Config.WineManagedVersion ?? WineManagedVersion.Stable,
                 Config.WineBinaryPath, Config.WineDebugVars,
                 wineLogFile /* logs/wine.log */, winePrefix /* wineprefix */,
                 Config.WineSyncType ?? WineSyncType.FSync)
```

Two things to note: the `StartupType` fallback here is **`Custom`**, while `LoadConfig` defaults the
same key to **`Managed`** (`Program.cs:134`) — a config that never went through `LoadConfig` gets the
`Custom`/`Release == null` shape; and `WineSyncType`'s fallback here is hard `FSync`, whereas
`Program.cs:136` chooses via `WineUtility.SystemFsyncSupport()` (see
[../WineUtility.cs.md](WineUtility.cs.md) §2).

## 5. Invariants and hazards

* **`Release` ⇔ `StartupType` invariant is not encoded.** Only the `Managed` branch assigns it; the type
  allows the broken combination `StartupType == Custom, Release == null` (which is what actually
  happens). Readers must branch on `StartupType` first — `CompatibilityTools.cs:28` does
  (`Managed ? wineDirectory/Release.Name/bin : CustomBinPath`), but
  `CompatibilityTools.cs:85`/`:99` (`DownloadTool`) do not.
* **`SyncType` is an enum, upstream used strings.** Upstream stored `EsyncOn`/`FsyncOn` as `string?`
  fields and set `WINEESYNC`/`WINEFSYNC` from them; the fork decides in
  `CompatibilityTools.cs:187`/`:190` from the enum. Both flags can never be set at once now, whereas
  upstream could (wine prefers futex2 when both are set).
* **No `LogFile` existence check:** `CompatibilityTools.cs:67` opens `wineSettings.LogFile.FullName` with a
  `StreamWriter` in the tool's constructor, truncating it. A `FileInfo` for a path in a non-existent
  directory therefore throws from `CompatibilityTools`, not from here.
* **`Prefix` is not created here:** that happens in `CompatibilityTools`'s ctor
  (`:69`–`:75`, `wineSettings.Prefix.Create()` at `:78`), i.e. `WineSettings` hands out a possibly
  non-existent `DirectoryInfo` and lets the consumer create it.

## 6. Upstream delta (`lib/FFXIVQuickLauncher/src/XIVLauncher.Common.Unix/Compatibility/WineSettings.cs`)

| Aspect | upstream | fork |
| --- | --- | --- |
| Location | `Compatibility/WineSettings.cs` | moved into `Compatibility/Wine/`, namespace `.Wine` appended |
| Kind | `public class`, mutable `{ get; private set; }` fields | `public readonly struct`, `{ get; init; }` |
| Sync flags | `string EsyncOn`, `string FsyncOn` | single `WineSyncType` enum |
| Release selection | hard-coded URL/name constants in `CompatibilityTools` (`#if WINE_XIV_ARCH_LINUX`, …) | `IWineRelease` chosen in this ctor from a runtime distro probe |
| Wording | "The game installation and wine setup…" | "WINE setup…" (`:10`); "custom location containing wine binaries" → "…WINE binaries" (`:13`) |
| Nullability | ctor took `WineStartupType?` and `bool?` flags | non-nullable params (`Nullable=disable` in the csproj) |

## 7. Referenced

* `CompatibilityTools.cs` (`:40` field type, `:67` ctor reads `LogFile`), `Wine/Releases/*` (parameter
  types), `Program.cs:363` (sole construction), `SettingsTabWine.cs:15`/`:24`–`:53` (enum rows),
  `ILauncherConfig.cs:5` (`using XIVLauncher.Common.Unix.Compatibility.Wine;`) and `:57`–`:70` (field types).
* References: [../Releases/IWineRelease.cs.md](Releases/IWineRelease.cs.md) (`:4` import),
  `CompatUtil.GetWineIdForDistro` ([../CompatUtil.cs.md](../CompatUtil.cs.md)),
  `docs/XIVLauncher.Core/Configuration/ILauncherConfig.cs.md` (config keys),
  `docs/XIVLauncher.Core/Components/SettingsPage/Tabs/SettingsTabWine.cs.md` (UI rows).
