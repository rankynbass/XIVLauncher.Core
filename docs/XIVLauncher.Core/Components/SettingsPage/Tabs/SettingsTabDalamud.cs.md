# SettingsTabDalamud.cs — Specification

**File:** `src/XIVLauncher.Core/Components/SettingsPage/Tabs/SettingsTabDalamud.cs` · 64 lines · 3483 bytes
**Namespace:** `XIVLauncher.Core.Components.SettingsPage.Tabs` · **Declaration:** `public class SettingsTabDalamud : SettingsTab`
**Role:** The "Dalamud" tab — enable flag, injection method, branch switcher, injection delay, and the manual-injection pair whose save path drives the updater

---

## 1. Purpose

Three things make this tab the most *behavioural* one in the package: `Entries` is built in the **ctor** (not a field initializer), two rows hold references to other rows (`enableManualInjection`), and two rows' `save` lambdas write into `Program.DalamudUpdater` rather than into config. It is also the tab that has no `Draw()` override yet draws more than a plain list — because the branch row brings its own drawing (`DalamudBranchMetaSettingsEntry.cs.md` §Draw).

## 2. Entries — ctor-built, with an inline self-reference

```csharp
public override SettingsEntry[] Entries { get; }          // :9  — accessor with NO getter body
private SettingsEntry<bool> enableManualInjection;        // :10 — assigned inside the array literal below
public SettingsTabDalamud()                               // :12
{
    this.Entries = new SettingsEntry[] { /* :17 … :62 */ };
}
```

`SettingsTab.cs.md` §Entries: the accessor is declared without a getter (abstract-shaped `override … { get; }` on a non-abstract override = a slot only the ctor fills). Same shape appears in `Tabs/SettingsTabWine.cs.md` §ctor (`Entries = new SettingsEntry[] { … }` with `startupTypeSetting`/`dxvkVersionSetting` fields). Consequences of ctor-assignment here, both load-bearing:

- The **array literal is evaluated during the ctor body**, so `enableManualInjection` is non-null only after construction — and since construction happens inside `SettingsPage.cs.md` §tabs (`private readonly SettingsTab[] tabs = { … new SettingsTabDalamud() … }`, during `LauncherApp.cs:135`), before any `Draw()`/`Load()`/`Save()` can run, the invariant holds. If this tab is ever constructed lazily, the two closures below (`:48 CheckVisibility = () => enableManualInjection.Value`) start throwing NRE instead of being false.
- **Row order in the literal is display order** (`SettingsTab.cs.md` §Draw iterates `Entries`) — see §3 table.

| Row | Anchor | Type | Config field / effect |
| --- | --- | --- | --- |
| `Strings.EnableDalamudSetting` | `:17` | `SettingsEntry<bool>` | `Program.Config.DalamudEnabled ?? true` |
| `Strings.LoadMethodSetting` | `:18` | `SettingsEntry<DalamudLoadMethod>` | `?? DalamudLoadMethod.DllInject` |
| `Strings.DalamudBranchSwitcher` | `:19` | `DalamudBranchMetaSettingsEntry` | getter `Config.DalamudBetaKind ?? "release"`; save lambda is **`s => {}`** (see `DalamudBranchMetaSettingsEntry.cs.md` §empty-save-lambda) |
| `Strings.DalamudInjectionDelaySetting` | `:20` | `NumericSettingsEntry` (`0, int.MaxValue, 1000`) | `Program.Config.DalamudLoadDelay` |
| `Strings.DalamudManualInjectionEnableSetting` | `:22-:38` | `SettingsEntry<bool>`, **assigned to `enableManualInjection`** | getter `Config.DalamudManualInjectionEnabled ?? false`; save lambda has side effects (§4) |
| `Strings.DalamudLocalInjectionPathSetting` |  `:40-:61` | `SettingsEntry<DirectoryInfo>` | getter `Config.DalamudManualInjectPath` (nullable, no fallback); save lambda gated by `enableManualInjection.Value == false` (`:42`); hooks `CheckVisibility` (`:48`), `CheckValidity` (`:49-:60`) |

`Title` is `:8 Strings.DalamudTitle`. There is no `Draw()` override → the tab's rows are drawn by `SettingsTab.cs.md` §Draw in list order, then `base.Draw()`.

## 3. The two coupling mechanisms

Both directions of the coupling are plain closures over a sibling row — the package has no dependency graph for rows.

1. **Path row follows the enable row**: `CheckVisibility = () => enableManualInjection.Value` (`:48`) hides the path row until manual injection is on (`SettingsEntry{T}.cs.md` §hooks bullet). Same shape as `Tabs/SettingsTabWine.cs.md` §dependency chain (`startupTypeSetting.Value == WineStartupType.Managed` / `== WineStartupType.Custom`).
2. **Enable row's save lambda recomputes the runner**: on save, after writing `Config.DalamudManualInjectionEnabled` (`:24`), if disabling it clears `Program.DalamudUpdater.RunnerOverride` (`:28`) and returns; if enabling, it scans `Config.DalamudManualInjectPath` for `Program.DALAMUD_INJECTOR_NAME` (`:32-:34`) and sets `RunnerOverride = new FileInfo(Path.Combine(path, name))` (`:36`). The path row's save lambda (`:42-:45`) returns early when `enableManualInjection.Value == false` (`:42`) or `input is null` (`:43`), otherwise writes the path (`:44`) *and* sets `RunnerOverride`. See `docs/XIVLauncher.Core/Program.cs.md` §`RunnersEnabled` for the gate that decides whether the override is honoured at launch, and `lib/FFXIVQuickLauncher/src/XIVLauncher.Common/Dalamud/DalamudUpdater.cs` for `RunnerOverride`'s consumer; `MainPage.cs.md` §9 is the launcher-side read.

