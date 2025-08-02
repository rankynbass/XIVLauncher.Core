using System.Numerics;
using System.Runtime.InteropServices;

using ImGuiNET;

using XIVLauncher.Common.Unix.Compatibility.Dxvk;
using XIVLauncher.Common.Unix.Compatibility.Nvapi;
using XIVLauncher.Common.Unix.Compatibility.Wine;
using XIVLauncher.Common.Util;

namespace XIVLauncher.Core.Components.SettingsPage.Tabs;

public class SettingsTabWine : SettingsTab
{
    private SettingsEntry<RBWineStartupType> startupTypeSetting;

    private WineSettingsEntry wineVersionSetting;

    private SettingsEntry<string> wineCustomBinaryPath;

    private ToolSettingsEntry dxvkVersionSetting;

    private ToolSettingsEntry nvapiVersionSetting;

    private SettingsEntry<bool> protonDxvkSetting;

    private bool isProton => startupTypeSetting.Value == RBWineStartupType.Managed ? Program.WineManager.IsProton(wineVersionSetting.Value) : WineSettings.IsValidProtonBinaryPath(wineCustomBinaryPath.Value);

    public SettingsTabWine()
    {
        Entries = new SettingsEntry[]
        {
            startupTypeSetting = new SettingsEntry<RBWineStartupType>("Wine Install", "Choose how XIVLauncher will start and manage your wine installation.",
                () => Program.Config.RB_WineStartupType ?? RBWineStartupType.Managed, x => Program.Config.RB_WineStartupType = x),

            wineVersionSetting = new WineSettingsEntry("Wine Version", "Choose which Wine version to use.", () => Program.Config.RB_WineVersion ?? Program.WineManager.DEFAULT,
                s => Program.Config.RB_WineVersion = s, Program.WineManager.Version, Program.WineManager.DEFAULT )
            {
                CheckVisibility = () => startupTypeSetting.Value == RBWineStartupType.Managed
            },

            wineCustomBinaryPath = new SettingsEntry<string>("Wine or Proton Binary Path",
                "Set the path XIVLauncher will use to run applications via Wine/Proton.\nIt should be an absolute path to a folder containing wine and/or win64 and wineserver binaries, or the proton binary.",
                () => Program.Config.WineBinaryPath, s => Program.Config.WineBinaryPath = s)
            {
                CheckVisibility = () => startupTypeSetting.Value == RBWineStartupType.Custom,
                CheckValidity = s =>
                {
                    if (WineSettings.IsValidWineBinaryPath(s) || WineSettings.IsValidProtonBinaryPath(s))
                    {
                        return null;
                    }
                    return "Invalid wine or proton path.";
                },
            },

            new SettingsEntry<RBUmuLauncherType>("Umu Launcher", "Use Umu Launcher to run Proton inside the Steam Runtime container (recommended).", () => Program.Config.RB_UmuLauncher ?? RBUmuLauncherType.System, x => Program.Config.RB_UmuLauncher = x)
            {
                CheckVisibility = () => isProton
            },

            dxvkVersionSetting = new ToolSettingsEntry("Dxvk Version", "Choose which Dxvk version to use.", () => Program.Config.RB_DxvkVersion ?? Program.DxvkManager.DEFAULT,
            s => Program.Config.RB_DxvkVersion = s, Program.DxvkManager.Version, Program.DxvkManager.DEFAULT)
            {
                CheckVisibility = () => !isProton
            },

            new SettingsEntry<bool>("Enable DXVK ASYNC", "Enable DXVK ASYNC patch.", () => Program.Config.DxvkAsyncEnabled ?? true, b => Program.Config.DxvkAsyncEnabled = b)
            {
                CheckVisibility = () => dxvkVersionSetting.Value.Contains("async") && !isProton
            },

            new SettingsEntry<bool>("Enable GPLAsync Cache", "Enable GPLASync Cache.", () => Program.Config.RB_GPLAsyncCacheEnabled ?? true, b => Program.Config.RB_GPLAsyncCacheEnabled = b)
            {
                CheckVisibility = () => dxvkVersionSetting.Value.Contains("gplasync") && !isProton
            },

            nvapiVersionSetting = new ToolSettingsEntry("Dxvk-Nvapi Version (Needed for DLSS)", "Choose which version of Dxvk-Nvapi to use. Needs Wine 9.0+ and Dxvk 2.0+", () => Program.Config.RB_NvapiVersion ?? Program.NvapiManager.DEFAULT,
                s => Program.Config.RB_NvapiVersion = s, Program.NvapiManager.Version, Program.NvapiManager.DEFAULT)
            {
                CheckVisibility = () => dxvkVersionSetting.Value != "DISABLED" && !isProton,
            },

            protonDxvkSetting = new SettingsEntry<bool>("Enable Dxvk", "Disable to use WineD3D", () => Program.Config.RB_DxvkEnabled ?? true, b => Program.Config.RB_DxvkEnabled = b)
            {
                CheckVisibility = () => isProton
            },

            new SettingsEntry<bool>("Enable Dxvk-Nvapi (DLSS)", "Requires Dxvk and compatible GPU to work.", () => Program.Config.RB_NvapiEnabled ?? true, b => Program.Config.RB_NvapiEnabled = b)
            {
                CheckVisibility = () => isProton
            },

            new SettingsEntry<bool>("Enable Feral's GameMode", "Enable launching with Feral Interactive's GameMode CPU optimizations.", () => Program.Config.GameModeEnabled ?? true, b => Program.Config.GameModeEnabled = b)
            {
                CheckVisibility = () => RuntimeInformation.IsOSPlatform(OSPlatform.Linux),
                CheckWarning = b =>
                {
                    return (Program.IsGameModeInstalled) ? null : "GameMode was not detected on your system.";
                }
            },

            new SettingsEntry<bool>("Enable ESync", "Enable eventfd-based synchronization.", () => Program.Config.ESyncEnabled ?? true, b => Program.Config.ESyncEnabled = b),
            
            new SettingsEntry<bool>("Enable FSync", "Enable fast user mutex (futex2).", () => Program.Config.FSyncEnabled ?? true, b => Program.Config.FSyncEnabled = b)
            {
                CheckVisibility = () => RuntimeInformation.IsOSPlatform(OSPlatform.Linux),
            },

            new SettingsEntry<bool>("Enable NTSync", "Enable NTSync. Requires a compatible kernel.", () => Program.Config.NTSyncEnabled ?? false, b => Program.Config.NTSyncEnabled = b)
            {
                CheckVisibility = () => RuntimeInformation.IsOSPlatform(OSPlatform.Linux),
            },

            new SettingsEntry<bool>("Enable Wayland Driver", "Enable Wine's experimental Wayland Driver.", () => Program.Config.WaylandEnabled ?? false, b => Program.Config.WaylandEnabled = b)
            {
                CheckVisibility = () => RuntimeInformation.IsOSPlatform(OSPlatform.Linux),
            },

            new SettingsEntry<bool>("Set Windows version to 7", "Default for Wine 8.1+ is Windows 10, but this causes issues with some Dalamud plugins. Windows 7 is recommended for Legacy Wine.", () => Program.Config.SetWin7 ?? false, b => Program.Config.SetWin7 = b),

            new SettingsEntry<string>("WINEDEBUG Variables", "Configure debug logging for wine. Useful for troubleshooting.", () => Program.Config.WineDebugVars ?? string.Empty, s => Program.Config.WineDebugVars = s)
        };
    }

