using XIVLauncher.Common.Unix.Compatibility;

namespace XIVLauncher.Common.Unix.Compatibility.Nvapi.Releases;

public sealed class NvapiLegacyRelease054 : IToolRelease
{
    public string Folder { get; } = "dxvk-nvapi-v0.5.4";
    public string DownloadUrl { get; } = "https://github.com/jp7677/dxvk-nvapi/releases/download/v0.5.4/dxvk-nvapi-v0.5.4.tar.gz";
    public bool TopLevelFolder { get; } = false;
    public string Name { get; } = "Legacy 0.5.4";
    public string Description { get; } = "Dxvk-nvapi 0.5.4 to enable DLSS. May work with Dxvk 1.10.3.";
}