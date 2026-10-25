using Eto.Drawing;
using Eto.Forms;

using Serilog;

using XIVLauncher.Common.Http;

using XIVLauncher.Core.Resources.Localization;

namespace XIVLauncher.Core.Components;

/// <summary>
/// The OTP entry page: a centered entry box, a paste button, and OK/Cancel, plus the optional OTP HTTP
/// listener and Steam Deck gamepad input.
/// </summary>
/// <remarks>
/// Replaces the immediate-mode OTP child window. The old keyboard-focus trick becomes an Eto
/// <see cref="Control.Focus"/> post on reset; the paste icon button reads the Eto clipboard.
/// </remarks>
public class OtpEntryPage : Page
{
    private string otp = string.Empty;
    private OtpListener? otpListener;
    private TaskCompletionSource<string?> resultTcs = new();
    private bool steamDeckInputActive;

    private readonly Common.Input otpInput;

    public string? Result { get; private set; }

    public bool Cancelled { get; private set; }

    public Task<string?> GetResultAsync() => this.resultTcs.Task;

    public OtpEntryPage(LauncherApp app)
        : base(app)
    {
        if (Program.Steam is not null)
            Program.Steam.OnGamepadTextInputDismissed += this.SteamOnOnGamepadTextInputDismissed;

        // HEAD sized the OTP entry at 150 px with a 28 px paste button beside it, and the two buttons
        // below at half that combined width.
        this.otpInput = new Common.Input(string.Empty, string.Empty, null, 7, flags: Common.InputFlags.Decimal | Common.InputFlags.EnterReturnsTrue)
        {
            InputWidth = 150,
        };
        this.otpInput.Enter += () => this.TryAcceptOtp(this.otpInput.Value);

        var pasteButton = new Common.Button(string.Empty) { Width = 28, Height = 28 };
        pasteButton.ShowIcon(FontAwesomeIcon.Paste);
        pasteButton.Click += () =>
        {
            this.otpInput.Value = Clipboard.Instance.Text ?? string.Empty;
            this.TryAcceptOtp(this.otpInput.Value);
        };

        var okButton = new Common.Button(Strings.OKLabel) { Width = 89, Height = 30 };
        okButton.Click += () => this.TryAcceptOtp(this.otpInput.Value);

        var cancelButton = new Common.Button(Strings.CancelLabel) { Width = 89, Height = 30 };
        cancelButton.Click += () =>
        {
            this.Cancelled = true;
            this.resultTcs.TrySetResult(null);
        };

        // HEAD drew this as a centered 300x200 child window with a 40% alpha background. The Eto
        // equivalent is a centered, fixed-width panel with the same tint, stacked in the same order.
        var box = new Panel
        {
            Width = 300,
            BackgroundColor = EtoColors.OtpChildBackground,
            Content = new StackLayout
            {
                Spacing = 10,
                Items =
                {
                    new Label { Text = Strings.EnterYourOTP, TextAlignment = TextAlignment.Center },
                    new StackLayout
                    {
                        Orientation = Orientation.Horizontal,
                        Items =
                        {
                            new StackLayoutItem(this.otpInput, VerticalAlignment.Center),
                            new StackLayoutItem(pasteButton, VerticalAlignment.Center),
                        },
                    },
                    new StackLayout
                    {
                        Orientation = Orientation.Horizontal,
                        Items = { okButton, cancelButton },
                    },
                },
            },
        };

        var layout = new DynamicLayout { Padding = new Padding(0, 200) };
        layout.AddCentered(box);
        this.Content = layout;
    }

    private void SteamOnOnGamepadTextInputDismissed(bool success)
    {
        if (!this.steamDeckInputActive)
            return;

        this.steamDeckInputActive = false;

        if (success && Program.Steam is not null)
            this.TryAcceptOtp(Program.Steam.GetEnteredGamepadText());
    }

    public void Reset()
    {
        this.otp = string.Empty;
        this.Result = null;
        this.Cancelled = false;
        this.resultTcs = new TaskCompletionSource<string?>();

        Ui.Post(() =>
        {
            this.otpInput.Value = string.Empty;
            this.otpInput.Focus();
        });

        // TODO(goat): This doesn't work if you call it right after starting the app... Steam probably takes a little while to initialize. Might be annoying for autologin.
        // BUG: We have to turn this off when using OTP server, because there's no way to dismiss open keyboards
        if (Program.Steam != null && Program.Steam.IsValid && Program.IsSteamDeckHardware && App.Settings.IsOtpServer is false)
        {
            var success = Program.Steam.ShowGamepadTextInput(false, false, Strings.EnterYourOTP, 6, string.Empty);
            this.steamDeckInputActive = success;
            Log.Verbose("ShowGamepadTextInput: {Success}", success);
        }

        if (App.Settings.IsOtpServer ?? false)
        {
            try
            {
                // Start Listen
                this.StartOtpListener();
                Log.Debug("OTP server started...");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Could not start OTP HTTP listener");
            }
        }
    }

    private void TryAcceptOtp(string otp)
    {
        if (string.IsNullOrEmpty(otp) || otp.Length != 6)
        {
            Log.Error("Invalid OTP: {Otp}", otp);
            return;
        }

        this.otp = otp;

        Log.Verbose("Received OTP: {Otp}", otp);
        this.Result = otp;
        this.resultTcs.TrySetResult(otp);
    }

    public void StartOtpListener()
    {
        if (this.otpListener is null)
        {
            this.otpListener = new OtpListener("core-" + AppUtil.GetAssemblyVersion());
            this.otpListener.OnOtpReceived += this.TryAcceptOtp;
            Task.Run(this.otpListener.Start);
        }
    }

    public void StopOtpListener()
    {
        if (this.otpListener is not null)
        {
            this.otpListener.Stop();
            this.otpListener.OnOtpReceived -= this.TryAcceptOtp;
        }

        this.otpListener = null;
    }
}
