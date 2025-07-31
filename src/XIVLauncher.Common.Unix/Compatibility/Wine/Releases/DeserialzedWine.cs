using System.IO;

namespace XIVLauncher.Common.Unix.Compatibility.Wine.Releases;

public class DeserializedWine() : IWineRelease
{
    public string Name { get; set; }
    public string ParentFolder { get; set; }
    public string Label { get; set; }
    public string Description { get; set; }
    public string DownloadUrl { get; set; }
    public string[] Checksums { get; set; }
    public bool Lsteamclient { get; set; }
    public bool IsProton { get; set; }
}