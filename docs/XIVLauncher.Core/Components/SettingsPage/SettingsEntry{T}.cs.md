# SettingsEntry{T}.cs — Specification

**File:** `src/XIVLauncher.Core/Components/SettingsPage/SettingsEntry{T}.cs` · 143 lines · 4292 bytes
**Namespace:** `XIVLauncher.Core.Components.SettingsPage` · **Declaration:** `public class SettingsEntry<T> : SettingsEntry`
**Role:** The generic "one persisted config value ↔ one ImGui row" renderer — value type is chosen by `T`, widgets by `typeof(T)` at runtime

---

## 1. Purpose

Almost every row on the Settings page is `new SettingsEntry<SomeType>(label, description, () => Program.Config.X, x => Program.Config.X = x)`. The class is the *entire* persistence bridge: the two lambdas are the getter and setter for one field of `Program.Config` (see `docs/XIVLauncher.Core/Program.cs.md` §`Config` and `lib/FFXIVQuickLauncher/src/XIVLauncher.Common/Configuration/ILauncherConfig.cs`), and `InternalValue` is the frame-local copy the user edits in the widget. Nothing here knows about `App.Settings`/`LauncherApp` — the row talks to the process-wide `Program.Config` through the lambdas only, which is why no tab class needs a `LauncherApp` (contrast `MainPage.cs.md` §ctor, where children do).

## 2. Surface

| Member | Anchor | Form | Contract |
| --- | --- | --- | --- |
| `load` / `save` | `:10` / `:11` | `private readonly Func<T?>` / `Action<T?>` | ctor-supplied config bridge |
| `Name` | `:13` | `public override string { get; }` | ctor-assigned |
| `Description` | `:15` | `public string { get; }` | ctor-assigned, always drawn (grey) |
| `CheckValidity` | `:17` | `public Func<T?, string?>? { get; init; }` | `null` ⇒ always valid; non-empty return ⇒ invalid + that text drawn |
| `CheckWarning` | `:19` | `public Func<T?, string?>? { get; init; }` | non-`null` return is drawn in red but does **not** affect `IsValid` |
| `CheckVisibility` | `:21` | `public Func<bool>? { get; init; }` | `null` ⇒ visible |
| `IsVisible` | `:23` | `public override bool => CheckVisibility?.Invoke() ?? true` | classic accessor over the `init` accessor above |
| `Value` | `:25` | `public T? Value => InternalValue == default ? default : (T)InternalValue` | the cast-out of the `object?` slot |
| ctor | `:27-:33` | `public SettingsEntry(name, description, load, save)` | four args; the three `Check*` hooks are set with designated initializers at the call site |
| `Draw()` | `:35-:126` | `override` | §3 |
| `Load()` | `:128-:140` | `override` | §4 |
| `Save()` | `:142` | `override` (expression body) | `=> this.save(this.Value)` |

## 3. `Draw()` — the runtime type switch

`var type = typeof(T);` (`:37`) then a chain of `type == …` tests, so the *widget* is picked at runtime from the runtime type (the generic parameter is erased by monovaluation, hence the test, and the chain order is what decides which widgets a type gets):

| `T` | Anchor | Widget | Max length / notes |
| --- | --- | --- | --- |
| `DirectoryInfo` | `:39` | `TextWrapped(Name)` + `ImGui.InputText($"###{Id}", ref nativeBuffer, 10000)` | writes back `new DirectoryInfo(buf)` or `null` for empty input (`:48`) |
| `string` | `:51` | `TextWrapped(Name)` + `ImGui.InputText($"###{Id}", ref nativeBuffer, 1000)` | writes the raw string (`:58`) |
| `bool` | `:62` | `ImGui.Checkbox($"{Name}###{Id}", ref nativeValue)` | **name is in the label**, no separate `TextWrapped` |
| any enum | `:71` | `TextWrapped(Name)` + `BeginCombo($"###{Id}", descriptions[idx].FriendlyName)` + `Selectable(…)` per member | `idx = (int)(InternalValue ?? 0)` (`:75`) |
| everything else | — | **no widget** | only `NumericSettingsEntry.cs.md` exploits this (its `T = int` reaches no branch; it supplies `InputInt` in its own `Draw` and gets the description/validity tail by calling `base.Draw()`) |

