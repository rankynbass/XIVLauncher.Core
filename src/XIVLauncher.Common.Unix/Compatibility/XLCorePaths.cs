using System.IO;

namespace XIVLauncher.Common.Unix.Compatibility.Wine;

public class XLCorePaths( DirectoryInfo prefix, DirectoryInfo tools, DirectoryInfo game, DirectoryInfo config, DirectoryInfo steam)
{
    public DirectoryInfo Prefix { get; } = prefix;

    public DirectoryInfo ToolsFolder { get; } = tools;

    public DirectoryInfo GameFolder { get; } = game;

    public DirectoryInfo ConfigFolder { get; } = config;

    public DirectoryInfo SteamFolder { get; } = steam;
}