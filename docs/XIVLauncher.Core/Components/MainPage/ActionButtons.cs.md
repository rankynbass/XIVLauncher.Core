# ActionButtons.cs — Specification

**File:** `src/XIVLauncher.Core/Components/MainPage/ActionButtons.cs` · 58 lines · 1718 bytes
**Namespace:** `XIVLauncher.Core.Components.MainPage` · **Declaration:** `public class ActionButtons : Component`
**Role:** Three icon buttons — account, service status, launcher settings — whose visibility is decided by whether anyone subscribed to their event

---

## 1. Purpose

`ActionButtons` is the MainPage's quick-action strip. It is a *pure* component: no state, no children, no geometry of its own. It draws three icon-labelled buttons in the parent's cursor flow and exposes three events; everything behavioural is bound by `MainPage.cs.md`'s ctor:

```csharp
MainPage.cs:48  this.actionButtons.OnSettingsButtonClicked += () => this.App.State = LauncherApp.LauncherState.Settings;
MainPage.cs:49  this.actionButtons.OnStatusButtonClicked  += () => AppUtil.OpenBrowser("https://is.xivup.com/");
MainPage.cs:50  this.actionButtons.OnAccountButtonClicked += () => AppUtil.OpenBrowser("https://sqex.to/Msp");
```

That is the whole dependency: the class never references `LauncherApp`, `Program`, or `Strings` for behaviour — only for the tooltip caption (`Strings`) and the icon font.

## 2. Surface

| Member | Kind | Notes |
| --- | --- | --- |
| `OnAccountButtonClicked` | `public event Action?` | person icon → `Strings.MyAccount` tooltip |
| `OnStatusButtonClicked` | `public event Action?` | heartbeat icon → `Strings.ServiceStatus` |
| `OnSettingsButtonClicked` | `public event Action?` | cog icon → `Strings.LauncherSettings` |
| `Draw()` | `override` | three icon buttons + tooltips, then `base.Draw()` |

Usings: `System.Numerics`, `Hexa.NET.ImGui`, `XIVLauncher.Core.Resources.Localization` (`Strings`); `Component.cs.md` is the base (this is a plain `Component`, not a `Page` — it never opens its own window and never touches `Padding`).

## 3. `BeginDisabled(event == null)` — subscription decides availability

```csharp
ImGui.BeginDisabled(this.OnAccountButtonClicked == null);
if (ImGui.Button(FontAwesomeIcon.User.ToIconString(), btnSize)) this.OnAccountButtonClicked?.Invoke();
...
ImGui.EndDisabled();
```

The condition is **inverted** relative to the usual "disabled means user cannot act": a button is greyed only when *no handler is attached*, i.e. the class renders "nothing will happen" honestly. Consequences:

- With `MainPage.cs.md` binding all three, **all three are always enabled** in the shipped app; the greying is a composability aid (a future host that binds only the settings button gets two visibly dead buttons rather than three silent ones).
- `BeginDisabled` only affects **appearance and interaction** in ImGui — the click still evaluates `if (ImGui.Button(...))`, but a disabled item never returns `true`, so the `?.Invoke()` is unreachable when unsubscribed; the `?.` is therefore belt-and-braces, not the mechanism. Keep both: removing the guard is a behavioural change, removing `?.` is not.
- Contrast the *other* disabled pattern in this package: `Common/Checkbox.cs.md` gates on a user-facing `IsEnabled`, and `OtpEntryPage.cs.md` uses none. The event-emptiness test here is the only "am I wired up?" gate.

## 4. Font scoping — the tooltip needs the text font

```
PushFont(FontManager.IconFont, 0.0f)          ← opens icon scope
  Button(User icon) …
  PushFont(FontManager.TextFont, 0.0f)        ← nested: tooltip caption
  AddTooltip(Strings.MyAccount)
  PopFont()                                   ← closes TextFont
  (… same for the other two buttons …)
PopFont()                                     ← closes IconFont
```

