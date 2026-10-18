# SettingsTab.cs — Specification

**File:** `src/XIVLauncher.Core/Components/SettingsPage/SettingsTab.cs` · 44 lines · 923 bytes
**Namespace:** `XIVLauncher.Core.Components.SettingsPage` · **Declaration:** `public abstract class SettingsTab : Component`
**Role:** The generic "a tab is a list of entries that loads/saves/draws as a unit" contract shared by all seven tab classes

---

## 1. Purpose

`SettingsTab` is the *other* hub of the Settings page: `SettingsPage.cs.md` owns the tab bar, the search mode, and the finish button; `SettingsTab` owns the rows. Every tab class (the seven under `Tabs/`) either relies on this class's three loop methods or overrides `Draw()` and calls `base.Draw()` to keep them. Two consequences of that design are the load-bearing invariants in §3 and §4.

## 2. Surface

| Member | Anchor | Form | Notes |
| --- | --- | --- | --- |
| `Entries` | `:6` | `public abstract SettingsEntry[] { get; }` | abstract accessor; three override shapes exist — see §3.1 |
| `IsUnixExclusive` | `:8` | `public virtual bool => false` | classic virtual accessor; overridden `=> true` **only** by `Tabs/SettingsTabWine.cs.md` |
| `Title` | `:10` | `public abstract string { get; }` | abstract accessor; every tab returns a `Strings` key |
| `Draw()` | `:12-:24` | `public override void` (overrides `Component.Draw`) | entries loop, then `base.Draw()` |
| `Load()` | `:26-:32` | `public void` — **not** virtual | iterates `Entries` |
| `Save()` | `:34-:42` | `public virtual void` | iterates `Entries`; overridable (and overridden by `Tabs/SettingsTabWine.cs.md`) |

## 2.1 Localization of `Title` and labels

Every key is a `Strings.<Key>` static accessor in the generated `src/XIVLauncher.Core/Resources/Localization/Strings.Designer.cs`; the *values* live in `Strings.resx` (default) and one `.resx` per language (`de`, `es`, `fr`, `hu`, `it`, `ja`, `ko`, `nl`, `no`, `pt`, `ru`, `sv`, `tw`, `zh`), resolved lazily through `System.Resources.ResourceManager` with `resourceCulture == null`, i.e. by the process locale — this fork has no `SetLanguage` API (grep for it finds nothing in `src/`). Two consequences: a key rename is a `.resx` + `Designer.cs` pair edit (not a code edit), and **a localized `Title` is also an ImGui id** — `SettingsPage.cs.md` §Draw feeds `Title` into `BeginTabItem` and into `$"###settings_scrolling_{Title}"`, so per-tab ImGui state does not carry across a locale change between runs. Labels composed in code (`string.format`/`string.Format` of a key) are code edits — see `Tabs/SettingsTabTroubleshooting.cs.md` §ForceLocale entry and `Tabs/SettingsTabWine.cs.md` §dependency chain.

## 3. The entries loop is *reusable*, not extended

```csharp
public override void Draw()
{
    foreach (SettingsEntry settingsEntry in Entries)
    {
        if (settingsEntry.IsVisible)
        {
            settingsEntry.Draw();
            ImGui.Dummy(new Vector2(10) * ImGuiHelpers.GlobalScale);
        }
    }

    base.Draw();          // Component.Draw() — the (empty-by-convention) Children loop
}
```

- A tab that overrides `Draw()` **replaces** this method; the way it keeps its rows is by calling `base.Draw()` (= `SettingsTab.Draw()`, since `SettingsTab` is the supertype), which is exactly what `Tabs/SettingsTabGame.cs.md`, `Tabs/SettingsTabTroubleshooting.cs.md`, `Tabs/SettingsTabAbout.cs.md`, `Tabs/SettingsTabDebug.cs.md`, `Tabs/SettingsTabWine.cs.md` do. The position of that call is the tab's own layout choice: first (`Game`, `Wine`, `Troubleshooting` after its header) or last (`About`, `Debug`). `Tabs/SettingsTabPatching.cs.md` and `Tabs/SettingsTabDalamud.cs.md` do not override `Draw()` at all. `Tabs/SettingsTabTroubleshooting.cs.md` §Draw shows the mixed form (header, `base.Draw()`, then more content).
- Every visible row is followed by `ImGui.Dummy(10 * GlobalScale)`, including the last one — so row spacing is uniform and trailing; a tab that wants tighter grouping adds its own `Spacing`/`Separator` around `base.Draw()` (see the two specs that do).
- The `IsVisible` check here is one of **two** independent visibility filters in the page (`SettingsPage.cs.md` §search mode has its own) — changing the filter's meaning must be done in both places, and the reason a row is hidden always comes from its own `IsVisible` accessor (`SettingsEntry.cs.md` §IsVisible).
- `base.Draw()` at the end is a no-op today (nothing ever adds `Children` to a tab, cf. `Component.cs.md` §children/`Enabled` being dead plumbing) but is the correct tail for a `Component` override; keep it.

## 3.1. `Entries` override shapes

