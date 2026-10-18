# NumericSettingsEntry.cs — Specification

**File:** `src/XIVLauncher.Core/Components/SettingsPage/NumericSettingsEntry.cs` · 33 lines · 943 bytes
**Namespace:** `XIVLauncher.Core.Components.SettingsPage` · **Declaration:** `public class NumericSettingsEntry : SettingsEntry<int>`
**Role:** The one `int` row type — an `ImGui.InputInt` widget with min/max/step, riding on the generic entry's description/validity tail

---

## 1. Purpose

`SettingsEntry<T>`'s runtime type switch (`SettingsEntry{T}.cs.md` §3) has no branch for `int`, so a numeric row cannot get a widget from the generic class. `NumericSettingsEntry` fills the gap by *subclassing* `SettingsEntry<int>`: it overrides `Draw()` to emit the `InputInt`, then calls `base.Draw()` to inherit the grey description, validity and warning sections (`SettingsEntry{T}.cs.md` §3 tail rows). This is the canonical "unusual widget, standard tail" pattern in the package — contrast `DalamudBranchMetaSettingsEntry.cs.md`, which overrides `Draw()` *without* `base.Draw()`.

## 2. Surface

| Member | Anchor | Form | Notes |
| --- | --- | --- | --- |
| `MinValue` / `MaxValue` / `Step` | `:7`–`:9` | `public int { get; set; }` | mutable accessors, set from ctor defaults |
| ctor | `:11-:21` | `public NumericSettingsEntry(name, description, load, save, minValue = 0, maxValue = int.MaxValue, step = 1)` | `: base(name, description, load, save)` — delegates to `SettingsEntry<int>`'s ctor |
| `id` | `:10` | `private readonly Guid id = Guid.NewGuid()` | **its own** Guid, distinct from `Component.Id` |
| `Draw()` | `:23-:31` | `override` | §3 |

Inherited and relied on: `Value`, `Name`, `Description`, `CheckValidity`/`CheckWarning`/`CheckVisibility`, `Load()`, `Save()`, `IsValid`, `InternalValue` — all from `SettingsEntry{T}.cs.md`; the class declares none of them.

## 3. `Draw()`

```csharp
var nativeValue = this.Value;
ImGuiHelpers.TextWrapped(this.Name);
if (ImGui.InputInt($"###{this.id:N}", ref nativeValue, Step))
    this.InternalValue = Math.Max(this.MinValue, Math.Min(this.MaxValue, nativeValue));
base.Draw();
```

- **`TextWrapped(Name)` first, then `InputInt` with an empty-label id** — same layout contract as the `string`/`DirectoryInfo` branches in `SettingsEntry{T}.cs.md` §3 (a numeric row reads like a text row, and `SettingsPage.cs.md` §search mode draws the row without extra context, so the name must be part of the row).
- **The clamp is applied on write, not on display**: `Math.Max(min, Math.Min(max, nativeValue))` means an out-of-range typed value is silently coerced into range and stored. The displayed value, however, is what `InputInt` echoed (the pre-clamp number) until the next frame re-reads `Value` — that one-frame lag is a consequence of `SettingsEntry{T}.cs.md` §5's "the row is re-read every frame" design, and it is *not* a way to store an out-of-range value. If a tab ever needs a visible "clamped" notice, the place for it is a `CheckWarning` hook at the construction site, not more logic here (which would then disagree with `Load()`'s un-clamped value).
- **`base.Draw()` runs the `SettingsEntry<int>` type switch again**, which for `int` emits nothing (`SettingsEntry{T}.cs.md` §3, "everything else — no widget") and then the description/validity/warning tail. So the two `Draw()`s compose without duplication — but note that `SettingsEntry<int>.Draw()` *also* recomputes `IsValid` there; validity therefore reflects `CheckValidity(this.Value)` after the clamp, which is what a numeric validator sees.
- **`this.id`, not `Id`**: `Component.cs.md` §`Id` is `protected` and readable here, but this class deliberately mints its own `private readonly Guid id`. Two consequences to keep: (a) the ImGui id for a numeric row is `${this.id:N}` — the `:N` Guid format (hex, no hyphens), so ids written elsewhere (`SettingsEntry{T}.cs.md` uses `${Id.ToString()}`, hyphenated) differ in *format*, not uniqueness — an ImGui id that must be stable per row, not shared; (b) anything that later keys ImGui state by `Component.Id` will not find numeric rows. Do not "simplify" by reusing `Id` unless you also confirm nothing depends on the format.
- **The value is handed to the widget by `ref`** (see the binding-semantics discussion in `Common/Input.cs.md` §`ref` overloads and `Common/Checkbox.cs.md`), and `this.Value` is `T?` = `int?` here — the write-back lands in the nullable slot, and `InternalValue = <int>` re-boxes it. Keep `Math.Max/Math.Min` ordering (min outer, max inner) if you touch it: reversing them makes `MinValue` win over `MaxValue` when a tab passes an inverted range (e.g. `Tabs/SettingsTabPatching.cs.md` §`MaximumSpeedSetting` passes `0, int.MaxValue, 1000` — a correct range, but the pattern invites copying with swapped bounds).

