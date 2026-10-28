# SettingsPage.cs

**File:** `src/XIVLauncher.Core/Components/SettingsPage/SettingsPage.cs` · 244 lines · 8,821 bytes

## Purpose

The settings screen: tab list, search box, finish button. It replaces the immediate-mode settings page,
where tab visibility, entry visibility, and finish-button enablement were recomputed every draw frame.
Here those recomputations happen at defined moments: when the page is shown, when the search text changes,
and when any entry reports a value change.

## Declaration

`public class SettingsPage : Page` (line 25), namespace `XIVLauncher.Core.Components.SettingsPage`.
Constructor `SettingsPage(LauncherApp app) : base(app)` (line 62).

## Members

| Member | Line | Behavior |
| --- | --- | --- |
| `tabs` | 27, `BuildTabs()` 33–54 | Built once. Game, Patching, Dalamud, About, Debug always; **Wine (lines 41–42) and Troubleshooting (lines 50–51) are appended only on Linux** — their entries reference Unix-only state (`Program.CompatibilityTools` is null on Windows), so building them there would dereference Unix state. |
| `tabControl` | 56 | Eto `TabControl`. |
| `searchBox` | 57, 64–69 | `TextBox` with `Strings.SearchForSettings` placeholder, `MaxLength = 100`, `Width = 250`. |
| `finishButton` | 58, 77–78 | `Common.Button`, width 40, icon `FontAwesomeIcon.Check`. |
| `searchInput` | 60 | Last committed search text; `string.Empty` when the page is shown. |

- `searchBox.TextChanged` (71–75): stores `searchInput`, calls `RebuildPages()`.
- `finishButton.Click` (80–88): `Save()` on every tab, then `App.State = LauncherState.Main`.
- Every entry in every tab is subscribed to `UpdateFinishButton` (90–92) at construction.
- Windows-only block (lines 94–100): `Multiline` is set to `true` via reflection on the WinForms `TabControl` control (lines 98–99), because six tab titles overflow a single strip row and Eto exposes no `Multiline` property. No explicit width is set: `DynamicLayout` stretches the tab control to the layout width (comment lines 96–97).
- Content (102–126): `pageLayout` is a `DynamicLayout` with `Padding = new Padding(10)`, `Spacing = new Size(10, 10)`. The search row (110–115) and finish row (119–124) each live in their own nested `BeginVertical`/`BeginHorizontal` section — nesting rows directly in one table makes sibling rows share columns, stretching the button to the search box's width (comment 108–109). Each row is `AddSpace(xscale: true)` + `Add(control, xscale: false, yscale: false)`, so the search box (113) and finish button (122) sit at the right edge. The tab control is added expand in both axes (117). `this.Content = pageLayout` (126).

## Lifecycle

`OnShow()` (129–143): `LoadValues()` on all tabs (133–134), clears `searchInput` (136) and `searchBox.Text` (137), `RebuildPages()` (140), then `base.OnShow()` (143). Serilog progress logging at lines 131, 139, 141 (`[settings] OnShow: loading values` / `rebuilding pages` / `pages rebuilt` — this proved the settings page opens in ~27 ms on WinForms).

`VisibleTabs()` (146–153): yields a tab if it is not Unix-exclusive, or the OS is Unix (gate line 150). `SettingsTabWine` is the Unix-exclusive tab, so non-Unix builds hide it.

`RebuildPages()` (155–223):
- Clears `tabControl.Pages` (157).
- Empty search: per visible tab, `RefreshEntries()` (164), then `TabPage { Text = tab.Title, Content = new Scrollable { Content = tab, Border = BorderType.None, ExpandContentHeight = true, ExpandContentWidth = false } }` (165–172), `EnableNativeAutoScroll(page)` (176), `Pages.Add(page)` (177); per-tab Serilog debug lines 162, 178.
- Non-empty search: build one results `DynamicLayout` (spacing `Size(10, 10)`, 183) with `MinimumSize = new Size(500, 500)` (184). For each visible tab, entries whose `Name` contains the trimmed search text (case-insensitive, 189) are re-parented into the results layout, preceded by a grey tab-title label (195), gated on `IsVisible` (199). If nothing matched, a grey `Strings.NoResultsFound` label is added (203). The results go into `TabPage { Text = Strings.SearchResults, Content = new Scrollable { ... } }` (206–214), `EnableNativeAutoScroll(resultPage)` (217), `Pages.Add(resultPage)` (218).
- Calls `UpdateFinishButton()` (221).

