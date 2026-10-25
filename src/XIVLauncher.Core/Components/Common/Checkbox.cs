using Eto.Forms;

namespace XIVLauncher.Core.Components.Common;

/// <summary>
/// A launcher checkbox: a native Eto checkbox wrapped in a <see cref="Component"/>.
/// </summary>
/// <remarks>
/// Replaces the immediate-mode ImGui checkbox, which had to implement "click the label toggles the box"
/// by hand. Eto renders the label as part of the checkbox, so label clicks toggle natively and
/// <see cref="OnChange"/> fires with the new value.
/// </remarks>
public class Checkbox : Component
{
    private readonly Eto.Forms.CheckBox checkBox;

    public Checkbox(string label, bool value = false, bool isEnabled = true)
    {
        this.checkBox = new Eto.Forms.CheckBox
        {
            Text = label,
            Checked = value,
            Enabled = isEnabled,
        };

        this.checkBox.CheckedChanged += (_, _) => this.OnChange?.Invoke(this.Value);

        this.Content = this.checkBox;
    }

    /// <summary>Raises with the new value whenever the checkbox is toggled.</summary>
    public event Action<bool>? OnChange;

    public string Label
    {
        get => this.checkBox.Text;
        set => this.checkBox.Text = value;
    }

    public bool IsEnabled
    {
        get => this.checkBox.Enabled;
        set => this.checkBox.Enabled = value;
    }

    public bool Value
    {
        get => this.checkBox.Checked == true;
        set => this.checkBox.Checked = value;
    }

    /// <summary>Moves keyboard focus to the checkbox.</summary>
    public new void Focus() => this.checkBox.Focus();
}
