# SettingsTabDebug.cs — Specification

**File:** `src/XIVLauncher.Core/Components/SettingsPage/Tabs/SettingsTabDebug.cs` · 51 lines · 1784 bytes
**Namespace:** `XIVLauncher.Core.Components.SettingsPage.Tabs` · **Declaration:** `public class SettingsTabDebug : SettingsTab`
**Role:** The "Debug" tab — build/environment readout and a full process-environment table; no settings rows

---

## 1. Purpose

`SettingsTabDebug` is the second entry-less tab (see `Tabs/SettingsTabAbout.cs.md` §1) and is the launcher's "dump everything the launch path depends on" surface: OS and runtime version, hardware/Flatpak detection, and every environment variable the process currently sees. It exists so a user can reproduce the environment half of a crash report without leaving the UI (the other half, launcher state, is `docs/XIVLauncher.Core/Support/Troubleshooting.cs.md` via `Tabs/SettingsTabTroubleshooting.cs.md` §log pack button, whose generator `docs/XIVLauncher.Core/Support/PackGenerator.cs.md`-side captures the same variables — see that spec).

## 2. Surface

| Member | Anchor | Notes |
| --- | --- | --- |
| `Entries` | `:11` | `Array.Empty<SettingsEntry>()` — no rows |
| `Title` | `:12` | `Strings.DebugTitle` |
| `Draw()` | `:14-:50` | `override`; `base.Draw()` **last** (`:50`) |

No ctor, no fields: everything is read per frame. `:1 using System.Collections` supplies the `DictionaryEntry` pattern type of the env table; uses `Environment` (`System`), `docs/XIVLauncher.Core/Program.cs.md` (`IsSteamDeckHardware`), `docs/XIVLauncher.Core/ImGuiHelpers.cs.md` (`TextWrapped`), `docs/XIVLauncher.Core/CoreEnvironmentSettings.cs.md` (the variables the launch path sets — see §5), `lib/FFXIVQuickLauncher` `Util` (`OrderBy` extension for `.OrderBy(...)`), `src/XIVLauncher.Core/Resources/Localization/Strings.Designer.cs`.

## 3. Generic information block (`:16-:27`)

| Line | Content |
| --- | --- |
| `:16` | `TextUnformatted("Generic Information")` + `Separator()` |
| `:18` | `Environment.OSVersion` |
| `:19` | `Environment.Version` |
| `:21` | `if (Program.IsSteamDeckHardware) Text("Steam Deck Hardware Detected")` |
| `:24-:26` | `#if FLATPAK` → `Text("Running as a Flatpak")` |
| `:27` | `Spacing()` |

- **`TextUnformatted` for the two readouts** (literal, monospace-friendly no-wrap) vs `Text` for the detection lines — see `docs/XIVLauncher.Core/ImGuiHelpers.cs.md` §wrap helpers for the family; the readouts must not wrap because they are the line users copy into reports.
- **`IsSteamDeckHardware` is the same probe `MainPage.cs.md` §9 (`Hide()` vs `SteamDeckPrompt`, `:762`/`:766`) and `docs/XIVLauncher.Core/Program.cs.md` gate on** — this row is the user-visible confirmation of the branch those take, and `SteamDeckPromptPage.cs.md` §role is the page shown on the other side of that branch. Keep the reference to `Program.` (process-wide detection), not a re-probe.
- **`#if FLATPAK`** is one of the build regions in this package (`FtsPage.cs.md` §flatpak region is the other main one, and `docs/XIVLauncher.Core/Program.cs.md` records the packaging switches). The literal string is English, not a `Strings` key — consistent with `MainPage.cs.md` §"`#if !DEBUG` means literal English strings" reasoning. The region must stay compile-guarded so non-Flatpak builds don't claim to be one.

## 4. Environment table (`:30-:47`)

```csharp
if (ImGui.BeginTable("EnvironmentTable", 2, Borders | RowBg | Resizable | ScrollY))
{
    TableSetupScrollFreeze(0, 1);                                   // header row pinned while scrolling
    TableSetupColumn("Key",   WidthStretch, 0.35f);
    TableSetupColumn("Value");
    TableHeadersRow();
    foreach (var entry in Environment.GetEnvironmentVariables().OrderBy(e => e.Key.ToString()))
    { TextWrapped(key ?? "null");  TextWrapped(value ?? "null"); }
    EndTable();
}
```

