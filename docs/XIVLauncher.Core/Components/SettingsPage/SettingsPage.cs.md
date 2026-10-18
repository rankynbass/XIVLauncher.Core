# SettingsPage.cs — Specification

**File:** `src/XIVLauncher.Core/Components/SettingsPage/SettingsPage.cs` · 160 lines · 4974 bytes
**Namespace:** `XIVLauncher.Core.Components.SettingsPage` · **Declaration:** `public class SettingsPage : Page`
**Role:** The Settings "tab screen": owns the ImGui tab bar, the seven tabs' life-cycle (load on show / save on finish), a search mode, and the exit lever

---

## 1. Purpose

`SettingsPage` is the `Page` `LauncherApp.cs.md` binds as `setPage` (`LauncherApp.cs:118` field, `LauncherApp.cs:135 this.setPage = new SettingsPage(this);`, `LauncherApp.cs:99` in the `CurrentPage` switch, `LauncherApp.cs:66-67 this.setPage.OnShow();` in the `State` setter). Everything user-editable in the launcher lives behind this class: the seven tab objects are constructed once here, values are read on show and written only when the finish button is pressed.

Three loops share one vocabulary (`SettingsTab.cs.md` §entries loop, `SettingsEntry.cs.md` §`IsVisible`): the tab bar loop, the search loop, and the finish button's validity sweep. Keep new settings reachable from all three by adding a row to a tab (`Tabs/SettingsTab*.cs.md`), not by adding UI here.

## 2. Surface

| Member | Anchor | Kind | Notes |
| --- | --- | --- | --- |
| `tabs` | `:15-:24` | `private readonly SettingsTab[]` **field initializer** | construction order == tab bar order (§3) |
| `searchInput` | `:26` | `private string = ""` | reset in `OnShow()`, written by the search box each frame |
| ctor | `:28-:31` | `public SettingsPage(LauncherApp app) : base(app)` | empty body — `Page`'s protected ctor (`Page.cs.md`) |
| `OnShow()` | `:33-:44` | `override` | loads every tab, clears search, then `base.OnShow()` |
| `Draw()` | `:46-:158` | `override` | §4–§6 |

Uses: `Tabs/` (`SettingsTabGame`, `SettingsTabPatching`, `SettingsTabWine`, `SettingsTabDalamud`, `SettingsTabAbout`, `SettingsTabDebug`, `SettingsTabTroubleshooting`), `SettingsTab.cs.md`, `SettingsEntry.cs.md`, `src/XIVLauncher.Core/Resources/Localization/Strings.Designer.cs` (`SearchResults`, `SearchForSettings`, `NoResultsFound`), `docs/XIVLauncher.Core/ImGuiHelpers.cs.md`, `docs/XIVLauncher.Core/ImGuiColors.cs.md`, `docs/XIVLauncher.Core/FontAwesomeIcon.cs.md`, `Page.cs.md`/`Component.cs.md`.

## 3. Tab list and construction order

```csharp
private readonly SettingsTab[] tabs = { new SettingsTabGame(), new SettingsTabPatching(), new SettingsTabWine(),
                                       new SettingsTabDalamud(), new SettingsTabAbout(), new SettingsTabDebug(),
                                       new SettingsTabTroubleshooting() };
```

- **Field initializer, so the tabs exist at page construction** (`LauncherApp.cs:135`). Every tab's `Entries` are built during those ctors (see `Tabs/SettingsTabGame.cs.md` §entries-as-field-initializer and `Tabs/SettingsTabDalamud.cs.md` §ctor), which in turn evaluate `Program.Config.*` reads (`docs/XIVLauncher.Core/Program.cs.md` §`Config`) — construction ordering (`Program.cs` §init, `LauncherApp.cs:135`) is what makes those reads legal. Do not move tab construction into `OnShow()`: identity of tab objects is what the `Tabs/` specs' cross-row references (`startupTypeSetting.Value`, `enableManualInjection.Value`) and `SettingsPage.cs.md` §save rely on.
- **Array order is the tab bar order** (and, in search mode, the grouping order). Reordering changes what the user sees first, and `Tabs/SettingsTabWine.cs.md` §`IsUnixExclusive` is the one entry that disappears on non-Unix (§4 filter), so an order edit is also a "which tab do I land on first" edit.
- **`tabs` is the only collection of tabs in the package** — there is no registration mechanism; a new tab class must be added to this array *and* imported (`:8 using XIVLauncher.Core.Components.SettingsPage.Tabs;` covers it as long as it lives in the `Tabs` namespace, cf. `Tabs/SettingsTabAbout.cs.md` §namespace).

