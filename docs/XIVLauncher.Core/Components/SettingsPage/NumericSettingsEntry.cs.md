# NumericSettingsEntry.cs — Specification

**File:** `src/XIVLauncher.Core/Components/SettingsPage/NumericSettingsEntry.cs` · 55 lines · 1,858 bytes
**Namespace:** `XIVLauncher.Core.Components.SettingsPage` · **Declaration:** `public class NumericSettingsEntry : SettingsEntry<int>` (line 13)
**Role:** Bounded integer settings row backed by a native Eto `NumericStepper`

---

## 1. Purpose

Replaces the ImGui integer input (HEAD used `ImGui.InputInt(..., ref nativeValue, Step)` and clamped in its own draw pass). Here the stepper clamps through its own minimum/maximum, and the row clamps again on change so the stored value stays inside the configured range.

Using: `Eto.Forms` (`NumericStepper`).

## 2. Surface

| Member | Kind | Line | Notes |
| --- | --- | --- | --- |
| `MinValue` | `public int { get; set; }` | 15 | lower bound, set from ctor argument (22) |
| `MaxValue` | `public int { get; set; }` | 16 | upper bound (23) |
| `Step` | `public int { get; set; }` | 17 | stepper increment (24) |
| ctor | `public` | 19-34 | `SettingsEntry<int>` base, then range fields, then stepper range repair (28-33) |
| `CreateInput()` | `protected override Control` | 36-54 | builds `NumericStepper`, `applyValue`, `ValueChanged` |

Ctor signature: `(string name, string description, Func<int> load, Action<int> save, int minValue = 0, int maxValue = int.MaxValue, int step = 1)` (19).

## 3. Construction order (the important constraint)

`SettingsEntry<T>`'s constructor calls the virtual `CreateInput()` (`SettingsEntry{T}.cs:62`) **before** `NumericSettingsEntry`'s constructor body runs. During `CreateInput()` the auto-properties `MinValue`/`MaxValue`/`Step` are still `0`, so the object initializer at lines 39-43 sets `MinValue = 0`, `MaxValue = 0`, `Increment = 0`.

The Eto GTK handler applies these directly to the spin button's adjustment:

- `NumericStepperHandler` creates `Gtk.SpinButton(double.MinValue, double.MaxValue, 1)` (`/tmp/eto-src/src/Eto.Gtk/Forms/Controls/NumericStepperHandler.cs:8`)
- `MaxValue` setter → `Control.Adjustment.Upper` (line 136)
- `MinValue` setter → `Control.Adjustment.Lower` (line 146)
- `Value` setter → `Control.Value = Math.Max(MinValue, Math.Min(MaxValue, value))` (line 128)

With `Lower == Upper == 0`, every value write clamps to `0`; a verified Eto run (`min=0 max=0 inc=0 value=0` after `Value = 1000`) confirms this. The constructor body therefore re-applies the real range on the created stepper (28-33), after which `LoadValues()` can display the loaded value and the arrows/typing work.

**Consequence:** do not remove lines 28-33; the object initializer in `CreateInput` (39-43) cannot fix the range because the range fields do not exist yet at that point.

## 4. Value flow

- `this.applyValue = () => stepper.Value = this.Value` (45) — registered during `CreateInput`, invoked by `SettingsEntry<T>.LoadValues()` (`SettingsEntry{T}.cs:218`), i.e. on `SettingsTab.LoadValues()` from `SettingsPage.OnShow()` (`SettingsPage.cs:86-88`).
- `stepper.ValueChanged` (47-51): `InternalValue = Math.Max(MinValue, Math.Min(MaxValue, (int)stepper.Value))` (49), then `UpdateState()` (50) → validity/warning labels + `Changed` event (`SettingsEntry.cs.md` §4).
- `NumericStepper.Value` is a `double`; the cast at line 49 truncates.
- Persistence happens only in `SettingsEntry<T>.Save()` (`SettingsEntry{T}.cs:222`), triggered by the finish button (`SettingsPage.cs:61-63`).

## 5. Consumers

| Consumer | Line | Use |
| --- | --- | --- |
| `SettingsTabPatching` | `SettingsTabPatching.cs:23-24` | `MaximumSpeedSetting` row: `() => (int)Program.Config.PatchSpeedLimit`, `x => Program.Config.PatchSpeedLimit = x`, `minValue: 0`, `maxValue: int.MaxValue`, `step: 1000` |

`Program.Config` is the `ILauncherConfig` (`Program.cs:39`), and `PatchSpeedLimit` is consumed by the patcher in the lib (`lib/FFXIVQuickLauncher`, out of docs scope). `MainPage` passes the speed limit to `PatchManager` (`MainPage.cs:939`).

## 6. Quirks

- **Range repair is required** (see §3); without it the speed-limit row shows `0` and cannot be raised.
- The row keeps the name label (the base entry's `nameLabel`) because the input is not a check box; the label sits above the stepper (`SettingsEntry{T}.cs.md` §6).
- `WidthChars = 5` is the Eto GTK default (`NumericStepperHandler.cs:9`), so a value of `2147483647` (10 digits) is wider than the default width request.
- No `CheckValidity`/`CheckWarning` hooks are wired in this subclass; validity is driven only by the base `UpdateState` path (always valid when no `CheckValidity` is set).
- The row is a `SettingsEntry<int>`, so `Value` is `int?`; `SettingsEntry<T>.Value` casts `InternalValue` (`SettingsEntry{T}.cs:47`).

## 7. Cross-references

Uses: `SettingsEntry<T>` (`SettingsEntry{T}.cs.md`), `Eto.Forms.NumericStepper` (Eto GTK handler: `/tmp/eto-src/src/Eto.Gtk/Forms/Controls/NumericStepperHandler.cs`).
Referenced by: `SettingsTabPatching.cs.md`, `SettingsPage.cs.md` §2.
