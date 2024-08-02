using System.Numerics;
using System.Runtime.InteropServices;

using ImGuiNET;

using XIVLauncher.Common.Unix.Compatibility;
using XIVLauncher.Common.Util;
using XIVLauncher.Core.UnixCompatibility;

namespace XIVLauncher.Core.Components.SettingsPage.Tabs;

public class SettingsTabWine : SettingsTab
{
    private SettingsEntry<WineType> wineTypeSetting;

    private DictionarySettingsEntry wineVersionSetting;

    private DictionarySettingsEntry protonVersionSetting;

    private readonly string toolDirectory = Path.Combine(Program.storage.Root.FullName, "compatibilitytool", "wine");

    public SettingsTabWine()
    {
        Entries = new SettingsEntry[]
        {
            wineTypeSetting = new SettingsEntry<WineType>("Installation Type", "Choose how XIVLauncher will start and manage your game installation.",
                () => Program.Config.WineType ?? WineType.Managed, x => Program.Config.WineType = x)
            {
                // CheckWarning = x =>
                // {
                //     if (x == WineType.Proton && !)
                //     {
                //         return "Umu-launcher is not installed. You have to install it yourself. See https://github.com/Open-Wine-Components/umu-launcher for more info.";
                //     }
                //     return null;
                // }
            },

            wineVersionSetting = new DictionarySettingsEntry("Wine Version", $"Wine versions in {toolDirectory}\nEntries marked with *Download* will be downloaded when you log in.", Wine.Versions, () => Program.Config.WineVersion, s => Program.Config.WineVersion = s, Wine.GetDefaultVersion())
            {
                CheckVisibility = () => wineTypeSetting.Value == WineType.Managed
            },
            
            new SettingsEntry<string>("Wine Binary Path",
                "Set the path XIVLauncher will use to run applications via wine.\nIt should be an absolute path to a folder containing wine64 and wineserver binaries.",
                () => Program.Config.WineBinaryPath, s => Program.Config.WineBinaryPath = s)
            {
                CheckVisibility = () => wineTypeSetting.Value == WineType.Custom
            },

            protonVersionSetting = new DictionarySettingsEntry("Proton Version", "The Wine configuration and Wine explorer buttons below may not function properly with Proton.", Proton.Versions, () => Program.Config.ProtonVersion, s => Program.Config.ProtonVersion = s, Proton.GetDefaultVersion())
            {
                CheckVisibility = () => wineTypeSetting.Value == WineType.Proton,
                // CheckWarning = x =>
                // {
                //     if (wineTypeSetting.Value == WineType.Proton)
                //         return "Proton is designed to be run in a container. You may have issues if you use it without Umu-launcher.";
                //     return null;
                // }
            },

            new DictionarySettingsEntry("Runtime Version", "Sniper runtime is recommeded for use with Proton.", Runtime.Versions, () => Program.Config.RuntimeVersion, s => Program.Config.RuntimeVersion = s, Runtime.GetDefaultVersion())
            {
                CheckVisibility = () => wineTypeSetting.Value == WineType.Proton,
                // CheckWarning = x =>
                // {
                //     if (wineTypeSetting.Value == WineType.Proton)
                //         return "Proton is designed to be run in a container. You may have issues if you use it without Umu-launcher.";
                //     return null;
                // }
            },

            new SettingsEntry<bool>("Enable Feral's GameMode", "Enable launching with Feral Interactive's GameMode CPU optimizations.", () => Program.Config.GameModeEnabled ?? true, b => Program.Config.GameModeEnabled = b)
            {
                CheckVisibility = () => RuntimeInformation.IsOSPlatform(OSPlatform.Linux),
                CheckValidity = b =>
                {
                    if (b == true && !CoreEnvironmentSettings.IsGameModeInstalled())
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

            new SettingsEntry<bool>("Enable Wayland", "Requires compatible wine build.If \"Enable Wayland Driver\" button is available below, you MUST press it.\n The UI may freeze for a few seconds, please be patient.", () => Program.Config.WaylandEnabled ?? false, b => Program.Config.WaylandEnabled = b),

            new NumericSettingsEntry("Wayland Desktop Scaling", "Set this equal to your desktop scaling. Needed for Wine Wayland driver.\nUse the \"Update Wine Scaling\" button below to change this.", () => Program.Config.WineScale ?? 100, i => Program.Config.WineScale = (i > 400 || i < 100 || i % 25 !=0) ? 100 : i, 100, 400, 25),

            new SettingsEntry<string>("Wine DLL Overrides", "Add extra WINEDLLOVERRIDES. No spaces, semicolon separated. Do not use msquic, mscoree, d3d11, dxgi. These are already set.", () => Program.Config.WineDLLOverrides ?? "", s => Program.Config.WineDLLOverrides = s)
            {
                CheckValidity = s =>
                {
                    if (String.IsNullOrEmpty(s)) return null;
                    if (s.Contains(' ')) return "Invalid! No spaces allowed!";
                    if (s.Contains("d3d11") || s.Contains("dxgi") || s.Contains("mscoree") || s.Contains("msquic")) return "Invalid! msquic, mscoree, d3d11, and/or dxgi alread set.";

                    return null;
                },
            },

            new SettingsEntry<string>("WINEDEBUG Variables", "Configure debug logging for wine. Useful for troubleshooting.", () => Program.Config.WineDebugVars ?? string.Empty, s => Program.Config.WineDebugVars = s),
        };
    }

    public override SettingsEntry[] Entries { get; }

    public override bool IsUnixExclusive => true;

    public override string Title => "Wine";

    public override void Draw()
    {
        base.Draw();

        ImGui.Separator();

        ImGui.Dummy(SPACER);

        if (wineTypeSetting.Value == WineType.Managed)
        {
            if (Wine.Versions[wineVersionSetting.Value].ContainsKey("mark"))
            {
                ImGui.BeginDisabled();
                ImGui.Text("Compatibility tool isn't set up. Please start the game at least once.");

                ImGui.Dummy(SPACER);
            }
        }
        else if (wineTypeSetting.Value == WineType.Proton)
        {
            if (Proton.Versions[protonVersionSetting.Value].ContainsKey("mark"))
            {
                ImGui.BeginDisabled();
                ImGui.Text("Compatibility tool isn't set up. Please start the game at least once.");

                ImGui.Dummy(SPACER);
            }
        }

        if (!File.Exists(Path.Combine(ToolSetup.Prefix.FullName, "wayland_driver")))
        {
            if (ImGui.Button("Enable Wayland Driver"))
            {
                this.Save();
                Program.CompatibilityTools.AddRegistryKey("HKEY_CURRENT_USER\\Software\\Wine\\Drivers", "Graphics", "x11,wayland");
                Program.CompatibilityTools.RunInPrefix($"reg add \"HKEY_LOCAL_MACHINE\\System\\CurrentControlSet\\Hardware Profiles\\Current\\Software\\Fonts\" /v LogPixels /t REG_DWORD /d {ToolSetup.Dpi} /f").WaitForExit();
                var startTime = DateTime.UtcNow;
                while (!File.Exists(Path.Combine(ToolSetup.Prefix.FullName, "system.reg")) && !File.Exists(Path.Combine(ToolSetup.Prefix.FullName, "user.reg")) || !File.Exists(Path.Combine(ToolSetup.Prefix.FullName, "userdef.reg")))
                {
                    if (DateTime.UtcNow - startTime > TimeSpan.FromSeconds(10))
                        break;
                }
                File.Create(Path.Combine(ToolSetup.Prefix.FullName, "wayland_driver"));
            }
            ImGui.SameLine();
        }

        if (ImGui.Button("Update Wine Scaling"))
        {
            this.Save();
            Program.CompatibilityTools.RunInPrefix($"reg add \"HKEY_LOCAL_MACHINE\\System\\CurrentControlSet\\Hardware Profiles\\Current\\Software\\Fonts\" /v LogPixels /t REG_DWORD /d {ToolSetup.Dpi} /f").WaitForExit();
        }

        ImGui.Dummy(SPACER);

        if (ImGui.Button("Open prefix"))
        {
            PlatformHelpers.OpenBrowser(Program.CompatibilityTools.Settings.Prefix.FullName);
        }

        ImGui.SameLine();

        if (ImGui.Button("Open Wine configuration"))
        {
            this.Save();
            Program.CompatibilityTools.RunExternalProgram("winecfg");
        }

        ImGui.SameLine();

        if (ImGui.Button("Open Wine explorer (run apps in prefix)"))
        {
            Program.CompatibilityTools.RunExternalProgram("explorer");
        }

        ImGui.SameLine();

        if (ImGui.Button("Open Wine explorer (use WineD3D"))
        {
            Program.CompatibilityTools.RunExternalProgram("explorer", wineD3D: true);

        }

        ImGui.Dummy(SPACER);


        if (ImGui.Button("Set Wine to Windows 7"))
        {
            Program.CompatibilityTools.RunExternalProgram($"winecfg /v win7", redirectOutput: true, writeLog: true);
        }

        ImGui.SameLine();

        if (ImGui.Button("Set Wine to Windows 10"))
        {
            Program.CompatibilityTools.RunExternalProgram($"winecfg /v win10", redirectOutput: true, writeLog: true);
        }

        ImGui.Dummy(SPACER);

        if (ImGui.Button("Kill all wine processes"))
        {
            Program.CompatibilityTools.Kill();
        }

        if (wineTypeSetting.Value == WineType.Managed)
        {
            if (Wine.Versions[wineVersionSetting.Value].ContainsKey("mark"))
            {
                ImGui.EndDisabled();
            }

            if (Wine.Versions[wineVersionSetting.Value].ContainsKey("mark"))
            {
                ImGui.SameLine();

                if (ImGui.Button($"{Wine.Versions[wineVersionSetting.Value]["mark"]} now!"))
                {
                    Wine.Versions[wineVersionSetting.Value]["mark"] = "Downloading";
                    this.Save();
                    Task.Run(async () => await Program.CompatibilityTools.DownloadWine().ConfigureAwait(false))
                        .ContinueWith(t => 
                        {
                            Wine.Versions[wineVersionSetting.Value].Remove("mark");
                            Wine.Initialize();
                        });
                }
            }
        }
        else if (wineTypeSetting.Value == WineType.Proton)
        {
            if (Proton.Versions[protonVersionSetting.Value].ContainsKey("mark"))
            {
                ImGui.EndDisabled();
            }

            if (Proton.Versions[protonVersionSetting.Value].ContainsKey("mark"))
            {
                ImGui.SameLine();

                if (ImGui.Button($"{Proton.Versions[protonVersionSetting.Value]["mark"]} now!"))
                {
                    Proton.Versions[protonVersionSetting.Value]["mark"] = "Downloading";
                    this.Save();
                    Task.Run(async () => await Program.CompatibilityTools.DownloadProton().ConfigureAwait(false))
                        .ContinueWith(t => 
                        {
                            Proton.Versions[protonVersionSetting.Value].Remove("mark");
                            Proton.Initialize();
                        });
                }
            }
        }

        ImGui.Dummy(SPACER);

        if (Program.IsReshadeEnabled() is not null)
        {
            ImGui.Text($"Reshade is {(Program.IsReshadeEnabled().Value ? "ENABLED" : "DISABLED")}");
        
            if (ImGui.Button("Toggle Reshade"))
            {
                Program.ToggleReshade();
            }
        }
        else
            ImGui.Text($"Reshade is not installed");
    }

    public override void Save()
    {
        base.Save();
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            Program.CreateCompatToolsInstance();
    }
}
