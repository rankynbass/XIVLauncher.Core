# Button.cs — Specification

**File:** `src/XIVLauncher.Core/Components/Common/Button.cs` · 45 lines · 1379 bytes
**Namespace:** `XIVLauncher.Core.Components.Common` · **Declaration:** `public class Button : Component`
**Role:** Reusable labelled button with a colour triple and a `Click` event — the primitive every page composes actions onto

---

## 1. Purpose

`Button` is the shared "pressable label" for the launcher UI. It draws one ImGui button and, when pressed, fires an `event Action? Click` to which callers subscribe (`+=`) their own closures. It carries no domain knowledge: login, settings, OTP, FTS and news links all use it.

Known binding sites (all outside this file):

| Site | Effect of a click |
| --- | --- |
| `MainPage/LoginFrame.cs.md` | `loginButton.Click += TriggerLogin` → `OnLogin?(LoginAction.Game)` |
| `LoadingPage/LoadingPage.cs.md` | `cancelButton.Click += () => this.Cancelled?.Invoke()` |
| `LoadingPage/LoadingPage.cs.md` | `disableAutoLoginButton.Click += () => { hasDisabledAutoLogin = true; App.Settings.IsAutologin = false; }` |
| `Component.cs.md` (base) / page draw loops | none — the button is a leaf |

Usings: `System.Numerics`, `Hexa.NET.ImGui`; reads `ImGuiColors` (`ImGuiColors.cs.md`).

## 2. Surface

| Member | Kind | Notes |
| --- | --- | --- |
| `IsEnabled` | `public bool`, `get`/`set`, `= true` | **never read anywhere in the project** — see §4 |
| `Label` | `public string`, `get`/`set` | doubles as ImGui id *and* displayed text |
| `Color` / `HoverColor` / `TextColor` | `public Vector4`, `get`/`set` | ImGui `Button`, `ButtonHovered`, `Text` colours |
| `Click` | `public event Action?` | nullary event; `+=`/`-=` composition, `?.Invoke()` fire |
| `Width` | `public int?`, `get`/`set` | `null` ⇒ auto width; otherwise explicit pixel width |
| `Button(label, isEnabled = true, color = null, hoverColor = null, textColor = null)` | ctor | null colour ⇒ defaults, see §3 |

Declared *after* the fields, mid-class — cosmetic only. The ctor assigns `Label`, `IsEnabled`, and the three colours with `x ?? default`.

## 3. Colour defaults

| Field | Default (`??` fallback) | Source |
| --- | --- | --- |
| `Color` | `ImGuiColors.Blue` | `ImGuiColors.cs.md` |
| `HoverColor` | `ImGuiColors.BlueShade3` | same file, darker ramp step ⇒ "pressed/hover" look |
| `TextColor` | `ImGuiColors.DalamudWhite` | plain theme white |

Because the ctor resolves `null` to defaults, a caller **cannot** express "use the theme's own button colour" — an explicit `Vector4` must be passed. Conversely, mutating `Color` after construction *does* take effect the next frame, since the accessors are read in `Draw()` per frame. All three are get/set, so per-frame re-tinting (e.g. a "disabled-looking" state) is possible without `ImGui.BeginDisabled`.

## 4. `IsEnabled` is dead code

`IsEnabled` is initialised `true`, settable, and read **nowhere** — neither in this `Draw()` nor by any consumer (grep across `src/` finds only `App.Settings.Addons.Where(x => x.IsEnabled)`, an unrelated `AddonEntry` field). It is *not* `Component.Enabled` either, which is equally unread outside `Component.cs.md` §3. Practical consequences:

- `Button.IsEnabled = false` changes nothing on screen; the button stays clickable.
- To actually grey out a button, callers use `ImGui.BeginDisabled(...)` around a raw `ImGui.Button` (`MainPage/ActionButtons.cs.md`) or the `Checkbox`/`Input` pattern of gating inside their own draw (`Common/Checkbox.cs.md`, `Common/Input.cs.md`).
- A future fix should pick one meaning ("skip draw", "draw disabled", "ignore click") and implement it in this `Draw()`; keeping the field name is currently load-bearing for the two sibling controls' *own* `IsEnabled` fields, which are honoured.

## 5. `Draw()` — exact order and state

```
PushStyleVar(FramePadding, (16,16));  PushStyleVar(FrameRounding, 0);
PushStyleColor(Button, Color);        PushStyleColor(ButtonHovered, HoverColor);
PushStyleColor(Text, TextColor);
ImGui.Button(Label, Vector2(Width ?? -1, 0))   → on true: Click?.Invoke()
PopStyleVar(2);  PopStyleColor(3);
```