## 4. `OnShow()` and the load/save lifecycle

```csharp
public override void OnShow()
{
    foreach (var settingsTab in this.tabs) settingsTab.Load();   // SettingsTab.cs.md §Load → entry Load()
    this.searchInput = string.Empty;
    base.OnShow();                                              // Page.OnShow() — empty today
}
```

- **`Load()` on every show** (`LauncherApp.cs` §`State` setter → `setPage.OnShow()`, and `SettingsTab.cs.md` §Load is non-virtual so this is the single load path) means persisted state is re-read whenever the page is entered, and each row's `CheckVisibility`/`IsValid` are recomputed against those fresh values (`SettingsEntry{T}.cs.md` §hooks, §4). The derived-visibility chains in `Tabs/SettingsTabWine.cs.md` and `Tabs/SettingsTabDalamud.cs.md` depend on this.
- **`searchInput` is cleared on show** — entering Settings (or re-entering it via a `State` assignment, which always runs `OnShow()`, see `docs/XIVLauncher.Core/LauncherApp.cs.md` §`State`) resets to browse mode. That is also why leaving Settings is the only way to "clear the search".
- **`base.OnShow()` is a hook-preserver**: `Page.OnShow()` is empty (`Page.cs.md` §4) and `Component` declares no `OnShow`, so today the call is a no-op. Keep it: a future `Page`/`Component` hook must not be skipped by this override, and removing the call is a visible-in-diff decision to make deliberately.
- **Save is not here**: `Save()` runs once per tab, from the finish button only (§6), gated by the validity sweep. Symmetry with `Load()` is *not* structural (see `SettingsTab.cs.md` §Load vs §Save asymmetry), so treat the pair's sites as page policy, not class contract.

## 5. `Draw()` — tab bar, then the finish button, then the search box

### 5.1 Browse mode (default)

`ImGui.BeginTabBar("###settingsTabs")` (id with the `###` empty-display convention, cf. `Common/Checkbox.cs.md`/`Common/Input.cs.md` and `MainPage/NewsFrame.cs.md` §id conventions), then per tab:

1. `if (settingsTab.IsUnixExclusive && Environment.OSVersion.Platform != PlatformID.Unix) continue;` — the only `IsUnixExclusive` consumer for tabs; today it hides exactly `Tabs/SettingsTabWine.cs.md` (`IsUnixExclusive => true`). Note the *legacy* platform test (`Environment.OSVersion.Platform`), used identically in the per-row `CheckVisibility` closures of `Tabs/SettingsTabGame.cs.md`, `Tabs/SettingsTabTroubleshooting.cs.md`, `Tabs/SettingsTabDebug.cs.md` — keep that spelling inside this package even though `Tabs/SettingsTabWine.cs.md`'s row predicates use `RuntimeInformation.IsOSPlatform`, and `docs/XIVLauncher.Core/Program.cs.md` uses that for `IsSteamDeckHardware`.
2. `ImGui.BeginTabItem(settingsTab.Title)` — the tab's *localized* title (`SettingsTab.cs.md` §Title, each tab returns a `Strings` key) is both the label and the ImGui identifier.
3. `ImGui.BeginChild($"###settings_scrolling_{settingsTab.Title}", new Vector2(-1, -1))` — a child window sized to the remaining tab area, whose **id contains the localized title** and which makes the tab's content scrollable (`SettingsTab.Draw()`'s output can therefore exceed the viewport; the tabs rely on this for long entry lists). Consequence recorded in `SettingsTab.cs.md` §Draw and each tab spec: a title change (a `Strings.cs` edit) changes the child id, i.e. per-tab ImGui state does not survive a rename.
4. `settingsTab.Draw()` — the entries loop (`SettingsTab.cs.md` §Draw) plus whatever extra the tab draws around `base.Draw()`.

### 5.2 Search mode (when `searchInput` is non-empty)

A single tab item `Strings.SearchResults`, and per tab (same Unix filter):

