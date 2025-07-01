namespace XIVLauncher.Common.Unix.Compatibility.Wine;

public class IWineRelease : IToolRelease
{
    string Name { get; }
    string ParentFolder { get; }
    string Label { get; }
    string Description { get; }
    string DownloadUrl { get; }
    string Checksum { get; }
    string[] Checksums { get; }
    bool lsteamclient { get; }
}