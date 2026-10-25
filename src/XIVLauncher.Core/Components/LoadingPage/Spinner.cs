using System.Diagnostics;
using System.Numerics;

using Eto.Drawing;
using Eto.Forms;

namespace XIVLauncher.Core.Components.LoadingPage;

/// <summary>
/// The rotating arc spinner shown on the loading page.
/// </summary>
/// <remarks>
/// Custom drawing is kept for this visual: 30 arc segments stroked with a round-capped pen, using the
/// same wobble math the ImGui draw list produced. Redraw is driven by a UI timer instead of the render
/// loop, and the colour comes from the launcher palette instead of the ImGui style.
/// </remarks>
public class Spinner : Drawable
{
    private const int NUM_SEGMENTS = 30;

    private readonly float radius;
    private readonly Pen pen;
    private readonly Stopwatch stopwatch = Stopwatch.StartNew();
    private readonly UITimer timer = new() { Interval = 1.0 / 30.0 };

    public Spinner(float radius, int thickness, Color color)
    {
        this.radius = radius;

        this.pen = new Pen(color, thickness)
        {
            LineCap = PenLineCap.Round,
            LineJoin = PenLineJoin.Round,
        };

        // The old dummy reserved radius*2 plus frame padding on both sides.
        this.Size = new Size((int)(radius * 2) + 32, (int)(radius * 2) + 32);

        this.Paint += (_, e) => this.Draw(e.Graphics);

        this.timer.Elapsed += (_, _) => this.Invalidate();
        this.timer.Start();
    }

    /// <summary>Stops the animation timer; the spinner stops rotating.</summary>
    public void Stop() => this.timer.Stop();

    private void Draw(Graphics g)
    {
        var time = (float)(this.stopwatch.Elapsed.TotalSeconds / 1.2);

        var start = MathF.Abs(MathF.Sin(time * 1.8f) * (NUM_SEGMENTS - 5));
        var aMin = MathF.PI * 2.0f * start / NUM_SEGMENTS;
        var aMax = MathF.PI * 2.0f * (NUM_SEGMENTS - 3) / NUM_SEGMENTS;

        var center = new Vector2(this.Width / 2f, this.Height / 2f);

        PointF? previous = null;

        for (var i = 0; i < NUM_SEGMENTS; i++)
        {
            var angle = aMin + (i / (float)NUM_SEGMENTS) * (aMax - aMin) + time * 8;

            var point = new PointF(
                center.X + MathF.Cos(angle) * this.radius,
                center.Y + MathF.Sin(angle) * this.radius);

            if (previous is { } previousPoint)
                g.DrawLine(this.pen, previousPoint, point);

            previous = point;
        }
    }
}
