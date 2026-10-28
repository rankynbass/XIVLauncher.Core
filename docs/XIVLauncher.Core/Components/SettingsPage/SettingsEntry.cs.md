# SettingsEntry.cs — Specification

**File:** `src/XIVLauncher.Core/Components/SettingsPage/SettingsEntry.cs` · 41 lines · 1,658 bytes
**Namespace:** `XIVLauncher.Core.Components.SettingsPage` · **Declaration:** `public abstract class SettingsEntry : Component` (line 14)
**Role:** Base settings row — name, input control, description, validity/warning labels, visibility/enabled gates, and the `Changed` event the settings page watches

---

## 1. Purpose

Replaces the immediate-mode settings row, which re-evaluated validity, visibility, and warnings every frame. `SettingsEntry` is an Eto `Panel` (via `Component`, `Components/Component.cs.md`) holding five labels and one input control built once. Validity, warning, and enabled labels update when the entry's value changes; the page recomputes the finish button from the resulting `Changed` events.

Using: `Eto.Forms` (base `Component` inherits `Eto.Forms.Panel`).

## 2. Surface

| Member | Kind | Line | Notes |
| --- | --- | --- | --- |
| `InternalValue` | `protected object?` | 16 | boxed current value, shared with the typed subclass |
| `Name` | `public abstract string` | 18 | setting label text; also the search key |
| `IsValid` | `public bool { get; protected set; }` | 20 | defaults to `true` |
| `IsVisible` | `public virtual bool` | 24 | default `true`; platform gates hide the row at page build (doc comment 22-23) |
| `IsEnabled` | `public virtual bool` | 28 | default `true`; conditional gates render the row disabled (doc comment 26-27) |
| `RefreshState()` | `public abstract` | 31 | recomputes validity, warning, and enabled state without raising `Changed` (doc comment 30) |
| `LoadValues()` | `public abstract` | 33 | reads the value from config |
| `Save()` | `public abstract` | 35 | writes the value to config |
| `Changed` | `public event Action?` | 38 | no sender/args; raised by `RaiseChanged()` (doc comment 37) |
| `RaiseChanged()` | `protected` | 40 | `this.Changed?.Invoke()` |

## 3. `IsVisible` / `IsEnabled` semantics

Two gates with two lifetimes:

- **Platform gates** (`CheckVisibility`, `SettingsEntry{T}.cs.md` §2) — rows that can never be usable on this OS (e.g. the Unix-only game config path, `SettingsTabGame.cs.md` §3) return `false`; the row is **absent** from the tab layout and search results. Evaluated at page build.
- **Conditional gates** (`CheckEnabled`) — rows gated by other settings (manual-injection path, Wine startup-type combos, Steam-compat rows) stay **visible and rendered disabled** (`Enabled = false` cascades to all child controls; GTK container sensitivity, WinForms `Panel.Enabled`). Re-evaluated live.

Evaluation sites:

| Site | Line(s) | When |
| --- | --- | --- |
| `SettingsTab.RefreshEntries()` | `SettingsTab.cs:62-63` | tab rebuild — `IsVisible` filter (`SettingsTab.cs.md` §4) |
| `SettingsPage.RebuildPages()` | `SettingsPage.cs:205` | search results — `IsVisible` filter |
| `SettingsPage.RefreshAllStates()` | `SettingsPage.cs:245-250` | every entry `Changed` — `RefreshState()` on every entry |
| `SettingsPage.UpdateFinishButton()` | `SettingsPage.cs:254` | validity scan uses `IsVisible && IsEnabled && !IsValid` |

`IsVisible` changes only when the page rebuilds; `IsEnabled` updates live on every entry change, so a checkbox/combo gate flips the dependent row's enabled state without a rebuild.

## 4. `Changed` event contract

`Changed` is a parameterless `Action`. The typed entry raises it from `UpdateState(notifyChanged: true)` (`SettingsEntry{T}.cs:278-279`), which runs on every input `TextChanged`/`CheckedChanged`/`SelectedIndexChanged` handler and on `LoadValues()` (`SettingsEntry{T}.cs:287`). `SettingsPage` subscribes every entry of every tab at construction (`SettingsPage.cs:91-98`): one handler refreshes the finish button, the other runs `RefreshAllStates()`, so one entry's change re-evaluates every row's gate, validity, and warning.

## 5. Consumers

| Consumer | Lines | Uses |
| --- | --- | --- |
| `SettingsEntry<T>` | `SettingsEntry{T}.cs:26` | subclass; `Name`, `InternalValue`, `IsValid`, `IsVisible`, `IsEnabled`, `RefreshState`, `LoadValues`, `Save`, `RaiseChanged` |
| `NumericSettingsEntry` | `NumericSettingsEntry.cs:13` | `SettingsEntry<int>` subclass (`NumericSettingsEntry.cs.md`) |
| `DalamudBranchMetaSettingsEntry` | `DalamudBranchMetaSettingsEntry.cs:15` | `SettingsEntry<string>` subclass (`DalamudBranchMetaSettingsEntry.cs.md`) |
| `SettingsTab` | `SettingsTab.cs:30-43, 61-63` | `LoadValues`/`Save` loops, `IsVisible` filter in `RefreshEntries` |
| `SettingsPage` | `SettingsPage.cs:91-98, 205, 254` | `Changed` subscription, `Name` search, `IsVisible` filter, `IsEnabled`/`IsValid` scan |

Full consumer map: `SettingsPage.cs.md` §7.

## 6. Quirks

- `Component.Id` (`Components/Component.cs.md` §3) is inherited and unused; ImGui-style ids are not needed.
- `IsValid` defaults `true` and is only written by the typed subclass' `UpdateState`; an entry with no `CheckValidity` is always valid.
- `IsEnabled` defaults `true`; only the typed subclass overrides it, and the enabled write happens inside `UpdateState` (`SettingsEntry{T}.cs:257`), which `RefreshState()` invokes with `notifyChanged: false`.
- `LoadValues`/`Save` are named `LoadValues` (not `Load`) because the base `Component` had no `Load`; the rename removes the `new`-hiding warnings settings entries had against the old immediate-mode API.
- Re-parenting an entry into search results moves the whole `Panel` (all five labels), so an entry can only live in one place at a time (`SettingsPage.cs.md` §6).

## 7. Cross-references

Uses: `Component` (`Components/Component.cs.md`).
Referenced by: `SettingsEntry{T}.cs.md`, `NumericSettingsEntry.cs.md`, `DalamudBranchMetaSettingsEntry.cs.md`, `SettingsTab.cs.md`, `SettingsPage.cs.md`.
