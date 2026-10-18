# ImGuiHelpers.cs — Specification

**File:** `src/XIVLauncher.Core/ImGuiHelpers.cs` · 46 lines · 1,108 bytes
**Namespace:** `XIVLauncher.Core` · **Declaration:** `public static class ImGuiHelpers`
**Role:** Small drawing conveniences shared by all component/page code

---

## 1. Purpose

Wraps the three recurring ImGui idioms — reading the current viewport/scale, drawing wrapped text, and centring text horizontally inside the current window — plus a tooltip helper. Everything is a thin, stateless wrapper over Hexa.NET.ImGui; no launcher-specific data is referenced.

## 2. Accessors

| Member | Type | Value |
| --- | --- | --- |
| `ViewportSize` | `Vector2`, get-only | `ImGui.GetIO().DisplaySize` — the size of the current window viewport in pixels |
| `GlobalScale` | `float`, get-only | `ImGui.GetStyle().FontScaleMain` — the effective UI scale |

Both are **accessors, not cached fields**: they re-query ImGui on each use, so they are correct mid-frame after a window resize but must not be hoisted into a cached "constant" if a page resizes during its own draw. `LauncherApp.Draw()` sizes windows with `ImGui.SetNextWindowSize(ImGuiHelpers.ViewportSize)`.

> `GlobalScale` reports `FontScaleMain` only; the DPI scale applied in `ImGuiBindings` (`ConfigDpiScaleFonts`, `FontScaleDpi`, `ScaleAllSizes(mainScale)`) is a separate factor and is *not* multiplied in here.

## 3. Drawing helpers

| Signature | ImGui sequence / contract |
| --- | --- |
| `static void TextWrapped(string text)` | `PushTextWrapPos()` → `TextUnformatted(text)` → `PopTextWrapPos()`. Wraps at the current wrap position (defaults to the window's right edge when called with no explicit X) — i.e. "wrap to window width". |
| `static void CenteredText(string text)` | `CenterCursorForText(text)` then `TextUnformatted(text)` |
| `static void CenterCursorForText(string text)` | measures with `CalcTextSize(text).X`, then `CenterCursorFor((int)width)` |
| `static void CenterCursorFor(int itemWidth)` | `SetCursorPosX(windowWidth / 2 - itemWidth / 2)` where `windowWidth = (int)ImGui.GetWindowWidth()` |
| `static void AddTooltip(string text)` | if `IsItemHovered()` → `BeginTooltip()` / `TextUnformatted(text)` / `EndTooltip()` |

### Notes

- **`CenterCursorFor` is the generic primitive** (integer pixel width), so it also centres non-text items when the caller supplies their own measured width.
- Integer truncation: `windowWidth / 2 - itemWidth / 2` uses `int` arithmetic, so centring can be off by up to 1 px vs the float-accurate form. At `GlobalScale > 1` this is still sub-pixel-ish, but it is a real half-pixel bias for odd widths.
- Measurement cost: `CenteredText` calls `CalcTextSize` (an extra text measurement pass per string). Acceptable for page-sized UI; avoid in tight loops (a spinner/list drawing hundreds of centred strings pays double).
- `AddTooltip` must be called **immediately after** the item it describes (ImGui's `IsItemHovered` refers to the last submitted item) — the same ordering rule as all ImGui tooltip helpers.
- All helpers draw in the *current* ImGui window and inherit current cursor/pen state; they never call `SetCursorPos(Y)`, so vertical placement is the caller's job.
- `TextWrapped` and `CenteredText` both use `TextUnformatted`, so **no ImGui markup colours/escaping** are interpreted in the string (deliberate: user/game text is rendered verbatim).
- Class is `static`: nothing here can be instantiated or overridden; a plugin cannot specialise these helpers.

## 4. Consumers

Drawing code in the component/page units (`Components/*`, `Background`, `LoadingPage`, `SettingsPage`, …) and `LauncherApp.Draw()`. Sub-packages are out of scope for this spec pass.
