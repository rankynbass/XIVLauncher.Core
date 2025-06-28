namespace XIVLauncher.Common.Unix.Compatibility.Nvapi;

public interface INvapiRelease : IToolRelease
{
    string Label { get; }
    string Description { get; }
    string Name { get; }
    string DownloadUrl { get; }
    string Checksum { get; }
}