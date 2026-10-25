# Spinner.cs — Specification

**File:** `src/XIVLauncher.Core/Components/LoadingPage/Spinner.cs` · 74 lines · 2,382 bytes
**Namespace:** `XIVLauncher.Core.Components.LoadingPage` · **Declaration:** `public class Spinner : Drawable`
**Role:** The rotating arc spinner on the loading page — custom-drawn, timer-driven

---

## 1. Purpose

`Spinner` keeps the launcher's custom-drawn visual: 30 arc segments stroked with a round-capped pen, computed with the same wobble math the ImGui draw list produced. Redraw is driven by an Eto `UITimer` (30 fps) instead of the render loop, and colour/thickness come from the constructor arguments rather than the ImGui style.

Usings: `System.Diagnostics` (`Stopwatch`), `System.Numerics` (`Vector2`), `Eto.Drawing`, `Eto.Forms`.

## 2. Surface

| Member | Kind | Notes |
| --- | --- | --- |
| `NUM_SEGMENTS` | `private const int = 30` (line 19) | segment count, used by the angle math |
| `radius` | `private readonly float` (line 21) | arc radius, from ctor |
| `pen` | `private readonly Pen` (line 22) | `new Pen(color, thickness)` with `LineCap = PenLineCap.Round`, `LineJoin = PenLineJoin.Round` (lines 30–35) |
| `stopwatch` | `private readonly Stopwatch` (line 23) | `Stopwatch.StartNew()` at construction — monotonic clock for the animation |
| `timer` | `private readonly UITimer` (line 24) | `Interval = 1.0 / 30.0` (seconds) |
| `Spinner(float radius, int thickness, Color color)` | ctor (lines 26–44) | builds pen, sets size, wires paint + timer, starts timer |
| `Size` | set in ctor (line 37) | `((int)(radius * 2) + 32, (int)(radius * 2) + 32)` |
| `Paint` handler | line 39 | `(_, e) => this.Draw(e.Graphics)` |
| `timer.Elapsed` handler | line 41 | `(_, _) => this.Invalidate()` |
| `timer.Start()` | line 42 | animation begins at construction |
| `Stop()` | `public` (line 46) | `=> this.timer.Stop()` |
| `Draw(Graphics g)` | `private` (lines 48–73) | paint body |

The constructor is UI-thread-only: `Drawable` is an Eto control, so `Spinner` must be created after the Eto `Application` exists (`Program.cs.md`).

## 3. Animation math

```csharp
var time = (float)(this.stopwatch.Elapsed.TotalSeconds / 1.2);
var start = MathF.Abs(MathF.Sin(time * 1.8f) * (NUM_SEGMENTS - 5));
var aMin  = MathF.PI * 2.0f * start / NUM_SEGMENTS;
var aMax  = MathF.PI * 2.0f * (NUM_SEGMENTS - 3) / NUM_SEGMENTS;
var center = new Vector2(this.Width / 2f, this.Height / 2f);
```

For each `i` in `0..NUM_SEGMENTS - 1` (lines 60–72):

- `angle = aMin + (i / NUM_SEGMENTS) * (aMax - aMin) + time * 8`
- point on the circle at `center + (cos(angle) * radius, sin(angle) * radius)`
- `g.DrawLine(this.pen, previous, point)` when a previous point exists — the arc is stroked as 29 segments, not as filled wedges.

Consequences:

- `time` grows without bound; `MathF.Sin(time * 1.8f)` makes the arc's start angle oscillate (the "wobble"), and `time * 8` makes the whole arc rotate. In a 32-bit `float`, `time` loses precision after a long-running session, which degrades the smoothness of the rotation — the animation is long-lived because `LauncherApp` never recreates the page.
- `center` is recomputed from `Width`/`Height` per paint, so the spinner re-centres when the widget is resized. The size formula reserves `radius * 2 + 32` px, i.e. 16 px padding on both axes, matching the old ImGui dummy.
- `PenLineCap.Round` / `PenLineJoin.Round` give the rounded segment ends; `PenLineCap` values available in Eto are `Square`, `Butt`, `Round` (`Eto/Drawing/PenLineCap.cs`).

## 4. Redraw model

`timer.Elapsed` calls `Invalidate()`, which schedules a GTK repaint; `Paint` runs on the UI thread with `e.Graphics`. The interval is `1.0 / 30.0` seconds (~33 ms).

- The timer runs on the UI thread, so the paint math is UI-thread work; there is no background thread here.
- `Visible = false` on the spinner (`LoadingPage.cs:142`) hides the widget, but **the timer keeps running**: GTK skips paint for an invisible widget, so `Invalidate()` becomes a no-op repaint request. `Stop()` exists for the case where animation must be turned off, and **nothing in `src/XIVLauncher.Core` calls it** (grep: only `Visible` is used on the spinner).
- A `Drawable` with a `Paint` handler never fills its background; the spinner draws only its arc, over whatever is behind it in the page stack.

## 5. Consumers

| Consumer | Site | Use |
| --- | --- | --- |
| `LoadingPage` ctor | `LoadingPage.cs:65` | `new Spinner(SPINNER_RADIUS /*15*/, 5, EtoColors.Blue)` |
| `LoadingPage.Refresh` | `LoadingPage.cs:142` | `this.spinner.Visible = this.IsIndeterminate` |

`SPINNER_RADIUS = 15` and thickness `5` come from `LoadingPage.cs.md`; the colour is `EtoColors.Blue` (`EtoColors.cs.md`). The spinner is placed in `LoadingPage`'s stack with `HorizontalAlignment.Center` (`LoadingPage.cs:108`).

## 6. Quirks

- `Stop()` is public and unused: the spinner is animated for the process lifetime. A long-running launcher (idle on the main page) keeps the timer firing 30 times/second; `Visible = false` does not stop it.
- `Stopwatch` is never reset, so animation state is continuous across page switches; the spinner does not restart when the loading page is shown.
- `Draw` allocates `PointF`/`Vector2` values per paint (30 iterations at 30 fps). Allocation is small, but a GTK repaint storm (resize + animation) runs the loop repeatedly.
- The widget size is derived from `radius` at construction, so changing `radius` after construction does not resize the widget; there is no setter.
- `Drawable` has no focus, no keyboard handling, and no accessibility semantics: the spinner is decoration.

## 7. Referenced / referenced-by

Uses: `Eto.Forms.Drawable`, `Eto.Drawing.Pen`/`PenLineCap`/`PenLineJoin`, `System.Diagnostics.Stopwatch`, `System.Numerics.Vector2`.
Referenced by: `LoadingPage.cs` (lines 65, 142).
