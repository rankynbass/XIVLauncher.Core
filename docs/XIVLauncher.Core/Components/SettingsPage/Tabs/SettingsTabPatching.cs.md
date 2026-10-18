# SettingsTabPatching.cs — Specification

**File:** `src/XIVLauncher.Core/Components/SettingsPage/Tabs/SettingsTabPatching.cs` · 26 lines · 1100 bytes
**Namespace:** `XIVLauncher.Core.Components.SettingsPage.Tabs` · **Declaration:** `public class SettingsTabPatching : SettingsTab`
**Role:** The "Patching" tab — patch path, download speed limit, keep-patches; pure data, no custom drawing

---

## 1. Purpose

The smallest tab in the package: three rows and nothing else. It overrides only `Entries` and `Title`, so the whole visible shape comes from `SettingsTab.cs.md` §Draw (entries loop + `Dummy` spacing) — it is the reference example of "a tab that does not override `Draw()`" (the other is `Tabs/SettingsTabDalamud.cs.md` §Draw, which also has no override but a ctor-built list).

## 2. Entries

| Row | Anchor | `T` | Backing field | Hooks |
| --- | --- | --- | --- | --- |
| `Strings.PatchPathSetting` | `:6-:17` | `DirectoryInfo` | `Program.Config.PatchPath` | `CheckValidity`: unset/blank ⇒ `Strings.PatchPathSettingNotSetValidation`, else `null` |
| `Strings.MaximumSpeedSetting` | `:18` | `NumericSettingsEntry` (`int`) | `Program.Config.PatchSpeedLimit` (cast `(int)` on read, widened back on write) | ctor range `0 … int.MaxValue`, step `1000` (see `NumericSettingsEntry.cs.md` §call sites) |
| `Strings.KeepPatchesSetting` | `:19` | `bool` | `Program.Config.KeepPatches` (`?? false` getter) | — |

All three are `new SettingsEntry<…>(label, description, () => Program.Config.X, x => Program.Config.PatchX = x)` (`SettingsEntry{T}.cs.md` §config bridge), evaluated during construction inside `SettingsPage.cs.md` §tabs.

## 3. Display shape

`SettingsTab.cs.md` §Draw only: patch path text field, speed `InputInt`, keep-patches checkbox, each followed by the grey description (`SettingsEntry{T}.cs.md` §3 tail). No header, no buttons, no extra `Spacing`/`Separator` — and no `Draw()` override, so the tab is the template for "rows only".

## 4. What the values feed (why these three rows)

- **`PatchPath`** is the patch store directory: `MainPage.cs.md` §10 (`HandleBootCheck` null-defaults it to `{Roaming}/patches` when it doesn't exist, and `TryHandlePatchAsync` builds `Program.Patcher`/`PatchInstaller` from it). The validity check here only rejects *blank* (`CheckValidity` never inspects existence — a non-existent but set path passes the UI check and gets created/derived launcher-side), so the red banner for this row means "not set", not "not usable". Keep that distinction in mind if adding an existence check: an existence check runs per frame (`SettingsEntry{T}.cs.md` §cheap-predicates) and would disagree with `MainPage.cs.md` §10's own defaulting.
- **`PatchSpeedLimit`** is the `AriaPatchAcquisition` throughput cap: it is an argument of the `Program.Patcher = new PatchManager(…)` construction (`MainPage.cs.md` §10). The int↔double casts here are the *only* place the narrowing happens — see that spec for the consumption side, and keep the casts if the field type changes (the pattern is the same one `NumericSettingsEntry.cs.md` §extension points warns about).
- **`KeepPatches`** decides whether patch files are retained after install (library side: `lib/FFXIVQuickLauncher/src/XIVLauncher.Common/Game/Patch/{PatchInstaller,Acquisition}`). Nothing in `Components/` reads it; the row exists to expose a library behaviour, which is why the tab carries no logic.

## 5. Invariants and extension points

- **No visibility hooks**: all three rows are always visible, on every platform, in browse mode; and the numeric/bool rows are always `IsValid` (`NumericSettingsEntry.cs.md` §no hooks, `SettingsEntry{T}.cs.md` §4) — only the path row can block `SettingsPage.cs.md` §finish button. The "patching tab can make Settings un-savable" condition is exactly "patch path not set", and it fires even when `MainPage.cs.md` §10 would have defaulted the path successfully (blank config ≠ defaulted config). Keep the row-visible-and-invalid shape rather than auto-writing a default from here: writing from a validity predicate would fight `SettingsEntry{T}.cs.md` §"predicates run per frame".
- **Rows are added with designated-initializer hooks** (`SettingsEntry{T}.cs.md` §`init` accessors); a new row here follows `Tabs/SettingsTabGame.cs.md` §entry examples, not this file's sparser form, if it needs hooks.
- **If extra content is ever needed** (a header, a button like the ones in `Tabs/SettingsTabGame.cs.md` or `Tabs/SettingsTabTroubleshooting.cs.md`), add `public override void Draw()` and call `base.Draw()` in the position that puts the content below/above the rows (`SettingsTab.cs.md` §base.Draw() is the entries loop, and §position list names which tabs put it first vs last).

## 6. Referenced / referenced-by

Instantiates: `SettingsEntry{T}.cs.md` (×2), `NumericSettingsEntry.cs.md`; base of `SettingsTab.cs.md`; consumed by `SettingsPage.cs.md` §tabs/§Draw/§OnShow/§finish button.
Consumed by (launcher side): `MainPage.cs.md` §10 (patch path, patch speed limit), library `Game/Patch/*` (keep patches).
Config vocabulary: `docs/XIVLauncher.Core/Program.cs.md` §`Config`; the 41-member schema itself is `docs/XIVLauncher.Core/Configuration/ILauncherConfig.cs.md` §2/§3 (upstream's equivalent is `lib/FFXIVQuickLauncher/src/XIVLauncher/Settings/ILauncherSettingsV3.cs`). `PatchPath` (`:47`, `DirectoryInfo?`) is persisted through `docs/XIVLauncher.Core/Configuration/Parsers/DirectoryInfoParser.cs.md`; `KeepPatches` (`:49`) and `PatchSpeedLimit` (`:51`, non-nullable `long`) are not in `ILauncherConfig.cs.md` §5's defaulting map — see that doc's §3 for why the latter cannot be `??=`-defaulted.
Labels: `src/XIVLauncher.Core/Resources/Localization/Strings.Designer.cs` (`…PatchPathSetting…`, `MaximumSpeedSetting…`, `KeepPatchesSetting…`).