Consequences to preserve:

- **`enableManualInjection.Value` is read from the *row*, not from config** — i.e. it reflects what the user has set since the last `Save()`/`Load()` cycle (`SettingsEntry{T}.cs.md` §`Load()`), and for a value-type `T` a null box collapses to `default` (`SettingsEntry{T}.cs.md` §null boxes read as default ⇒ `bool` default *is* `false`). So both guards below fail-closed when the row was never loaded/visible, which is the safe direction (no manual override is applied) — do not "simplify" `== false` into `!…Value` on the assumption they differ.
- **`Save()` is the only writer of `RunnerOverride`** from this surface (`SettingsPage.cs.md` §finish button → `SettingsTab.cs.md` §Save → row `Save()`), so navigating Settings without pressing the finish button changes *config* nothing and *runner* nothing: the pair "enable manual injection" + "set path" only takes effect together at that single save pass. Order inside that pass is the `Entries` order above — enable before path — which is what makes the path lambda's `enableManualInjection.Value` guard see the *new* value rather than the old one (`SettingsEntry{T}.cs.md` §Save bullet; `InternalValue` is written by the earlier row's `Save()` before the later row's guard runs).
- **`RunnerOverride` survives without a fresh save**: a user who enabled manual injection in a previous session and reopens Settings gets the config-backed state, but `RunnerOverride` is only recomputed when one of these two rows saves. See `docs/XIVLauncher.Core/Program.cs.md` §`DalamudUpdater` for whether the runner state is rebuilt per launch — if it is, this is a non-issue; if not, it's a real stale-state surface and the fix belongs on that side, not in `Draw()` (which runs every frame, and `SettingsEntry{T}.cs.md` §cheap-predicates applies).

## 4. Directory scans (cost + the "why the validator is worded this way" note)

Three places pay filesystem work, and their location is deliberate:

- `CheckValidity` of the path row (`:49-:60`): `input.Exists`, then `input.GetFiles().FirstOrDefault(x => x.Name == Program.DALAMUD_INJECTOR_NAME)` → `Strings.DalamudLocalInjectionPathSettingNoDirValidation` / `…NoInjValidation`, else `null`. It runs **per frame** (`SettingsEntry{T}.cs.md` §Draw bullet) and per `Load()`. Keep it: the two distinct messages are the UX affordance that tells a user whether they mistyped the directory or pointed at one without the injector binary, and the injector-name match uses the single `Program.DALAMUD_INJECTOR_NAME` constant — the same constant the save lambdas use (`:34`, `:45`), so what the validator accepts is exactly what the save path will install. Do not let the validator become laxer or stricter than the save path.
- The enable row's save lambda (`:34`) does the same scan once per save.
- `GetFiles()` here is the non-recursive `Directory.GetFiles()` (not the recursive pattern used in `MainPage.cs.md` §`while (true)` repair loop) — a *flat* directory containing the injector file, i.e. the injector must sit directly in the chosen folder. Keep that depth if you touch the validator or the save lambdas; both must stay in agreement (`:34`/`:45` vs `:55`).

## 5. Invariants and extension points

- **`s => {}` (empty save lambda) at `:19`** is required by the ctor signature, not a placeholder to implement: that row is `DalamudBranchMetaSettingsEntry`, whose `Save()` is overridden and never calls `this.save` (`DalamudBranchMetaSettingsEntry.cs.md` §empty-save-lambda). Keep it whenever that row's ctor shape is kept.
- **`Config.DalamudLoadDelay` is read without `??`** (`:20`, and `NumericSettingsEntry.cs.md` §call sites) — the config field is a non-nullable `int`, so there is no unset state to default. Contrast `DalamudEnabled ?? true`, `IsOtpServer ?? false`-style rows: the `??` count in this file mirrors the config field's nullability, so adding `??` to a non-nullable field is a no-op that hides a type change, and removing one from a nullable field creates the `default`-collapse (`SettingsEntry{T}.cs.md` §null boxes read as default).
- **No `IsUnixExclusive`, no `Draw()`, no `CheckVisibility` tied to platform** — the whole tab is cross-platform; Steam-Deck-specific Dalamud surfaces live elsewhere (`MainPage.cs.md` §9 `Hide()` vs `SteamDeckPrompt`, `SteamDeckPromptPage.cs.md`).
- **`Program.DALAMUD_INJECTOR_NAME`** (`docs/XIVLauncher.Core/Program.cs.md`) is the single naming source; `lib/FFXIVQuickLauncher/src/XIVLauncher.Common/Dalamud/DalamudUpdater.cs` is the runner machinery. Anything adding another injector-name check should reuse the constant rather than re-spelling a filename.

## 6. Referenced / referenced-by

Instantiates: `SettingsEntry{T}.cs.md` (×4: DalamudEnabled, DalamudLoadMethod, manual-enable bool, manual-path DirectoryInfo), `DalamudBranchMetaSettingsEntry.cs.md`, `NumericSettingsEntry.cs.md`. Base of `SettingsTab.cs.md` (ctor-built entries; `Load`/`Save` not overridden).
Iterated by `SettingsPage.cs.md` §Draw/§search/§finish button; loaded by `SettingsTab.cs.md` §Load.
State effects: `Program.DalamudUpdater.RunnerOverride` (`docs/XIVLauncher.Core/Program.cs.md` §`RunnersEnabled` gate) ← `MainPage.cs.md` §9; `Config.DalamudEnabled/LoadMethod/LoadDelay/BetaKind/ManualInjectPath/ManualInjectionEnabled` → `docs/XIVLauncher.Core/Configuration/ILauncherConfig.cs.md`.
