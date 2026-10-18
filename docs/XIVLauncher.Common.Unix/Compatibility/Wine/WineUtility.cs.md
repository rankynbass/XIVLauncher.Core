# WineUtility.cs — Specification

**File:** `src/XIVLauncher.Common.Unix/Compatibility/Wine/WineUtility.cs` · 23 lines · 644 bytes · ASCII, LF, no BOM
**Namespace:** `XIVLauncher.Common.Unix.Compatibility.Wine` (`:4`)
**Declarations:** `public enum FsyncSupport` (`:6`–`:11`), `public static class WineUtility` (`:13`),
`public static FsyncSupport SystemFsyncSupport()` (`:15`–`:22`)

**Role:** a one-shot capability probe: "can this machine use wine's futex2 (FSync) path?" It is the only
non-UI reader of kernel version in the fork, and it feeds the default value of `Config.WineSyncType`.

---

## 1. Enum

| Member | Line | Meaning in the enum's own words |
| --- | --- | --- |
| `Supported` | `:8` | FSync may be used |
| `UnsupportedPlatform` | `:9` | not Linux (OSX, or the Windows-shaped branch) |
| `OutdatedKernel` | `:10` | Linux kernel too old for futex2 |

Declared before the class, at file scope — so the fork can name it unqualified via
`using XIVLauncher.Common.Unix.Compatibility.Wine;` (as `Program.cs` and `SettingsTabWine.cs` do).

## 2. `SystemFsyncSupport()` — body walk

| Line | Effect |
| --- | --- |
| `:17` | `if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux))` → return `UnsupportedPlatform` (`:18`) |
| `:19` | `if (Environment.OSVersion.Version.Major < 5 && (Minor < 16 \|\| Major < 6))` → return `OutdatedKernel` (`:20`) |
| `:21` | otherwise `Supported` |

`Environment.OSVersion.Version.Major/.Minor` is the running **kernel** release (`uname -r` shaped), so on
Linux `5.15` reads as `Major == 5, Minor == 15`.

### 2.1 The predicate is not the documented rule

Because `Major < 5` logically implies `Major < 6`, the parenthesised disjunction is always true at the
point it is evaluated. The whole condition therefore reduces to **`Major < 5`**, giving:

| kernel | result | expected per `WineSyncType.FSync` help text ("requires Linux Kernel 5.16+") |
| --- | --- | --- |
| 4.20 (LTS 4.x) | `OutdatedKernel` | correct |
| 5.4 | **`Supported`** | should be `OutdatedKernel` |
| 5.15 | **`Supported`** | should be `OutdatedKernel` |
| 5.16 / 6.x / 7.x | `Supported` | correct |
| 0.x (unknown/unparsable version) | `OutdatedKernel` | conservative |

So kernels 5.0–5.15 — exactly the range wine's futex2 path fails on — are reported as supported. Nothing
else in the tree re-checks this, which makes it a live hazard rather than a cosmetic bug: `Program.cs:136`
defaults to FSync on a 5.15 kernel, and `SettingsTabWine.cs:41` validates it as fine (see §3).

## 3. Consumers

| Site | Use |
| --- | --- |
| `Program.cs:136` | `Config.WineSyncType ??= SystemFsyncSupport() == FsyncSupport.Supported ? WineSyncType.FSync : WineSyncType.ESync` — **only** equality against `Supported`, so both failure modes collapse to `ESync` |
| `SettingsTabWine.cs:41` | `CheckValidity` of the `SettingsEntry<WineSyncType>` row switches on all three members (`:43` `UnsupportedPlatform` → `Strings.EnableFsyncSettingUnsupportedPlatformValidation`, `:45` `OutdatedKernel` → `Strings.EnableFSyncSettingMinKernelValidation`, `:47` `Supported` falls through `default:` → `null`) |

Both consumers are inside the *settings defaulting/validation* paths; neither is on the launch path — the
env-var emission in `CompatibilityTools.cs:187`/`:190` reads `Settings.SyncType` without consulting this
probe again. Consequence: a user (or an ini file) can force `FSync` on an unsupported kernel and get wine
without futex2 support.

Note the asymmetry in the two consumers: `Program.cs:136` compares `== Supported`, while the UI switch
treats `Supported` and `default:` identically (`:47`–`:48`). Since the switch lists all three members, the
`default:` arm is defensive only.

## 4. Referenced by / referenced

* Referenced by: `Program.cs:20` (`using XIVLauncher.Common.Unix.Compatibility.Wine;`) + `:136`,
  `SettingsTabWine.cs:7` (import) + `:41`,
  `WineSettings.cs`/`CompatibilityTools.cs` do **not** call it (they only consume the already-resolved
  `WineSyncType` enum).
* References: `Environment.OSVersion` and `RuntimeInformation.*` from the stdlib `System` layer
  (`:1`/`:2` imports). No other fork type is involved.
* Upstream counterpart: **none** — upstream had no FSync probe; `WineSettings` carried
  `EsyncOn`/`FsyncOn` string flags set straight from the ini. See
  [../WineSettings.cs.md](WineSettings.cs.md) §6.