`EnableNativeAutoScroll(page)` (228–234, doc comment 224–227): Windows-only — reads the native WinForms `TabPage` control through reflection and sets `AutoScroll = true`. A `TabPage` is a Panel; AutoScroll shows scrollbars when its content exceeds the page. Eto's `Scrollable` cannot be used as the WinForms scrolling mechanism inside a `TabPage` (its `CustomScrollable` layout loop hangs the UI), so the content's `MinimumSize` floor (500×500, set by the tab layout and results layout) is what triggers the native scrollbars. The Eto `Scrollable` page content (165–172, 206–214) is active on both platforms; this flag complements it on Windows.

`UpdateFinishButton()` (237–243): `invalid = any visible entry that is not valid` across all tabs (239; visibility is checked, not search inclusion). Sets `finishButton.IsEnabled = !invalid` (241) and swaps the icon between `Ban` and `Check` (242).

## Consumers

`LauncherApp` owns the page:

| Consumer | Line | Role |
| --- | --- | --- |
| `LauncherApp.cs` | 13 | `using XIVLauncher.Core.Components.SettingsPage;` |
| `LauncherApp.cs` | 76 | `this.setPage.OnShow();` when state enters Settings |
| `LauncherApp.cs` | 113 | `LauncherState.Settings => this.setPage` |
| `LauncherApp.cs` | 130 | `private readonly SettingsPage setPage;` |
| `LauncherApp.cs` | 147 | `this.setPage = new SettingsPage(this);` |

`MainPage` opens settings from the gear action button (`MainPage.cs:59`
`App.State = LauncherApp.LauncherState.Settings`).

Consumes `EtoColors.Grey3` for the grey labels, `Strings.SearchForSettings`, `Strings.SearchResults`,
`Strings.NoResultsFound`, `FontAwesomeIcon.Check`, `FontAwesomeIcon.Ban`,
`Common.Button`, `Page`, `SettingsTab`, `SettingsEntry`, and the seven tab classes
(`SettingsTabWine` and `SettingsTabTroubleshooting` are constructed on Linux only).

## Quirks

- **Entries are re-parented for search.** `RebuildPages()` moves entry controls into the results panel.
  An entry is a `Panel` (a `SettingsEntry<T>` row), so its whole label+input+description row moves. When the
  search is cleared, `RefreshEntries()` re-parents each entry into its own tab's fresh `DynamicLayout`; Eto/GTK
  reparents widgets, so a row is always attached to exactly one parent.
- **Search matches names only, visibility checked at build time.** Entries are matched by `Name`, and only
  entries currently reporting `IsVisible` are shown in results. A row that becomes visible later is not added
  until the next rebuild (search change, page show).
- **Finish-button validity ignores search.** `UpdateFinishButton()` scans all tabs' entries, including ones
  not shown in the results tab. A hidden invalid entry (for example, an empty game path on a hidden row)
  blocks saving even when the user cannot see it.
- **`Save()` runs on all tabs, visible or not.** The finish button saves every tab in the `tabs` array,
  including tabs hidden by `VisibleTabs()`. A Windows build saving Wine settings, for example, writes them
  to config.
- **Pages are rebuilt on every search keystroke.** `TextChanged` fires per keystroke, and each rebuild
  clears all tab pages, calls `RefreshEntries()` (fresh `DynamicLayout` per tab), and recreates `TabPage` +
  `Scrollable` wrappers. The tab control's selection resets to the first tab as a side effect. Serilog
  debug lines 162, 178 trace the rebuilds.
- **Search box text is cleared on show.** Leaving settings and re-entering resets `searchInput` and
  `searchBox.Text` before rebuild, so a saved search filter never persists.
- **Entry `Changed` subscriptions are permanent.** Each entry's `Changed` event is wired to
  `UpdateFinishButton` once, at construction; there is no unsubscribe. The entries outlive page rebuilds.
- **`Environment.OSVersion.Platform` decides Unix-exclusivity per rebuild**, so the Wine tab's presence can
  change only if the OS changes (which cannot happen at runtime).
- **`Common.Button` empty label + icon** renders the glyph as the button text; the finish button has no
  text label.
- **Tab control content wrappers are recreated each rebuild**: `TabPage.Content` is a fresh
  `Scrollable` wrapper on both platforms (165–172, 206–214), so scrolling state (scroll position) is
  reset on every rebuild. On Windows the native `AutoScroll` flag (`EnableNativeAutoScroll`, 228) complements
  it: Eto WinForms' `CustomScrollable` layout loop hangs the UI thread inside a `TabPage`
  (`SettingsTab.cs.md` §6), so scrolling relies on the native WinForms Panel scrollbar.
- **The Windows tab strip is multiline** (lines 94–100): `Multiline` is set through reflection on the
  underlying WinForms control (98–99) — Eto has no `Multiline` property, and six tab titles do not fit one
  strip row. No explicit width: the `DynamicLayout` stretches the tab control to the layout width, so the
  strip follows window resize.
