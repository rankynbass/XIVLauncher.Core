# DxvkLegacy.cs — Specification

**File:** `src/XIVLauncher.Common.Unix/Compatibility/Dxvk/Releases/DxvkLegacy.cs` · 8 lines · 477 bytes · ASCII, LF, no BOM
**Namespace:** `XIVLauncher.Common.Unix.Compatibility.Dxvk.Releases` (`:1`)
**Declaration:** `public sealed class DxvkLegacyRelease : IDxvkRelease` (`:3`)

**Role:** the download descriptor behind `DxvkVersion.Legacy` — async-patched DXVK 1.10.3 from the
third-party `Sporif/dxvk-async` repo.

## 1. Constants

| Member | Line | Value |
| --- | --- | --- |
| `Name` | `:5` | `dxvk-async-1.10.3` |
| `DownloadUrl` | `:6` | `https://github.com/Sporif/dxvk-async/releases/download/1.10.3/dxvk-async-1.10.3.tar.gz` |
| `Checksum` | `:7` | one SHA-512 digest |

Unlike its siblings ([./DxvkStable.cs.md](DxvkStable.cs.md), [./DxvkBeta.cs.md](DxvkBeta.cs.md)), this URL is a
**GitHub release asset** (`…/releases/download/…`) pointing at a third-party repo, not the `goatcorp/xlcore-distrib`
blob mirror. Two consequences:

* Upstream pinned **1.10.1** of the same project
  (`DXVK_DOWNLOAD = "…/dxvk-async/releases/download/1.10.1/dxvk-async-1.10.1.tar.gz"`, upstream `Dxvk.cs:11`);
  the fork bumped it to 1.10.3 — see [../Dxvk.cs.md](../Dxvk.cs.md) §6.
* An un-published/renamed upstream release breaks the fork's Legacy track with a 404 inside
  `httpClient.GetByteArrayAsync` (`Dxvk.cs:78`) instead of a user-facing message.

## 2. Selection

`Dxvk.cs:54` (`DxvkVersion.Legacy => new DxvkLegacyRelease()`), driven by ini key `DxvkVersion`
(`docs/XIVLauncher.Core/Configuration/ILauncherConfig.cs.md` §2), surfaced through the UI row in
`SettingsTabWine.cs` (see `docs/XIVLauncher.Core/Components/SettingsPage/Tabs/SettingsTabWine.cs.md`).

## 3. Referenced

* Referenced by: `Dxvk/Dxvk.cs:54`.
* References: [../IDxvkRelease.cs.md](../IDxvkRelease.cs.md).
