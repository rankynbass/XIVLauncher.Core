namespace XIVLauncher.Common.Unix.Compatibility.Dxvk;

public interface IDxvkRelease : IToolRelease
{
    string Label { get; }
    string Description { get; }
    string Name { get; }
    string DownloadUrl { get; }
    string Checksum { get; }
}
