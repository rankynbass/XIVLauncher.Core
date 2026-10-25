using Eto.Drawing;

namespace XIVLauncher.Core.Components;

/// <summary>
/// Base for launcher pages: a full-window <see cref="Component"/> that talks to the launcher state machine.
/// </summary>
/// <remarks>
/// Pages are created once by <see cref="LauncherApp"/> and swapped into the window by
/// <see cref="LauncherForm"/>. The <c>Padding</c> the ImGui draw loop read from the page becomes the
/// Eto panel padding, set in the page constructor.
/// </remarks>
public class Page : Component
{
    protected LauncherApp App { get; }

    protected Page(LauncherApp app)
    {
        this.App = app;
    }

    public override void OnShow() { }
}