- **The id convention differs per widget**, and matches `Common/Checkbox.cs.md`/`Common/Input.cs.md`: `$"###{Id}"` = empty display text, id = `Component.Id` (`Component.cs.md` §Id, `protected Guid Id { get; } = Guid.NewGuid()` — accessible here because this class derives from `Component`). The `Checkbox` call is the only one that puts `Name` *before* the `###`, because a checkbox's label is the only thing the user sees. Do not "add a label" to the `InputText`/`Combo` ids: the name is already drawn with `TextWrapped`, and a second copy of the name in the label is what makes the row read twice.
- **The enum branch's description strings come from C# attributes**: `x.GetAttribute<SettingsDescriptionAttribute>()` (`:77`) reads `[SettingsDescription("Friendly", "description")]` annotations on the enum members (`lib/FFXIVQuickLauncher/src/XIVLauncher.Common/SettingsAnnotation.cs`, `AttributeUsage(AttributeTargets.Field)`), e.g. `lib/FFXIVQuickLauncher/src/XIVLauncher.Common.Unix/Compatibility/{Dxvk,WineSettings}.cs`. The fallback `?? new SettingsDescriptionAttribute(x.ToString(), string.Empty)` is what keeps un-annotated enums (`ClientLanguage`, `DpiAwareness`, `WineSyncType`, `DalamudLoadMethod`… mixed) renderable.
- **`"dummy"` is a real sentinel** (`:82`: `desc == string.Empty || desc == "dummy"` suppresses the `" - " + desc` suffix): the library annotates some enums with the literal string `"dummy"` (`lib/FFXIVQuickLauncher/src/XIVLauncher.Common/Dalamud/DalamudLoadMethod.cs:5/:8/:11`). Keep the filter, or those combos gain a meaningless " - dummy" tail.
- **`descriptions[value]` is indexed by the enum's ordinal** and `BeginCombo`'s display value is `descriptions[idx]`, i.e. the code assumes `Enum.GetValues(type)` returns members in ordinal order starting at 0 and that the annotated array aligns with it. True for default-declared C# enums; an enum with explicit non-contiguous ordinals would silently mis-label rows. If such a type is ever added, replace the index arithmetic with a value→index lookup here, not in the tab.
- **The grey description block is unconditional** (`:95-:97`), then validity (`:99-:114`) then warning (`:116-:123`), then `base.Draw()` (`:125`, = `Component.Draw()` children loop). Ordering, not grouping, is what a subclass inherits when it calls `base.Draw()` — see `NumericSettingsEntry.cs.md` §Draw and `DalamudBranchMetaSettingsEntry.cs.md` §Draw (which deliberately does **not** call `base.Draw()` and re-implements the grey description instead).

## 4. `Load()` and the validity double-definition

```csharp
public override void Load()
{
    this.InternalValue = this.load();
    if (this.CheckValidity != null) this.IsValid = this.CheckValidity(this.Value) == null;
    else                            this.IsValid = true;
}
```

vs. `Draw()`'s version (`:101-:103`): `IsValid = string.IsNullOrEmpty(validityMsg)`. Two observations:

- `Load()` runs from `SettingsTab.cs.md` §Load (itself called by `SettingsPage.cs.md` §OnShow) *before* any draw; `Draw()` re-evaluates validity every frame. That is what keeps `SettingsPage.cs.md` §finish button's `y.IsVisible && !y.IsValid` test live while the user types into a path field.
- The two use **different emptiness tests**: a validator that returns `""` is *valid* after `Load()` and *invalid* on the next draw. Harmless today (no validator returns `""` — every tab validator returns `null` for OK), but it is a trap for new validators: returning empty text instead of `null` makes the two paths disagree, and the row flickers valid/invalid across `Load()` and `Draw()`. Keep validators returning `null`-for-OK (the convention every tab uses).
- `CheckWarning` is evaluated only in `Draw()` — a warning therefore cannot block the finish button by construction (`SettingsPage.cs.md` §finish button consults `IsValid` only). That's the intended split: "this is dangerous, be aware" vs "this will not work".

## 5. Invariants and extension points

