# AppUtil.cs — Specification

**File:** `src/XIVLauncher.Core/AppUtil.cs` · 72 lines · 2,177 bytes
**Namespace:** `XIVLauncher.Core` · **Declaration:** `public static partial class AppUtil`
**Role:** Assembly/reflection utilities + Steam-Deck-aware browser launch

---

## 1. Purpose

A stateless utility class answering "who am I" questions about the running build (version, git hash, build origin), exposing embedded build resources (fonts, images) at runtime, and providing a single browser-opening entry point that prefers the Steam overlay on Steam Deck.

The class is `static partial`: `static` (no instance state) and `partial` because the second part lives in `AppUtil.VersionInfo.cs` (`AppUtil.VersionInfo.cs.md`), and a third part is generated at build time (`obj/Debug/net10.0/linux-x64/CustomAssemblyInfo.cs`, excluded from docs).

Usings: `System.Reflection` (`AssemblyMetadataAttribute`), `XIVLauncher.Common.Util` (`PlatformHelpers.OpenBrowser`).

## 2. Members

| Signature | Contract |
| --- | --- |
| `static byte[] GetEmbeddedResourceBytes(string resourceName)` (line 9) | Full bytes of a compile-time embedded resource identified by its **logical name**; throws `ArgumentException` (with `nameof(resourceName)`) when the stream is missing (lines 13–16). |
| `static string? GetGitHash()` (line 26) | Value of the `GitHash` `AssemblyMetadata` attribute, or `null`. |
| `static string? GetBuildOrigin()` (line 37) | Value of the `BuildOrigin` `AssemblyMetadata` attribute, or `null`. |
| `static string GetAssemblyVersion()` (line 48) | `VersionInfo.Instance().Version.FileVersion.ToString()`; never null by the doc comment's contract. |
| `static void OpenBrowser(string url)` (line 61) | Opens `url` via the Steam overlay on Deck, otherwise via `PlatformHelpers.OpenBrowser`. |

## 3. `GetEmbeddedResourceBytes`

- Assembly is `typeof(AppUtil).Assembly` (line 11) — the `XIVLauncher.Core` executable assembly. HEAD used `typeof(ImGuiBindings).Assembly` to pin the same assembly from a utility class; with `ImGuiBindings` deleted, `AppUtil` is the reference type, which is the same assembly, so the behaviour is unchanged.
- `using var s = assembly.GetManifestResourceStream(resourceName)` (line 13) — `null` ⇒ `ArgumentException` (lines 14–16).
- `var ret = new byte[s.Length]` then `s.ReadExactly(ret, 0, (int)s.Length)` (lines 17–18): the stream length is taken as the exact byte count, and `ReadExactly` throws if the stream ends early. `(int)s.Length` truncates a >2 GB stream, which is irrelevant for embedded assets.
- No caching: every call re-opens the stream and copies a fresh buffer.

Embedded resource logical names (from `XIVLauncher.Core.csproj` lines 90–99):

| Logical name | Source |
| --- | --- |
| `FontAwesome5FreeSolid.otf` | `Resources/fonts/FontAwesome5FreeSolid.otf` (csproj line 90) |
| `NotoSansCJKjp-Regular.otf` | `Resources/fonts/NotoSansCJKjp-Regular.otf` (line 91) |
| `logo.png` | `Resources/logo.png` (line 93) |
| `bg_logo.png` | `Resources/bg_logo.png` (line 95) |
| `steamdeck_fts.png` | `Resources/steamdeck_fts.png` (line 97) |
| `steamdeck_fterror.png` | `Resources/steamdeck_fterror.png` (line 98) |
| `steamdeck_switchprompt.png` | `Resources/steamdeck_switchprompt.png` (line 99) |

The table is not exhaustive for `src/XIVLauncher.Core/Resources/`: `Resources/Localization/Strings.resx` (csproj lines 83–86) is compiled as a CLR resource table, not a `LogicalName` blob, so none of the names above covers it; the class reading it is spec'ed in `docs/XIVLauncher.Core/Resources/Localization/Strings.Designer.cs.md`.

Consumers in `src/XIVLauncher.Core`:

| Consumer | Site | Resource |
| --- | --- | --- |
| `AppFonts.ResourceStream` | `AppFonts.cs:49` | font streams (`AppFonts.cs.md`) |
| `AppImages.FromResource` | `AppImages.cs.md` | `bg_logo.png`, `logo.png`, `steamdeck_*.png` |

