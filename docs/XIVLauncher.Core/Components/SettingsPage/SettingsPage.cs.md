# SettingsPage.cs

**File:** `src/XIVLauncher.Core/Components/SettingsPage/SettingsPage.cs` · 195 lines · 7,208 bytes

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
- Windows-only block (lines 94–103): `tabControl.Width = 1000` (line 99) — WinForms `StackLayout` sizes items to their natural width (GTK stretches to full width), so the tab control needs an explicit width matching the rows' `RowLabelWidth`; `Multiline` is set to `true` via reflection on the WinForms `TabControl` control (lines 101–102), because six tab titles overflow a single strip row and Eto exposes no `Multiline` property.
- Content (105–116): vertical `StackLayout`, `Padding = 10`, spacing 10: search box centered, tab control centered with `Expand = true`, finish button centered.

## Lifecycle

`OnShow()` (118–133): `LoadValues()` on all tabs, clears `searchInput` and `searchBox.Text`,
`RebuildPages()`, then `base.OnShow()`. Serilog progress logging at lines 120, 128, 130
(`[settings] OnShow: loading values` / `rebuilding pages` / `pages rebuilt` — this proved the
settings page opens in ~27 ms on WinForms).

`VisibleTabs()` (135–142): yields a tab if it is not Unix-exclusive, or the OS is Unix.
`SettingsTabWine` is the Unix-exclusive tab, so non-Unix builds hide it.

`RebuildPages()` (144–186):
- Clears `tabControl.Pages`.
- Empty search: `RefreshEntries()` per visible tab, then add `TabPage { Text = tab.Title, Content = SettingsTab.WrapScrollable(tab) }` (line 154) — `WrapScrollable` returns a `Scrollable` on Linux, a plain `Panel` on Windows (`SettingsTab.cs.md`); per-tab Serilog debug lines 152, 155.
- Non-empty search: build one results `StackLayout` (spacing 10). For each visible tab, entries whose `Name`
  contains the trimmed search text (case-insensitive) are re-parented into the results layout, preceded by a
  grey tab-title label. If nothing matched, a grey `Strings.NoResultsFound` label is added. The results layout
  goes into `TabPage { Text = Strings.SearchResults, Content = SettingsTab.WrapScrollable(results) }` (line 182).
- Calls `UpdateFinishButton()` (line 185).

`UpdateFinishButton()` (188–194): `invalid = any visible entry that is not valid` across all tabs
(visibility is checked, not search inclusion). Sets `finishButton.IsEnabled = !invalid` and swaps the icon
between `Ban` and `Check`.

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
  search is cleared, `RefreshEntries()` re-parents each entry into its own tab's fresh `StackLayout`; Eto/GTK
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
  clears all tab pages, calls `RefreshEntries()` (fresh `StackLayout` per tab), and recreates `TabPage` +
  `WrapScrollable` wrappers. The tab control's selection resets to the first tab as a side effect.
- **Search box text is cleared on show.** Leaving settings and re-entering resets `searchInput` and
  `searchBox.Text` before rebuild, so a saved search filter never persists.
- **Entry `Changed` subscriptions are permanent.** Each entry's `Changed` event is wired to
  `UpdateFinishButton` once, at construction; there is no unsubscribe. The entries outlive page rebuilds.
- **`Environment.OSVersion.Platform` decides Unix-exclusivity per rebuild**, so the Wine tab's presence can
  change only if the OS changes (which cannot happen at runtime).
- **`Common.Button` empty label + icon** renders the glyph as the button text; the finish button has no
  text label.
- **Tab control content wrappers are recreated each rebuild**: `TabPage.Content` is a fresh
  `SettingsTab.WrapScrollable` wrapper — a `Scrollable` on Linux, a plain `Panel` on Windows (Eto
  WinForms' `CustomScrollable` loops forever inside a `TabPage`, so scrolling is not available
  there; `SettingsTab.cs.md`), so scrolling state (scroll position) is reset on every rebuild.
- **The Windows tab strip is width-pinned and multiline** (lines 94–103): `Width = 1000` matches
  the rows' `RowLabelWidth` because WinForms `StackLayout` sizes to natural width, and `Multiline`
  is set through reflection on the underlying WinForms control — Eto has no `Multiline` property,
  and six tab titles do not fit one strip row.
