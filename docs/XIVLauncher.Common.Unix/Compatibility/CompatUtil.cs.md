# CompatUtil.cs — Specification

**File:** `src/XIVLauncher.Common.Unix/Compatibility/CompatUtil.cs` · 105 lines · 3159 bytes · ASCII, LF, no BOM
**Namespace:** `XIVLauncher.Common.Unix.Compatibility.Wine` (`:8`)
**Declarations:** `public enum WineReleaseDistro` (`:10`–`:16`), `public static class CompatUtil` (`:18`),
`public static WineReleaseDistro GetWineIdForDistro()` (`:20`–`:92`),
`public static bool EnsureChecksumMatch(string filePath, string[] checksums)` (`:94`–`:104`)

**Role:** two pieces of host-distro intelligence the fork added on top of upstream: pick a pre-built wine
tarball per distribution at **runtime**, and verify a downloaded tarball's SHA-512 digest.

---

## 1. `WineReleaseDistro`

| Member | Line | Notes |
| --- | --- | --- |
| `ubuntu` | `:12` | also the fallback and the snap/flatpak answer |
| `fedora` | `:13` | matched by value substring `fedora` **or** `tumbleweed` |
| `arch` | `:14` | matched by substring `arch` |
| `macOS` | `:15` | returned on OSX; **no release asset exists for it** (see [Wine/Releases/WineStable.cs.md](Wine/Releases/WineStable.cs.md) §2) |

The members are lowercase because their **names are interpolated into release asset filenames** —
`Wine{Stable,Beta,Legacy}Release.DownloadUrl` splice `{wineDistroId}` (`Wine/Releases/WineStable.cs:6`).
That coupling is the reason the enum cannot be renamed freely.

## 2. `GetWineIdForDistro()`

### 2.1 Platform gate

| Line | Behaviour |
| --- | --- |
| `:22` | `RuntimeInformation.IsOSPlatform(OSPlatform.OSX)` → return `macOS` (`:24`) |
| `:27` | `!IsOSPlatform(Linux)` → **throw** `InvalidOperationException("GetWineIdForDistro called on unsupported platform")` (`:29`) |
| `:32` onward | the rest of the body is inside `try { … } catch { return ubuntu; }` (`:88`–`:91`) |

The `throw` sits **before** the `try` block opens at `:32`, so it is not swallowed by the bare `catch` at
`:88`–`:91`: it propagates to the caller. Only reachable if a wine settings object is built on a non-Linux/non-OSX platform; in the
fork `Program.CreateCompatToolsInstance()` returns early on Windows (`Program.cs:360`), so in practice the
throw is defensive.

### 2.2 Reading `/etc/os-release`

| Line | Step |
| --- | --- |
| `:34` | `const string OS_RELEASE_PATH = "/etc/os-release"` |
| `:36` | missing file → `ubuntu` (`:38`) |
| `:40`–`:49` | read all lines; `line.Split('=', 2)` → at most 2 parts; a line with no `=` becomes key→`""` (`:46`), otherwise key→value (`:48`); accumulated into `Dictionary<string,string> osInfo` (`:41`) |

Properties are not de-quoted: `/etc/os-release` values are usually quoted (`ID="arch"`), and the matching
logic below uses `Contains`, so quotes don't matter for the substring tests — but they would for an
equality test, and `:54`/`:60` **are** equality tests. In practice `/etc/os-release` on flatpak/snap
setups writes `ID=org.freedesktop.platform` unquoted, which is what the code expects.

### 2.3 Flatpak / snap short-circuit (`:52`–`:65`)

| Line | Test | Result |
| --- | --- | --- |
| `:52` | `osInfo.ContainsKey("ID")` | gate for both special cases |
| `:54` | `ID == "org.freedesktop.platform"` | `ubuntu` (`:56`) — the flatpak/Steam Deck "Gamescope/OS release" identity |
| `:58` | else if `HOME_URL` present | nested test |
| `:60` | `ID == "ubuntu-core"` && `HOME_URL == "https://snapcraft.io"` | `ubuntu` (`:62`) |

Both arms exist because inside a flatpak/snap chroot the *host* distro is invisible: the reported distro is
the container, whose glibc/ABi is Ubuntu-derived. Returning `ubuntu` selects the Ubuntu-linked wine build.

### 2.4 The general scan (`:68`–`:84`)

Iterates `foreach (var kvp in osInfo)` (insertion order of `/etc/os-release`), testing **values only**
(`kvp.Value.ToLower()`), in per-line priority order:

1. value contains `fedora` **or** `tumbleweed` → `fedora` (`:70`→`:72`)
2. value contains `arch` → `arch` (`:75`→`:77`)
3. value contains `ubuntu` or `debian` → `ubuntu` (`:80`→`:82`)