## 4. Ctor defaults and the call sites

Defaults are `0 … int.MaxValue` step `1`. Every call site in the package overrides them:

| Site | Range | Step |
| --- | --- | --- |
| `Tabs/SettingsTabPatching.cs.md` §`MaximumSpeedSetting` | `0 … int.MaxValue` | `1000` |
| `Tabs/SettingsTabDalamud.cs.md` §`DalamudInjectionDelaySetting` | `0 … int.MaxValue` | `1000` |

Both step by 1000 (ms for the Dalamud delay, bytes/s for the patch speed limit — see that spec's and `MainPage.cs.md` §9/§10 for what the value feeds). Consequence: today every numeric row is effectively "unbounded above, coarse step"; the `MinValue`/`MaxValue` accessors exist as the mechanism, not as used bounds. If a bounded row is added, the accessors are `{ get; set; }` (mutable) — assignable in the ctor body here, and re-assignable later, which the `init` pattern of `SettingsEntry{T}.cs.md` §hooks deliberately forbids for the `Check*` hooks. Keep min/max/step ctor-param style so the tabs' call sites stay one-liners.

## 5. Invariants and extension points

- **`T` is fixed to `int`** by the class declaration (`SettingsEntry<int>`), so `Load()`/`Save()` inherited from `SettingsEntry<int>` type-check against `Func<int?>`/`Action<int?>`. A `float`/`long` variant would be a *new* class (`SettingsEntry<double>` + `InputDouble`), not a change to this one — `Tabs/SettingsTabPatching.cs.md` §`MaximumSpeedSetting` shows the workaround (`Program.Config.PatchSpeedLimit` is widened/narrowed by casts in the lambdas: `(int)Program.Config.PatchSpeedLimit` on read, `x => Program.Config.PatchSpeedLimit = x` on write).
- **`Step` is the widget's increment, not a validation rule**: nothing enforces "value is a multiple of step"; `InputInt`'s step only nudges the typed value (see `docs/XIVLauncher.Core/ImGuiHelpers.cs.md` for how ImGui sliders/step behave). Do not add a modulo check to `IsValid`; a step is a UX affordance here.
- **No `Check*` hooks are set at either call site**, so numeric rows are always visible and always valid — they can never contribute to the finish button's `!y.IsValid` red state (`SettingsPage.cs.md` §finish button). A numeric validator, if added, belongs in the tab's designated initializer, which is legal despite the `init` restriction (assignment happens at construction).

## 6. Referenced / referenced-by

Derives from: `SettingsEntry{T}.cs.md` (its ctor, `Value`, `Draw` tail, `Load`/`Save`), which in turn implements `SettingsEntry.cs.md`; uses `Component.cs.md` §`Draw` chain via `base.Draw()`.
Instantiated by: `Tabs/SettingsTabPatching.cs.md`, `Tabs/SettingsTabDalamud.cs.md`.
Values feed: `Program.Config.PatchSpeedLimit` (→ `Program.Patcher` construction, see `MainPage.cs.md` §10) and `Program.Config.DalamudLoadDelay` (→ `DalamudLauncher` ctor argument, see `MainPage.cs.md` §9), via `docs/XIVLauncher.Core/Program.cs.md` §`Config`.
