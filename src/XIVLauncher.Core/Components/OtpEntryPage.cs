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
/// <see cref="Control.Focus"/> post on reset; the paste icon button reads the clipboard through GtkSharp,
/// because the Eto.Gtk clipboard getter returns null. The 40% alpha child-window background becomes an
/// opaque bitmap tint, because GTK renders no alpha and paints no panel background.
/// </remarks>
public class OtpEntryPage : Page
{
    /// <summary>HEAD sized this child window at 300x200, with the entry at 150 px.</summary>
    private const int ChildWidth = 300;
    private const int ChildHeight = 200;

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
#if LINUX
            // Eto.Gtk binds Clipboard.Text to a write-only GtkSharp property, so the Eto read is always
            // null; the synchronous GtkSharp request is the one that sees the clipboard.
            this.otpInput.Value = Gtk.Clipboard.Get(Gdk.Selection.Clipboard).WaitForText() ?? string.Empty;
#else
            // The Eto clipboard reads correctly on the WinForms platform.
            this.otpInput.Value = Clipboard.Instance.Text ?? string.Empty;
#endif
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

        // HEAD drew this as a centered 300x200 child window with a 40% alpha background, a 40 px top gap,
        // and 188 px of centered content. GTK renders no alpha and never paints a panel background, so the
        // tint is baked into an opaque bitmap layered behind the stack. Rows are centred with
        // StackLayoutItem (GTK ignores label text alignment) and the stack is pinned to the box width so
        // centring spans the box, not the rows' natural width. Row items stay plain: a vertical
        // alignment pushes the fixed-size paste button below the entry's top edge.
        var rows = new StackLayout
        {
            Padding = new Padding(0, 40, 0, 0),
            Spacing = 10,
            Width = ChildWidth,
            Items =
            {
                new StackLayoutItem(new Label { Text = Strings.EnterYourOTP, TextAlignment = TextAlignment.Center }, HorizontalAlignment.Center),
                new StackLayoutItem(new StackLayout
                {
                    Orientation = Orientation.Horizontal,
                    Items = { this.otpInput, pasteButton },
                }, HorizontalAlignment.Center),
                new StackLayoutItem(new StackLayout
                {
                    Orientation = Orientation.Horizontal,
                    Items = { okButton, cancelButton },
                }, HorizontalAlignment.Center),
            },
        };

        var layers = new PixelLayout();
        layers.Add(CreateTintView(), 0, 0);
        layers.Add(rows, 0, 0);

        var layout = new DynamicLayout { Padding = new Padding(0, 200) };
        layout.AddCentered(new Panel { Size = new Size(ChildWidth, ChildHeight), Content = layers });
        this.Content = layout;
    }

    /// <summary>The child box tint as a bitmap: GTK cannot draw a translucent background, so the 40% grey
    /// is baked in as an opaque fill.</summary>
    private static ImageView CreateTintView()
    {
        var bitmap = new Bitmap(ChildWidth, ChildHeight, PixelFormat.Format32bppRgba);

        using var graphics = new Graphics(bitmap);
        graphics.FillRectangle(EtoColors.OtpChildOpaqueBackground, 0, 0, ChildWidth, ChildHeight);

        return new ImageView { Image = bitmap, Size = new Size(ChildWidth, ChildHeight) };
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
