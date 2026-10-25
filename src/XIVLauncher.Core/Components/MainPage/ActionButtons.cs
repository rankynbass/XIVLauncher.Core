using Eto.Forms;


using XIVLauncher.Core.Resources.Localization;

namespace XIVLauncher.Core.Components.MainPage;

/// <summary>
/// The row of icon buttons under the login frame: account page, service status, and launcher settings.
/// </summary>
/// <remarks>
/// Replaces the three immediate-mode icon buttons, whose tooltips were added with style pushes. Tooltips
/// are set on the Eto buttons directly.
/// </remarks>
public class ActionButtons : Component
{
    private readonly Common.Button accountButton;
    private readonly Common.Button statusButton;
    private readonly Common.Button settingsButton;

    public ActionButtons()
    {
        this.accountButton = MakeIconButton(FontAwesomeIcon.User, Strings.MyAccount, () => this.OnAccountButtonClicked?.Invoke());
        this.statusButton = MakeIconButton(FontAwesomeIcon.Heartbeat, Strings.ServiceStatus, () => this.OnStatusButtonClicked?.Invoke());
        this.settingsButton = MakeIconButton(FontAwesomeIcon.Cog, Strings.LauncherSettings, () => this.OnSettingsButtonClicked?.Invoke());

        this.Content = new StackLayout
        {
            Orientation = Orientation.Horizontal,
            Items = { this.accountButton, this.statusButton, this.settingsButton },
        };
    }

    /// <summary>Raises for the account button (opens the account management page).</summary>
    public event Action? OnAccountButtonClicked;

    /// <summary>Raises for the status button (opens the server status page).</summary>
    public event Action? OnStatusButtonClicked;

    /// <summary>Raises for the settings button (opens the settings page).</summary>
    public event Action? OnSettingsButtonClicked;

    private static Common.Button MakeIconButton(FontAwesomeIcon icon, string toolTip, Action clicked)
    {
        var button = new Common.Button(string.Empty)
        {
            Width = 80,
        };

        button.ShowIcon(icon);
        button.ToolTip = toolTip;
        button.Click += clicked;

        return button;
    }
}
