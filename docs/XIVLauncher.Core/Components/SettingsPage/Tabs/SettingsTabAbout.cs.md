# SettingsTabAbout.cs — Specification

**File:** `src/XIVLauncher.Core/Components/SettingsPage/Tabs/SettingsTabAbout.cs` · 101 lines · 3,373 bytes
**Namespace:** `XIVLauncher.Core.Components.SettingsPage.Tabs` · **Declaration:** `public class SettingsTabAbout : SettingsTab` (line 17)
**Role:** About tab — logo image, version line, contributors link, repository/Discord/license buttons, and the Generate TSPack log-pack button

---

## 1. Purpose

Replaces the immediate-mode About tab. The SDL GPU texture becomes an embedded Eto `ImageView`; the selectable contributor text becomes a `LinkButton`; the three action buttons become plain Eto `Button`s. The About tab also carries the Troubleshooting tab's log-pack button, since About is available on Windows, where the Troubleshooting tab is not built.

Usings: `Eto.Drawing` (`Size`), `Eto.Forms`, `XIVLauncher.Common.Util` (`PlatformHelpers`), `XIVLauncher.Core.Resources.Localization` (`Strings`), `XIVLauncher.Core.Support` (`PackGenerator`).

## 2. Surface

| Member | Kind | Line | Notes |
| --- | --- | --- | --- |
| `versionLabel` | `private readonly Label` | 19 | version string, set once in ctor |
| `contributorsLink` | `private readonly LinkButton` | 20 | wrapped link, click opens contributors page |
| `Entries` | `override SettingsEntry[]` | 22 | **empty** — the tab has no settings rows |
| `Title` | `override string` | 24 | `Strings.AboutTitle` (`Strings.Designer.cs:84`) |
| ctor | `public` | 26-33 | version text, contributor text + wrap + click |
| `HeaderContent` | `override List<Control>` | 35-44 | ImageView + version label + contributors link |
| `FooterContent` | `override List<Control>` | 46-72 | horizontal `StackLayout` of three buttons, TSPack `StackLayout`, grey TSPack caption label |
| `CreateTSPackButton` | `private static Button` | 74-85 | log-pack button factory (see §6) |
| `CreateBrowserButton` | `private static Button` | 87-100 | button factory with url/license branch |

`RefreshDynamic()` is not overridden.

## 3. Construction

- `versionLabel.Text = $"XIVLauncher Core v{AppUtil.GetAssemblyVersion()}({AppUtil.GetGitHash()})"` (line 28) — built once at tab construction, so the version/hash line never changes while the process runs (`AppUtil.cs.md` §3).
- `contributorsLink.Text = string.Format(Strings.XLCoreCreatedBy, "goaaats, Blooym, rankynbass")` (line 30), `Wrap = WrapMode.Character` (31), `Click += (_, _) => AppUtil.OpenBrowser("https://github.com/goatcorp/XIVLauncher.Core/graphs/contributors")` (32).

## 4. Header and footer content

`HeaderContent` (35-44) returns, per access:

1. `new ImageView { Image = AppImages.FromResource("logo.png"), Size = new Size(256, 256) }` (37-41) — a **new** image view and a **new** `Bitmap` decode on every rebuild (`AppImages.cs.md`).
2. `this.versionLabel` (42).
3. `this.contributorsLink` (43).

`FooterContent` (46-72) returns, per access:

1. A **new** horizontal `StackLayout` (48-57) containing three freshly built buttons (53-55):

| Button | Label | URL |
| --- | --- | --- |
| repository | `Strings.OpenRepositoryButton` | `https://github.com/goatcorp/XIVLauncher.Core` |
| Discord | `Strings.JoinDiscordButton` | `https://discord.gg/3NMcUV5` |
| licenses | `Strings.SeeSoftwareLicensesButton` | `null` → license branch |

2. A **new** horizontal `StackLayout` (61-65) containing `CreateTSPackButton()` (64) — the same action as the Troubleshooting tab's log-pack button, placed here because About is available on Windows, where the Troubleshooting tab is not built (comment at lines 59-60).
3. A grey `Strings.GenerateTSPackTroubleshooting` caption label with `WrapMode.Character` (66-71).

