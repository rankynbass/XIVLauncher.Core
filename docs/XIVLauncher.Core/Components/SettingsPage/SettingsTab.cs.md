# SettingsTab.cs — Specification

**File:** `src/XIVLauncher.Core/Components/SettingsPage/SettingsTab.cs` · 79 lines · 2,392 bytes
**Namespace:** `XIVLauncher.Core.Components.SettingsPage` · **Declaration:** `public abstract class SettingsTab : Component` (line 16)
**Role:** Settings tab container — entry list, tab-specific header/footer widgets, and a rebuildable `DynamicLayout` with a 500×500 content floor

---

## 1. Purpose

Replaces the immediate-mode tab, which drew each visible entry every frame. A tab is an Eto `Panel` (via `Component`, `Components/Component.cs.md`): its entries and header/footer widgets are built once, and the tab rebuilds its layout (`RefreshEntries`) when the settings page shows, so visibility checks (which depend on other settings) run at the same point they used to.

Each rebuild replaces the tab's `Content` with a fresh `DynamicLayout` (line 74) whose `MinimumSize` floor is 500×500 (line 72) and whose `Padding` is 10 px on all edges (line 56); scrolling is not the tab's job — the settings page puts each tab inside a `Scrollable` tab page and enables native `AutoScroll` on Windows (see §4, §6).

Using: `System.Runtime.InteropServices` (import left over from the removed `WrapScrollable` gate; `RuntimeInformation` is no longer referenced here), `Eto.Forms` (`DynamicLayout`, `Control`), `Eto.Drawing` (`Size`, `Padding`).

## 2. Surface

| Member | Kind | Line | Notes |
| --- | --- | --- | --- |
| `Entries` | `public abstract SettingsEntry[]` | 18 | the tab's rows, built in the tab ctor |
| `IsUnixExclusive` | `public virtual bool` | 20 | default `false`; hidden on non-Unix platforms |
| `Title` | `public abstract string` | 22 | tab caption + search-results caption |
| `HeaderContent` | `protected virtual List<Control>` | 25 | widgets above entries; default empty |
| `FooterContent` | `protected virtual List<Control>` | 28 | widgets below entries; default empty |
| `LoadValues()` | `public` | 30-36 | calls `LoadValues()` on every entry (line 34) |
| `Save()` | `public virtual` | 38-44 | calls `Save()` on every entry (line 42) |
| `RefreshEntries()` | `public` | 47-75 | rebuilds `Content` (see §4) |
| `RefreshDynamic()` | `protected virtual` | 78 | hook for tab state that depends on other settings; empty base |

## 3. Value load/save flow

`SettingsPage.OnShow()` calls `tab.LoadValues()` for every tab in the fixed array, including tabs hidden by `IsUnixExclusive` (`SettingsPage.cs.md` §5). The finish button's click handler calls `tab.Save()` for every tab, then `App.State = Main` (`SettingsPage.cs:80-88`). `Save` is virtual so tabs with extra widgets can persist them; tabs that only store entries rely on the base loop.

## 4. `RefreshEntries()`

Order inside `RefreshEntries` (47–75):

1. `this.RefreshDynamic()` (49) — tab-specific state refresh first.
2. `var layout = new DynamicLayout { Spacing = new Size(10, 10), Padding = new Padding(10) }` (52–56; comment 51: "10 px edge padding so controls sit clear of the tab borders").
3. Every `HeaderContent` control → `layout.Add(control)` (58–59).
4. Every entry with `IsVisible` true → `layout.Add(settingsEntry)` (61–65).
5. Every `FooterContent` control → `layout.Add(control)` (67–68).
6. `layout.MinimumSize = new Size(500, 500)` (72; comment 70–71: "drives the Scrollable's vertical scrollbars. Below this size, the tab content scrolls instead of being clipped").
7. `this.Content = layout` (74).

`DynamicLayout` adds expand by default, so entries and header/footer widgets take the full width; spacing is 10 px both axes, and `Padding = new Padding(10)` keeps every control 10 px clear of the tab edges. The tab itself never scrolls: the settings page puts the tab inside a `Scrollable` tab page and enables native `AutoScroll` on Windows (`SettingsPage.cs.md` §4/§6). The 500×500 floor is what makes the scrollbars engage in a small window.

## 5. Consumers

