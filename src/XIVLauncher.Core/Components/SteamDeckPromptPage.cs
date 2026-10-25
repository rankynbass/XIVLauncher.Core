using Eto.Drawing;
using Eto.Forms;

namespace XIVLauncher.Core.Components;

/// <summary>
/// The Steam Deck "switch to Steam" prompt shown after launching the game on a Steam Deck.
/// </summary>
/// <remarks>
/// Replaces the immediate-mode prompt: a single full-window image, no interactive regions.
/// </remarks>
public class SteamDeckPromptPage : Page
{
    private readonly ImageView switchPromptImage;

    public SteamDeckPromptPage(LauncherApp app)
        : base(app)
    {
        this.switchPromptImage = new ImageView
        {
            Image = AppImages.FromResource("steamdeck_switchprompt.png"),
            Size = new Size(1280, 800),
        };

        var layout = new PixelLayout();
        layout.Add(this.switchPromptImage, 0, 0);

        this.Content = layout;
    }
}
