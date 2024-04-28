using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Serilog;
using XIVLauncher.Common;
using XIVLauncher.Common.Unix.Compatibility;

namespace XIVLauncher.Core.UnixCompatibility;

public static class UmuLauncher
{

    public static bool IsUmuInstalled => !string.IsNullOrEmpty(UmuPath);

    public static string UmuPath { get; private set; }
    
    static UmuLauncher()
    {
        var HOME = System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile);

        if (File.Exists(Path.Combine(HOME, ".local", "bin", "umu-run")))
            UmuPath = Path.Combine(HOME, ".local", "bin", "umu-run");
        else if (File.Exists(Path.Combine("/", "usr", "bin", "umu-run")))
            UmuPath = Path.Combine("/", "usr", "bin", "umu-run");
        else if (File.Exists(Path.Combine("/", "usr", "local", "bin", "umu-run")))
            UmuPath = Path.Combine("/", "usr", "local", "bin", "umu-run");
        else
            UmuPath = "";
    }
}