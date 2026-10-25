using Eto.Forms;

namespace XIVLauncher.Core.Components.Common;

/// <summary>
/// A launcher push button: a native Eto button wrapped in a <see cref="Component"/>.
/// </summary>
/// <remarks>
/// Replaces the immediate-mode ImGui button, which drew itself every frame and detected Enter on a
/// focused button. Eto fires <see cref="Click"/> on press, and the default-button/focus behaviour comes
/// from the platform, so the "focused button + Enter" special case disappears.
/// </remarks>
public class Button : Component
{
    private readonly Eto.Forms.Button button;

    public Button(string label, bool isEnabled = true)
    {
        this.button = new Eto.Forms.Button
        {
            Text = label,
            Enabled = isEnabled,
        };

        this.button.Click += (_, _) => this.Click?.Invoke();

        this.Content = this.button;
    }

    /// <summary>Raises when the button is pressed.</summary>
    public event Action? Click;

    public bool IsEnabled
    {
        get => this.button.Enabled;
        set => this.button.Enabled = value;
    }

    public string Label
    {
        get => this.button.Text;
        set => this.button.Text = value;
    }

    /// <summary>Requested button width, <c>null</c> lets the layout size the button.</summary>
    public new int? Width
    {
        get => this.button.Width < 0 ? null : this.button.Width;
        set => this.button.Width = value ?? -1;
    }

    /// <summary>Shows the icon glyph as the button label, using the Font Awesome font. A smaller
    /// <paramref name="fontSize"/> keeps the glyph inside a fixed-size button, because GTK sizes a
    /// button's height from its font.</summary>
    public void ShowIcon(FontAwesomeIcon icon, float? fontSize = null)
    {
        this.button.Font = fontSize is null ? AppFonts.IconFont : AppFonts.IconFontAt(fontSize.Value);
        this.button.Text = AppFonts.Glyph(icon);
    }

    /// <summary>The tooltip shown when the pointer rests on the button.</summary>
    public new string ToolTip
    {
        get => this.button.ToolTip;
        set => this.button.ToolTip = value;
    }

    /// <summary>Moves keyboard focus to the button.</summary>
    public new void Focus() => this.button.Focus();
}
