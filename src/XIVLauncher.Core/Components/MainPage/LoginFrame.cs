using System.Numerics;

using Eto.Forms;

using XIVLauncher.Core.Accounts.Secrets.Providers;

using XIVLauncher.Core.Resources.Localization;

namespace XIVLauncher.Core.Components.MainPage;

/// <summary>
/// The login column: username, password, the OTP/Steam/free-trial/auto-login checkboxes, the login
/// button, the login-action dropdown and the account picker button.
/// </summary>
/// <remarks>
/// Replaces the immediate-mode login child window. The right-click login-action popup becomes an Eto
/// <see cref="ContextMenu"/> attached to the login button, so the caret icon button just shows the same
/// menu, and the user icon button opens <see cref="AccountSwitcher"/>.
/// </remarks>
public class LoginFrame : Component
{
    /// <summary>HEAD drew the entries with SetNextItemWidth(window width): each entry fills the login
    /// column, which is 400 px wide inside the page's 32 px padding.</summary>
    private const int LoginInputWidth = 400 - 2 * 32;

    /// <summary>HEAD sized the login-action caret and account picker buttons as 45x45 squares.</summary>
    private const int ExtraButtonSize = 45;

    private readonly MainPage mainPage;

    private readonly Common.Input loginInput;
    private readonly Common.Input passwordInput;
    private readonly Common.Checkbox oneTimePasswordCheckbox;
    private readonly Common.Checkbox useSteamServiceCheckbox;
    private readonly Common.Checkbox freeTrialCheckbox;
    private readonly Common.Checkbox autoLoginCheckbox;
    private readonly Common.Button loginButton;
    private readonly Label secretsWarningLabel;

    public string Username
    {
        get => this.loginInput.Value;
        set => this.loginInput.Value = value;
    }

    public string Password
    {
        get => this.passwordInput.Value;
        set => this.passwordInput.Value = value;
    }

    public bool IsOtp
    {
        get => this.oneTimePasswordCheckbox.Value;
        set => this.oneTimePasswordCheckbox.Value = value;
    }

    public bool IsSteam
    {
        get => this.useSteamServiceCheckbox.Value;
        set => this.useSteamServiceCheckbox.Value = value;
    }

    public bool IsAutoLogin
    {
        get => this.autoLoginCheckbox.Value;
        set => this.autoLoginCheckbox.Value = value;
    }

    public bool IsFreeTrial
    {
        get => this.freeTrialCheckbox.Value;
        set => this.freeTrialCheckbox.Value = value;
    }

    /// <summary>Raises with the login action the user requested.</summary>
    public event Action<LoginAction>? OnLogin;

    public LoginFrame(MainPage mainPage)
    {
        this.mainPage = mainPage;

        void TriggerLogin() => this.OnLogin?.Invoke(LoginAction.Game);

        this.loginInput = new Common.Input(Strings.UsernameInput, Strings.UsernameInputHint, new Vector2(12f, 0f), 128);
        this.loginInput.InputWidth = LoginInputWidth;
        this.loginInput.Enter += TriggerLogin;

        this.passwordInput = new Common.Input(Strings.PasswordInput, Strings.PasswordInputHint, new Vector2(12f, 0f), 128, flags: Common.InputFlags.Password | Common.InputFlags.EnterReturnsTrue);
        this.passwordInput.InputWidth = LoginInputWidth;
        this.passwordInput.Enter += TriggerLogin;

        this.oneTimePasswordCheckbox = new Common.Checkbox(Strings.UseOneTimePasswordCheckbox);
        this.useSteamServiceCheckbox = new Common.Checkbox(Strings.UseSteamServiceAccount);
        this.freeTrialCheckbox = new Common.Checkbox(Strings.FreeTrialAccountCheckbox);
        this.autoLoginCheckbox = new Common.Checkbox(Strings.LogInAutomaticCheckbox);

        var loginActionMenu = new ContextMenu();

        loginActionMenu.Items.Add(MenuItem(Strings.LaunchWithoutDalamudButton, () => this.OnLogin?.Invoke(LoginAction.GameNoDalamud)));
        loginActionMenu.Items.Add(new SeparatorMenuItem());
        loginActionMenu.Items.Add(MenuItem(Strings.LaunchWithoutPluginsButton, () => this.OnLogin?.Invoke(LoginAction.GameNoPlugins)));
        loginActionMenu.Items.Add(new SeparatorMenuItem());
        loginActionMenu.Items.Add(MenuItem(Strings.LaunchWithNoCustomPluginsButton, () => this.OnLogin?.Invoke(LoginAction.GameNoThirdparty)));
        loginActionMenu.Items.Add(new SeparatorMenuItem());
        loginActionMenu.Items.Add(MenuItem(Strings.PatchWithoutLaunchingButton, () => this.OnLogin?.Invoke(LoginAction.GameNoLaunch)));
        loginActionMenu.Items.Add(new SeparatorMenuItem());
        loginActionMenu.Items.Add(MenuItem(Strings.RepairGameFilesButton, () => this.OnLogin?.Invoke(LoginAction.Repair)));

        if (LauncherApp.IsDebug)
        {
            loginActionMenu.Items.Add(new SeparatorMenuItem());
            loginActionMenu.Items.Add(MenuItem("Fake Login", () => this.OnLogin?.Invoke(LoginAction.Fake)));
        }

        this.loginButton = new Common.Button(Strings.LoginButton);
        this.loginButton.Click += TriggerLogin;
        this.loginButton.ContextMenu = loginActionMenu;

        // GTK sizes a button's height from its font, so the 45 px square buttons get a smaller glyph
        // than the page font; the width stays at the theme's button minimum.
        var caretButton = new Common.Button(string.Empty) { Width = ExtraButtonSize, Height = ExtraButtonSize };
        caretButton.ShowIcon(FontAwesomeIcon.CaretDown, 14f);
        caretButton.Click += () => loginActionMenu.Show(this.loginButton);

        var friendsButton = new Common.Button(string.Empty) { Width = ExtraButtonSize, Height = ExtraButtonSize };
        friendsButton.ShowIcon(FontAwesomeIcon.UserFriends, 14f);
        friendsButton.Click += () => this.mainPage.AccountSwitcher.Open(this.loginButton);

        this.secretsWarningLabel = new Label
        {
            Text = Strings.NoSecretsProviderWarning,
            TextColor = EtoColors.Red,
            Wrap = WrapMode.Word,
            // Without an explicit width, GTK sizes the wrapped label to the whole message, and that
            // natural width becomes the login column's width, pushing the news column off-screen.
            Width = 400,
            Visible = Program.Secrets is DummySecretProvider,
        };

        this.Content = new StackLayout
        {
            Spacing = 10,
            Items =
            {
                this.loginInput,
                this.passwordInput,
                this.oneTimePasswordCheckbox,
                this.useSteamServiceCheckbox,
                this.freeTrialCheckbox,
                this.autoLoginCheckbox,
                this.loginButton,
                new StackLayout
                {
                    Orientation = Orientation.Horizontal,
                    Items = { caretButton, friendsButton },
                },
                this.secretsWarningLabel,
                // The login column scales with the window in the page's table. Eto.Gtk packs items at the
                // top with no expand flag, so an empty stretch item at the end takes the remaining height
                // and keeps the action button row at the bottom of the page.
                new StackLayoutItem(new Panel(), VerticalAlignment.Stretch),
            },
        };
    }

    private static ButtonMenuItem MenuItem(string text, Action clicked)
    {
        var item = new ButtonMenuItem { Text = text };
        item.Click += (_, _) => clicked();
        return item;
    }
}
