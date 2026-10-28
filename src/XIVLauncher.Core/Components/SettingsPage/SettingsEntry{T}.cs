using Eto.Drawing;
using Eto.Forms;

using XIVLauncher.Common;
using XIVLauncher.Common.Util;

namespace XIVLauncher.Core.Components.SettingsPage;

/// <summary>
/// A typed settings row: the value is read from and written to the launcher configuration, and the
/// input control is chosen from the value type.
/// </summary>
/// <remarks>
/// Replaces the immediate-mode typed entry, which switched on <c>typeof(T)</c> to pick an ImGui widget
/// each frame. Here the switch picks the native control once:
/// <list type="bullet">
///   <item><description>paths and strings: a text box</description></item>
///   <item><description>booleans: a check box (the label is the setting name)</description></item>
///   <item><description>enums: a drop-down listing the friendly names with descriptions</description></item>
/// </list>
/// Validity and warning checks run when the value changes, so the labels update without a redraw loop.
/// </remarks>
public class SettingsEntry<T> : SettingsEntry
{
    /// <summary>DynamicLayout stretches each row's controls to the tab width, so the wrapped labels
    /// wrap at the column width; no explicit label widths are needed.</summary>

    private readonly Func<T?> load;
    private readonly Action<T?> save;
    protected Action applyValue = () => { };

    private readonly Label nameLabel;
    protected readonly Control input;
    private readonly Label descriptionLabel;
    private readonly Label validityLabel;
    private readonly Label warningLabel;

    public override string Name { get; }

    public string Description { get; }

    public Func<T?, string?>? CheckValidity { get; init; }

    public Func<T?, string?>? CheckWarning { get; init; }

    public Func<bool>? CheckVisibility { get; init; }

    public override bool IsVisible => CheckVisibility?.Invoke() ?? true;

    public T? Value => this.InternalValue == default ? default : (T)this.InternalValue;

    public SettingsEntry(string name, string description, Func<T?> load, Action<T?> save)
    {
        this.load = load;
        this.save = save;
        this.Name = name;
        this.Description = description;

        this.nameLabel = new Label
        {
            Text = this.Name,
            Wrap = WrapMode.Character,
        };

        this.input = this.CreateInput();

        this.nameLabel.MouseDown += (_, e) =>
        {
            if (e.Buttons == Eto.Forms.MouseButtons.Primary)
            {
                this.input.Focus();
                e.Handled = true;
            }
        };

        this.descriptionLabel = new Label
        {
            Text = this.Description,
            TextColor = EtoColors.Grey3,
            Wrap = WrapMode.Character,
        };

        this.validityLabel = new Label
        {
            TextColor = EtoColors.Red,
            Wrap = WrapMode.Character,
            Visible = false,
        };

        this.warningLabel = new Label
        {
            TextColor = EtoColors.Red,
            Wrap = WrapMode.Character,
            Visible = false,
        };

        var rowLayout = new DynamicLayout
        {
            Spacing = new Size(5, 5),
        };

        rowLayout.Add(this.nameLabel);
        rowLayout.Add(this.input);
        rowLayout.Add(this.descriptionLabel);
        rowLayout.Add(this.validityLabel);
        rowLayout.Add(this.warningLabel);

        this.Content = rowLayout;
    }

    /// <summary>Creates the input control for the value type, plus the delegate that pushes a loaded
    /// value into it.</summary>
    protected virtual Control CreateInput()
    {
        var type = typeof(T);

        if (type == typeof(DirectoryInfo))
        {
            var textBox = new TextBox { MaxLength = 10000 };

            textBox.TextChanged += (_, _) =>
            {
                this.InternalValue = !string.IsNullOrEmpty(textBox.Text) ? new DirectoryInfo(textBox.Text) : null;
                this.UpdateState();
            };

            this.applyValue = () => textBox.Text = (this.Value as DirectoryInfo)?.FullName ?? string.Empty;

            return textBox;
        }

        if (type == typeof(string))
        {
            var textBox = new TextBox { MaxLength = 1000 };

            textBox.TextChanged += (_, _) =>
            {
                this.InternalValue = textBox.Text;
                this.UpdateState();
            };

            this.applyValue = () => textBox.Text = this.Value as string ?? string.Empty;

            return textBox;
        }

        if (type == typeof(bool))
        {
            var checkBox = new CheckBox { Text = this.Name };

            // The check box carries the setting name, so the separate name label is not needed.
            this.nameLabel.Visible = false;

            checkBox.CheckedChanged += (_, _) =>
            {
                this.InternalValue = checkBox.Checked == true;
                this.UpdateState();
            };

            this.applyValue = () => checkBox.Checked = this.Value as bool? ?? false;

            return checkBox;
        }

        if (type.IsEnum)
        {
            var values = Enum.GetValues(type);
            var descriptions = values.Cast<Enum>()
                                     .Select(x => x.GetAttribute<SettingsDescriptionAttribute>() ?? new SettingsDescriptionAttribute(x.ToString(), string.Empty))
                                     .ToArray();

            var dropDown = new DropDown();

            foreach (int value in values)
            {
                var desc = descriptions[value].Description;
                desc = desc == string.Empty || desc == "dummy" ? string.Empty : " - " + desc;
                dropDown.Items.Add(new ListItem { Text = descriptions[value].FriendlyName + desc, Key = value.ToString() });
            }

            dropDown.SelectedIndexChanged += (_, _) =>
            {
                if (dropDown.SelectedIndex < 0)
                    return;

                this.InternalValue = (T)Enum.ToObject(type, int.Parse(dropDown.Items[dropDown.SelectedIndex].Key));
                this.UpdateState();
            };

            this.applyValue = () =>
            {
                var idx = (int)(this.InternalValue ?? 0);
                dropDown.SelectedIndex = idx;
            };

            return dropDown;
        }

        throw new InvalidOperationException($"No settings input for {type}");
    }

    protected void UpdateState()
    {
        if (this.CheckValidity != null)
        {
            var validityMsg = this.CheckValidity.Invoke(this.Value);
            this.IsValid = string.IsNullOrEmpty(validityMsg);

            this.validityLabel.Text = validityMsg ?? string.Empty;
            this.validityLabel.Visible = !this.IsValid;
        }
        else
        {
            this.IsValid = true;
            this.validityLabel.Visible = false;
        }

        var warningMessage = this.CheckWarning?.Invoke(this.Value);

        this.warningLabel.Text = warningMessage ?? string.Empty;
        this.warningLabel.Visible = warningMessage != null;

        this.RaiseChanged();
    }

    public override void LoadValues()
    {
        this.InternalValue = this.load();

        this.applyValue();
        this.UpdateState();
    }

    public override void Save() => this.save(this.Value);
}
