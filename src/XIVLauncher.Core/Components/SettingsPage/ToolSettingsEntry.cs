using ImGuiNET;
using System;
using System.Collections;
using System.Collections.Generic;
using XIVLauncher.Common.Unix.Compatibility;

namespace XIVLauncher.Core.Components.SettingsPage;

public class ToolSettingsEntry : SettingsEntry<string>
{
    public Dictionary<string, CompatToolRelease> Pairs;

    public string DefaultValue;

    public bool ShowDescription;

    public bool ShowItemDescription;

    public ToolSettingsEntry(string name, string description, Dictionary<string, CompatToolRelease> pairs, Func<string> load, Action<string?> save, string defaultValue, bool showSelectedDesc = false, bool showItemDesc = true)
        : base(name, description, load, save)
    { 
        this.Pairs = pairs;
        this.DefaultValue = defaultValue;
        this.ShowDescription = showSelectedDesc;
        this.ShowItemDescription = showItemDesc;
    }


    public override void Draw()
    {
        string idx = (string)(this.InternalValue ?? DefaultValue);

        ImGuiHelpers.TextWrapped(this.Name);

        if (!Pairs.ContainsKey(idx))
            idx = DefaultValue;
        var label = Pairs[idx].Name + (ShowDescription ? " - " + Pairs[idx].Description : "");

        if (ImGui.BeginCombo($"###{Id.ToString()}", label))
        {
            foreach ( string key in Pairs.Keys)
            {
                var itemlabel = Pairs[key].Name + (ShowItemDescription ? " - " + Pairs[key].Description : "");
                if (ImGui.Selectable(itemlabel, idx == key))
                {
                    this.InternalValue = key;
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