`FontManager.cs` (HEAD's consumer of both font blobs) is deleted.

## 4. `GetGitHash` / `GetBuildOrigin`

Both read assembly-metadata attributes injected by the build:

- `XIVLauncher.Core.csproj` target `GetGitHash` (line 118) runs `git describe --long --always --dirty` in the project directory when `$(BuildHash)` is empty.
- The `WriteGitHash` target writes `AssemblyMetadata("GitHash", ...)` and `AssemblyMetadata("BuildOrigin", $(GITHUB_REPOSITORY))` into `$(IntermediateOutputPath)CustomAssemblyInfo.cs` (csproj lines 137–156), included as `Compile` (line 141).

Consequences: `GetGitHash()` is `null` when the build runs outside a git repo, and `GetBuildOrigin()` is `null` when `GITHUB_REPOSITORY` is not set (i.e. local builds). Consumers:

| Consumer | Site | Use |
| --- | --- | --- |
| `Program.SetupLogging` | `Program.cs:73` | `Log.Information("Starting a session(v{Version} - {Hash})", ...)` |
| `Program.Main` | `Program.cs:257-258` | version + hash passed to `new LauncherForm(launcherApp, version, hash)` (line 278) |
| `SettingsTabAbout` | `SettingsTabAbout.cs:27` | `versionLabel.Text = $"XIVLauncher Core v{AppUtil.GetAssemblyVersion()}({AppUtil.GetGitHash()})"` |
| `Troubleshooting` | `Troubleshooting.cs:124-126` | `LauncherVersion`, `LauncherHash` (`GetGitHash() ?? "<unavailable>"`), `Official = GetBuildOrigin() == "goatcorp/XIVLauncher.Core"` |

## 5. `GetAssemblyVersion`

`VersionInfo.Instance().Version.FileVersion.ToString()` (lines 48–51). `<Version>1.4.0</Version>` with `<FileVersion>$(Version)</FileVersion>` (csproj lines 12–13), so the returned string is the project version. `AppUtil.VersionInfo.cs.md` documents the generated `VersionInfo` contract.

## 6. `OpenBrowser`

Branch condition (all three must hold, line 63): `Program.IsSteamDeckHardware` **and** `Program.Steam != null` **and** `Program.Steam.IsValid`.

- Deck path: `Program.Steam.ActivateGameOverlayToWebPage(url)` (line 65) — opens the Steam overlay browser, not a system browser (doc comment).
- Other path: `PlatformHelpers.OpenBrowser(url)` (`XIVLauncher.Common.PlatformAbstractions`, submodule; line 69).

Consumers in `src/XIVLauncher.Core`:

| Consumer | Site |
| --- | --- |
| `MainPage` status button | `MainPage.cs:60` → `https://is.xivup.com/` |
| `MainPage` account button | `MainPage.cs:61` → `https://sqex.to/Msp` |
| `NewsFrame` banner click | `NewsFrame.cs:57` → `bannerList[currentBanner].Link.ToString()` |
| `NewsFrame` news entry | `NewsFrame.cs:181` → `newsEntry.Url` |
| `SettingsTabAbout` contributors link | `SettingsTabAbout.cs:31` → GitHub contributors graph |
| `SettingsTabAbout` license list | `SettingsTabAbout.cs:66` (URL); line 68 uses `PlatformHelpers.OpenBrowser(Path.Combine(AppContext.BaseDirectory, "license.txt"))` directly, bypassing this method |
| `FtsPage` guide button | `FtsPage.cs:49` → `https://goatcorp.github.io/faq/steamdeck` |

Other direct `PlatformHelpers.OpenBrowser` calls, not through this method: `SettingsTabTroubleshooting.cs:67` (logs folder), `SettingsTabWine.cs:93` (Wine prefix folder).

## 7. Dependency surface

- Internal: `AppUtil.VersionInfo` (same class, other file), `Program` (`IsSteamDeckHardware`, `Steam`) (`Program.cs.md`).
- External: `System.Reflection` (`Assembly.GetManifestResourceStream`, `GetCustomAttributes<AssemblyMetadataAttribute>`), `System.IO.Stream` (via `using var s`), `XIVLauncher.Common.Util`/`PlatformAbstractions` (submodule `lib/FFXIVQuickLauncher`, out of scope).
- Build-time: assembly-metadata attributes written into `CustomAssemblyInfo.cs` (excluded from docs).

## 8. Invariants / caveats

- No caching: `GetGitHash`, `GetBuildOrigin`, `GetAssemblyVersion` re-reflect on every call. They are called at startup, in the About tab, in the news/troubleshooting flows, and once per log session — not in a per-frame path, and there is no immediate-mode draw loop any more.
- `GetEmbeddedResourceBytes` returns a fresh copy each call: `AppFonts` reads both font blobs once at `Initialize` (`AppFonts.cs.md`), `AppImages` caches decoded bitmaps per resource name (`AppImages.cs.md`).
- `OpenBrowser` silently no-ops on Deck when Steam is present but `IsValid` is false **only for the Deck branch**; when `Program.Steam == null` the `PlatformHelpers` path runs, so the fallback exists. On Deck with an invalid Steam instance, `PlatformHelpers.OpenBrowser` runs — the overlay is not opened.
- Because `AppUtil` is `static`, it cannot be instantiated.
- `GetAssemblyVersion` returns `FileVersion`, which is `1.4.0` here, not the informational package version; `Troubleshooting.LauncherVersion` and the About label both show that value.

## 9. Referenced / referenced-by

Uses: `Program`, `VersionInfo`, `PlatformHelpers`.
Referenced by: `Program.cs` (lines 73, 257, 258), `AppFonts.cs` (line 49), `AppImages.cs.md`, `MainPage.cs` (lines 60, 61), `NewsFrame.cs` (lines 57, 181), `SettingsTabAbout.cs` (lines 27, 31, 66), `SettingsTabTroubleshooting.cs` (line 67, `PlatformHelpers` directly), `SettingsTabWine.cs` (line 93, `PlatformHelpers` directly), `FtsPage.cs` (line 49), `Troubleshooting.cs` (lines 124–126), `OtpEntryPage.cs` (line 169, `GetAssemblyVersion` in the listener tag).
