using Eto.Drawing;
using Eto.Forms;

using XIVLauncher.Common.Util;
using XIVLauncher.Core.Resources.Localization;

namespace XIVLauncher.Core.Components.SettingsPage.Tabs;

/// <summary>
/// The about tab: the launcher logo, version, contributors, and the repository/Discord/license actions.
/// </summary>
/// <remarks>
/// Replaces the immediate-mode about tab: the SDL GPU texture becomes an embedded Eto image, and the
/// selectable contributor text becomes a link button.
/// </remarks>
public class SettingsTabAbout : SettingsTab
{
    private readonly Label versionLabel = new();
    private readonly LinkButton contributorsLink = new();

    public override SettingsEntry[] Entries => [];

    public override string Title => Strings.AboutTitle;

    public SettingsTabAbout()
    {
        this.versionLabel.Text = $"XIVLauncher Core v{AppUtil.GetAssemblyVersion()}({AppUtil.GetGitHash()})";

        this.contributorsLink.Text = string.Format(Strings.XLCoreCreatedBy, "goaaats, Blooym, rankynbass");
        this.contributorsLink.Wrap = WrapMode.Character;
        this.contributorsLink.Click += (_, _) => AppUtil.OpenBrowser("https://github.com/goatcorp/XIVLauncher.Core/graphs/contributors");
    }

    protected override List<Control> HeaderContent =>
    [
        new ImageView
        {
            Image = AppImages.FromResource("logo.png"),
            Size = new Size(256, 256),
        },
        this.versionLabel,
        this.contributorsLink,
    ];

    protected override List<Control> FooterContent =>
    [
        new StackLayout
        {
            Orientation = Orientation.Horizontal,
            Items =
            {
                CreateBrowserButton(Strings.OpenRepositoryButton, "https://github.com/goatcorp/XIVLauncher.Core"),
                CreateBrowserButton(Strings.JoinDiscordButton, "https://discord.gg/3NMcUV5"),
                CreateBrowserButton(Strings.SeeSoftwareLicensesButton, null),
            },
        },
    ];

    private static Button CreateBrowserButton(string label, string? url)
    {
        var button = new Button { Text = label };

        button.Click += (_, _) =>
        {
            if (url is not null)
                AppUtil.OpenBrowser(url);
            else
                PlatformHelpers.OpenBrowser(Path.Combine(AppContext.BaseDirectory, "license.txt"));
        };

        return button;
    }
}
