using System.IO;

namespace XIVLauncher.Common.Unix.Compatibility.Wine.Releases;

public sealed class ProtonCustomRelease(string label, string desc, string name, string folder, string url, string checksum = "skip") : IWineRelease
{
    public string Label { get; } = label;
    public string Description { get; } = desc;
    public string Name { get; } = name;
    public string ParentFolder { get; } = folder;
    public string DownloadUrl { get; } = url;
    public bool lsteamclient { get; } = true;
    public string[] Checksums { get; } = [ checksum ];
    public string Checksum { get; } = "";
}