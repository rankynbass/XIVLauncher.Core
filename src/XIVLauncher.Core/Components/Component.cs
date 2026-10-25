using Eto.Forms;

namespace XIVLauncher.Core.Components;

/// <summary>
/// Base for all launcher widgets: an Eto <see cref="Panel"/> that owns its child controls.
/// </summary>
/// <remarks>
/// The ImGui version drew widgets in immediate mode, keeping a tree of child components and drawing
/// every frame. Eto widgets are persistent, so a component is built once (in its constructor) and
/// shown, hidden, enabled/disabled and refreshed through the platform. Enabling/disabling comes from
/// <see cref="Control.Enabled"/>, which propagates to child widgets, replacing the old
/// <c>Enabled</c> + <c>Children</c> draw recursion.
/// </remarks>
public abstract class Component : Panel
{
    protected Guid Id { get; } = Guid.NewGuid();

    /// <summary>Called every time the containing page is shown; use it to refresh dynamic state.</summary>
    public virtual void OnShow() { }
}
