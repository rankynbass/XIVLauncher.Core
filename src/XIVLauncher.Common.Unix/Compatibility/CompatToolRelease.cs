namespace XIVLauncher.Common.Unix.Compatibility;

public class CompatToolRelease : IToolRelease
{
    public string Folder { get; }
    
    public string DownloadUrl { get; }
    
    // True if the tarball has a folder at the top level This should be the same as the Folder.
    // If not, set to false, and create Folder to extract into.
    public bool TopLevelFolder { get; }
    
    public string Name { get; }
    
    public string Description { get; }
}