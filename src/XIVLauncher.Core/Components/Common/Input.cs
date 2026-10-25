using System.Numerics;

using Eto.Forms;

using Serilog;

namespace XIVLauncher.Core.Components.Common;

/// <summary>Text-entry behaviour flags, replacing the ImGui input flags the launcher used.</summary>
[Flags]
public enum InputFlags
{
    None = 0,

    /// <summary>Text is masked (a password entry).</summary>
    Password = 1,

    /// <summary>Only decimal digits are accepted.</summary>
    Decimal = 2,

    /// <summary>The enter key completes the entry.</summary>
    EnterReturnsTrue = 4,
}

/// <summary>
/// A labelled text entry, optionally a password entry, wrapped in a <see cref="Component"/>.
/// </summary>
/// <remarks>
/// Replaces the immediate-mode ImGui text input, which kept its own text buffer and re-drew the entry
/// every frame. Typing is handled by the platform entry, so text is read straight from
/// <see cref="TextControl.Text"/>. Steam Deck gamepad text input is opened when the entry gains focus,
/// as before, and its result is written back into the entry when the gamepad overlay closes.
/// </remarks>
public class Input : Component
{
    private readonly TextControl input;
    private readonly Label label;

    private bool steamDeckInputActive;
    private bool sanitizing;

    public Input(
        string label,
        string hint,
        Vector2? spacing,
        uint maxLength = 255,
        bool isEnabled = true,
        InputFlags flags = InputFlags.None)
    {
        // An empty label is the "no label" case (the OTP box uses it); hidden so it reserves no height.
        this.label = new Label
        {
            Text = label,
            Visible = !string.IsNullOrEmpty(label),
        };

        // MaxLength lives on TextBox and PasswordBox, not on TextControl, so it is set when the
        // concrete control is created.
        this.input = flags.HasFlag(InputFlags.Password)
            ? new PasswordBox
            {
                Enabled = isEnabled,
                MaxLength = (int)maxLength,
            }
            : new TextBox
            {
                Enabled = isEnabled,
                PlaceholderText = hint,
                MaxLength = (int)maxLength,
            };

        this.Label = label;
        this.Hint = hint;
        this.MaxLength = maxLength;
        this.Flags = flags;
        this.Spacing = spacing;
        this.SteamDeckPrompt = string.Empty;

        if (flags.HasFlag(InputFlags.Decimal))
        {
            this.input.TextChanged += (_, _) => this.SanitizeDecimal();
        }

        this.input.KeyDown += (_, e) =>
        {
            if (e.Key == Keys.Enter && this.Flags.HasFlag(InputFlags.EnterReturnsTrue))
                this.Enter?.Invoke();
        };

        this.input.GotFocus += (_, _) =>
        {
            if (Program.Steam is { IsValid: true } && this.HasSteamDeckInput)
                this.steamDeckInputActive = Program.Steam.ShowGamepadTextInput(flags.HasFlag(InputFlags.Password), false, this.SteamDeckPrompt, (int)this.MaxLength, this.Value);
        };

        Program.Steam?.OnGamepadTextInputDismissed += a =>
        {
            if (!this.steamDeckInputActive)
                return;

            this.steamDeckInputActive = false;

            if (a && Program.Steam is { IsValid: true })
                this.Value = Program.Steam.GetEnteredGamepadText();
        };

        this.Content = new StackLayout
        {
            Spacing = (int)(spacing?.X ?? 10),
            Items = { this.label, this.input },
        };
    }

    public string Label { get; }

    public string Hint { get; }

    public uint MaxLength { get; }

    public InputFlags Flags { get; }

    public bool IsEnabled
    {
        get => this.input.Enabled;
        set => this.input.Enabled = value;
    }

    public Vector2? Spacing { get; }

    public bool HasSteamDeckInput { get; set; }

    public string SteamDeckPrompt { get; set; }

    /// <summary>Whether the entry should grab keyboard focus when its page is shown.</summary>
    public bool TakeKeyboardFocus { get; set; }

    /// <summary>
    /// Width of the entry itself. The wrapper's own Width sizes the label plus entry panel; GTK sizes an
    /// entry by its own width request, so HEAD's "SetNextItemWidth" equivalent lives here.
    /// </summary>
    public int InputWidth
    {
        get => this.input.Width;
        set => this.input.Width = value;
    }

    /// <summary>Executed on detection of the enter key.</summary>
    public event Action? Enter;

    public string Value
    {
        get => this.input.Text;
        set => this.input.Text = value;
    }

    /// <summary>Moves keyboard focus to the entry, which also opens Steam Deck text input when configured.</summary>
    public new void Focus() => this.input.Focus();

    private void SanitizeDecimal()
    {
        if (this.sanitizing)
            return;

        var text = this.input.Text;

        if (text.All(char.IsDigit))
            return;

        this.sanitizing = true;
        this.input.Text = new string(text.Where(char.IsDigit).ToArray());
        this.sanitizing = false;

        Log.Debug("Stripped non-digit input from the {Label} entry", this.Label);
    }
}
