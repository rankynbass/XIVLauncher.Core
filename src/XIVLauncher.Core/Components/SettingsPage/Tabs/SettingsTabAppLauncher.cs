using System.Numerics;
using System.Runtime.InteropServices;
using ImGuiNET;
using XIVLauncher.Common.Unix.Compatibility;
using XIVLauncher.Common.Util;

namespace XIVLauncher.Core.Components.SettingsPage.Tabs;

public class SettingsTabAppLauncher : SettingsTab
{
    public SettingsTabAppLauncher()
    {
        Entries = new SettingsEntry[]
        {
            new SettingsEntry<bool>("Enable App #1","", () => Program.Config.HelperApp1Enabled, b => Program.Config.HelperApp1Enabled = b),
            new SettingsEntry<string>("App #1", "Set a path for an exe file that you want launched with FFXIV. Warning: If it's invalid, the game may hang.", () => Program.Config.HelperApp1, s => Program.Config.HelperApp1 = s)
            {
                CheckWarning = s =>
                {
                    if(!File.Exists(s) && !string.IsNullOrWhiteSpace(s))
                        return "That program doesn't exist.";
                    return null;
                },
            },
            new SettingsEntry<bool>("Use WineD3D with App #1","", () => Program.Config.HelperApp1WineD3D, b => Program.Config.HelperApp1WineD3D = b),
            new SettingsEntry<bool>("Enable App #2","", () => Program.Config.HelperApp2Enabled, b => Program.Config.HelperApp2Enabled = b),
            new SettingsEntry<string>("App #2", "Set a path for an exe file that you want launched with FFXIV. Warning: If it's invalid, the game may hang.", () => Program.Config.HelperApp2, s => Program.Config.HelperApp2 = s)
            {
                CheckWarning = s =>
                {
                    if(!File.Exists(s) && !string.IsNullOrWhiteSpace(s))
                        return "That program doesn't exist.";
                    return null;
                },
            },
            new SettingsEntry<bool>("Use WineD3D with App #2","", () => Program.Config.HelperApp2WineD3D, b => Program.Config.HelperApp2WineD3D = b),
            new SettingsEntry<bool>("Enable App #3","", () => Program.Config.HelperApp3Enabled, b => Program.Config.HelperApp3Enabled = b),
            new SettingsEntry<string>("App #3", "Set a path for an exe file that you want launched with FFXIV. Warning: If it's invalid, the game may hang.", () => Program.Config.HelperApp3, s => Program.Config.HelperApp3 = s)
            {
                CheckWarning = s =>
                {
                    if(!File.Exists(s) && !string.IsNullOrWhiteSpace(s))
                        return "That program doesn't exist.";
                    return null;
                },
            },
            new SettingsEntry<bool>("Use WineD3D with App #3","", () => Program.Config.HelperApp3WineD3D, b => Program.Config.HelperApp3WineD3D = b),
            new SettingsEntry<bool>("Enable App #4","", () => Program.Config.HelperApp4Enabled, b => Program.Config.HelperApp4Enabled = b),
            new SettingsEntry<string>("App #4", "Set a path for an exe file that you want launched with FFXIV. Warning: If it's invalid, the game may hang.", () => Program.Config.HelperApp4, s => Program.Config.HelperApp4 = s)
            {
                CheckWarning = s =>
                {
                    if(!File.Exists(s) && !string.IsNullOrWhiteSpace(s))
                        return "That program doesn't exist.";
                    return null;
                },
            },
            new SettingsEntry<bool>("Use WineD3D with App #4","", () => Program.Config.HelperApp4WineD3D, b => Program.Config.HelperApp4WineD3D = b),
            new SettingsEntry<bool>("Enable App #5","", () => Program.Config.HelperApp5Enabled, b => Program.Config.HelperApp5Enabled = b),
            new SettingsEntry<string>("App #5", "Set a path for an exe file that you want launched with FFXIV. Warning: If it's invalid, the game may hang.", () => Program.Config.HelperApp5, s => Program.Config.HelperApp5 = s)
            {
                CheckWarning = s =>
                {
                    if(!File.Exists(s) && !string.IsNullOrWhiteSpace(s))
                        return "That program doesn't exist.";
                    return null;
                },
            },
            new SettingsEntry<bool>("Use WineD3D with App #5","", () => Program.Config.HelperApp5WineD3D, b => Program.Config.HelperApp5WineD3D = b),
        };
    }

    public override SettingsEntry[] Entries { get; }

    public override bool IsUnixExclusive => true;

    public override string Title => "App Launcher";

public override void Draw()
    {
        ImGuiHelpers.TextWrapped("This tab is for helper applications, such as IINACT or the Discord IPC Bridge.\nThese will be launched right before the game.");

        base.Draw();
    }
}