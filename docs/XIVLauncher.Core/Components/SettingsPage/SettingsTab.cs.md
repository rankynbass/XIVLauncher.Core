# SettingsTab.cs — Specification

**File:** `src/XIVLauncher.Core/Components/SettingsPage/SettingsTab.cs` · 75 lines · 2,262 bytes
**Namespace:** `XIVLauncher.Core.Components.SettingsPage` · **Declaration:** `public abstract class SettingsTab : Component` (line 13)
**Role:** Settings tab container — entry list, tab-specific header/footer widgets, and a rebuildable, scrollable layout

---

## 1. Purpose

Replaces the immediate-mode tab, which drew each visible entry every frame. A tab is an Eto `Panel` (via `Component`, `Components/Component.cs.md`): its entries and header/footer widgets are built once, and the tab rebuilds its layout (`RefreshEntries`) when the settings page shows, so visibility checks (which depend on other settings) run at the same point they used to.

Each rebuild wraps the vertical stack in Eto `Scrollable`, so tabs with many rows can scroll inside the settings page.

Using: `Eto.Forms` (`StackLayout`, `StackLayoutItem`, `Scrollable`, `Control`).

## 2. Surface

| Member | Kind | Line | Notes |
| --- | --- | --- | --- |
| `Entries` | `public abstract SettingsEntry[]` | 15 | the tab's rows, built in the tab ctor |
| `IsUnixExclusive` | `public virtual bool` | 17 | default `false`; hidden on non-Unix platforms |
| `Title` | `public abstract string` | 19 | tab caption + search-results caption |
| `HeaderContent` | `protected virtual List<Control>` | 22 | widgets above entries; default empty |
| `FooterContent` | `protected virtual List<Control>` | 25 | widgets below entries; default empty |
| `LoadValues()` | `public` | 27-33 | calls `LoadValues()` on every entry (line 31) |
| `Save()` | `public virtual` | 35-41 | calls `Save()` on every entry (line 39) |
| `RefreshEntries()` | `public` | 44-71 | rebuilds `Content` (see §4) |
| `RefreshDynamic()` | `protected virtual` | 74 | hook for tab state that depends on other settings; empty base |

## 3. Value load/save flow

`SettingsPage.OnShow()` calls `tab.LoadValues()` for every tab in the fixed array, including tabs hidden by `IsUnixExclusive` (`SettingsPage.cs.md` §5). The finish button's click handler calls `tab.Save()` for every tab, then `App.State = Main` (`SettingsPage.cs:59-67`). `Save` is virtual so tabs with extra widgets can persist them; tabs that only store entries rely on the base loop.

## 4. `RefreshEntries()`

Order inside `RefreshEntries` (44-68):

1. `this.RefreshDynamic()` (46) — tab-specific state refresh first.
2. `var items = new List<StackLayoutItem>()` (48).
3. Every `HeaderContent` control → `StackLayoutItem(control, expand: true)` (50-51).
4. Every entry with `IsVisible` true → `StackLayoutItem(settingsEntry, expand: true)` (53-57).
5. Every `FooterContent` control → `StackLayoutItem(control, expand: true)` (59-60).
6. `new StackLayout { Spacing = 10 }` (62), items added in a loop (64-65).
7. `this.Content = new Scrollable { Content = layout }` (67-70).

All stack items expand, so entries and header/footer widgets take the full tab width; vertical spacing is 10 px. The outer `Scrollable` provides tab scrolling; the settings page also wraps each tab page in `Scrollable` (`SettingsPage.cs:116`).

## 5. Consumers

| Consumer | Line(s) | Uses |
| --- | --- | --- |
| `SettingsPage.tabs` | `SettingsPage.cs:24-33` | seven tabs: Game, Patching, Wine, Dalamud, About, Debug, Troubleshooting (`Tabs/*.md`) |
| `SettingsPage.OnShow` | `SettingsPage.cs:86-88` | `tab.LoadValues()` |
| `SettingsPage.finishButton.Click` | `SettingsPage.cs:61-63` | `tab.Save()` |
| `SettingsPage.VisibleTabs` | `SettingsPage.cs:102` | `tab.IsUnixExclusive` |
| `SettingsPage.RebuildPages` | `SettingsPage.cs:115` | `tab.RefreshEntries()` |
| `SettingsPage.RebuildPages` | `SettingsPage.cs:116` | `new TabPage { Text = tab.Title, Content = new Scrollable { Content = tab } }` |
| `SettingsPage.RebuildPages` | `SettingsPage.cs:126, 133` | `tab.Entries` search filter, `tab.Title` caption |
| `SettingsPage.UpdateFinishButton` | `SettingsPage.cs:151` | `tab.Entries` validity scan |

Tab subclasses override `Entries`, `Title`, `HeaderContent`, `FooterContent`, `RefreshDynamic`: see `Tabs/SettingsTabGame.cs.md`, `Tabs/SettingsTabPatching.cs.md`, `Tabs/SettingsTabWine.cs.md`, `Tabs/SettingsTabDalamud.cs.md`, `Tabs/SettingsTabAbout.cs.md`, `Tabs/SettingsTabDebug.cs.md`, `Tabs/SettingsTabTroubleshooting.cs.md`.

## 6. Quirks

- **Header/footer widgets can be rebuilt per access.** `HeaderContent`/`FooterContent` are expression-bodied properties; tabs that construct widgets inside the property (About at `SettingsTabAbout.cs:34-49`/`45-57`, Debug at `SettingsTabDebug.cs:38`) create fresh `Control` objects on every `RefreshEntries()` call, so previous ImageView/StackLayout/Button instances are dropped and recreated. Tabs that keep widgets in fields and return them (Troubleshooting at `SettingsTabTroubleshooting.cs:122`/`124`, Wine at `SettingsTabWine.cs:150`, Game at `SettingsTabGame.cs:74`) reuse the same instances.
- Rebuilds reparent entry panels; an entry that is currently inside the search-results panel is pulled back into its tab when the search is cleared (`SettingsPage.cs.md` §6).
- `LoadValues()` is not virtual, so a tab cannot add extra load logic beyond its entries; `Save()` is virtual.
- `RefreshEntries()` allocates a new `StackLayout`, new `StackLayoutItem`s, and a new `Scrollable` on every call; the tab's `Content` is replaced, and the old layout/scroll widget is discarded. The page wrapper also creates its own `Scrollable` for each tab page, so tab content can be nested inside two scroll containers.
- `Component.Id` (`Components/Component.cs.md` §3) is inherited and unused.

## 7. Cross-references

Uses: `Component` (`Components/Component.cs.md`), `SettingsEntry` (`SettingsEntry.cs.md`).
Referenced by: `SettingsPage.cs.md` and the seven tab specs under `Components/SettingsPage/Tabs/`.