The abstract accessor is satisfied four ways, and the choice constrains what a row's `Check*` closures may read:

1. **arrow accessor** — `public override SettingsEntry[] Entries => Array.Empty<SettingsEntry>();` (`Tabs/SettingsTabAbout.cs.md` `:14`, `Tabs/SettingsTabDebug.cs.md` `:11`): a computed accessor, re-allocating an empty array on every read (`SettingsPage.cs.md` §tabs' `flatMap` and `SettingsTab.cs.md` §Draw each allocate per call — see `SettingsPage.cs.md` §allocation notes).
2. **accessor + initialiser** — `public override SettingsEntry[] Entries { get; } = { … }` (`Tabs/SettingsTabGame.cs.md` `:10`, `Tabs/SettingsTabPatching.cs.md` `:7`, `Tabs/SettingsTabTroubleshooting.cs.md` `:11`): evaluated once at field-init time.
3. **ctor assignment** — slot accessor (`Tabs/SettingsTabDalamud.cs.md` `:9`, `Tabs/SettingsTabWine.cs.md` `:77`) plus `this.Entries = new SettingsEntry[] { siblingRow = new … , … }` in the ctor body. Required when a row's `CheckVisibility`/`Save` closure reads a *sibling row object*: shapes 1–3 run at field-init time, so a `private` sibling field read there would be unassigned. The closures themselves are lazy, so the only ordering requirement is "assigned before the first `Draw()`/`Load()`/`Save()`", guaranteed by the tab-construction rule in `SettingsPage.cs.md` §tabs.

## 4. `Load()` vs `Save()` — the asymmetry

- `Load()` is **non-virtual**: no tab may override it (compile error, not a silent shadow). The single load path is `SettingsPage.cs.md` §OnShow → `tab.Load()` → `entry.Load()`, i.e. rows are re-read from persisted state *every time the Settings page is shown* (`LauncherApp.cs` §`State` setter calls `setPage.OnShow()`). That is the mechanism `Tabs/SettingsTabDalamud.cs.md` §cross-entry coupling and `Tabs/SettingsTabWine.cs.md` §dependency chain rely on: a fresh load means derived visibility (`CheckVisibility` reading sibling `Value`s) recomputes against fresh values.
- `Save()` **is** virtual, and one tab overrides it to add work after the rows are written: `Tabs/SettingsTabWine.cs.md` §Save (`base.Save(); Program.CreateCompatToolsInstance();`). Any new tab that must react to "the user pressed the finish button" extends `Save()` in that shape (`base.Save()` first).
- `Save()` runs only from the page's finish button (`SettingsPage.cs.md` §finish button, gated by `IsValid`), and `SettingsEntry.cs.md` §4's `IsValid` default `false` is what makes an un-loaded page refuse to save.

## 5. `IsUnixExclusive`

Declared `public virtual bool => false` (the classic accessor form, deliberately *not* the accessor-block used by `Entries`/`Title`). Only `Tabs/SettingsTabWine.cs.md` overrides it, and the only consumer is `SettingsPage.cs.md` §Draw / §search mode, which test it as `settingsTab.IsUnixExclusive && Environment.OSVersion.Platform != PlatformID.Unix` → `continue`. Two things to keep in mind:

- It hides a **whole tab**, whereas per-entry `CheckVisibility` (`SettingsEntry.cs.md` §IsVisible) hides rows; `Tabs/SettingsTabGame.cs.md` and `Tabs/SettingsTabTroubleshooting.cs.md` use the latter for Unix-only rows even though the tab itself is cross-platform. Choose per-granularity, not per-platform.
- The check is the legacy `Environment.OSVersion.Platform == PlatformID.Unix` test, not `RuntimeInformation.IsOSPlatform(...)` — the *narrower* `IsOSPlatform(Linux)` spelling appears in `Tabs/SettingsTabWine.cs.md` §GameMode row gate (`:66`), one level *deeper* than this flag. Both spellings appear in the package, at three depths: page-level (`SettingsPage.cs.md` §Draw/§search), tab-level (this flag), row-level (`CheckVisibility` in `Tabs/SettingsTabGame.cs.md`, `Tabs/SettingsTabTroubleshooting.cs.md`, `Tabs/SettingsTabWine.cs.md`). Keep whichever the neighbouring code already uses so a diff reads as intentional — see §3.1 for the Entries-shape analogue.

## 6. Referenced / referenced-by

Consumed by: `SettingsPage.cs.md` (`tabs[]` array, `Title`, `IsUnixExclusive`, `Entries` in search mode, `Draw`, `Load`, `Save`).
Base of: all seven `Tabs/SettingsTab*.cs.md`.
Provides entries to: `SettingsEntry.cs.md` (and through it `SettingsEntry{T}.cs.md`, `NumericSettingsEntry.cs.md`, `DalamudBranchMetaSettingsEntry.cs.md`).
Uses: `Component.cs.md` (`Draw` override + `base.Draw()`), `docs/XIVLauncher.Core/ImGuiHelpers.cs.md` §`GlobalScale`.
