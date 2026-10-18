# Checkbox.cs — Specification

**File:** `src/XIVLauncher.Core/Components/Common/Checkbox.cs` · 64 lines · 1683 bytes
**Namespace:** `XIVLauncher.Core.Components.Common` · **Declaration:** `public class Checkbox : Component`
**Role:** A tick box (drawn with no text of its own) plus a clickable label drawn next to it, with an `OnChange(bool)` event

---

## 1. Purpose

`Checkbox` is the toggle-box primitive. All four instances in the launcher are the login frame's option flags (see `MainPage/LoginFrame.cs.md` §checkboxes): `oneTimePasswordCheckbox`, `useSteamServiceCheckbox`, `freeTrialCheckbox`, `autoLoginCheckbox`, constructed as `new Checkbox(Strings.UseOneTimePasswordCheckbox)` etc. Note that `SettingsPage`'s boolean settings do **not** use this component — `SettingsPage/SettingsEntry{T}.cs.md` draws its own `ImGui.Checkbox($"{Name}###{Id}", ref nativeValue)` instead (label + id in one string, no click handler, no event).

Usings: `System.Numerics`, `Hexa.NET.ImGui`.

## 2. Surface

| Member | Kind | Notes |
| --- | --- | --- |
| `inputBacking` | `private bool = false` | the only storage; handed to ImGui **by reference** in `Draw()` |
| `Label` | `public string`, **`get` only** | set once by the ctor; drawn as separate `ImGui.Text` |
| `IsEnabled` | `public bool`, `get`/`set`, `= true` | gates `BeginDisabled` around the box only — see §5 |
| `Value` | `public bool`, `get`/`set` | explicit proxy: `get => inputBacking; set => inputBacking = value;` |
| `OnChange` | `public event Action<bool>?` | fired by **both** click paths with the *new* value |
| `Checkbox(label, value = false, isEnabled = true)` | ctor | `Label`/`Value`/`IsEnabled` assignment |

## 3. Draw sequence

```
PushStyleVar(FramePadding, (0.5,0.5));  PushStyleVar(FrameRounding, 0)
PushStyleColor(FrameBg, BlueShade1);    PushStyleColor(FrameBgActive, BlueShade2)
PushStyleColor(FrameBgHovered, BlueShade2); PushStyleColor(TextDisabled, TextDisabled)
PushStyleColor(Text, Text)
[!IsEnabled → BeginDisabled]
ImGui.Checkbox($"###{Id}", ref inputBacking)   → true ⇒ OnChange?.Invoke(inputBacking)
[!IsEnabled → EndDisabled]
ImGui.SameLine();
ImGui.Text(Label)
IsItemClicked(Left) → inputBacking = !inputBacking; OnChange?.Invoke(inputBacking)
PopStyleVar(2); PopStyleColor(5)
```

- **The box itself carries no label:** the id form `$"###{Id}"` means "empty display text, id = instance `Guid`" (the `###` split convention used throughout the package — `Component.cs.md` §5). So the visible text of a checkbox is entirely the `ImGui.Text(Label)` that follows, and the box's own ImGui state is keyed to the *instance*, not the label. Two checkboxes of the same class never collide, but they also never share state.
- **`FramePadding (0.5, 0.5)`** is deliberately tiny (compare `Common/Button.cs.md`'s 16,16 and `Common/Input.cs.md`'s 12,10): the tick box is a compact square, so the label text sits close to it.
- `FrameBg`/`FrameBgActive`/`FrameBgHovered` are the box's fill (`ImGuiColors.BlueShade1`/`BlueShade2` — `ImGuiColors.cs.md`); `TextDisabled` and `Text` are pushed for the disabled/normal text colours but are **popped only at the end**, so they also colour the label text.
- `ImGui.SameLine()` then `ImGui.Text(Label)` puts the caption to the right of the box on the same line; the checkbox itself consumes a line slot, so a `Checkbox` is one row tall unless the label wraps (no width clamp here — a long `Strings.*` label wraps at the containing window's width, cf. `MainPage/LoginFrame.cs.md`'s fixed 32f window padding).

## 4. Two independent click paths — and only one of them is the box

