using Eto.Forms;

using XIVLauncher.Core.Resources.Localization;

namespace XIVLauncher.Core.Components.LoadingPage;

/// <summary>
/// The loading page: status lines, a cancel/disable-auto-login pair, and either the rotating spinner or
/// a progress bar.
/// </summary>
/// <remarks>
/// The ImGui version read <c>Line1/Line2/Line3/Progress</c> during every draw; here the setters push the
/// new state to the UI thread and refresh the labels, so background patching threads can keep writing
/// to the same properties.
/// </remarks>
public class LoadingPage : Page
{
    private const int SPINNER_RADIUS = 15;

    private readonly Spinner spinner;
    private readonly ProgressBar progressBar;
    private readonly Label progressLabel;
    private readonly Label line1Label;
    private readonly Label line2Label;
    private readonly Label line3Label;
    private readonly Label autoLoginLabel;
    private readonly Common.Button cancelButton;
    private readonly Common.Button disableAutoLoginButton;

    private bool hasDisabledAutoLogin;

    private bool isIndeterminate;
    private bool canCancel = true;
    private bool canDisableAutoLogin;
    private string line1 = "Loading text line 1";
    private string? line2;
    private string? line3;
    private string? progressText;
    private float progress;

    public bool IsIndeterminate
    {
        get => this.isIndeterminate;
        set => this.SetStateField(ref this.isIndeterminate, value);
    }

    public bool CanCancel
    {
        get => this.canCancel;
        set => this.SetStateField(ref this.canCancel, value);
    }

    public bool CanDisableAutoLogin
    {
        get => this.canDisableAutoLogin;
        set => this.SetStateField(ref this.canDisableAutoLogin, value);
    }

    public string Line1
    {
        get => this.line1;
        set => this.SetStateField(ref this.line1, value);
    }

    public string? Line2
    {
        get => this.line2;
        set => this.SetStateField(ref this.line2, value);
    }

    public string? Line3
    {
        get => this.line3;
        set => this.SetStateField(ref this.line3, value);
    }

    public string? ProgressText
    {
        get => this.progressText;
        set => this.SetStateField(ref this.progressText, value);
    }

    public float Progress
    {
        get => this.progress;
        set => this.SetStateField(ref this.progress, value);
    }

    /// <summary>Stores the value and posts the resulting widget update.
    /// Every one of these properties is written from login, patching, Dalamud, and repair threads, so the
    /// widget write that follows has to be marshalled: a direct <c>Label.Text</c> on a worker thread
    /// segfaults inside pango/GTK size negotiation.</summary>
    private void SetStateField<T>(ref T field, T value)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return;

        field = value;
        Ui.Post(this.ApplyState);
    }

    /// <summary>Raises when the cancel button is pressed.</summary>
    public event Action? Cancelled;

    public LoadingPage(LauncherApp app)
        : base(app)
    {
        this.spinner = new Spinner(SPINNER_RADIUS, 5, EtoColors.Blue);

        // Eto's ProgressBar value is an integer, so progress is scaled to permille.
        this.progressBar = new ProgressBar
        {
            MinValue = 0,
            MaxValue = 1000,
            Width = 400,
        };

        this.progressLabel = new Label { TextAlignment = TextAlignment.Center };

        this.line1Label = new Label { Wrap = WrapMode.Character };
        this.line2Label = new Label { Wrap = WrapMode.Character };
        this.line3Label = new Label { Wrap = WrapMode.Character };
        this.autoLoginLabel = new Label { Text = Strings.AutoLoginDisabledNextStart };

        this.cancelButton = new Common.Button(Strings.CancelLabel);
        this.cancelButton.Width = 320;
        this.cancelButton.Click += () => this.Cancelled?.Invoke();

        this.disableAutoLoginButton = new Common.Button(Strings.DisableAutoLoginButton);
        this.disableAutoLoginButton.Width = 320;
        this.disableAutoLoginButton.Click += () =>
        {
            this.hasDisabledAutoLogin = true;
            App.Settings.IsAutologin = false;
            this.Refresh();
        };

        // The draw order in HEAD was: lines, cancel/disable buttons, then the spinner or the progress
        // bar. The same order, with the same spacers, as a stack.
        this.Content = new StackLayout
        {
            Spacing = 8,
            Items =
            {
                new StackLayoutItem(new Panel(), expand: true),
                new StackLayoutItem(this.line1Label, HorizontalAlignment.Center),
                new StackLayoutItem(this.line2Label, HorizontalAlignment.Center),
                new StackLayoutItem(this.line3Label, HorizontalAlignment.Center),
                new StackLayoutItem(this.cancelButton, HorizontalAlignment.Center),
                new StackLayoutItem(this.disableAutoLoginButton, HorizontalAlignment.Center),
                new StackLayoutItem(this.autoLoginLabel, HorizontalAlignment.Center),
                new StackLayoutItem(this.spinner, HorizontalAlignment.Center),
                new StackLayoutItem(this.progressBar, HorizontalAlignment.Center),
                new StackLayoutItem(this.progressLabel, HorizontalAlignment.Center),
                new StackLayoutItem(new Panel(), expand: true),
            },
        };
    }

    /// <summary>Resets the per-session "auto-login disabled" flag, called when loading starts.</summary>
    public void Reset()
    {
        this.hasDisabledAutoLogin = false;
        Ui.Post(this.ApplyState);
    }

    public override void OnShow() => Ui.Post(this.ApplyState);

    /// <summary>Pushes the current line/progress/cancel state onto the controls, posted to the UI thread.</summary>
    public void Refresh() => Ui.Post(this.ApplyState);

    /// <summary>Writes the state onto the controls. UI thread only.</summary>
    private void ApplyState()
    {
        Ui.AssertUIThread("LoadingPage.ApplyState");

        this.line1Label.Text = this.Line1;

        this.line2Label.Text = this.Line2 ?? string.Empty;
        this.line2Label.Visible = !string.IsNullOrEmpty(this.Line2);

        this.line3Label.Text = this.Line3 ?? string.Empty;
        this.line3Label.Visible = !string.IsNullOrEmpty(this.Line3);

        var showDisableAutoLogin = this.CanDisableAutoLogin && (App.Settings.IsAutologin ?? false);

        this.cancelButton.Visible = this.CanCancel;
        this.disableAutoLoginButton.Visible = showDisableAutoLogin;
        this.autoLoginLabel.Visible = this.hasDisabledAutoLogin && !showDisableAutoLogin;

        this.spinner.Visible = this.IsIndeterminate;
        this.progressBar.Visible = !this.IsIndeterminate;
        this.progressBar.Value = (int)Math.Round(this.Progress * 1000);

        this.progressLabel.Text = this.ProgressText ?? string.Empty;
        this.progressLabel.Visible = !this.IsIndeterminate && !string.IsNullOrEmpty(this.ProgressText);
    }
}
