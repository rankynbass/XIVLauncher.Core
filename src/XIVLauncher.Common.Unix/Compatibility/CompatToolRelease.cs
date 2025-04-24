namespace XIVLauncher.Common.Unix.Compatibility;

public class CompatToolRelease : IToolRelease
{
    public string Folder { get; set; }
    
    public string DownloadUrl { get; set; }
    
    // True if the tarball has a folder at the top level This should be the same as the Folder.
    // If not, set to false, and create Folder to extract into.
    public bool TopLevelFolder { get; set; }
    
    public string Name { get; set; }
    
    public string Description { get; set; }
}