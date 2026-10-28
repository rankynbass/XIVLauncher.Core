# SettingsPage.cs

**File:** `src/XIVLauncher.Core/Components/SettingsPage/SettingsPage.cs` · 259 lines · 9,515 bytes

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
| `searchBox` | 64–69 | `TextBox` with `Strings.SearchForSettings` placeholder, `MaxLength = 100`, `Width = 250`. |
| `finishButton` | 77–78 | `Common.Button`, width 40, icon `FontAwesomeIcon.Check`. |
| `searchInput` | 60 | Last committed search text; `string.Empty` when the page is shown. |

- `searchBox.TextChanged` (71–75): stores `searchInput` (73), calls `RebuildPages()` (74).
- `finishButton.Click` (80–88): `Save()` on every tab, then `App.State = LauncherState.Main`.
- Every entry in every tab is subscribed at construction (91–98): `Changed += UpdateFinishButton` (93) and
  `Changed += RefreshAllStates` (97). The comment (95–96) explains the wiring: rows are always shown; gates
  flip `Enabled`, and any entry change re-evaluates every row's `CheckEnabled` so checkbox/combo gates
  update live without a page rebuild.

- Windows-only block (101–107): `Multiline` is set to `true` via reflection on the WinForms `TabControl`
  control (105), because six tab titles overflow a single strip row and Eto exposes no `Multiline` property.
  No explicit width is set: `DynamicLayout` stretches the tab control to the layout width (comment 103–104).
- Content (108–132): `pageLayout` is a `DynamicLayout` with `Padding = new Padding(10)`, `Spacing = new Size(10, 10)` (108). The search row (116–121) and finish row (125–130) each live in their own nested `BeginVertical`/`BeginHorizontal` section — nesting rows directly in one table makes sibling rows share columns, stretching the button to the search box's width (comment 114–115). Each row is `AddSpace(xscale: true)` + `Add(control, xscale: false, yscale: false)`, so the search box (119) and finish button (128) sit at the right edge. The tab control is added expand in both axes (123). `this.Content = pageLayout` (132).

## Lifecycle

`OnShow()` (135–150): `LoadValues()` on all tabs (139–140), clears `searchInput` (142) and `searchBox.Text` (143), `RebuildPages()` (146), then `base.OnShow()` (149). Serilog progress logging lines (137, 145, 147: `[settings] OnShow: loading values` / `rebuilding pages` / `pages rebuilt` — this proved the settings page opens in ~27 ms on WinForms).

`VisibleTabs()` (152–159): yields a tab if it is not Unix-exclusive, or the OS is Unix (gate line 157). `SettingsTabWine` is the Unix-exclusive tab, so non-Unix builds hide it.

`RebuildPages()` (161–227):
- Clears `tabControl.Pages` (163).
- Empty search: per visible tab (167), `RefreshEntries()` (170), then `TabPage { Text = tab.Title, Content = new Scrollable { Content = tab, Border = BorderType.None, ExpandContentHeight = true, ExpandContentWidth = false } }` (171–178), `EnableNativeAutoScroll(page)` (182), `Pages.Add(page)` (183); per-tab Serilog debug lines trace the rebuilds.
- Non-empty search: build one results `DynamicLayout` (spacing `Size(10, 10)`, 189–190) with `MinimumSize = new Size(500, 500)` (190). For each visible tab (193), entries whose `Name` contains the trimmed search text (case-insensitive, 195) are re-parented into the results layout, preceded by a grey tab-title label (202), gated on `IsVisible` (205, `results.Add(settingsEntry)` 206). If nothing matched, a grey `Strings.NoResultsFound` label is added (210). The results go into `TabPage { Text = Strings.SearchResults, Content = new Scrollable { ... } }` (212–220), `EnableNativeAutoScroll(resultPage)` (223), `Pages.Add(resultPage)` (224).
- Calls `UpdateFinishButton()` (227).

