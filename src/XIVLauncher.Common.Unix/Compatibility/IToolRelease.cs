namespace XIVLauncher.Common.Unix.Compatibility;

public interface IToolRelease
{
    string Name { get; }
    string Label { get; }
    string Description { get; }
    string DownloadUrl { get; }
    string[] Checksums { get; }
    bool lsteamclient { get; }
}
