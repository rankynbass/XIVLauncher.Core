# SettingsTab.cs — Specification

**File:** `src/XIVLauncher.Core/Components/SettingsPage/SettingsTab.cs` · 82 lines · 2,916 bytes
**Namespace:** `XIVLauncher.Core.Components.SettingsPage` · **Declaration:** `public abstract class SettingsTab : Component` (line 16)
**Role:** Settings tab container — entry list, tab-specific header/footer widgets, and a rebuildable layout with a Linux-gated scroll wrapper

---

## 1. Purpose

Replaces the immediate-mode tab, which drew each visible entry every frame. A tab is an Eto `Panel` (via `Component`, `Components/Component.cs.md`): its entries and header/footer widgets are built once, and the tab rebuilds its layout (`RefreshEntries`) when the settings page shows, so visibility checks (which depend on other settings) run at the same point they used to.

Each rebuild wraps the vertical stack in `WrapScrollable` (lines 77–78): a `Scrollable` on Linux, the content itself unchanged on Windows. Tabs with many rows can scroll inside the settings page on Linux; on Windows scrolling is not available (see §6).

Using: `System.Runtime.InteropServices` (`RuntimeInformation`), `Eto.Forms` (`StackLayout`, `StackLayoutItem`, `Scrollable`, `Control`), `Eto.Drawing`.

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
| `RefreshEntries()` | `public` | 47-71 | rebuilds `Content` (see §4) |
| `WrapScrollable(content)` | `internal static Control` | 77-78 | `Scrollable` on Linux, `content` passed through on Windows (see §4) |
| `RefreshDynamic()` | `protected virtual` | 81 | hook for tab state that depends on other settings; empty base |

## 3. Value load/save flow

`SettingsPage.OnShow()` calls `tab.LoadValues()` for every tab in the fixed array, including tabs hidden by `IsUnixExclusive` (`SettingsPage.cs.md` §5). The finish button's click handler calls `tab.Save()` for every tab, then `App.State = Main` (`SettingsPage.cs:80-88`). `Save` is virtual so tabs with extra widgets can persist them; tabs that only store entries rely on the base loop.

## 4. `RefreshEntries()`

Order inside `RefreshEntries` (47–70):

1. `this.RefreshDynamic()` (49) — tab-specific state refresh first.
2. `var items = new List<StackLayoutItem>()` (51).
3. Every `HeaderContent` control → `StackLayoutItem(control, expand: true)` (53–54).
4. Every entry with `IsVisible` true → `StackLayoutItem(settingsEntry, expand: true)` (56–60).
5. Every `FooterContent` control → `StackLayoutItem(control, expand: true)` (62–63).
6. `new StackLayout { Spacing = 10 }` (65), items added in a loop (67–68).
7. `this.Content = WrapScrollable(layout)` (70).

All stack items expand, so entries and header/footer widgets take the full tab width; vertical spacing is 10 px. `WrapScrollable` (77–78) returns `new Scrollable { Content = content }` on Linux (`RuntimeInformation.IsOSPlatform(OSPlatform.Linux)`), and returns `content` unchanged on Windows. The outer wrapper provides tab scrolling on Linux; the settings page also wraps each tab page the same way (`SettingsPage.cs:154, 182`).

## 5. Consumers

| Consumer | Line(s) | Uses |
| --- | --- | --- |
| `SettingsPage.tabs` | `SettingsPage.cs:27, 33-54` | seven tab classes; Windows builds five (Game, Patching, Dalamud, About, Debug) — Wine (41-42) and Troubleshooting (50-51) are appended on Linux only (`Tabs/*.md`) |
| `SettingsPage.OnShow` | `SettingsPage.cs:121-123` | `tab.LoadValues()` |
| `SettingsPage.finishButton.Click` | `SettingsPage.cs:82-84` | `tab.Save()` |
| `SettingsPage.VisibleTabs` | `SettingsPage.cs:139` | `tab.IsUnixExclusive` |
| `SettingsPage.RebuildPages` | `SettingsPage.cs:153` | `tab.RefreshEntries()` |
| `SettingsPage.RebuildPages` | `SettingsPage.cs:154` | `new TabPage { Text = tab.Title, Content = SettingsTab.WrapScrollable(tab) }` |
| `SettingsPage.RebuildPages` | `SettingsPage.cs:165, 172` | `tab.Entries` search filter, `tab.Title` caption |
| `SettingsPage.UpdateFinishButton` | `SettingsPage.cs:190` | `tab.Entries` validity scan |

Tab subclasses override `Entries`, `Title`, `HeaderContent`, `FooterContent`, `RefreshDynamic`: see `Tabs/SettingsTabGame.cs.md`, `Tabs/SettingsTabPatching.cs.md`, `Tabs/SettingsTabWine.cs.md`, `Tabs/SettingsTabDalamud.cs.md`, `Tabs/SettingsTabAbout.cs.md`, `Tabs/SettingsTabDebug.cs.md`, `Tabs/SettingsTabTroubleshooting.cs.md`.

## 6. Quirks

- **Header/footer widgets can be rebuilt per access.** `HeaderContent`/`FooterContent` are expression-bodied properties; tabs that construct widgets inside the property (About at `SettingsTabAbout.cs:35-44`/`46-72`, Debug at `SettingsTabDebug.cs:38`) create fresh `Control` objects on every `RefreshEntries()` call, so previous ImageView/StackLayout/Button instances are dropped and recreated. Tabs that keep widgets in fields and return them (Troubleshooting at `SettingsTabTroubleshooting.cs:122`/`124`, Wine at `SettingsTabWine.cs:150`, Game at `SettingsTabGame.cs:74`) reuse the same instances.
- Rebuilds reparent entry panels; an entry that is currently inside the search-results panel is pulled back into its tab when the search is cleared (`SettingsPage.cs.md` §6).
- `LoadValues()` is not virtual, so a tab cannot add extra load logic beyond its entries; `Save()` is virtual.
- `RefreshEntries()` allocates a new `StackLayout`, new `StackLayoutItem`s, and a new wrapper on every call; the tab's `Content` is replaced, and the old layout/scroll widget is discarded. The page wrapper also creates its own `WrapScrollable` wrapper for each tab page, so on Linux tab content can be nested inside two scroll containers; on Windows neither wrapper scrolls.
- **`WrapScrollable` gates scrolling to Linux because Eto WinForms' `Scrollable` (`CustomScrollable`) hangs inside a `TabPage`:** `OnLayout` → `UpdateScrollSize` → setting the content `MinimumSize` → resize event → `PerformLayout` → `OnLayout` again — an infinite layout loop, and the UI thread never returns to the message loop ("Not Responding"). Verified via `dotnet-dump` + `clrstack` on the settings page. Windows content is returned unwrapped, so long content can overflow without scrolling.
- `Component.Id` (`Components/Component.cs.md` §3) is inherited and unused.

## 7. Cross-references

Uses: `Component` (`Components/Component.cs.md`), `SettingsEntry` (`SettingsEntry.cs.md`).
Referenced by: `SettingsPage.cs.md` and the seven tab specs under `Components/SettingsPage/Tabs/`.
