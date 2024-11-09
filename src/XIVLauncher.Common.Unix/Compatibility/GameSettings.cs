using System.IO;
using System.Linq;
using System.Collections.Generic;


namespace XIVLauncher.Common.Unix.Compatibility;

public class GameSettings
{
    public bool GameModeEnabled { get; }

    public DirectoryInfo ToolsFolder { get; }

    public DirectoryInfo GameFolder { get; }

    public DirectoryInfo ConfigFolder { get; }

    public DirectoryInfo SteamFolder { get; }

    public bool IsFlatpak { get; }

    public Dictionary<string, string> Environment { get; }

    public GameSettings(bool? gamemodeOn, DirectoryInfo toolsFolder, DirectoryInfo steamFolder, DirectoryInfo gamePath, DirectoryInfo gameConfigPath, bool isFlatpak, Dictionary<string, string> extraEnvVars = null)
    {
        GameModeEnabled = gamemodeOn ?? false;
        ToolsFolder = toolsFolder;
        SteamFolder = steamFolder;
        GameFolder = gamePath;
        ConfigFolder = gameConfigPath;
        IsFlatpak = isFlatpak;
        Environment = extraEnvVars ?? new Dictionary<string, string>();
    }
}