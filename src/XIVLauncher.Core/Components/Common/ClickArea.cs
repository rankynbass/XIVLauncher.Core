using Eto.Drawing;
using Eto.Forms;

namespace XIVLauncher.Core.Components.Common;

/// <summary>
/// A transparent clickable rectangle, used to place clickable regions over a fixed background image.
/// </summary>
/// <remarks>
/// The Steam Deck guide screens drew invisible ImGui buttons (transparent button colours) over the
/// artwork at fixed pixel coordinates. Eto buttons are themed and cannot be made transparent, so this is
/// the replacement: a drawable that raises <see cref="Click"/> when the left button is pressed.
/// </remarks>
public class ClickArea : Drawable
{
    public ClickArea(int width, int height)
    {
        this.Size = new Size(width, height);

        this.MouseDown += (_, e) =>
        {
            if (e.Buttons == MouseButtons.Primary)
                this.Click?.Invoke();
        };
    }

    /// <summary>Raises when the left mouse button is pressed inside the area.</summary>
    public event Action? Click;
}
