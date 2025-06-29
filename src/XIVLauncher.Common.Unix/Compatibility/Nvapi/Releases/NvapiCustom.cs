namespace XIVLauncher.Common.Unix.Compatibility.Nvapi.Releases;

public sealed class NvapiCustomRelease(string label, string desc, string folder, string url, string checksum = "skip") : IToolRelease
{
    public string Label { get; } = label;
    public string Description { get; } = desc;
    public string Name { get; } = folder;
    public string DownloadUrl { get; } = url;
    public string Checksum { get; } = checksum;
}