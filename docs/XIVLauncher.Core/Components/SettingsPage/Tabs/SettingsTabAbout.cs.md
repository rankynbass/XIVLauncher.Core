# SettingsTabAbout.cs — Specification

**File:** `src/XIVLauncher.Core/Components/SettingsPage/Tabs/SettingsTabAbout.cs` · 73 lines · 2,386 bytes
**Namespace:** `XIVLauncher.Core.Components.SettingsPage.Tabs` · **Declaration:** `public class SettingsTabAbout : SettingsTab` (line 16)
**Role:** About tab — logo image, version line, contributors link, repository/Discord/license buttons

---

## 1. Purpose

Replaces the immediate-mode About tab. The SDL GPU texture becomes an embedded Eto `ImageView`; the selectable contributor text becomes a `LinkButton`; the three action buttons become plain Eto `Button`s.

Usings: `Eto.Drawing` (`Size`), `Eto.Forms`, `XIVLauncher.Common.Util` (`PlatformHelpers`), `XIVLauncher.Core.Resources.Localization` (`Strings`).

## 2. Surface

| Member | Kind | Line | Notes |
| --- | --- | --- | --- |
| `versionLabel` | `private readonly Label` | 18 | version string, set once in ctor |
| `contributorsLink` | `private readonly LinkButton` | 19 | wrapped link, click opens contributors page |
| `Entries` | `override SettingsEntry[]` | 21 | **empty** — the tab has no settings rows |
| `Title` | `override string` | 23 | `Strings.AboutTitle` (`Strings.Designer.cs:84`) |
| ctor | `public` | 25-32 | version text, contributor text + wrap + click |
| `HeaderContent` | `override List<Control>` | 34-43 | ImageView + version label + contributors link |
| `FooterContent` | `override List<Control>` | 45-57 | horizontal `StackLayout` of three buttons |
| `CreateBrowserButton` | `private static Button` | 59-72 | button factory with url/license branch |

`RefreshDynamic()` is not overridden.

## 3. Construction

- `versionLabel.Text = $"XIVLauncher Core v{AppUtil.GetAssemblyVersion()}({AppUtil.GetGitHash()})"` (line 27) — built once at tab construction, so the version/hash line never changes while the process runs (`AppUtil.cs.md` §3).
- `contributorsLink.Text = string.Format(Strings.XLCoreCreatedBy, "goaaats, Blooym, rankynbass")` (line 29), `Wrap = WrapMode.Character` (30), `Click += (_, _) => AppUtil.OpenBrowser("https://github.com/goatcorp/XIVLauncher.Core/graphs/contributors")` (31).

## 4. Header and footer content

`HeaderContent` (34-43) returns, per access:

1. `new ImageView { Image = AppImages.FromResource("logo.png"), Size = new Size(256, 256) }` (36-40) — a **new** image view and a **new** `Bitmap` decode on every rebuild (`AppImages.cs.md`).
2. `this.versionLabel` (41).
3. `this.contributorsLink` (42).

`FooterContent` (45-57) returns a **new** horizontal `StackLayout` (47-49) containing three freshly built buttons (52-54):

| Button | Label | URL |
| --- | --- | --- |
| repository | `Strings.OpenRepositoryButton` | `https://github.com/goatcorp/XIVLauncher.Core` |
| Discord | `Strings.JoinDiscordButton` | `https://discord.gg/3NMcUV5` |
| licenses | `Strings.SeeSoftwareLicensesButton` | `null` → license branch |

## 5. `CreateBrowserButton` (59-72)

`new Button { Text = label }` (61); click handler (63-69): `url is not null` → `AppUtil.OpenBrowser(url)` (66); otherwise `PlatformHelpers.OpenBrowser(Path.Combine(AppContext.BaseDirectory, "license.txt"))` (68). `AppUtil.OpenBrowser` includes the Steam Deck branch (Steam overlay) and the generic `PlatformHelpers.OpenBrowser` path (`AppUtil.cs.md` §4); the license branch calls `PlatformHelpers` directly, so it never uses the Steam overlay.

## 6. Consumers

| Consumer | Line | Use |
| --- | --- | --- |
| `SettingsPage.tabs` | `SettingsPage.cs:30` | fifth tab in the fixed array |
| `SettingsPage.OnShow` | `SettingsPage.cs:88` | `tab.LoadValues()` — no entries, so no values are loaded |
| `SettingsPage.RebuildPages` | `SettingsPage.cs:115-116` | `RefreshEntries()` + `TabPage(Text = Title, Scrollable(tab))` |
| `SettingsPage.RebuildPages` | `SettingsPage.cs:126` | `tab.Entries` search filter — always empty, so About never matches a search |

## 7. Quirks

- **No entries** (`Entries => []`, line 21): the About tab contributes nothing to search results, and `UpdateFinishButton` sees no rows from it (`SettingsPage.cs.md` §6).
- `HeaderContent`/`FooterContent` build new widgets per access, so each `RefreshEntries()` allocates a new ImageView (decoding `logo.png` again) and three new buttons; the previous instances are dropped (`SettingsTab.cs.md` §6).
- The license button opens `AppContext.BaseDirectory/license.txt`; `license.txt` is not copied by `XIVLauncher.Core.csproj` and is absent from the build output, so the link targets a missing file. The same behavior exists in HEAD (`git show HEAD:.../SettingsTabAbout.cs` line 68), so it is pre-existing.
- The version string is computed once at construction (`AppUtil.GetAssemblyVersion()`, `AppUtil.GetGitHash()`), not per rebuild, so a rebuild does not re-run git/build metadata lookups.
- `RefreshDynamic()` is unused; nothing on this tab depends on other settings.

## 8. Cross-references

Uses: `SettingsTab` (`SettingsTab.cs.md`), `AppImages` (`AppImages.cs.md`), `AppUtil` (`AppUtil.cs.md`), `Strings` (`Resources/Localization/Strings.Designer.cs.md`), `PlatformHelpers` (lib: `lib/FFXIVQuickLauncher/src/XIVLauncher.Common/PlatformAbstractions/PlatformHelpers.cs`, out of docs scope).
Referenced by: `SettingsPage.cs.md` §2.