## 5. `CreateBrowserButton` (87-100)

`new Button { Text = label }` (89); click handler (91-97): `url is not null` → `AppUtil.OpenBrowser(url)` (94); otherwise `PlatformHelpers.OpenBrowser(Path.Combine(AppContext.BaseDirectory, "license.txt"))` (96). `AppUtil.OpenBrowser` includes the Steam Deck branch (Steam overlay) and the generic `PlatformHelpers.OpenBrowser` path (`AppUtil.cs.md` §4); the license branch calls `PlatformHelpers` directly, so it never uses the Steam overlay.

## 6. `CreateTSPackButton` (74-85)

`new Button { Text = Strings.GenerateTSPackTroubleshootingButton }` (76); click handler (78-82): `PackGenerator.SavePack(Program.storage)` (80) writes the log pack, then `PlatformHelpers.OpenBrowser(Path.Combine(Program.storage.GetFolder("logs").FullName))` (81) opens the logs folder in the file browser. Same code path as the Troubleshooting tab's button (`Support/Troubleshooting.cs.md`); `SavePack` and the logs-folder open are cross-platform, so the button works on Windows. Verified live: clicking it writes `trouble-*.tspack` files into `%APPDATA%/xlcore/logs/`.

## 7. Consumers

| Consumer | Line | Use |
| --- | --- | --- |
| `SettingsPage.tabs` | `SettingsPage.cs:45` | added in `BuildTabs()` on all platforms (Windows has no Troubleshooting tab, so About carries its button) |
| `SettingsPage.OnShow` | `SettingsPage.cs:139-140` | `tab.LoadValues()` — no entries, so no values are loaded |
| `SettingsPage.RebuildPages` | `SettingsPage.cs:170, 171-178` | `RefreshEntries()` + `TabPage(Text = Title, Scrollable(tab))` |
| `SettingsPage.RebuildPages` | `SettingsPage.cs:195` | `tab.Entries` search filter — always empty, so About never matches a search |

## 8. Quirks

- **No entries** (`Entries => []`, line 22): the About tab contributes nothing to search results, and `UpdateFinishButton` sees no rows from it (`SettingsPage.cs.md` §6).
- `HeaderContent`/`FooterContent` build new widgets per access, so each `RefreshEntries()` allocates a new ImageView (decoding `logo.png` again), five new buttons, and a new caption label; the previous instances are dropped (`SettingsTab.cs.md` §6).
- The license button opens `AppContext.BaseDirectory/license.txt`; `license.txt` is not copied by `XIVLauncher.Core.csproj` and is absent from the build output, so the link targets a missing file. The same behavior exists in HEAD (`git show HEAD:.../SettingsTabAbout.cs` line 96), so it is pre-existing.
- The TSPack button duplicates the Troubleshooting tab's log-pack button; the duplication is intentional (comment at lines 59-60), because the Troubleshooting tab is Linux-only in `BuildTabs()` and About is the only Windows-visible place to generate a log pack (`SettingsPage.cs.md` §2).
- The version string is computed once at construction (`AppUtil.GetAssemblyVersion()`, `AppUtil.GetGitHash()`), not per rebuild, so a rebuild does not re-run git/build metadata lookups.
- `RefreshDynamic()` is unused; nothing on this tab depends on other settings.

## 9. Cross-references

Uses: `SettingsTab` (`SettingsTab.cs.md`), `AppImages` (`AppImages.cs.md`), `AppUtil` (`AppUtil.cs.md`), `Strings` (`Resources/Localization/Strings.Designer.cs.md`), `PackGenerator` (`Support/Troubleshooting.cs.md`), `Program.storage`, `PlatformHelpers` (lib: `lib/FFXIVQuickLauncher/src/XIVLauncher.Common/PlatformAbstractions/PlatformHelpers.cs`, out of docs scope).
Referenced by: `SettingsPage.cs.md` §2.
