# DxvkBeta.cs — Specification

**File:** `src/XIVLauncher.Common.Unix/Compatibility/Dxvk/Releases/DxvkBeta.cs` · 8 lines · 493 bytes · ASCII, LF, no BOM
**Namespace:** `XIVLauncher.Common.Unix.Compatibility.Dxvk.Releases` (`:1`)
**Declaration:** `public sealed class DxvkBetaRelease : IDxvkRelease` (`:3`)

**Role:** the download descriptor behind `DxvkVersion.Beta` — GPLAsync-patched DXVK 2.7.

## 1. Constants

| Member | Line | Value |
| --- | --- | --- |
| `Name` | `:5` | `dxvk-gplasync-v2.7-1` |
| `DownloadUrl` | `:6` | `https://raw.githubusercontent.com/goatcorp/xlcore-distrib/refs/heads/main/dxvk-gplasync-v2.7-1.tar.gz` |
| `Checksum` | `:7` | one SHA-512 digest |

Same shape as [./DxvkStable.cs.md](DxvkStable.cs.md): the archive is a blob in the `goatcorp/xlcore-distrib`
`main` branch (`:6`), and `Name` must equal the archive's root directory (see
[../Dxvk.cs.md](../Dxvk.cs.md) §4 invariant, `Dxvk.cs:58`/`:85`).

## 2. Selection and cost

* Selected by `Dxvk.cs:53` (`DxvkVersion.Beta => new DxvkBetaRelease()`), i.e. ini key `DxvkVersion`
  (`docs/XIVLauncher.Core/Configuration/ILauncherConfig.cs.md` §2 field `:64`).
* Its `SettingsDescription` help (`Dxvk.cs:20`) requires Mesa 25.0 / Nvidia 550 — **prose only**, nothing
  in the fork probes driver versions (see [../Dxvk.cs.md](../Dxvk.cs.md) §1).
* Choosing Beta adds a *third* directory under `<toolsFolder>/dxvk`, which matters for the cleanup hack in
  `CompatibilityTools.cs:56`/`:59` (`DXVK_CLEANUP_THRESHHOLD = 5` → with three versions plus strays,
  switching repeatedly can wipe the other trees).

## 3. Referenced

* Referenced by: `Dxvk/Dxvk.cs:53`.
* References: [../IDxvkRelease.cs.md](../IDxvkRelease.cs.md) — see its §3 for the singular-checksum shape.
