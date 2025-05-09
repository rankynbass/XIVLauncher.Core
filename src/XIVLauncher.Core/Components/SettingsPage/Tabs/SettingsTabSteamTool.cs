﻿using System.Numerics;
using System.Runtime.InteropServices;
using System.IO;
using ImGuiNET;
using XIVLauncher.Common.Unix.Compatibility;
using XIVLauncher.Common.Util;
using XIVLauncher.Core.UnixCompatibility;

namespace XIVLauncher.Core.Components.SettingsPage.Tabs;

public class SettingsTabSteamTool : SettingsTab
{
    private SettingsEntry<string> steamPath;
    
    private SettingsEntry<string> steamFlatpakPath;

    private SettingsEntry<string> steamSnapPath;

    private bool steamInstalled = SteamCompatibilityTool.IsSteamInstalled;

    private bool steamToolInstalled = SteamCompatibilityTool.IsSteamToolInstalled;
    
    private bool steamFlatpakInstalled = SteamCompatibilityTool.IsSteamFlatpakInstalled;
    
    private bool steamFlatpakToolInstalled = SteamCompatibilityTool.IsSteamFlatpakToolInstalled;

    private bool steamSnapInstalled = SteamCompatibilityTool.IsSteamSnapInstalled;

    private bool steamSnapToolInstalled = SteamCompatibilityTool.IsSteamSnapToolInstalled;

    public SettingsTabSteamTool()
    {
        Entries = new SettingsEntry[]
        {
            steamPath = new SettingsEntry<string>("Steam Path (native)", "Path to the native steam user folder. Only change this if you have your Steam user folder set to a non-default location.",
                () => Program.Config.SteamPath ?? Path.Combine(CoreEnvironmentSettings.HOME, ".local", "share", "Steam"), s => Program.Config.SteamPath = s),
            steamFlatpakPath = new SettingsEntry<string>("Steam Path (flatpak)", "Path to the flatpak Steam user folder. Only change this if you have your snap Steam user folder set to a non-default location.",
                () => Program.Config.SteamFlatpakPath ?? Path.Combine(CoreEnvironmentSettings.HOME, ".var", "app", "com.valvesoftware.Steam", ".local", "share", "Steam" ), s => Program.Config.SteamFlatpakPath = s)
            {    
                CheckVisibility = () => Program.IsSteamDeckHardware != true,
            },
            steamSnapPath = new SettingsEntry<string>("Steam Path (snap)", "Path to the snap Steam user folder. Only change this if you have your snap Steam user folder set to a non-default location.",
                () => Program.Config.SteamSnapPath ?? Path.Combine(CoreEnvironmentSettings.HOME, "snap", "steam", "common", ".local", "share", "Steam"), s => Program.Config.SteamSnapPath = s)
            {    
                CheckVisibility = () => Program.IsSteamDeckHardware != true,
            },
        };
    }

    public override SettingsEntry[] Entries { get; }

    public override bool IsUnixExclusive => true;

    public override string Title => "Steam Tool (XLM)";

