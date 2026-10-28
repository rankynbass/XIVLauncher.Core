using System.IO;

using Eto;
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

    public Func<bool>? CheckEnabled { get; init; }

    /// <summary>Platform gates (rows that can never be enabled on this OS) hide the row entirely;
    /// evaluated at page build. Conditional gates use <see cref="CheckEnabled"/>.</summary>
    public Func<bool>? CheckVisibility { get; init; }

    public override bool IsEnabled => CheckEnabled?.Invoke() ?? true;

    public override bool IsVisible => CheckVisibility?.Invoke() ?? true;

    /// <summary>Gate re-evaluation without raising <see cref="Changed"/> (see SettingsPage.RefreshAllStates).</summary>
    public override void RefreshState() => this.UpdateState(notifyChanged: false);

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
            // Eto 2.13's FilePicker supports FileAction.SelectFolder, which opens a native folder
            // chooser (GtkFileChooserAction.SelectFolders on GTK, VistaFolderBrowserDialog on WinForms).
            var picker = new FilePicker();

            picker.FileAction = FileAction.SelectFolder;

            // The chooser button only shows the picked folder's name (and is not editable on GTK),
            // so mirror the full path in a label below the picker.
            var pathLabel = new Label
            {
                Wrap = WrapMode.Character,
                TextColor = EtoColors.Grey3,
            };

            void ShowPath(string path)
            {
                pathLabel.Text = string.IsNullOrEmpty(path) ? string.Empty : "Path: " + path;
                pathLabel.Visible = !string.IsNullOrEmpty(path);
            }

            picker.FilePathChanged += (_, _) =>
            {
                var path = picker.FilePath;

                ShowPath(path);

                this.InternalValue = string.IsNullOrEmpty(path)
                    ? null
                    : new DirectoryInfo(path);

                this.UpdateState();
            };

            this.applyValue = () =>
            {
                var dir = this.Value as DirectoryInfo;
                picker.FilePath = dir?.FullName ?? string.Empty;
                ShowPath(picker.FilePath);
            };

            var pickerRow = new DynamicLayout { Spacing = new Size(0, 2) };
            pickerRow.Add(picker);
            pickerRow.Add(pathLabel);

            return pickerRow;
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

            // A read-only combo box: it shows the current choice in a text field, but ReadOnly
            // blocks typing — the drop-down list is the only way to change it.
            var comboBox = new ComboBox { ReadOnly = true };

            // keys[SelectedIndex] is the numeric value of the item, since ComboBox items are plain strings.
            var keys = values.Cast<object>().Select(v => (int)v).ToArray();

            foreach (int value in keys)
            {
                var desc = descriptions[value].Description;
                desc = desc == string.Empty || desc == "dummy" ? string.Empty : " - " + desc;
                comboBox.Items.Add(descriptions[value].FriendlyName + desc);
            }

            comboBox.SelectedIndexChanged += (_, _) =>
            {
                if (comboBox.SelectedIndex < 0)
                    return;

                this.InternalValue = (T)Enum.ToObject(type, keys[comboBox.SelectedIndex]);
                this.UpdateState();
            };

            this.applyValue = () =>
            {
                var idx = (int)(this.InternalValue ?? 0);
                comboBox.SelectedIndex = idx;
            };

            return comboBox;
        }

        throw new InvalidOperationException($"No settings input for {type}");
    }

    protected void UpdateState(bool notifyChanged = true)
    {
        // Rows are never hidden; a gated row renders disabled, cascading to all its children
        // (GTK container sensitivity, WinForms Panel.Enabled).
        this.Enabled = this.IsEnabled;

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

        if (notifyChanged)
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
