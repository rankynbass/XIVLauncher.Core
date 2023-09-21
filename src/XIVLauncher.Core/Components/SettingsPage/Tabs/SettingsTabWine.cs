using System.Numerics;
using System.Runtime.InteropServices;
using ImGuiNET;
using XIVLauncher.Common.Util;
using XIVLauncher.Core.UnixCompatibility;

namespace XIVLauncher.Core.Components.SettingsPage.Tabs;

public class SettingsTabWine : SettingsTab
{
    private SettingsEntry<WineType> wineTypeSetting;

    private readonly string toolDirectory = Path.Combine(Program.storage.Root.FullName, "compatibilitytool", "wine");

    public SettingsTabWine()
    {
        Entries = new SettingsEntry[]
        {
            wineTypeSetting = new SettingsEntry<WineType>("Installation Type", "Choose how XIVLauncher will start and manage your game installation.",
                () => Program.Config.WineType ?? WineType.Managed, x => Program.Config.WineType = x)
            {
                CheckValidity = x =>
                {
                    if (x == WineType.Proton && !Proton.IsValid())
                    {
                        var userHome = System.Environment.GetEnvironmentVariable("HOME") ?? "/home/username";
                        return $"No proton version found! Check launcher.ini and make sure that SteamPath points your Steam root\nUsually this is {userHome}/.steam/root or {userHome}/.local/share/Steam";
                    }
                    return null;
                }
            },

            new DictionarySettingsEntry("Wine Version", $"Wine versions in {toolDirectory}\nEntries marked with *Download* will be downloaded when you log in.", Wine.Versions, () => Program.Config.WineVersion, s => Program.Config.WineVersion = s, Wine.GetDefaultVersion())
            {
                CheckVisibility = () => wineTypeSetting.Value == WineType.Managed
            },
            
            new SettingsEntry<string>("Wine Binary Path",
                "Set the path XIVLauncher will use to run applications via wine.\nIt should be an absolute path to a folder containing wine64 and wineserver binaries.",
                () => Program.Config.WineBinaryPath, s => Program.Config.WineBinaryPath = s)
            {
                CheckVisibility = () => wineTypeSetting.Value == WineType.Custom
            },

            new SettingsEntry<string>("Steam Path", "Set the location of your steam folder (requires restart)", () => Program.Config.SteamPath, s => Program.Config.SteamPath = s)
            {
                CheckVisibility = () => wineTypeSetting.Value == WineType.Proton,
            },

            new DictionarySettingsEntry("Proton Version", "The Wine configuration and Wine explorer buttons below may not function properly with Proton.", Proton.Versions, () => Program.Config.ProtonVersion, s => Program.Config.ProtonVersion = s, Proton.GetDefaultVersion())
            {
                CheckVisibility = () => wineTypeSetting.Value == WineType.Proton,
            },

            new DictionarySettingsEntry("Steam Container Runtime", "Use Steam's container system. Proton is designed with this in mind, but may run without it. Slow to launch.", Proton.Runtimes, () => Program.Config.SteamRuntime, s => Program.Config.SteamRuntime = s, Proton.GetDefaultRuntime())
            {
                CheckVisibility = () => wineTypeSetting.Value == WineType.Proton && !OSInfo.IsFlatpak,
            },

            new SettingsEntry<bool>("Enable Feral's GameMode", "Enable launching with Feral Interactive's GameMode CPU optimizations.", () => Program.Config.GameModeEnabled ?? true, b => Program.Config.GameModeEnabled = b)
            {
                CheckVisibility = () => RuntimeInformation.IsOSPlatform(OSPlatform.Linux),
                CheckValidity = b =>
                {
                    var handle = IntPtr.Zero;
                    if (b == true && !NativeLibrary.TryLoad("libgamemodeauto.so.0", out handle))
                        return "GameMode was not detected on your system.";
                    NativeLibrary.Free(handle);
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

            new SettingsEntry<string>("WINEDEBUG Variables", "Configure debug logging for wine. Useful for troubleshooting.", () => Program.Config.WineDebugVars ?? string.Empty, s => Program.Config.WineDebugVars = s)
        };
    }

    public override SettingsEntry[] Entries { get; }

    public override bool IsUnixExclusive => true;

    public override string Title => "Wine";

    public override void Draw()
    {
        base.Draw();

        ImGui.Separator();

        ImGui.Dummy(new Vector2(10) * ImGuiHelpers.GlobalScale);

        if (!Program.CompatibilityTools.IsToolDownloaded)
        {
            ImGui.BeginDisabled();
            ImGui.Text("Compatibility tool isn't set up. Please start the game at least once.");

            ImGui.Dummy(new Vector2(10) * ImGuiHelpers.GlobalScale);
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

        ImGui.SameLine();

        if (ImGui.Button("Open Wine explorer (use WineD3D"))
        {
            Program.CompatibilityTools.RunInPrefix("explorer", wineD3D: true);

        }

        ImGui.Dummy(new Vector2(10) * ImGuiHelpers.GlobalScale);


        if (ImGui.Button("Set Wine to Windows 7"))
        {
            Program.CompatibilityTools.RunInPrefix($"winecfg /v win7", redirectOutput: true, writeLog: true);
        }

        ImGui.SameLine();

        if (ImGui.Button("Set Wine to Windows 10"))
        {
            Program.CompatibilityTools.RunInPrefix($"winecfg /v win10", redirectOutput: true, writeLog: true);
        }

        ImGui.Dummy(new Vector2(10) * ImGuiHelpers.GlobalScale);

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
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            Program.CreateCompatToolsInstance();
    }
}