```csharp
var eligible = settingsTab.Entries.Where(x => x.Name.Contains(searchInput.Trim(), case-insensitive)).ToImmutableArray();
if (!eligible.Any()) continue;
any = true;                                   // set BEFORE the per-entry IsVisible filter
ImGui.BeginChild("SearchResults")              // shared id, no ### prefix, no tab discriminator
    TextColored(DalamudGrey, tab.Title)        // group header
    Dummy(5)
    foreach (entry in eligible) { if (!entry.IsVisible) continue; entry.Draw(); }
    Separator()
    Dummy(10)
```

- The filter matches on **`Name`** (`SettingsEntry.cs.md` §Name — for `SettingsEntry<T>` rows that is the ctor-supplied label, including the `string.Format`-composed labels of `Tabs/SettingsTabTroubleshooting.cs.md` (the only such labels today: the ForceLocale row)), not on `Description`: a user searching a word that appears only in the help text finds nothing. That is the current contract; extending it to `Description` would also make the hidden-row problem below worse.
- **`any` is set before the `IsVisible` filter**, so an invisible match counts as a result: the tab header, separator and spacing still render with zero entries under them. That is the observable behaviour today (e.g. searching a Fix-hack name on Windows shows its header with no row); if a "No results found" accuracy fix is wanted, move the `any = true` below the visibility check — that changes the semantics of `Tabs/SettingsTabTroubleshooting.cs.md` §Unix-only rows, so record the decision in those specs too.
- **`"SearchResults"` (no `###`) is one child id reused across all seven tab groups in one frame** — unlike §5.1's per-tab id. ImGui creates a separate child window per `BeginChild` call, but any *id-keyed* ImGui state (stored text, per-window state) would be shared across groups; that is why the search view deliberately carries no per-group state (no scrolling child, no per-group input persistence). Keep per-group ids unique (with `###` + tab discriminator) if a fix adds id-keyed state here.
- **Search text is applied one frame late**: the branch is chosen *before* the search box at the bottom of `Draw()` runs, so typing a query shows results from the *next* frame. The `MainPage.cs.md`-style "idempotent draw" discipline applies: the box writes `searchInput` (`ref this.searchInput`, max 100 chars) every frame; nothing else writes it except `OnShow()`.
- `!any → TextColored(DalamudGrey, Strings.NoResultsFound)` is inside the search `TabItem`, so "no results" is not a separate tab.

### 5.3 Cursor-placed finish button (§ the exit lever)

```csharp
ImGui.SetCursorPos(ImGuiHelpers.ViewportSize - new Vector2(60));
if (ImGui.BeginChild("###settingsFinishButton", ImGuiChildFlags.NavFlattened))
{
    PushStyleVar(FrameRounding, 100f); PushFont(FontManager.IconFont, 0.0f);
    var invalid = tabs.Any(x => x.Entries.Any(y => y.IsVisible && !y.IsValid));
    if (invalid) { BeginDisabled(); PushStyleColor(Text, DalamudRed); Button(Ban.ToIconString(), Vector2(40)); PopStyleColor(); EndDisabled(); }
    else         { if (Button(Check.ToIconString(), Vector2(40))) { tabs → Save(); App.State = LauncherState.Main; } }
    PopStyleVar(); PopFont();
}
```

