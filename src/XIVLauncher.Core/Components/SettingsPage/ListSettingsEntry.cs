using ImGuiNET;
using XIVLauncher.Core.UnixCompatibility;

namespace XIVLauncher.Core.Components.SettingsPage;

public class ListSettingsEntry : SettingsEntry<string>
{
    public List<string> Items;

    public string DefaultValue;

    public ListSettingsEntry(string name, string description, List<string> items, Func<string> load, Action<string?> save, string defaultValue)
        : base(name, description, load, save)
    { 
        this.Items = items;
        this.DefaultValue = defaultValue;
    }


    public override void Draw()
    {
        var nativeValue = this.Value;
        string idx = (string)(this.InternalValue ?? DefaultValue);
        if (!Items.Contains(idx))
        {
            idx = DefaultValue;
            this.InternalValue = DefaultValue;
        }
        ImGuiHelpers.TextWrapped(this.Name);

        if (ImGui.BeginCombo($"###{Id.ToString()}", idx))
        {
            foreach ( string item in Items )
            {
                if (ImGui.Selectable(item, idx == item))
                {
                    this.InternalValue = item;
                }
            }
            ImGui.EndCombo();
        }

        ImGui.PushStyleColor(ImGuiCol.Text, ImGuiColors.DalamudGrey);
        if (!string.IsNullOrEmpty(this.Description)) ImGuiHelpers.TextWrapped(this.Description);
        ImGui.PopStyleColor();

        if (this.CheckValidity != null)
        {
            var validityMsg = this.CheckValidity.Invoke(this.Value);
            this.IsValid = string.IsNullOrEmpty(validityMsg);

            if (!this.IsValid)
            {
                ImGui.PushStyleColor(ImGuiCol.Text, ImGuiColors.DalamudRed);
                ImGui.Text(validityMsg);
                ImGui.PopStyleColor();
            }
        }
        else
        {
            this.IsValid = true;
        }

        var warningMessage = this.CheckWarning?.Invoke(this.Value);

        if (warningMessage != null)
        {
            ImGui.PushStyleColor(ImGuiCol.Text, ImGuiColors.DalamudRed);
            ImGui.Text(warningMessage);
            ImGui.PopStyleColor();
        }
        
    }
}