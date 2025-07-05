using System.Numerics;
using System.Runtime.InteropServices;

using ImGuiNET;

using XIVLauncher.Common.Unix.Compatibility.Dxvk;
using XIVLauncher.Common.Util;

namespace XIVLauncher.Core.Components.SettingsPage.Tabs;

public class SettingsTabHud : SettingsTab
{
    public SettingsTabHud()
    {
        Entries = new SettingsEntry[]
        {
            new SettingsEntry<RBHudType>("DXVK Overlay", "Configure how much of the DXVK overlay is to be shown.", () => Program.Config.RB_HudType ?? RBHudType.None, type => Program.Config.RB_HudType = type),

            new SettingsEntry<string>("DXVK Hud Custom String", "Custom string for Dxvk Hud", () => Program.Config.RB_DxvkHudCustom ?? "1", s => Program.Config.RB_DxvkHudCustom = s)
            {
                CheckValidity = s =>
                {
                    if (!Dxvk.IsDxvkHudStringValid(s))
                        return "Dxvk string is invalid!";
                    return null;
                }
            },

            new SettingsEntry<string>("MangoHud Custom String", "Custom string for MangoHud", () => Program.Config.RB_MangoHudCustomString ?? "", s => Program.Config.RB_MangoHudCustomString = s)
            {
                CheckWarning = s =>
                {
                    if (!Dxvk.IsMangoHudInstalled)
                        return "Could not find MangoHud! This setting may not work.";
                    return null;
                }
            },

            new SettingsEntry<string>("MangoHud Custom File", "Custom config file for MangoHud", () => Program.Config.RB_MangoHudCustomFile ?? "", s => Program.Config.RB_MangoHudCustomFile = s)
            {
                CheckWarning = s =>
                {
                    if (!Dxvk.IsMangoHudInstalled)
                        return "Could not find MangoHud! This setting may not work.";
                    return null;
                },
                CheckValidity = s =>
                {                   
                    if (!File.Exists(s))
                        return "Could not find config file! This setting will not work.";
                    return null;
                }
            },

            new SettingsEntry<string>("WINEDEBUG Variables", "Configure debug logging for wine. Useful for troubleshooting.", () => Program.Config.WineDebugVars ?? string.Empty, s => Program.Config.WineDebugVars = s)
        };
    }

    public override SettingsEntry[] Entries { get; }

    public override bool IsUnixExclusive => true;

    public override string Title => "Overlay";

    public override void Draw()
    {
        base.Draw();
    }

    public override void Save()
    {
        base.Save();
        Program.CreateCompatToolsInstance();
    }
}