| Consumer | Line(s) | Uses |
| --- | --- | --- |
| `SettingsPage.tabs` | `SettingsPage.cs:27, 33-53` | `BuildTabs()`; Windows builds five (Game 37, Patching 38, Dalamud 44, About 45, Debug 46) — Wine (42) and Troubleshooting (51) are appended on Linux only (`Tabs/*.md`) |
| `SettingsPage.OnShow` | `SettingsPage.cs:133-134` | `tab.LoadValues()` |
| `SettingsPage.finishButton.Click` | `SettingsPage.cs:82-84` | `tab.Save()` |
| `SettingsPage.VisibleTabs` | `SettingsPage.cs:150` | `tab.IsUnixExclusive` gate |
| `SettingsPage.RebuildPages` | `SettingsPage.cs:164` | `tab.RefreshEntries()` |
| `SettingsPage.RebuildPages` | `SettingsPage.cs:165-176` | `new TabPage { Text = tab.Title, Content = new Scrollable { Content = tab, Border = BorderType.None, ExpandContentHeight = true, ExpandContentWidth = false } }`, `EnableNativeAutoScroll(page)`, `Pages.Add(page)` |
| `SettingsPage.RebuildPages` | `SettingsPage.cs:189, 195, 199` | `tab.Entries` search filter, `tab.Title` caption, `IsVisible` gate |
| `SettingsPage.UpdateFinishButton` | `SettingsPage.cs:239` | `tab.Entries` validity scan |

Tab subclasses override `Entries`, `Title`, `HeaderContent`, `FooterContent`, `RefreshDynamic`: see `Tabs/SettingsTabGame.cs.md`, `Tabs/SettingsTabPatching.cs.md`, `Tabs/SettingsTabWine.cs.md`, `Tabs/SettingsTabDalamud.cs.md`, `Tabs/SettingsTabAbout.cs.md`, `Tabs/SettingsTabDebug.cs.md`, `Tabs/SettingsTabTroubleshooting.cs.md`.

## 6. Quirks

- **Header/footer widgets can be rebuilt per access.** `HeaderContent`/`FooterContent` are expression-bodied properties; tabs that construct widgets inside the property (About at `SettingsTabAbout.cs:35-44`/`46-72`, Debug at `SettingsTabDebug.cs:38`) create fresh `Control` objects on every `RefreshEntries()` call, so previous ImageView/DynamicLayout/Button instances are dropped and recreated. Tabs that keep widgets in fields and return them (Troubleshooting at `SettingsTabTroubleshooting.cs:122`/`124`, Wine at `SettingsTabWine.cs:150`, Game at `SettingsTabGame.cs:69`) reuse the same instances.
- Rebuilds reparent entry panels; an entry that is currently inside the search-results panel is pulled back into its tab when the search is cleared (`SettingsPage.cs.md` §6).
- `LoadValues()` is not virtual, so a tab cannot add extra load logic beyond its entries; `Save()` is virtual.
- `RefreshEntries()` allocates a new `DynamicLayout` on every call; the tab's `Content` is replaced, and the old layout is discarded. The settings page also recreates its `Scrollable` tab pages per rebuild (`SettingsPage.cs:165-176`), so scroll position resets on every page show/search change; there is one scroll container per tab, not nested pairs.
- **Eto WinForms' `Scrollable` (`CustomScrollable`) hangs inside a `TabPage`** (`OnLayout` → `UpdateScrollSize` → setting the content `MinimumSize` → resize event → `PerformLayout` → `OnLayout` again — infinite layout loop, UI thread never returns to the message loop; verified via `dotnet-dump` + `clrstack`), which is why the tab no longer wraps its own content and why Windows scrolling is done through native `AutoScroll` on the `TabPage` handler (`EnableNativeAutoScroll`, `SettingsPage.cs:228`). The `Scrollable` tab-page wrapper in `SettingsPage.cs` is active on both platforms; the native flag complements it on Windows. The `using System.Runtime.InteropServices` at line 1 is a leftover of the removed `WrapScrollable` gate.
- `Component.Id` (`Components/Component.cs.md` §3) is inherited and unused.

## 7. Cross-references

Uses: `Component` (`Components/Component.cs.md`), `SettingsEntry` (`SettingsEntry.cs.md`).
Referenced by: `SettingsPage.cs.md` and the seven tab specs under `Components/SettingsPage/Tabs/`.