1. **The box:** `ImGui.Checkbox(...)` returns `true` on the frame the box is clicked, and toggles its own (ImGui-side) state; the code forwards that to `OnChange?.Invoke(inputBacking)`. Because `inputBacking` is passed `ref`, ImGui's post-click value is what gets reported.
2. **The label text:** `IsItemClicked(ImGuiMouseButton.Left)` immediately after `ImGui.Text(Label)` tests *the text item* (ImGui's "last item" rule). On a left-click of the caption the code flips `inputBacking` itself and fires `OnChange` again.

Consequences for implementors:

- Clicking the **caption** and clicking the **box** are different hit areas; both must work, and the caption path mutates the C# field while the box path mutates ImGui's stored state. Keeping them in sync across frames relies on ImGui re-reading/writing the `ref` each draw — see §6.
- `OnChange` is an `event`, so it is externally composable (`+=`) but not assignable; **no file in the project binds it** (grep: zero `OnChange +=` sites), so today the toggle state is only observed by reading `Value` (`MainPage/LoginFrame.cs.md` §accessors read `.Value` every frame). A future "persist on toggle" feature should bind `OnChange`, not poll `Value`.
- Because both paths can fire in the same frame only for different items, a single click never double-fires. But `OnChange` fires *after* the new value is settled — the parameter is post-toggle state (`true` = now checked).

## 5. `IsEnabled` is honoured — but only around the box

```csharp
if (!this.IsEnabled) ImGui.BeginDisabled();
... box ...
if (!this.IsEnabled) ImGui.EndDisabled();
```

- `BeginDisabled`/`EndDisabled` wrap **only** `ImGui.Checkbox`. `ImGui.Text(Label)` and its `IsItemClicked` toggle run **outside** the disabled region, so a `Checkbox` with `IsEnabled == false` still lets the user flip it by clicking the caption. If a "read-only flag" is wanted, gate the caption path too — the pairing here is symmetric-looking but incomplete.
- `IsEnabled` is never written by any caller (grep finds no `...Checkbox.IsEnabled =`), and the ctor default (`true`) is what all four login checkboxes use. It is live plumbing, unlike `Common/Button.cs.md`'s unread `IsEnabled`, so keeping the name is worthwhile.
- The disabled visual comes from `ImGuiCol.TextDisabled` (`ImGuiColors.cs.md`), pushed here for that purpose.

## 6. `inputBacking` vs ImGui state — the durable-truth hazard

`inputBacking` is passed **by reference** into `ImGui.Checkbox`. Dear ImGui keys checkbox state to the item's id (here: the per-instance `Guid`) and republishes it into the referenced bool on each call, so:

- The authoritative tick state across frames is ImGui's per-id storage, not the C# field.
- A programmatic `Value = true` (or the `LoginFrame.IsOtp = true` chain documented in `MainPage/LoginFrame.cs.md` §accessors and written by `MainPage.cs:96-98`) writes the field and is then re-read/re-published by the *next* `Draw()`; it is not a separate store, and it can be overridden by ImGui's own stored state for that id. Do not treat `Value` as "my model's value" — treat it as "the last value exchanged with the widget".
- `Guid`-keyed ids change per process run, so persisted tick state is never carried across sessions; the login checkboxes' defaults therefore always start `false` (`inputBacking = false` at construction) unless the ctor's `value` argument sets otherwise.
- `inputBacking` is read by the getters directly (`OnChange?.Invoke(this.inputBacking)`) and never by the class's own logic beyond the click path.

## 7. Extension points / intended fixes

- The box draws nothing textual, so localization lives entirely in `Label` and can overflow: an ellipsis/`TextEx` clamp is a change for this file.
- The caption-click toggle bypasses ImGui's own bookkeeping (`inputBacking = !inputBacking`), which is how ImGui's stored state is nudged back — a "proper" fix is either to toggle only through ImGui, or to stop using the `ref` overload (`ImGui.CheckboxEx`-style) and make `inputBacking` the single source of truth. Both choices change §6 and every caller that reads `Value`.
- No `OnChecked`/`OnUnchecked` split, no keyboard activation (unlike `Button`/`Input`, this component has **no** `Enter` fallback), and no per-checkbox tooltip — `SettingsPage/SettingsEntry{T}.cs.md` is the variant that carries descriptions.
- No id suffix on the caption text: `ImGui.Text(Label)` registers a text item whose id derives from the label, so two checkboxes with the same label in one window share that text item's identity (hit-test/collision surface), while their boxes stay distinct. Prefer distinct `Strings.*` keys per checkbox.

## 8. Referenced / referenced-by

Used by: `MainPage/LoginFrame.cs.md` (four instances), `MainPage/MainPage.cs.md` (writes their `Value` through `LoginFrame`'s accessors).
Uses: `Component.Id` (`Component.cs.md`), `ImGuiColors` (`ImGuiColors.cs.md`).
Contrast: `SettingsPage/SettingsEntry{T}.cs.md` (bool entries draw their own checkbox), `Common/Button.cs.md`, `Common/Input.cs.md`.
