using Eto.Drawing;
using Eto.Forms;

namespace XIVLauncher.Core.Components;

/// <summary>
/// The decorative FFXIV logo anchored to the bottom-left of the launcher window.
/// </summary>
/// <remarks>
/// The embedded <c>bg_logo.png</c> is 1280x800 with artwork in its bottom-left quadrant and the rest
/// transparent, so it is placed in a fixed-size transparent image view: the image view scales the
/// bitmap to its own size, which keeps the artwork in the bottom-left corner of the window, as the
/// ImGui draw loop did by positioning the texture at <c>(0, viewport.Y - height)</c>.
/// </remarks>
public class Background : Panel
{
    public Background()
    {
        this.Content = new ImageView
        {
            Image = AppImages.FromResource("bg_logo.png"),
            Size = new Size(1280, 800),
        };
    }
}
