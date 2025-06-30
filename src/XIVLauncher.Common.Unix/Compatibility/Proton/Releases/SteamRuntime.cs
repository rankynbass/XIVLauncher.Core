namespace XIVLauncher.Common.Unix.Compatibility.Proton.Releases;

public sealed class SteamRuntimeRelease : IToolRelease
{
    public string Label { get; } = "Steam Runtime";
    public string Description { get; } = "Steam Sniper container for running Proton. Recommended.";
    public string Name { get; } = "SteamLinuxRuntime_sniper";
    public string DownloadUrl { get; } = "https://repo.steampowered.com/steamrt3/images/latest-container-runtime-depot/SteamLinuxRuntime_sniper.tar.xz";
    public string Checksum { get; } = "skip";
}