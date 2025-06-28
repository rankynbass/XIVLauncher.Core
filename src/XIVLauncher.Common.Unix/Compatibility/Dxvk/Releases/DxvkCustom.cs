namespace XIVLauncher.Common.Unix.Compatibility.Dxvk.Releases;

public sealed class DxvkCustomRelease(string label, string desc, string folder, string url, string checksum = "skip") : IDxvkRelease
{
    public string Label { get; } = label;
    public string Description { get; } = desc;
    public string Name { get; } = folder;
    public string DownloadUrl { get; } = url;
    public string Checksum = checksum;
}