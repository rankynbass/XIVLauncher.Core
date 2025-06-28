using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Linq;

using XIVLauncher.Common.Unix.Compatibility.Wine.Releases;

namespace XIVLauncher.Common.Unix.Compatibility.Wine;

public class UnofficialWine
{
    public Dictionary<string, IWineRelease> Version { get; private set; }

    private string wineFolder { get; }

    private string rootFolder { get; }

    public UnofficialWine(string root)
    {
        this.rootFolder = root;
        this.wineFolder = Path.Combine(root, "compatibilitytool", "wine");

        Initialize();
    }

    public void Initialize()
    {
        Version = new Dictionary<string, IWineRelease>();

    }

    public void ClearVersions()
    {
        Version.Clear();
    }
}