- **`load`/`save` lambdas capture `Program.Config`, not a snapshot** — they are re-read on every `Load()` and written on every `Save()`. Do not replace them with pre-computed values; the whole point (see the tab specs) is that a row can be re-pointed at a different config field, and that a save lambda can perform side effects (`Tabs/SettingsTabDalamud.cs.md` §cross-entry coupling, `Tabs/SettingsTabGame.cs.md` §Draw).
- **The three `Check*` hooks are `init` accessors**: assignable only in the designated-initializer list at construction (`new SettingsEntry<bool>(…) { CheckVisibility = … }` — that is the shape used by every tab; `Tabs/SettingsTabWine.cs.md` §dependency chain and `Tabs/SettingsTabTroubleshooting.cs.md` §ForceLocale entry are the examples). They cannot be re-pointed later; a row needing dynamic hooks must implement them as closures reading live state, which is what the tabs do.
- **Visibility/validity predicates run per frame** (once in the tab filter, once or twice in `SettingsPage.cs.md`'s search path, once for validity) — they must be cheap. `Tabs/SettingsTabWine.cs.md` §fsync validity check is the one place that pays a real cost each frame (`WineUtility.SystemFsyncSupport()`); if more are added, cache in a `private` accessor on the tab (the `FeralGameModeFound` memo pattern) instead of making this class cache results — this class has no per-row state budget.
- **`Save()` passes `this.Value`, i.e. the `T?` cast through `default`-normalisation** (`:25`): `Value` returns `default` when `InternalValue` is `default`-comparable. For a row whose "unset" state must be distinguishable from `false`/`0`/`""`, `Value` cannot express it — the `bool` default *is* `false` (`Tabs/SettingsTabDalamud.cs.md` §`enableManualInjection.Value == false` test relies on that collapse). Use a wrapper type or a nullable-in-config field pattern instead (the tabs' `Program.Config.X ?? fallback` getters are that pattern).
- **`SettingsEntry<T>` is a concrete class with `abstract` members of its base**, so `T`-specific behaviour lives either in this type switch or in a subclass overriding `Draw()` (`NumericSettingsEntry.cs.md` is the precedent). Prefer the subclass when the widget is unusual and the tail (description/validity/warning) is standard: `base.Draw()` gives you the tail for free, `DalamudBranchMetaSettingsEntry.cs.md` shows the cost of skipping it (it must duplicate the grey `TextWrapped(Description)` and loses `IsValid` recomputation — see that spec's §Draw note).
- **null boxes read as `default`, not as an error**: reproducing `Value`'s expression in a scratch .NET 10 test of the same shape (`object internalValue = <null>; internalValue == default ? default : (T)internalValue`) evaluates the comparison **true** for value-type `T` (`bool → false`, enum → first member, `int → 0`), so the `(T)` cast is never reached and `Value` never throws. That is what keeps a row whose `load()` returns `null` (e.g. `Tabs/SettingsTabWine.cs.md` §`EnableDXVKOverlaySetting`, the one value-type row with no `??` fallback in its getter) renderable: it silently shows the first enum member. Do not "fix" that row by adding `?? DxvkHudType.None` without checking the library default (`Program.cs.md` §`CreateCompatToolsInstance` passes `Config.DxvkHudType` straight into the `CompatibilityTools` ctor, so the two sides must agree on what "unset" means).
- `load` returns `T?`, `save` takes `T?` — so both sides tolerate "unset". Keep that signature: the `DirectoryInfo` branch writes `null` (`:48`) and tab getters return `null` for unset paths (`Tabs/SettingsTabGame.cs.md` §`GamePathSetting`, `Tabs/SettingsTabPatching.cs.md`).

## 6. Referenced / referenced-by

Base of: `NumericSettingsEntry.cs.md`, `DalamudBranchMetaSettingsEntry.cs.md`; implements `SettingsEntry.cs.md`.
Instantiated by: `Tabs/SettingsTabGame.cs.md`, `Tabs/SettingsTabPatching.cs.md`, `Tabs/SettingsTabWine.cs.md`, `Tabs/SettingsTabDalamud.cs.md`, `Tabs/SettingsTabTroubleshooting.cs.md` (and reached through `SettingsTab.cs.md` §Draw/§Load/§Save, plus `SettingsPage.cs.md` §search mode).
Uses: `docs/XIVLauncher.Core/ImGuiHelpers.cs.md` §`TextWrapped`, `docs/XIVLauncher.Core/ImGuiColors.cs.md` §`DalamudGrey`/`DalamudRed`, `Component.cs.md` §Id + `Draw` chain, `lib/FFXIVQuickLauncher/src/XIVLauncher.Common/SettingsAnnotation.cs`, Hexa.NET.ImGui `InputText`/`Checkbox`/`Combo`/`Selectable` (see the `ref`-binding discussion in `Common/Input.cs.md` and `Common/Checkbox.cs.md`).
