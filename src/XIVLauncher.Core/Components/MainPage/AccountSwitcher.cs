using Eto.Forms;

using XIVLauncher.Core.Accounts;
using XIVLauncher.Core.Resources.Localization;

namespace XIVLauncher.Core.Components.MainPage;

/// <summary>
/// The saved-account picker, opened from the user icon button in the login frame.
/// </summary>
/// <remarks>
/// Replaces the immediate-mode popup: the ImGui version registered a popup id, drew it when it was open,
/// and re-opened it on a flag set by <c>Open()</c>. Eto builds a <see cref="ContextMenu"/> on demand and
/// shows it anchored to a control, so the popup-id machinery is gone.
/// </remarks>
public class AccountSwitcher : Component
{
    private readonly AccountManager manager;

    public AccountSwitcher(AccountManager manager)
    {
        this.manager = manager;
    }

    /// <summary>Raises when an account is picked from the menu.</summary>
    public event EventHandler<XivAccount>? AccountChanged;

    /// <summary>Shows the account menu anchored to <paramref name="anchor"/>.</summary>
    public void Open(Control anchor)
    {
        var menu = new ContextMenu();

        if (this.manager.Accounts.Count == 0)
            menu.Items.Add(new ButtonMenuItem { Text = Strings.NoSavedAccounts, Enabled = false });

        foreach (var account in this.manager.Accounts)
        {
            var name = account.UserName;

            if (account.UseSteamServiceAccount)
                name += " (Steam)";

            if (account.UseOtp)
                name += " (OTP)";

            if (account.IsFreeTrial)
                name += " (Trial)";

            var item = new ButtonMenuItem { Text = name };
            item.Click += (_, _) => this.AccountChanged?.Invoke(this, account);

            menu.Items.Add(item);
        }

        menu.Show(anchor);
    }
}
