# AppUtil.cs — Specification

**File:** `src/XIVLauncher.Core/AppUtil.cs` · 72 lines · 2,183 bytes
**Namespace:** `XIVLauncher.Core` · **Declaration:** `public static partial class AppUtil`
**Role:** Assembly/reflection utilities + platform-level helper shims

---

## 1. Purpose

A stateless utility class that answers "who am I" questions about the running build (version, git hash, build origin), exposes embedded build resources (fonts, images) at runtime, and provides a single browser-opening entry point that is Steam-Deck aware.

The class is `static partial`: `static` (no instance state, no instantiation) and `partial` because the second part lives in `AppUtil.VersionInfo.cs` (and a third part is generated at build time, see §4).

## 2. Members

| Signature | Contract |
| --- | --- |
| `static byte[] GetEmbeddedResourceBytes(string resourceName)` | Returns the full bytes of a compile-time embedded resource identified by its **logical name**. Throws `ArgumentException` (with `nameof(resourceName)`) when the resource is missing. |
| `static string? GetGitHash()` | Value of the `GitHash` `AssemblyMetadata` attribute, or `null`. |
| `static string? GetBuildOrigin()` | Value of the `BuildOrigin` `AssemblyMetadata` attribute, or `null`. |
| `static string GetAssemblyVersion()` | `VersionInfo.Instance().Version.FileVersion.ToString()` — never null by contract of the doc comment. |
| `static void OpenBrowser(string url)` | Opens `url` in the system browser, or the Steam overlay web page on Deck. |

## 3. Behaviour details

### `GetEmbeddedResourceBytes`

- The host assembly is selected via `typeof(ImGuiBindings).Assembly`, **not** `typeof(AppUtil).Assembly` — this is deliberate: `AppUtil` is a `static` class and picking a type from the same file is fragile; `ImGuiBindings` pins the reference to the `XIVLauncher.Core` executable assembly.
- `s.Length` is used as an exact byte count, and `ReadExactly(ret, 0, (int)s.Length)` will throw `EndOfBuffer`/read-past-end if a resource's stream length disagrees with itself — acceptable for compile-time resources but means the method is unsuitable for streamed/variable data.
- Consumed callers: `FontManager.SetupFonts` (`"NotoSansCJKjp-Regular.otf"`, `"FontAwesome5FreeSolid.otf"`), and image resources in component classes.

Embedded resource logical names available (from `XIVLauncher.Core.csproj`):

| Logical name | Source |
| --- | --- |
| `FontAwesome5FreeSolid.otf` | `Resources/fonts/FontAwesome5FreeSolid.otf` |
| `NotoSansCJKjp-Regular.otf` | `Resources/fonts/NotoSansCJKjp-Regular.otf` |
| `logo.png` | `Resources/logo.png` |
| `bg_logo.png` | `Resources/bg_logo.png` |
| `steamdeck_fts.png`, `steamdeck_fterror.png`, `steamdeck_switchprompt.png` | `Resources/…` |

### `GetGitHash` / `GetBuildOrigin`

Both read the same assembly-metadata attributes injected by the build (`XIVLauncher.Core.csproj` task pair `GetGitHash`/`WriteGitHash`), which runs `git describe --long --always --dirty` in the project directory and writes `AssemblyMetadata("GitHash", <hash>)` plus `AssemblyMetadata("BuildOrigin", $(GITHUB_REPOSITORY))` into a generated `CustomAssemblyInfoFile`. Consequently these are `null` for local builds that skip the target (e.g. no git repo) — `Program.SetupLogging` logs them as `{Version} - {Hash}` with a `null` tail in that case.

### `GetAssemblyVersion`

The version string comes from the `<FileVersion>` project property (`1.4.0`), surfaced through the generated `VersionInfo` constructor — see `AppUtil.VersionInfo.cs.md` for the codegen contract. `Program.Main` uses it in the SDL window title (`XIVLauncher {version} ({hash}`).

### `OpenBrowser`

Branch condition (all three must hold): `Program.IsSteamDeckHardware` **and** `Program.Steam != null` **and** `Program.Steam.IsValid`. On Deck it calls `Steam.ActivateGameOverlayToWebPage(url)`; otherwise `PlatformHelpers.OpenBrowser(url)` (`XIVLauncher.Common.PlatformAbstractions`). The doc comment warns that on Steam Deck this opens the in-game overlay rather than a browser process.

## 4. Dependency surface

- Internal: `AppUtil.VersionInfo` (same `AppUtil` class, other file), `Program` (`IsSteamDeckHardware`, `Steam`), `XIVLauncher.Common.Util`, `XIVLauncher.Common.PlatformAbstractions`.
- External: `System.Reflection` (`Assembly.GetManifestResourceStream`, `GetCustomAttributes<AssemblyMetadataAttribute>`), `System.IO.Stream` (via `using var s`), `System.Diagnostics.Process` only in `CoreEnvironmentSettings`, not here.
- Build-time: `XIVLauncher.VersionGenerator` source generator + `obj/…/CustomAssemblyInfoFile` assembly attributes.

## 5. Invariants / caveats

- No caching: `GetGitHash`, `GetBuildOrigin`, and `GetAssemblyVersion` re-reflect on every call; keep them out of the per-frame ImGui path (they are currently used once, at startup, and in the window title).
- `GetEmbeddedResourceBytes` returns a fresh copy each call — callers must not mutate shared state expecting identity, and repeated calls re-copy multi-hundred-KB font blobs.
- `OpenBrowser` silently no-ops on Deck when Steam failed to initialise (`Program.Steam == null`); there is no fallback to a system browser in that state.
- Because `AppUtil` is `static`, it cannot be instantiated; a `new AppUtil(...)` in any part would be a compile error.
