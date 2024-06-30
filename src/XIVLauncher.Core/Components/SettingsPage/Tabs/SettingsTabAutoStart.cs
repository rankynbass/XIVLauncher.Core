using System.Numerics;
using ImGuiNET;

namespace XIVLauncher.Core.Components.SettingsPage.Tabs;

public class SettingsTabAutoStart : SettingsTab
{
    public SettingsTabAutoStart()
    {
        Entries = new SettingsEntry[]
        {
            new SettingsEntry<bool>("Enable App #1", "", () => Program.Config.HelperApp1Enabled ?? false, b => Program.Config.HelperApp1Enabled = b),
            new SettingsEntry<string>("App #1", "Set a path for an exe file that you want launched with FFXIV. Warning: If it's invalid, the game may hang.", () => Program.Config.HelperApp1 ?? string.Empty, s => Program.Config.HelperApp1 = s)
            {
                CheckWarning = s =>
                {
                    if(!File.Exists(s) && !string.IsNullOrWhiteSpace(s))
                        return "That program doesn't exist.";
                    return null;
                },
            },
            new SettingsEntry<string>("App #1 Arguments", "Any additional arguments for App #1. Environment variables not supported.", () => Program.Config.HelperApp1Args ?? "", s => Program.Config.HelperApp1Args = s),
            new SettingsEntry<bool>("Use WineD3D with App #1","", () => Program.Config.HelperApp1WineD3D ?? false, b => Program.Config.HelperApp1WineD3D = b)
            {
                CheckValidity = b =>
                {
                    // Dirty hack to get a separator line
                    ImGui.Dummy(new Vector2(10));
                    ImGui.Separator();
                    return null;
                }
            },

            new SettingsEntry<bool>("Enable App #2", "", () => Program.Config.HelperApp2Enabled ?? false, b => Program.Config.HelperApp2Enabled = b),
            new SettingsEntry<string>("App #2", "Set a path for an exe file that you want launched with FFXIV. Warning: If it's invalid, the game may hang.", () => Program.Config.HelperApp2 ?? string.Empty, s => Program.Config.HelperApp2 = s)
            {
                CheckWarning = s =>
                {
                    if(!File.Exists(s) && !string.IsNullOrWhiteSpace(s))
                        return "That program doesn't exist.";
                    return null;
                },
            },
            new SettingsEntry<string>("App #2 Arguments", "Any additional arguments for App #2. Environment variables not supported.", () => Program.Config.HelperApp2Args ?? "", s => Program.Config.HelperApp2Args = s),
            new SettingsEntry<bool>("Use WineD3D with App #2","", () => Program.Config.HelperApp2WineD3D ?? false, b => Program.Config.HelperApp2WineD3D = b)
            {
                CheckValidity = b =>
                {
                    // Dirty hack to get a separator line
                    ImGui.Dummy(new Vector2(10));
                    ImGui.Separator();
                    return null;
                }
            },

            new SettingsEntry<bool>("Enable App #3", "", () => Program.Config.HelperApp3Enabled ?? false, b => Program.Config.HelperApp3Enabled = b),
            new SettingsEntry<string>("App #3", "Set a path for an exe file that you want launched with FFXIV. Warning: If it's invalid, the game may hang.", () => Program.Config.HelperApp3 ?? string.Empty, s => Program.Config.HelperApp3 = s)
            {
                CheckWarning = s =>
                {
                    if(!File.Exists(s) && !string.IsNullOrWhiteSpace(s))
                        return "That program doesn't exist.";
                    return null;
                },
            },
            new SettingsEntry<string>("App #3 Arguments", "Any additional arguments for App #3. Environment variables not supported.", () => Program.Config.HelperApp3Args ?? "", s => Program.Config.HelperApp3Args = s),
            new SettingsEntry<bool>("Use WineD3D with App #3", "", () => Program.Config.HelperApp3WineD3D ?? false, b => Program.Config.HelperApp3WineD3D = b),
        };
    }

    public override SettingsEntry[] Entries { get; }

    // public override bool IsUnixExclusive => true;

    public override string Title => "Auto-Start";

    public override void Draw()
    {
        ImGui.Text("Please check back later.");

        base.Draw();
    }
}
