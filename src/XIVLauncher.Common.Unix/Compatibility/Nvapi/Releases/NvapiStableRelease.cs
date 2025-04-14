using XIVLauncher.Common.Unix.Compatibility;

namespace XIVLauncher.Common.Unix.Compatibility.Nvapi.Releases;

public sealed class NvapiStableRelease : IToolRelease
{
    public string Folder { get; } = "dxvk-nvapi-v0.9.0";
    public string DownloadUrl { get; } = "https://github.com/jp7677/dxvk-nvapi/releases/download/v0.9.0/dxvk-nvapi-v0.9.0.tar.gz";
    public bool TopLevelFolder { get; } = false;
    public string Name { get; } = "Stable";
    public string Description { get; } = "Dxvk-nvapi 0.9.0 to enable DLSS. For latest Nvidia drivers.";
}