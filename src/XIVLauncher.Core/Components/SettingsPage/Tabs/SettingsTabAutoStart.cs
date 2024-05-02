using System.Numerics;
using ImGuiNET;

namespace XIVLauncher.Core.Components.SettingsPage.Tabs;

public class SettingsTabAutoStart : SettingsTab
{
    public SettingsTabAutoStart()
    {
        Entries = new SettingsEntry[]
        {
            new SettingsEntry<bool>("Enable App #1", "", () => Program.Config.AutoStart1Enabled ?? false, b => Program.Config.AutoStart1Enabled = b),
            new SettingsEntry<string>("App #1", "Set a path for an exe file that you want launched with FFXIV. Warning: If it's invalid, the game may hang.", () => Program.Config.AutoStart1 ?? string.Empty, s => Program.Config.AutoStart1 = s)
            {
                CheckWarning = s =>
                {
                    if(!File.Exists(s) && !string.IsNullOrWhiteSpace(s))
                        return "That program doesn't exist.";
                    return null;
                },
            },
            new SettingsEntry<string>("App #1 Arguments", "Any additional arguments for App #1. Environment variables not supported.", () => Program.Config.AutoStart1Args ?? "", s => Program.Config.AutoStart1Args = s),
            new SettingsEntry<bool>("Use WineD3D with App #1","", () => Program.Config.AutoStart1WineD3D ?? false, b => Program.Config.AutoStart1WineD3D = b)
            {
                CheckValidity = b =>
                {
                    // Dirty hack to get a separator line
                    ImGui.Dummy(new Vector2(10));
                    ImGui.Separator();
                    return null;
                }
            },

            new SettingsEntry<bool>("Enable App #2", "", () => Program.Config.AutoStart2Enabled ?? false, b => Program.Config.AutoStart2Enabled = b),
            new SettingsEntry<string>("App #2", "Set a path for an exe file that you want launched with FFXIV. Warning: If it's invalid, the game may hang.", () => Program.Config.AutoStart2 ?? string.Empty, s => Program.Config.AutoStart2 = s)
            {
                CheckWarning = s =>
                {
                    if(!File.Exists(s) && !string.IsNullOrWhiteSpace(s))
                        return "That program doesn't exist.";
                    return null;
                },
            },
            new SettingsEntry<string>("App #2 Arguments", "Any additional arguments for App #2. Environment variables not supported.", () => Program.Config.AutoStart2Args ?? "", s => Program.Config.AutoStart2Args = s),
            new SettingsEntry<bool>("Use WineD3D with App #2","", () => Program.Config.AutoStart2WineD3D ?? false, b => Program.Config.AutoStart2WineD3D = b)
            {
                CheckValidity = b =>
                {
                    // Dirty hack to get a separator line
                    ImGui.Dummy(new Vector2(10));
                    ImGui.Separator();
                    return null;
                }
            },

            new SettingsEntry<bool>("Enable App #3", "", () => Program.Config.AutoStart3Enabled ?? false, b => Program.Config.AutoStart3Enabled = b),
            new SettingsEntry<string>("App #3", "Set a path for an exe file that you want launched with FFXIV. Warning: If it's invalid, the game may hang.", () => Program.Config.AutoStart3 ?? string.Empty, s => Program.Config.AutoStart3 = s)
            {
                CheckWarning = s =>
                {
                    if(!File.Exists(s) && !string.IsNullOrWhiteSpace(s))
                        return "That program doesn't exist.";
                    return null;
                },
            },
            new SettingsEntry<string>("App #3 Arguments", "Any additional arguments for App #3. Environment variables not supported.", () => Program.Config.AutoStart3Args ?? "", s => Program.Config.AutoStart3Args = s),
            new SettingsEntry<bool>("Use WineD3D with App #3", "", () => Program.Config.AutoStart3WineD3D ?? false, b => Program.Config.AutoStart3WineD3D = b),
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