- `ImGuiHelpers.AddTooltip` (`ImGuiHelpers.cs:37`) fires on `ImGui.IsItemHovered()` **of the item just drawn**, so the call must stay immediately after its `Button` — moving it under the *next* button retargets the tooltip silently. It renders `ImGui.TextUnformatted(text)`, which in an icon font would show boxes/missing glyphs: hence the nested `PushFont(FontManager.TextFont, 0.0f)`. `FontManager.cs.md` §"font roles" covers which faces are loaded.
- The `0.0f` size argument means "keep the current size" (same convention as `ImGuiHelpers.cs.md` §CenteredText and `OtpEntryPage.cs.md` §paste button) — these two font pushes change face, not size.
- The stack is balanced (`IconFont` opened once outside, `TextFont` opened/closed once per button, final `PopFont()` closes `IconFont`), so `MainPage.cs.md` §Draw's font state is untouched afterwards. Do not "simplify" to a single `PopFont()` after the third tooltip: the nested `TextFont` pushes would remain unbalanced, which corrupts the font state of every later component (`Component.cs.md` §push/pop discipline for the rule).
- `ImGui.SameLine()` separates the three blocks: the buttons share one row, so their combined width is `3 × btnSize` and the row is positioned by whatever `MainPage.cs.md`'s `Draw()` left as the cursor — this class contributes no `SetCursorPos`, no `Dummy`, and no centering (contrast `MainPage/NewsFrame.cs.md` and `OtpEntryPage.cs.md`).

## 5. Geometry and scale

```csharp
var btnSize = new Vector2(80) * ImGuiHelpers.GlobalScale;
```

- **`Vector2(80)` is the single-vector form** (both axes 80), scaled by `ImGuiHelpers.GlobalScale` (`ImGuiHelpers.cs.md` §scale). Every MainPage icon control in this file family follows the same shape: `MainPage/LoginFrame.cs.md`'s `extraButtonSize = new Vector2(45) * ImGuiHelpers.GlobalScale` and `OtpEntryPage.cs.md`'s unscaled `(28, 28)` — the unscaled OTP paste icon is the odd one out; keep it that way only if the intent is "this icon does not scale", otherwise it is a scaling bug in that other file, not here.
- **There is no gap** between the three buttons: `SameLine()` places them flush. Any spacing must come from the parent's cursor or an explicit `ImGui.Dummy` in this `Draw()`; adding one inside `BeginDisabled`/`EndDisabled` changes layout in the unsubscribed case too.
- The buttons are drawn with `ImGui.Button(label, size)` — the **sized overload**, whose label is an id/label string. Because the icon glyph *is* the label (`FontAwesomeIcon.Cog.ToIconString()` etc.), the label doubles as the ImGui id: two such buttons in one window collide. See `Component.cs.md` §5 for the id conventions used elsewhere (`###`-prefixed ids in `Component.cs.md`, `OtpEntryPage.cs.md`, `SettingsEntry.cs.md`).

## 6. Invariants and extension points

- **`base.Draw()` last** (`Component.cs.md` §Draw chain): the class has no children today, so it is a chain-passthrough hook; keep it even though the loop is empty, because `MainPage.cs.md` may add children (see `Component.cs.md` §children).
- **The events are `public`**, so subscription is externally composable: a host may also *unsubscribe* (`-=`), which is how the greying can be exercised. `MainPage.cs.md` is the only binder today (grep).
- **Tooltips are localized, labels are not:** the icon is the label and the caption is `Strings.*`. If a text label is ever added, follow `Common/Button.cs.md` §"Label as ImGui id" rather than the icon-label form here.
- **No click-handling is defined here**: the three handlers live in `MainPage.cs.md`, which means this class is platform/UI-only and can be re-used with different meanings. Keep new behaviour in the binder, not in the class — but see `LoginFrame.cs.md` §popup for the alternative convention (that class handles its popup inline), so the two MainPage children differ on where handlers live; prefer the binder side for this class since it exposes all three actions.
- **The class is constructed as `new ActionButtons()` with no app reference** (`MainPage.cs:42`), unlike the pages that take `LauncherApp` (`Page.cs.md` §protected ctor) or `LoginFrame`, which takes the owning `MainPage`. It therefore cannot reach app state — the reason the settings handler is written in `MainPage.cs` and not here, and the reason this class must not start reading `App.Settings`.

## 7. Referenced / referenced-by

Constructed and wired by: `MainPage.cs.md` ctor (`MainPage.cs:41` construction, `:48/:49/:50` handlers).
Drawn by: `MainPage.cs.md` §Draw (after `loginFrame.Draw()`, inside MainPage's `PushStyleVar(WindowPadding, 32)`).
Uses: `Component` (`Component.cs.md`), `ImGuiHelpers.cs.md` (`AddTooltip`, `GlobalScale`), `FontAwesomeIcon.cs.md` (`User`, `Heartbeat`, `Cog`), `FontManager.cs.md` (`IconFont`, `TextFont`), `Strings` (`src/XIVLauncher.Core/Resources/Localization/Strings.Designer.cs`), `AppUtil.cs.md` (the browser calls that live in the binder).
Related: `MainPage/LoginFrame.cs.md` (the other MainPage interactive cluster), `LoginAction.cs.md` (the action vocabulary — distinct from these three events, which carry no payload).