- **`FramePadding (16,16)` and `FrameRounding 0`** are forced for the button only: 16px inset on all sides (text-to-edge) and no rounded corners. The push/pop of `2` is balanced against those two pushes.
- **`ImGuiCol.ButtonActive` is *not* pushed.** So the "actively held" frame uses the theme value (`ImGuiColors.cs.md`), while pressed-hover comes from `HoverColor`. Compare `FtsPage.cs.md` §Draw, which hides buttons by pushing `Button`, `ButtonHovered` **and** `ButtonActive` to zero — this component deliberately leaves `ButtonActive` alone.
- **`ImGuiCol.Text` is pushed**, so the label colour is `TextColor`; the pop count of `3` matches the three colour pushes.
- **Size:** `Vector2(Width ?? -1, 0)`. In ImGui, `size.x < 0` ⇒ width auto-sized from the label text, `size.y <= 0` ⇒ auto from style frame padding + text line height. So `Width == null` gives a text-fitted button, and a non-null `Width` fixes the width while height stays auto. `Width` is per-frame mutable, which is how `LoadingPage` centres quarter-width buttons (`LoadingPage.cs.md` §Draw).
- **No `base.Draw()` call.** This is the only leaf in the package that skips the base draw chain (`Component.cs.md` §6), so a `Button` can never paint children.

### `Click` firing rules

`Click?.Invoke()` runs once per frame **per activation source**, and the sources are OR-ed:

```csharp
ImGui.Button(Label, size) || (ImGui.IsItemFocused() && ImGui.IsKeyPressed(ImGuiKey.Enter))
```

1. Mouse/pointer press-and-release on the button (ImGui's normal "button clicked" result).
2. Keyboard fallback: if this item currently has ImGui keyboard focus and `Enter` is *held* (`IsKeyPressed`, not `IsKeyShortcut`/pressed-once), it also fires. Two consequences: holding `Enter` re-fires every frame (repeat-fire — e.g. repeated login triggers, guarded only by whatever the callee checks); and focus is global to the window, set by `Input.Draw()`/`Input.TakeKeyboardFocus` (`Common/Input.cs.md`), so a focused *different* item makes this condition true for that item, not for the button. The fallback is best-effort, not a proper focus-navigation model.
3. Since `Click` is an `event` field of type `Action?`, multiple handlers can be composed; with more than one subscriber, the return of `ImGui.Button(...)` (and thus activation) still happens once — every subscriber runs in the same frame, in subscription order.

## 6. `Label` as ImGui id

`Label` is passed straight to `ImGui.Button`, so it is simultaneously the display text and the ImGui id string. Unlike `Checkbox`, `Input` and `SettingsEntry` — which use the `###` id-splitting form (`$"###{Id}"`, empty display text + unique id, `Component.cs.md` §5) — a `Button`'s id is derived from the **label text**, which means:

- Two buttons with the same localized label inside the same ImGui window collide (ImGui ids hash the string) — a real risk for duplicated `Strings.CancelLabel` buttons in one window.
- Localized strings containing `#`/`###` characters change the id/label split; keep label strings free of `#` or use the `###`-suffixed form.
- Renaming a `Strings.*` label changes ImGui identity, which ImGui uses to remember hover/focus/active state across frames — a cosmetic, not functional, hazard.

## 7. Usage contract for callers

- Subscribe with `+=` in a constructor or at the point of ownership (`loginButton.Click += TriggerLogin`), unsubscribe with `-=` if the closure's captured state must not run again; the field is an `event`, so it is writable only inside this class — external code can only add/remove handlers (`event` encapsulation).
- Handler closures run **inside the draw pass**, so they must be non-blocking and must not call `LauncherApp.ShowMessageBlocking` (deadlock, per `LauncherApp.cs.md` §5's "must never be called from the draw path") — page code that needs blocking dialogs routes through its own task/state machinery instead (`MainPage/MainPage.cs.md`).
- `Width` must be assigned **before** reading `Width.Value`; `LoadingPage` does `button.Width = …; CenterCursorFor(button.Width.Value)` in that order (see that file's §Draw). A null `Width` at `.Value` read time throws on the null unwrap.

## 8. Referenced / referenced-by

Used by: `LoadingPage/LoadingPage.cs.md` (two buttons), `MainPage/LoginFrame.cs.md` (login button).
Uses: `Component` (`Component.cs.md`), `ImGuiColors` (`ImGuiColors.cs.md`), Hexa.NET ImGui button/style bindings.
Related: `Common/Checkbox.cs.md`, `Common/Input.cs.md` (the sibling controls, each with their *own* honoured `IsEnabled`), `MainPage/ActionButtons.cs.md` (raw ImGui buttons with tooltips instead of this component).
