using System.IO;

namespace XIVLauncher.Common.Unix.Compatibility.Wine.Releases;

public sealed class WineCustomRelease(string label, string desc, string folder, string url, bool lsc, string[] checksums = null) : IWineRelease
{
    public string Label { get; } = label;
    public string Description { get; } = desc;
    public string Name { get; private set; } = folder;
    public string DownloadUrl { get; } = url;
    public bool lsteamclient { get; } = lsc;
    public string[] Checksums { get; } = checksums ?? ["skip"];
    public string Checksum { get; } = "";
}