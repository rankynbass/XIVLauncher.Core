# Spinner.cs — Specification

**File:** `src/XIVLauncher.Core/Components/LoadingPage/Spinner.cs` · 51 lines · 1484 bytes
**Namespace:** `XIVLauncher.Core.Components.LoadingPage` · **Declaration:** `public class Spinner : Component`
**Role:** The indeterminate-progress spinner: a 30-segment rotating arc drawn from `ImGui.GetTime()`, no state of its own

---

## 1. Purpose

`Spinner` is the "please wait" glyph shown by `LoadingPage` while `IsIndeterminate` is true (`LoadingPage.cs.md` §Draw: `ImGuiHelpers.CenterCursorFor(SPINNER_RADIUS * 2); this.spinner.Draw();`). Its entire geometry is a function of wall time — no fields are mutated in `Draw()`, so it is trivially re-drawable every frame.

Usings: `System.Numerics`, `Hexa.NET.ImGui`. Constructed once, in `LoadingPage`'s ctor: `new Spinner(SPINNER_RADIUS, 5, ImGui.GetColorU32(ImGuiCol.ButtonActive))`.

## 2. Surface

| Member | Kind | Notes |
| --- | --- | --- |
| `radius` | `private readonly float` | ctor param; also the reservation box size |
| `thickness` | `private readonly int` | stroke width of the arc |
| `color` | `private readonly uint` | ImGui colour (`ImU32`), captured at construction |
| `Spinner(float radius, int thickness, uint color)` | ctor | pure capture, no side effects |
| `Draw()` | `override` | dummy reservation → arc path → `base.Draw()` |

Everything is `readonly`: the spinner cannot be recoloured or resized per frame. The colour is sampled **once** in `LoadingPage`'s ctor via `ImGui.GetColorU32(ImGuiCol.ButtonActive)`, i.e. before the theme is pushed by the draw pass — a themed recolour at runtime does not propagate to the spinner. `LoadingPage.cs.md` should be read together with this constraint (the spinner inherits the *startup* style, not the loading-page style).

## 3. Layout contribution

```csharp
var framePadding = ImGui.GetStyle().FramePadding;
var pos = ImGui.GetCursorPos();
ImGui.Dummy(new Vector2(this.radius * 2) + (framePadding * 2));
```

- The component **reserves its own bounding box** (`2r + 2·framePadding` square) before drawing, so the cursor advances and the containing page can stack content below it. `LoadingPage` first calls `ImGuiHelpers.CenterCursorFor(SPINNER_RADIUS * 2)` (`ImGuiHelpers.cs.md`) to centre horizontally, then `Draw()` reserves the box — the two centring helpers use `radius*2` without the padding term, so the arc is offset left by one `framePadding` relative to the centring assumption. Cosmetic, but it is the reason the spinner does not look perfectly centred against the text lines around it.
- `centre` is derived from the **pre-Dummy cursor** (`pos`), and includes `+ framePadding.Y` but **not** `+ framePadding.X` — same asymmetry as above; the horizontal centring is not padding-corrected.
- Geometry is drawn through `ImGui.GetWindowDrawList()` (`PathLineTo`/`PathStroke`), i.e. absolute window coordinates, not the item cursor: a change of window/padding does not rescale the spinner, only its anchor.

## 4. The animation function

```csharp
const int NUM_SEGMENTS = 30;
var time    = ImGui.GetTime() / 1.2;
var start   = Math.Abs(Math.Sin(time * 1.8f) * (NUM_SEGMENTS - 5));   // 0..25, ping-pong
var aMin    = 2π · start / 30;
var aMax    = 2π · (30 - 3) / 30;                                    // fixed: 27/30 of a circle
var a       = aMin + (i / 30f) · (aMax - aMin);
point_i     = centre + (cos(a + time·8), sin(a + time·8)) · radius;
```

- **Two independent motions:** the arc's *sweep start* oscillates via `|sin|` (so `start` ping-pongs between 0 and 25 — a "grow/shrink" pendulum effect), while `+ time * 8` rotates the whole arc continuously at ~8 rad/s (scaled by the `/1.2` factor on `time`). The constants are tuned for look, not for frame-rate independence — `ImGui.GetTime()` is seconds since frame-loop start, so the motion is time-based and therefore frame-rate independent in speed, but its *phase* depends on app start time (no per-instance phase offset, so two spinners animate in lockstep).
- `aMax` is constant (`27/30` of a turn), so the arc length varies only through `aMin` — the drawn sweep is `aMax - aMin`, between 27/30 and 2/30 of a circle. `NUM_SEGMENTS` stays 30 regardless, so a short sweep is 30 points along a small arc (dense polyline): increasing `NUM_SEGMENTS` only increases tessellation cost, it does not make the arc smoother-looking at a fixed sweep.
- `PathLineTo` starts a polyline; `PathStroke(color, ImDrawFlags.RoundCornersAll, thickness)` closes the visual with rounded joins. No `PathFill`, so the spinner is an outline only. Note the path is not explicitly closed, so the two ends of the arc are open (with `RoundCornersAll` they get rounded caps).
- `const int NUM_SEGMENTS = 30;` is a C# 13 `const` local — compile-time inlined, cannot be reassigned; if a caller ever wants variable tessellation, that keyword must go.

## 5. Statelessness and reuse

- No mutable fields, no events, no `Enabled`-specific logic, no `OnShow()`: the component is a pure function of (position, style, time). It does call `base.Draw()` at the end (`Component.cs.md` §6), so unlike `Button`/`Checkbox`/`Input` it is not a strict leaf — children would be painted.
- Because nothing here caches geometry, per-frame cost is one dummy item plus 30 path points and one stroke. `Spinner` is safe to construct cheaply (ctor has no side effects), unlike `Common/Input.cs.md` whose ctor registers a Steam handler.
- There is no "stop" state: to hide the spinner the *caller* must stop calling `Draw()` (`LoadingPage.cs.md` gates on `IsIndeterminate`). Do not add a `Stop()` that mutates hidden state — the class has none.

## 6. Contract for callers

- Position comes solely from the ImGui cursor at call time; there is no size query, so a caller that wants to centre the spinner must replicate the `radius * 2` arithmetic (as `LoadingPage.cs.md` does with its own `private const int SPINNER_RADIUS = 15`). If this file ever changes the padding term in the `Dummy`, the caller's centring arithmetic must be changed in lockstep — the value is **duplicated**, not exposed. A `GetSize()` accessor is the natural fix if that drift matters.
- `thickness` is an `int` and is passed straight to `PathStroke`; sub-pixel thinning is not available, and `thickness <= 0` would produce an invisible stroke without error.
- `radius` is a `float`, but the centring helper in `LoadingPage` passes `SPINNER_RADIUS * 2` as an `int` — the int/float mix is caller-side, keep radii integral to avoid rounding drift in the centring call.

## 7. Referenced / referenced-by

Used by: `LoadingPage.cs.md` (constructed with `SPINNER_RADIUS = 15`, `thickness = 5`, colour from `ImGuiCol.ButtonActive`).
Uses: `Component.cs.md` (`base.Draw()`), `ImGui.GetTime`, `ImGuiListDrawEnlarged`-style draw-list API (Hexa.NET.ImGui), `Math` (stdlib).
Contrast: the determinate branch of `LoadingPage` uses `ImGui.ProgressBar` instead (see that spec) — `Spinner` deliberately carries no progress value.
