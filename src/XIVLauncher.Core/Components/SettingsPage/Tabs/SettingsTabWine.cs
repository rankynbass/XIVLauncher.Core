using System.Numerics;
using System.Runtime.InteropServices;
using ImGuiNET;
using XIVLauncher.Common.Unix.Compatibility;
using XIVLauncher.Common.Util;

namespace XIVLauncher.Core.Components.SettingsPage.Tabs;

public class SettingsTabWine : SettingsTab
{
    private SettingsEntry<WineStartupType> startupTypeSetting;

    public SettingsTabWine()
    {
        Entries = new SettingsEntry[]
        {
            startupTypeSetting = new SettingsEntry<WineStartupType>("Wine Version", "Choose how XIVLauncher will start and manage your wine installation.",
                () => Program.Config.WineStartupType ?? WineStartupType.Managed, x => Program.Config.WineStartupType = x),

            new SettingsEntry<string>("Wine Binary Path",
                "Set the path XIVLauncher will use to run applications via wine.\nIt should be an absolute path to a folder containing wine64 and wineserver binaries.",
                () => Program.Config.WineBinaryPath, s => Program.Config.WineBinaryPath = s)
            {
                CheckVisibility = () => startupTypeSetting.Value == WineStartupType.Custom
            },
            new DictionarySettingsEntry("Proton Version", "", ProtonManager.Versions, () => Program.Config.ProtonVersion, s => Program.Config.ProtonVersion = s, ProtonManager.GetDefaultVersion())
            {
                CheckVisibility = () => startupTypeSetting.Value == WineStartupType.Proton,
            },
#if !FLATPAK
            new DictionarySettingsEntry("Steam Container Runtime", "Use Steam's container system. Proton is designed with this in mind, but may run without it.", ProtonManager.Runtimes, () => Program.Config.SteamRuntime, s => Program.Config.SteamRuntime = s, ProtonManager.GetDefaultRuntime())
            {
                CheckVisibility = () => startupTypeSetting.Value == WineStartupType.Proton,
            },
#endif
            new SettingsEntry<bool>("Enable Feral's GameMode", "Enable launching with Feral Interactive's GameMode CPU optimizations.", () => Program.Config.GameModeEnabled ?? true, b => Program.Config.GameModeEnabled = b)
            {
                CheckVisibility = () => RuntimeInformation.IsOSPlatform(OSPlatform.Linux),
                CheckValidity = b =>
                {
                    if (b == true && (!File.Exists("/usr/lib/libgamemodeauto.so.0") && !File.Exists("/app/lib/libgamemodeauto.so.0")))
                        return "GameMode was not detected on your system.";

                    return null;
                }
            },

            new SettingsEntry<bool>("Enable ESync", "Enable eventfd-based synchronization.", () => Program.Config.ESyncEnabled ?? true, b => Program.Config.ESyncEnabled = b),
            new SettingsEntry<bool>("Enable FSync", "Enable fast user mutex (futex2).", () => Program.Config.FSyncEnabled ?? true, b => Program.Config.FSyncEnabled = b)
            {
                CheckVisibility = () => RuntimeInformation.IsOSPlatform(OSPlatform.Linux),
                CheckValidity = b =>
                {
                    if (b == true && (Environment.OSVersion.Version.Major < 5 && (Environment.OSVersion.Version.Minor < 16 || Environment.OSVersion.Version.Major < 6)))
                        return "Linux kernel 5.16 or higher is required for FSync.";

                    return null;
                }
            },

            new SettingsEntry<bool>("Set Windows version to 7", "Default for Wine 8.1+ is Windows 10, but this causes issues with some Dalamud plugins. Windows 7 is recommended for now.", () => Program.Config.SetWin7 ?? true, b => Program.Config.SetWin7 = b),

            new SettingsEntry<Dxvk.DxvkHudType>("DXVK Overlay", "Configure how much of the DXVK overlay is to be shown.", () => Program.Config.DxvkHudType, type => Program.Config.DxvkHudType = type),
            new SettingsEntry<string>("WINEDEBUG Variables", "Configure debug logging for wine. Useful for troubleshooting.", () => Program.Config.WineDebugVars ?? string.Empty, s => Program.Config.WineDebugVars = s)
        };
    }

    public override SettingsEntry[] Entries { get; }

    public override bool IsUnixExclusive => true;

    public override string Title => "Wine";

    public override void Draw()
    {
        base.Draw();

        if (!Program.CompatibilityTools.IsToolDownloaded || startupTypeSetting.Value == WineStartupType.Proton)
        {
            ImGui.BeginDisabled();
            if (startupTypeSetting.Value == WineStartupType.Proton)
                ImGui.Text("These options do not work properly with Proton yet.");
            else
                ImGui.Text("Compatibility tool isn't set up. Please start the game at least once.");

            ImGui.Dummy(new Vector2(10));
        }

        if (ImGui.Button("Open prefix"))
        {
            PlatformHelpers.OpenBrowser(Program.CompatibilityTools.Settings.Prefix.FullName);
        }

        ImGui.SameLine();

        if (ImGui.Button("Open Wine configuration"))
        {
            Program.CompatibilityTools.RunInPrefix("winecfg");
        }

        ImGui.SameLine();

        if (ImGui.Button("Open Wine explorer (run apps in prefix)"))
        {
            Program.CompatibilityTools.RunInPrefix("explorer", wineD3D: true, inject: true);
        }

        ImGui.SameLine();

        if (ImGui.Button("Open Wine explorer (use WineD3D)"))
        {
            Program.CompatibilityTools.RunInPrefix("explorer", "", null, false, false, true);
        }

        if (ImGui.Button("Kill all wine processes"))
        {
            Program.CompatibilityTools.Kill();
        }

        if (!Program.CompatibilityTools.IsToolDownloaded || startupTypeSetting.Value == WineStartupType.Proton)
        {
            ImGui.EndDisabled();
        }
    }

    public override void Save()
    {
        base.Save();
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            Program.CreateCompatToolsInstance();
    }
}