- **The table id is `"EnvironmentTable"`** — no `###` prefix, so it shares the `#`-prefix-free id space (contrast `SettingsPage.cs.md` §5.1's `###settings_scrolling_{Title}` and `Common/*` `###` ids; see the id-convention discussion in `Component.cs.md`/`MainPage/NewsFrame.cs.md`). Table state (column widths, scroll) is keyed by that id, so it persists per window; with the *same* id appearing in the tab child window created by `SettingsPage.cs.md` §5.1 there is exactly one such window, so no collision risk today — keep the id unique if another table is ever added to the Settings page.
- **`Resizable | ScrollY` + `TableSetupScrollFreeze(0, 1)`** is what makes the (long, sorted) list scrollable inside `SettingsPage.cs.md` §5.1's tab child while keeping the header visible. This is the reason the tab can be entry-less and still overflow the tab area — note the contrast with `SettingsPage.cs.md` §5.2's search child, which is *not* scrolling: rows shown through search mode get no scroll container of their own.
- **Sorted by key** via `OrderBy` (`lib/FFXIVQuickLauncher/src/XIVLauncher.Common/Util`'s LINQ-style extension; `:38` casts `GetEnvironmentVariables()`'s pairs to `DictionaryEntry`) — the sort is recomputed every frame (the env can change at runtime, see §5). Cost is fine at a few hundred variables; do not memoize unless the env is proven static in-process, which it is not.
- **Both columns are `TextWrapped`** (`docs/XIVLauncher.Core/ImGuiHelpers.cs.md`) with `?? "null"` fallbacks — a `null` key/value is rendered as the literal `null`, i.e. the table is total, not filtered. Keep the fallbacks if the filter changes, since `GetEnvironmentVariables()` can carry `null` keys.

## 5. What the table reveals, and why that matters

- **The launch path mutates process-global environment variables** (`MainPage.cs.md` §env-hack table, `:604`–`:643`: `LC_ALL`/`LC_CTYPE`, `LD_PRELOAD`, `XMODIFIERS`, `DOTNET_SYSTEM_GLOBALIZATION_USENLS`, `TZ`, plus Steam-compat `XL_PRELOAD` handling and the `AdditionalArgs` `%command%` env tokens). Those persist for the rest of the process. So after a game launch, **this table shows the launcher's own leaks** — a user opening the Debug tab post-launch sees `TZ=""`, `XMODIFIERS=@im=null`, etc. That is a real, currently-observable behaviour of this tab, and the tab is the place where the leak is visible, hence the natural place to report it. If the leak is fixed, fix it in `MainPage.cs.md` §env-hack ordering (the region is ordered and documented there); do not filter the table here — filtering hides exactly the evidence this tab exists to provide.
- **`CoreEnvironmentSettings.cs.md`** is the reader side of several of these (`GetCleanEnvironmentVariable("LANG")` in `Tabs/SettingsTabTroubleshooting.cs.md` §ForceLocale warning predicate, `IsSteamCompatTool` visibility gates) — i.e. this tab is the *print* of the state those predicates read, which is what makes a "the hack didn't apply" report diagnosable.
- **`Environment.OSVersion` here vs `Environment.OSVersion.Platform` in `SettingsPage.cs.md` §Draw filter and in the tabs' `CheckVisibility` closures**: the same legacy API in three shapes (`OSVersion` printed, `.Platform` compared to `PlatformID.Unix`, and `RuntimeInformation.IsOSPlatform(OSPlatform.Linux)` inside `Tabs/SettingsTabWine.cs.md` predicates). The Debug tab's printed line is the diagnostic for whether those three agree on a given platform — keep it printable (`TextUnformatted`) when touching that region.

## 6. Invariants and extension points

- **Entry-less ⇒ nothing here loads, saves, or searches** (`SettingsTab.cs.md` §Load/§Save over an empty `Entries`, `SettingsPage.cs.md` §search filter has no `Name`s to match). Any real setting added here must be a `SettingsEntry…` instance (`SettingsEntry.cs.md` §surface, `SettingsEntry{T}.cs.md` §instantiation shape used by the other tabs) rather than more hand-drawn text; the hand-drawn half is display-only.
- **`base.Draw()` last (`:50`)** = `SettingsTab.Draw()` over empty `Entries` then `Component.Draw()` (see `SettingsTab.cs.md` §base). Position matters only if rows are added — decide then per that spec's position list (`Tabs/SettingsTabAbout.cs.md` §base-last is the sibling choice).
- **No `Check*`, no `IsValid`, no buttons**: this tab cannot block exit (`SettingsPage.cs.md` §finish button consults rows) and cannot trigger actions. Adding an action button here should follow `Tabs/SettingsTabTroubleshooting.cs.md` §logs/cleanup patterns (button + grey explanation line) so the "what does this do" affordance stays in the same shape across the Settings page.
- **Everything is read per frame and must stay side-effect free**: the env table is the most expensive thing on the Settings page, so a new readout here should be a cheap accessor (like `Environment.Version`), not a subprocess/`stat` call — the syscall-per-frame precedent in `Tabs/SettingsTabWine.cs.md` §fsync predicate is already borderline; do not add more.