`return ubuntu` at `:86` if nothing matched.

Consequences worth recording:
* **First matching line wins, not the strongest match.** `NAME="Arch rolling"` on a Debian host whose later
  `CODENAME=Debian Stretch` line would match `debian` still returns `arch` because `NAME` precedes `CODENAME`.
* **Substring matching over-slices.** `arch` matches inside `search`, `march`, `_ARCH=`; e.g. a value like
  `HOME_URL=https://archlinux.org` is intended, but `ID=github` or `VENDOR="Search Labs"` yields `arch`.
* Keys are never consulted: a line `# comment` becomes key `# comment`→`""` and is scanned as a value
  (empty values never match, so comments are inert).
* `Dictionary.Add` (`:46`/`:48`) throws on duplicate keys; a well-formed `os-release` has unique keys, and
  if it ever didn't, the bare `catch` at `:88`–`:91` converts the failure into an `ubuntu` result.
* The bare `catch` also swallows IO errors from `File.ReadAllLines` (`:40`) and any `FormatException` —
  every internal failure means "Ubuntu build", silently (nothing is logged).

## 3. `EnsureChecksumMatch`

```
:96  if (checksums.Length == 0) return false;
:100 using var sha512 = SHA512.Create();
:101 using var stream   = File.OpenRead(filePath);
:102 var computedHash = Convert.ToHexString(sha512.ComputeHash(stream)).ToLowerInvariant();
:103 return checksums.Any(checksum => string.Equals(checksum, computedHash, StringComparison.OrdinalIgnoreCase));
```

* Empty expectation list = **failure**, not "skip verification" — the safe default.
* Digest comparison is case-insensitive (`:103`), so the mixed-case/lowercase digests stored in the release
  classes are interchangeable; all release files currently use lowercase hex.
* `ComputeHash(stream)` is the one-shot streaming overload: the whole file is hashed into memory as a
  single digest (no incremental API), which is fine for a ~30 MB `.tar.xz`.
* `Convert.ToHexString` yields `A4FF…` (upper), then `ToLowerInvariant` matches the lowercase constants.
* Missing file / unreadable path: `File.OpenRead` throws `FileNotFound` — the caller decides (see §4).

## 4. Callers

| Site | Use |
| --- | --- |
| `Wine/WineSettings.cs:57` | sole `GetWineIdForDistro()` call, inside the `Managed` ctor branch |
| `CompatibilityTools.cs:99` | `if (!CompatUtil.EnsureChecksumMatch(tempFilePath, Settings.Release.Checksums)) throw new InvalidDataException("SHA512 checksum verification failed")` — wine tarball (`DownloadTool`, `:95`–`:106`) |
| `Dxvk/Dxvk.cs:80` | `CompatUtil.EnsureChecksumMatch(tempPath, [checksum])` — the one-element **array literal** wrapper, needed because the Dxvk interface member is singular (`IDxvkRelease.cs:7` `Checksum`) |

Both throw sites are inside `async` tasks; the wine one surfaces through
the `EnsureTool` await at `MainPage.cs:709` (fault handling in
[../../XIVLauncher.Core/Components/MainPage/MainPage.cs.md](../../XIVLauncher.Core/Components/MainPage/MainPage.cs.md))
and the Dxvk one through `Dxvk.Dxvk.InstallDxvk`, awaited at `CompatibilityTools.cs:90`.

## 5. Fork-only file

Upstream has no `CompatUtil`: the distro choice was made at **compile time** with
`#if WINE_XIV_ARCH_LINUX` / `#elif WINE_XIV_FEDORA_LINUX` / `#else` constants in its
`CompatibilityTools.cs`, and checksum verification did not exist (upstream's `DownloadTool` untars
whatever it downloaded). See [CompatibilityTools.cs.md](CompatibilityTools.cs.md) §6 for the delta.
Upstream's Unix tree also carries `Compatibility/GameFixes/{GameFix,GameFixApply}.cs`, which the fork
deleted; nothing in either tree references them.

## 6. Referenced

* Referenced by: `Wine/Releases/WineStable.cs:3`/`WineBeta.cs:3`/`WineLegacy.cs:3` (parameter type),
  `Wine/WineSettings.cs:57`, `CompatibilityTools.cs:99`, `Dxvk/Dxvk.cs:80`, `CompatibilityTools.cs:13`
  (`using XIVLauncher.Common.Unix.Compatibility.Wine;`).
* References: `System.Security.Cryptography` (`SHA512.Create`), `System.Linq` (`.Any`),
  `System.IO`/stdlib file accessors (`File.Exists`, `File.ReadAllLines`, `File.OpenRead`),
  `RuntimeInformation.IsOSPlatform`.
