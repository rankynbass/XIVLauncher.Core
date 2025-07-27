using System.IO;

namespace XIVLauncher.Common.Unix.Compatibility.Wine.Releases;

public sealed class WineCustomRelease(string label, string desc, string name, string folder, string url, bool lsc, string[] checksums = null) : IWineRelease
{
    public string Label { get; } = label;
    public string Description { get; } = desc;
    public string Name { get; } = name;
    public string ParentFolder { get; } = folder;
    public string DownloadUrl { get; } = url;
    public string[] Checksums { get; } = checksums ?? ["skip"];
    public bool Lsteamclient { get; } = lsc;
    public bool IsProton { get; } = false;
}