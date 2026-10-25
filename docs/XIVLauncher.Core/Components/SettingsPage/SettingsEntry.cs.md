# SettingsEntry.cs — Specification

**File:** `src/XIVLauncher.Core/Components/SettingsPage/SettingsEntry.cs` · 32 lines · 1,052 bytes
**Namespace:** `XIVLauncher.Core.Components.SettingsPage` · **Declaration:** `public abstract class SettingsEntry : Component` (line 14)
**Role:** Base settings row — name, input control, description, validity/warning labels, and the `Changed` event the settings page watches

---

## 1. Purpose

Replaces the immediate-mode settings entry, which re-evaluated validity, visibility, and warnings every frame. `SettingsEntry` is an Eto `Panel` (via `Component`, `Components/Component.cs.md`) holding five labels and one input control built once. Validity/warning labels update when the entry's value changes; the page recomputes the finish button from the resulting `Changed` events.

Using: `Eto.Forms` (base `Component` inherits `Eto.Forms.Panel`).

## 2. Surface

| Member | Kind | Line | Notes |
| --- | --- | --- | --- |
| `InternalValue` | `protected object?` | 16 | boxed current value, shared with the typed subclass |
| `Name` | `public abstract string` | 18 | setting label text; also the search key |
| `IsValid` | `public bool { get; protected set; }` | 20 | defaults to `true` |
| `IsVisible` | `public abstract bool` | 22 | getter; evaluated on demand |
| `LoadValues()` | `public abstract` | 24 | reads the value from config |
| `Save()` | `public abstract` | 26 | writes the value to config |
| `Changed` | `public event Action?` | 29 | no sender/args; raised by `RaiseChanged()` |
| `RaiseChanged()` | `protected` | 31 | `this.Changed?.Invoke()` |

## 3. `IsVisible` semantics

`IsVisible` is a getter, not cached: every read evaluates the subclass' visibility check (e.g. `SettingsEntry<T>` evaluates `CheckVisibility?.Invoke() ?? true`, `SettingsEntry{T}.cs.md` §2). Reads happen:

| Site | Line(s) | When |
| --- | --- | --- |
| `SettingsTab.RefreshEntries()` | `SettingsTab.cs:55` | tab rebuild (`SettingsTab.cs.md` §4) |
| `SettingsPage.RebuildPages()` | `SettingsPage.cs:136` | page shown, search text change |
| `SettingsPage.UpdateFinishButton()` | `SettingsPage.cs:151` | after rebuild and after any entry `Changed` |

Visibility therefore changes only when the page rebuilds; a value change that flips another entry's visibility is not applied until a rebuild happens.

## 4. `Changed` event contract

`Changed` is a parameterless `Action`. The typed entry raises it from `UpdateState()` (`SettingsEntry{T}.cs:211`), which runs on every input `TextChanged`/`CheckedChanged`/`SelectedIndexChanged` handler and on `LoadValues()` (`SettingsEntry{T}.cs:219`). `SettingsPage` subscribes every entry of every tab at construction (`SettingsPage.cs:69-71`), so one entry's change refreshes the global finish-button state.

## 5. Consumers

| Consumer | Lines | Uses |
| --- | --- | --- |
| `SettingsEntry<T>` | `SettingsEntry{T}.cs:23` | subclass; `Name`, `InternalValue`, `IsValid`, `IsVisible`, `LoadValues`, `Save`, `RaiseChanged` |
| `NumericSettingsEntry` | `NumericSettingsEntry.cs:13` | `SettingsEntry<int>` subclass (`NumericSettingsEntry.cs.md`) |
| `DalamudBranchMetaSettingsEntry` | `DalamudBranchMetaSettingsEntry.cs:15` | `SettingsEntry<string>` subclass (`DalamudBranchMetaSettingsEntry.cs.md`) |
| `SettingsTab` | `SettingsTab.cs:27-39, 51-57` | `LoadValues`/`Save` loops, `IsVisible` filter |
| `SettingsPage` | `SettingsPage.cs:69-71, 126, 136, 151` | `Changed` subscription, `Name` search, `IsVisible`, `IsValid` |

Full consumer map: `SettingsPage.cs.md` §7.

## 6. Quirks

- `Component.Id` (`Components/Component.cs.md` §3) is inherited and unused; ImGui-style ids are not needed.
- `IsValid` defaults `true` and is only written by the typed subclass' `UpdateState`; an entry with no `CheckValidity` is always valid.
- `LoadValues`/`Save` are named `LoadValues` (not `Load`) because the base `Component` had no `Load`; the rename removes the `new`-hiding warnings settings entries had against the old immediate-mode API.
- Re-parenting an entry into search results moves the whole `Panel` (all five labels), so an entry can only live in one place at a time (`SettingsPage.cs.md` §6).

## 7. Cross-references

Uses: `Component` (`Components/Component.cs.md`).
Referenced by: `SettingsEntry{T}.cs.md`, `NumericSettingsEntry.cs.md`, `DalamudBranchMetaSettingsEntry.cs.md`, `SettingsTab.cs.md`, `SettingsPage.cs.md`.
