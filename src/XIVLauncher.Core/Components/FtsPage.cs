using Eto.Drawing;
using Eto.Forms;

using XIVLauncher.Core.Components.Common;

namespace XIVLauncher.Core.Components;

/// <summary>
/// The Steam Deck first-time-setup screen, with the invisible guide and finish regions over the artwork.
/// </summary>
/// <remarks>
/// Replaces the immediate-mode FTS page. The artwork is drawn at fixed coordinates with a
/// <see cref="PixelLayout"/>; the two invisible ImGui buttons become <see cref="ClickArea"/> rectangles
/// at the same positions.
/// </remarks>
public class FtsPage : Page
{
    private readonly ImageView steamdeckFtsImage;
    private readonly ImageView steamdeckAppIdErrorImage;

    private bool isSteamDeckAppIdError;

    public FtsPage(LauncherApp app)
        : base(app)
    {
        this.steamdeckFtsImage = new ImageView
        {
            Image = AppImages.FromResource("steamdeck_fts.png"),
            Size = new Size(1280, 800),
            Visible = false,
        };

        this.steamdeckAppIdErrorImage = new ImageView
        {
            Image = AppImages.FromResource("steamdeck_fterror.png"),
            Size = new Size(1280, 800),
            Visible = false,
        };

        var layout = new PixelLayout();

        layout.Add(this.steamdeckFtsImage, 0, 0);
        layout.Add(this.steamdeckAppIdErrorImage, 0, 0);

        var openGuideButton = new ClickArea(649, 101);
        openGuideButton.Click += () =>
        {
            if (!this.isSteamDeckAppIdError)
                AppUtil.OpenBrowser("https://goatcorp.github.io/faq/steamdeck");
            else
                Environment.Exit(0);
        };

        layout.Add(openGuideButton, 316, 481);

        var finishFtsButton = new ClickArea(649, 101);
        finishFtsButton.Click += () =>
        {
            if (!this.isSteamDeckAppIdError)
                this.FinishFts(true);
        };

        layout.Add(finishFtsButton, 316, 598);

        this.Content = layout;
    }

    public void OpenFtsIfNeeded()
    {
        if (CoreEnvironmentSettings.IsDeckFirstRun.HasValue)
        {
            if (CoreEnvironmentSettings.IsDeckFirstRun.Value)
            {
                App.State = LauncherApp.LauncherState.Fts;
                return;
            }

            return;
        }

        if (!(App.Settings.CompletedFts ?? false) && Program.IsSteamDeckHardware)
        {
            App.State = LauncherApp.LauncherState.Fts;
            return;
        }

        if (Program.IsSteamDeckHardware && (Program.Steam == null || !Program.Steam.IsValid))
        {
            // If IsIgnoringSteam == true, skip the error screen. This fixes a bug with Steam Deck always showing the Fts Error screen.
            if (App.Settings.IsIgnoringSteam ?? false) return;

            App.State = LauncherApp.LauncherState.Fts;
            this.isSteamDeckAppIdError = true;
        }
    }

    private void FinishFts(bool save)
    {
        App.State = LauncherApp.LauncherState.Main;

        if (save)
            App.Settings.CompletedFts = true;
    }

    public override void OnShow()
    {
        this.steamdeckFtsImage.Visible = !this.isSteamDeckAppIdError;
        this.steamdeckAppIdErrorImage.Visible = this.isSteamDeckAppIdError;
    }
}