    public override void Draw()
    {
        if (CoreEnvironmentSettings.IsSteamCompatTool)
        {
            ImGui.Dummy(SPACER);
            ImGui.Text("You are currently running XIVLauncher.Core as a Steam compatibility tool.");
            return;
        }
        ImGui.Text("\nUse this tab to install XIVLauncher.Core as a Steam compatibility tool.");
        ImGui.Text("\nAfter you have installed XIVLauncher as a Steam compatibility tool please close XIVLauncher and launch or restart Steam. Find 'Final Fantasy XIV Online'");
        ImGui.Text("in your steam library and open the 'Properties' menu and navigate to the 'Compatibility' tab. Enable 'Force the use of a specific Steam Play compatibility tool'");
        ImGui.Text("and from the dropdown menu select 'XLCore [XLM]'. If this option does not show up then restart Steam and try again. After finishing these steps,");
        ImGui.Text("XIVLauncher will be used when launching FINAL FANTASY XIV from Steam.");
        ImGui.Text("\nIf you wish to use file-based password storage, you will need to set the Launch Options to XL_SECRET_PROVIDER=FILE %%command%%.\nThis should be automatically done for Steam Deck users.");
        // Steam deck should never have flatpak steam
        if (Program.IsSteamDeckHardware != true)
        {
            ImGui.Text("\nIf you wish to install into Flatpak Steam, you must use Flatseal to give XIVLauncher access to Steam's flatpak path. This is commonly found at:");
            ImGui.Text($"~/.var/app/com.valvesoftware.Steam. If you do not give this permission, the install option will not even appear. You will also need to give Steam");
            ImGui.Text($"access to ~/.xlcore, so that you can continue to use your current xlcore folder.");
            ImGui.Text("\nDO NOT use native XIVLauncher to install to flatpak Steam. Use flatpak XIVLauncher instead.");
        }

        ImGui.Dummy(SPACER);        
        ImGui.Separator();
        ImGui.Dummy(SPACER);

        ImGui.Text($"Steam settings directory: {(steamInstalled ? "PRESENT" : "Not Present")}. Native Steam Tool: {(steamToolInstalled ? "INSTALLED" : "Not Installed")}.");
        ImGui.Dummy(SPACER);
        if (!steamInstalled) ImGui.BeginDisabled();
        if (ImGui.Button($"{(steamToolInstalled ? "Re-i" : "I")}nstall to native Steam"))
        {
            this.Save();
            Task.Run(async() => { await SteamCompatibilityTool.InstallXLM(Program.Config.SteamPath).ConfigureAwait(false); steamToolInstalled = SteamCompatibilityTool.IsSteamToolInstalled; } );
            
        }
        if (!steamInstalled) ImGui.EndDisabled();
        ImGui.SameLine();
        if (!steamToolInstalled) ImGui.BeginDisabled();
        if (ImGui.Button("Uninstall from native Steam"))
        {
            this.Save();
            SteamCompatibilityTool.UninstallXLM(Program.Config.SteamPath);
            steamToolInstalled = SteamCompatibilityTool.IsSteamToolInstalled;
        }
        if (!steamToolInstalled) ImGui.EndDisabled();

        if (!Program.IsSteamDeckHardware && steamFlatpakInstalled)
        {
            ImGui.Dummy(SPACER);
            ImGui.Separator();
            ImGui.Dummy(SPACER);

            ImGui.Text($"Flatpak Steam settings directory: PRESENT. Flatpak Steam Tool: {(steamFlatpakToolInstalled ? "INSTALLED" : "Not Installed")}");
            ImGui.Dummy(SPACER);
            if (!steamFlatpakInstalled) ImGui.BeginDisabled();
            if (ImGui.Button($"{(steamFlatpakToolInstalled ? "Re-i" : "I")}nstall to flatpak Steam"))
            {
                this.Save();
                Task.Run(async() => { await SteamCompatibilityTool.InstallXLM(Program.Config.SteamFlatpakPath).ConfigureAwait(false); steamFlatpakToolInstalled = SteamCompatibilityTool.IsSteamFlatpakToolInstalled; });
            }
            if (!steamFlatpakInstalled) ImGui.EndDisabled();
            ImGui.SameLine();
            if (!steamFlatpakToolInstalled)
            {
                ImGui.BeginDisabled();
            }
            if (ImGui.Button("Uninstall from Flatpak Steam"))
            {
                this.Save();
                SteamCompatibilityTool.UninstallXLM(Program.Config.SteamFlatpakPath);
                steamFlatpakToolInstalled = SteamCompatibilityTool.IsSteamFlatpakToolInstalled;
            }
            if (!steamFlatpakToolInstalled)
            {
                ImGui.EndDisabled();
            }
        }

        if (!Program.IsSteamDeckHardware && steamSnapInstalled)
        {
            ImGui.Dummy(SPACER);
            ImGui.Separator();
            ImGui.Dummy(SPACER);

            ImGui.Text($"Snap Steam settings directory: PRESENT. Snap Steam Tool: {(steamSnapToolInstalled ? "INSTALLED" : "Not Installed")}");
            ImGui.Dummy(SPACER);
            if (!steamSnapInstalled) ImGui.BeginDisabled();
            if (ImGui.Button($"{(steamSnapToolInstalled ? "Re-i" : "I")}nstall to snap Steam"))
            {
                this.Save();
                Task.Run(async() => { await SteamCompatibilityTool.InstallXLM(Program.Config.SteamSnapPath).ConfigureAwait(false); steamSnapToolInstalled = SteamCompatibilityTool.IsSteamSnapToolInstalled; });
            }
            if (!steamSnapInstalled) ImGui.EndDisabled();
            ImGui.SameLine();
            if (!steamSnapToolInstalled)
            {
                ImGui.BeginDisabled();
            }
            if (ImGui.Button("Uninstall from Snap Steam"))
            {
                this.Save();
                SteamCompatibilityTool.UninstallXLM(Program.Config.SteamSnapPath);
                steamSnapToolInstalled = SteamCompatibilityTool.IsSteamSnapToolInstalled;
            }
            if (!steamSnapToolInstalled)
            {
                ImGui.EndDisabled();
            }
        }

        ImGui.Dummy(SPACER);
        ImGui.Separator();
        ImGui.Dummy(SPACER);

        base.Draw();
    }

    public override void Save()
    {
        base.Save();
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            Program.CreateCompatToolsInstance();
    }
}
