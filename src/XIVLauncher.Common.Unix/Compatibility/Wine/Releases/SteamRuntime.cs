using System.IO;

namespace XIVLauncher.Common.Unix.Compatibility.Wine.Releases;

public sealed class SteamRuntimeRelease(string commonFolder) : IToolRelease
{
    public string Label { get; } = "Steam Runtime";
    public string Description { get; } = "Steam Sniper container for running Proton. Recommended.";
    public string Name { get; } = Path.Combine(commonFolder, "SteamLinuxRuntime_sniper");
    public string DownloadUrl { get; } = "https://repo.steampowered.com/steamrt3/images/latest-public-stable/SteamLinuxRuntime_sniper.tar.xz";
    public string Checksum { get; } = "skip";
}