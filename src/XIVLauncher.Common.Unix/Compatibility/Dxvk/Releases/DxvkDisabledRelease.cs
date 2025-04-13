using XIVLauncher.Common.Unix.Compatibility;

namespace XIVLauncher.Common.Unix.Compatibility.Dxvk.Releases;

public sealed class DxvkDisabledRelease : IToolRelease
{
    public string Folder { get; } = "";
    public string DownloadUrl { get; } = "";
    public bool TopLevelFolder { get; } = true;
    public string Name { get; } = "Disabled";
    public string Description { get; } = "Dxvk disabled. Use WineD3D instead.";
}