    public override SettingsEntry[] Entries { get; }

    public override bool IsUnixExclusive => true;

    public override string Title => "Wine";

    public override void Draw()
    {
        if (Program.WineManager.IsListUpdated)
        {
            Console.WriteLine("Wine List updated!");
            Program.WineManager.DoneUpdatingWineList();
            wineVersionSetting.Reset(Program.WineManager.Version, Program.WineManager.DEFAULT);
        }

        if (Program.DxvkManager.IsListUpdated)
        {
            Console.WriteLine("Dxvk List updated!");
            Program.DxvkManager.DoneUpdatingDxvkList();
            dxvkVersionSetting.Reset(Program.DxvkManager.Version, Program.DxvkManager.DEFAULT);
        }

        if (Program.NvapiManager.IsListUpdated)
        {
            Console.WriteLine("Nvapi List updated!");
            Program.NvapiManager.DoneUpdatingNvapiList();
            nvapiVersionSetting.Reset(Program.NvapiManager.Version, Program.NvapiManager.DEFAULT);
        }
        base.Draw();

        if (!Program.CompatibilityTools.IsToolDownloaded)
        {
            ImGui.BeginDisabled();
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
            Program.CompatibilityTools.RunInPrefix("explorer");
        }

        if (ImGui.Button("Kill all wine processes"))
        {
            Program.CompatibilityTools.Kill();
        }

        if (!Program.CompatibilityTools.IsToolDownloaded)
        {
            ImGui.EndDisabled();
        }
    }

    public override void Save()
    {
        base.Save();
        Program.CreateCompatToolsInstance();
    }
}
