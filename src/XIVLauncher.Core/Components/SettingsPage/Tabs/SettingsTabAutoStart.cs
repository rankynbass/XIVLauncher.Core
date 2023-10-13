using System.Numerics;
using ImGuiNET;

namespace XIVLauncher.Core.Components.SettingsPage.Tabs;

public class SettingsTabAutoStart : SettingsTab
{
    public SettingsTabAutoStart()
    {
        Entries = new SettingsEntry[]
        {
            new SettingsEntry<bool>("Enable App #1","", () => Program.Config.HelperApp1Enabled ?? false, b => Program.Config.HelperApp1Enabled = b),
            new SettingsEntry<string>("App #1", "Set a path for an exe file that you want launched with FFXIV. Warning: If it's invalid, the game may hang.", () => Program.Config.HelperApp1 ?? string.Empty, s => Program.Config.HelperApp1 = s)
            {
                CheckWarning = s =>
                {
                    if(!File.Exists(s) && !string.IsNullOrWhiteSpace(s))
                        return "That program doesn't exist.";
                    return null;
                },
            },
            new SettingsEntry<bool>("Use WineD3D with App #1","", () => Program.Config.HelperApp1WineD3D ?? false, b => Program.Config.HelperApp1WineD3D = b),
            new SettingsEntry<bool>("Enable App #2","", () => Program.Config.HelperApp2Enabled ?? false, b => Program.Config.HelperApp2Enabled = b)
            {
                CheckVisibility = () =>
                {
                    ImGui.Separator();
                    return true;
                }
            },
            new SettingsEntry<string>("App #2", "Set a path for an exe file that you want launched with FFXIV. Warning: If it's invalid, the game may hang.", () => Program.Config.HelperApp2 ?? string.Empty, s => Program.Config.HelperApp2 = s)
            {
                CheckWarning = s =>
                {
                    if(!File.Exists(s) && !string.IsNullOrWhiteSpace(s))
                        return "That program doesn't exist.";
                    return null;
                },
            },
            new SettingsEntry<bool>("Use WineD3D with App #2","", () => Program.Config.HelperApp2WineD3D ?? false, b => Program.Config.HelperApp2WineD3D = b),
            new SettingsEntry<bool>("Enable App #3","", () => Program.Config.HelperApp3Enabled ?? false, b => Program.Config.HelperApp3Enabled = b)
            {
                CheckVisibility = () =>
                {
                    ImGui.Separator();
                    return true;
                }
            },
            new SettingsEntry<string>("App #3", "Set a path for an exe file that you want launched with FFXIV. Warning: If it's invalid, the game may hang.", () => Program.Config.HelperApp3 ?? string.Empty, s => Program.Config.HelperApp3 = s)
            {
                CheckWarning = s =>
                {
                    if(!File.Exists(s) && !string.IsNullOrWhiteSpace(s))
                        return "That program doesn't exist.";
                    return null;
                },
            },
            new SettingsEntry<bool>("Use WineD3D with App #3","", () => Program.Config.HelperApp3WineD3D ?? false, b => Program.Config.HelperApp3WineD3D = b),
        };
    }

    public override SettingsEntry[] Entries { get; }

    public override bool IsUnixExclusive => true;

    public override string Title => "Auto-Start";

    public override void Draw()
    {
        ImGui.Text("Warning! This may cause FFXIV to be listed as running when you close steam.");
        ImGui.Text("You should be able to simply click the button to stop it, however.");
        ImGui.Text("May not work if DXVK is disabled (Game will not launch)");

        ImGui.Dummy(new Vector2(10) * ImGuiHelpers.GlobalScale);
        ImGui.Separator();
        ImGui.Dummy(new Vector2(10) * ImGuiHelpers.GlobalScale);

        base.Draw();
    }
}