- The button is drawn **outside** the tab bar (in the page's root window), so it is visible in every tab and in search mode; the same is true of the search box (§5.4). That is what makes "one lever to leave Settings" independent of which row you were looking at, and it is why `Tabs/SettingsTab*.cs.md` specs can add rows without touching exit behaviour.
- **Validity sweep**: *any* visible row that reports `!IsValid` disables the button (red ban icon, `BeginDisabled`) — the row itself draws the reason (`SettingsEntry{T}.cs.md` §3 validity block, red text). Combined with `SettingsEntry.cs.md` §`IsValid` default `false`, a row that was never `Load()`ed blocks exit silently-with-a-red-ban; the missing reason text is then a symptom of a tab not being in `tabs` or of an override skipping the validity tail (`DalamudBranchMetaSettingsEntry.cs.md` §Draw is the one row that can never contribute).
- **On press: `Save()` for *all* tabs** (not just the visited one — `SettingsTab.cs.md` §Save, and `Tabs/SettingsTabWine.cs.md` §Save's `base.Save(); Program.CreateCompatToolsInstance();` shows that tab-level save hooks run exactly once per exit), then `App.State = Main` — which runs `LauncherApp.cs` §`State` setter, including `if (this.state == LauncherState.Settings) this.mainPage.ReloadNews();` (`LauncherApp.cs:57`/`:59`) and `mainPage.OnShow()` (`LauncherApp.cs:70-71`). So leaving Settings is the mechanism that re-reads persisted config into the main page — see `MainPage.cs.md` §ctor/§`OnShow` and `NewsFrame.cs.md` §refresh trigger. Any new "leave Settings" path must assign `State`, not call `App.State`-equivalent internals, or the news/`OnShow` pair is skipped.
- `NavFlattened` + `FrameRounding 100f` + `PushFont(IconFont, 0.0f)`/`PopFont()` are the icon-button idiom also recorded in `MainPage/ActionButtons.cs.md` §font push (the `0.0f` size meaning "current size") and `Tabs/SettingsTabAbout.cs.md` §CalcTextSize/`docs/XIVLauncher.Core/FontAwesomeIcon.cs.md` (`Ban`/`Check` glyphs). Keep `PopStyleVar`/`PopFont` inside the `BeginChild` block, and keep the push/pop *nesting* even though the red push/pop sits inside the `if` branch (`docs/XIVLauncher.Core/ImGuiColors.cs.md` §colour-stack rule applies as elsewhere: counts per path, not per call site).

### 5.4 The search box

```csharp
ImGui.SetCursorPos(new Vector2(vpSize.X - 260, 4));
ImGui.SetNextItemWidth(250);
ImGui.InputTextWithHint("###searchInput", Strings.SearchForSettings, ref this.searchInput, 100);
```

Absolute positioning (top-right) rather than flow — the box's place is independent of tab content, and `SetCursorPos` immediately before it is the same "place the next item explicitly" idiom used by `SteamDeckPromptPage.cs.md` §cursor note, `Tabs/SettingsTabWine.cs.md` and `Tabs/SettingsTabAbout.cs.md` §`SameLine`. `ref this.searchInput` with `InputTextWithHint` (growing-string variant) rather than the fixed-buffer `InputText` of `Common/Input.cs.md` — see that spec's §`ref` binding note for why a `ref string` field works here and the fixed-buffer form does not.

`base.Draw()` at `:157` (last, = `Page`→`Component` children loop) is the standard page tail (`Page.cs.md` §Draw chain, cf. `MainPage.cs.md` §4 where it is first); keep it after the id-keyed widgets, since children (of which there are none today) belong in the flow, not the absolute corner.

## 6. Invariants and extension points

- **`searchInput` is plain `private string`, not volatile** — unlike `Common/Input.cs.md` §volatile field. That is safe because it is written on the draw loop and read only by the draw loop; anything that reads it off-loop (a new "apply the search to X" feature) re-introduces that spec's tearing concern.
- **Search mode bypasses the tab child windows** (§5.2's `"SearchResults"` child instead of `$"###settings_scrolling_{Title}"`): rows drawn in search mode are in a *non-scrolling* child (size defaults), so a query matching many rows can overflow the tab area. If that ever bites, the fix belongs here (per-group scrolling child, tab-discriminated id), not in `SettingsTab.cs.md` §Draw.
- **The tab bar's `if (BeginTabBar)` wrapper means the whole browse/search body may be skipped** (ImGui returns `false` when the tab bar is not active). Anything added between `BeginTabBar` and `EndTabBar` must be balanced on both return paths; the finish button and search box are deliberately *outside* that guard, which is why they always run.
- **No `LauncherApp` fields here**: state access is exclusively through the `App` back-reference (`Page.cs.md` §`App`, `protected`) — `App.State = Main` (§5.3) is the only page-level exit in the package besides `LoadingPage.cs.md`/`MainPage.cs.md` §`StopLoading`/§`State` assignments. Keep new exits as `App.State = …` so `OnShow`/news-reload semantics stay intact.
- **Adding a row is not a `SettingsPage.cs` edit** (see §3); adding a *tab* is, and must also respect the `IsUnixExclusive` vs per-row `CheckVisibility` choice (`SettingsTab.cs.md` §`IsUnixExclusive`) and the `Tabs` namespace import.
