using ImGuiNET;
using System;
using System.Collections;
using System.Collections.Generic;
using XIVLauncher.Common.Unix.Compatibility;
using XIVLauncher.Common.Unix.Compatibility.Wine;
using XIVLauncher.Common.Unix.Compatibility.Dxvk;
using XIVLauncher.Common.Unix.Compatibility.Nvapi;

using Serilog;

namespace XIVLauncher.Core.Components.SettingsPage;

public class ToolSettingsEntry : SettingsEntry<string>
{
    public Dictionary<string, IToolRelease> Pairs;

    public string DefaultValue;

    public bool ShowDescription;

    public bool ShowItemDescription;

    public ToolSettingsEntry(string name, string description, Func<string> load, Action<string?> save, Dictionary<string, IToolRelease> pairs, string defaultValue, bool showSelectedDesc = false, bool showItemDesc = true)
        : base(name, description, load, save)
    { 
        this.Pairs = pairs;
        this.DefaultValue = defaultValue;
        this.ShowDescription = showSelectedDesc;
        this.ShowItemDescription = showItemDesc;
        if (!Pairs.ContainsKey(DefaultValue))
        {
            // We don't care which one we get, we just don't want to crash!
            var idx = Pairs.FirstOrDefault().Key;
            Log.Warning($"The default value of \"{DefaultValue}\" is not a valid compatiblity tool. Using \"{idx}\"");
            DefaultValue = idx;
        }
    }

    // IWineRelease needs its own constructor
    public ToolSettingsEntry(string name, string description, Func<string> load, Action<string?> save, Dictionary<string, IWineRelease> pairs, string defaultValue, bool showSelectedDesc = false, bool showItemDesc = true)
        : base(name, description, load, save)
    {
        this.Pairs = new Dictionary<string, IToolRelease>();
        foreach (var pair in pairs)
            Pairs.Add(pair.Key, (IToolRelease)pair.Value);

        this.DefaultValue = defaultValue;
        this.ShowDescription = showSelectedDesc;
        this.ShowItemDescription = showItemDesc;
        if (!Pairs.ContainsKey(DefaultValue))
        {
            // We don't care which one we get, we just don't want to crash!
            var idx = Pairs.FirstOrDefault().Key;
            Log.Warning($"The default value of \"{DefaultValue}\" is not a valid compatiblity tool. Using \"{idx}\"");
            DefaultValue = idx;
        }
    }

    public override void Draw()
    {
        if (!Pairs.ContainsKey((string)this.InternalValue))
        {
            Log.Warning($"Value \"{(string)this.InternalValue}\" from launcher.ini is not a valid compatibility tool. Using default \"{DefaultValue}\"");
            this.InternalValue = DefaultValue;
        }

        string idx = (string)this.InternalValue;

        ImGuiHelpers.TextWrapped(this.Name);


        var label = Pairs[idx].Label + (ShowDescription ? " - " + Pairs[idx].Description : "");

        if (ImGui.BeginCombo($"###{Id.ToString()}", label))
        {
            foreach ( string key in Pairs.Keys)
            {
                var itemlabel = Pairs[key].Label + (ShowItemDescription ? " - " + Pairs[key].Description : "");
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

    public void Reset(Dictionary<string, IToolRelease> pairs)
    {
        this.Pairs.Clear();
        this.Pairs = pairs;
    }

    public void Reset(Dictionary<string, IWineRelease> pairs)
    {
        this.Pairs.Clear();
        foreach (var pair in pairs)
            Pairs.Add(pair.Key, (IToolRelease)pair.Value);
    }
}