`EnableNativeAutoScroll(page)` (234–241, doc comment 231–233): Windows-only — reads the native WinForms `TabPage` control through reflection and sets `AutoScroll = true`. A `TabPage` is a Panel; AutoScroll shows scrollbars when its content exceeds the page. Eto's `Scrollable` cannot be used as the WinForms scrolling mechanism inside a `TabPage` (its `CustomScrollable` layout loop hangs the UI), so the content's `MinimumSize` floor (500×500, set by the tab layout and results layout) is what triggers the native scrollbars. The Eto `Scrollable` page content (171–178, 212–220) is active on both platforms; this flag complements it on Windows.

`RefreshAllStates()` (245–250, doc comment 243–244): re-evaluates every entry's validity, warning, and enabled state. Runs on every entry `Changed`; entries whose gate reads another entry's value update live. Loops all tabs (247) and all entries (248), calling `RefreshState()` — `UpdateState(notifyChanged: false)` (`SettingsEntry{T}.cs.md` §State updates), so the sweep never re-raises `Changed`.

`UpdateFinishButton()` (252–258): `invalid = any entry that is visible, enabled, and not valid` across all tabs (254). Sets `finishButton.IsEnabled = !invalid` (256) and swaps the icon between `Ban` and `Check` (257).

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
- **Search matches names only; platform rows filtered at build.** Entries are matched by `Name` (195), and
  only rows reporting `IsVisible` are added to results (205). Platform-gated rows (e.g. the Unix-only game
  config path) never appear on the other OS; conditional gates do not affect search inclusion — a disabled
  row is included when its name matches.
- **Finish-button validity ignores search.** `UpdateFinishButton()` scans all tabs' entries (254), including
  ones not shown in the results tab. Rows that are visible **and enabled** count; disabled and hidden rows
  cannot be edited, so they never block saving.
- **`Save()` runs on all tabs, visible or not.** The finish button saves every tab in the `tabs` array,
  including tabs hidden by `VisibleTabs()`. A Windows build saving Wine settings, for example, writes them
  to config.
- **Pages are rebuilt on every search keystroke.** `TextChanged` fires per keystroke, and each rebuild
  clears all tab pages, calls `RefreshEntries()` (fresh `DynamicLayout` per tab), and recreates `TabPage` +
  `Scrollable` wrappers. The tab control's selection resets to the first tab as a side effect.
- **Gates flip `Enabled` live, not via rebuild.** Every entry's `Changed` runs `RefreshAllStates()` (97,
  245–250): toggling "Use local Dalamud" enables/disables its path row, combo gates in the Wine tab update
  on selection. No page rebuild happens on a gate change — rows are never hidden by conditional gates, only
  disabled; hidden rows are platform-gated and fixed at build.
- **Search box text is cleared on show.** Leaving settings and re-entering resets `searchInput` (142) and
  `searchBox.Text` (143) before rebuild, so a saved search filter never persists.
- **Entry `Changed` subscriptions are permanent.** Each entry's `Changed` event is wired once, at
  construction (93, 97): all entries to `UpdateFinishButton` and `RefreshAllStates`; there is no
  unsubscribe. The entries outlive page rebuilds.
- **`Environment.OSVersion.Platform` decides Unix-exclusivity per rebuild**, so the Wine tab's presence can
  change only if the OS changes (which cannot happen at runtime).
- **`Common.Button` empty label + icon** renders the glyph as the button text; the finish button has no
  text label.
- **Tab control content wrappers are recreated each rebuild**: `TabPage.Content` is a fresh
  `Scrollable` wrapper on both platforms (171–178, 212–220), so scrolling state (scroll position) is
  reset on every rebuild. On Windows the native `AutoScroll` flag (`EnableNativeAutoScroll`, 234) complements
  it: Eto WinForms' `CustomScrollable` layout loop hangs the UI thread inside a `TabPage`
  (`SettingsTab.cs.md` §6), so scrolling relies on the native WinForms Panel scrollbar.
- **The Windows tab strip is multiline** (101–107): `Multiline` is set through reflection on the
  underlying WinForms control (105) — Eto has no `Multiline` property, and six tab titles do not fit one
  strip row. No explicit width: the `DynamicLayout` stretches the tab control to the layout width, so the
  strip follows window resize.
