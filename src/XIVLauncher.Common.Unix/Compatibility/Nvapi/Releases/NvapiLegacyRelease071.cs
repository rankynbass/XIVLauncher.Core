using XIVLauncher.Common.Unix.Compatibility;

namespace XIVLauncher.Common.Unix.Compatibility.Nvapi.Releases;

public sealed class NvapiLegacyRelease071 : IToolRelease
{
    public string Folder { get; } = "dxvk-nvapi-v0.7.1";
    public string DownloadUrl { get; } = "https://github.com/jp7677/dxvk-nvapi/releases/download/v0.7.1/dxvk-nvapi-v0.7.1.tar.gz";
    public bool TopLevelFolder { get; } = false;
    public string Name { get; } = "Legacy 0.7.1";
    public string Description { get; } = "Dxvk-nvapi 0.7.1 to enable DLSS. For nvidia driver version <= 550.";
}