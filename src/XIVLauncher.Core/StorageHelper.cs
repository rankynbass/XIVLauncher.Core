using System.IO;
using System.Diagnostics;

using XIVLauncher.Common;

namespace XIVLauncher.Core;

public static class StorageHelper
{
    private static Platform platform;

    static StorageHelper()
    {
        if (OperatingSystem.IsWindows())
        {
            platform = Platform.Win32;
        }
        else if (OperatingSystem.IsLinux())
        {
            platform = Platform.Linux;
        }
        else if (OperatingSystem.IsMacOS())
        {
            platform = Platform.Mac;
        }
        else
        {
            throw new PlatformNotSupportedException("Unsupported platform");
        }
    }

    public static string? GetStoragePath(string appName)
    {
        if (!string.IsNullOrEmpty(CoreEnvironmentSettings.UserDir))
        {
            return CoreEnvironmentSettings.UserDir;
        }
        if (platform == Platform.Win32)
        {
            return null; // Windows storage unchanged.
        }
        else if (platform == Platform.Linux  || platform == Platform.Mac)
        {
            var xdgStoragePath = new DirectoryInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), appName));
            var oldxdgStoragePath = new DirectoryInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "xlcore"));
            var oldStoragePath = new DirectoryInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".xlcore"));
            if (xdgStoragePath.Exists)
            {
                return null;    // Use XDG Base Directory spec path if it exists. This is ~/.local/share/dev.goats.xivlauncher on Linux and ~/Library/Application Support/dev.goats.xivlauncher on Mac.
                                // This can be overridden with the XL_USERDIR environment variable, which will take precedence over both the old path and the XDG path.
            }
            if (oldxdgStoragePath.Exists)
            {
                // We will check to see if the old XDG path is on the same drive as the new XDG path. If it is, we can move it to the new location.
                // If it's not, we will leave it in place and use it as the storage path, since moving it would take forever and hang the program.
                if (IsMovable(oldxdgStoragePath.FullName, Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)))
                {
                    oldxdgStoragePath.MoveTo(xdgStoragePath.FullName); // Move XDG_DATA_HOME/xlcore to XDG_DATA_HOME/dev.goats.xivlauncher, since it's confirmed to be on the same drive.
                    return xdgStoragePath.FullName;
                }
                return oldxdgStoragePath.FullName;
            }
            if (oldStoragePath.Exists)
            {
                // We will check to see if ~/.xlcore is on the same drive as the new XDG path. If it is, we can move it to the new location.
                // If it's not, we will leave it in place and use it as the storage path, since moving it would take forever and hang the program.
                if (IsMovable(oldStoragePath.FullName, Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)))
                {
                    oldStoragePath.MoveTo(xdgStoragePath.FullName); // Move ~/.xlcore to new XDG path, since it's confirmed to be on the same drive.
                    return xdgStoragePath.FullName;
                }
                return oldStoragePath.FullName; // Pass the old storage path ~/.xlcore
            }
            return null; // Use XDG Base Directory spec path for new installs on Linux and Mac.
        }
        else
        {
            throw new PlatformNotSupportedException("Unsupported platform");
        }
    }

    private static bool IsMovable(string oldFolder, string newFolder)
    {
        try
        {
            var drives = DriveInfo.GetDrives();
            int oldHitCounter = 0;
            int newHitCounter = 0;

            // Use realpath to resolve symlinks. This is important for bazzite and other immutable distros.
            oldFolder = GetRealPath(oldFolder);
            newFolder = GetRealPath(newFolder);
            if (oldFolder == null || newFolder == null)
            {
                return false; // If we can't resolve the real paths, we assume the move failed and return false.
            }
            
            // Unix dotnet enumerates drives for each mounted path, including various virtual filesystems like /proc and /sys.
            // To determine if two folders are on the same drive, we'll match them to mounted partitions and count the hits.
            // This is to get around edge cases where someone did something weird like mount a partition to ~/.xlcore or ~/.local/share.
            // If they match the same number of partitions, and at least one, we can be reasonably sure they're on the same partition, and safe to move.
            foreach (var drive in drives)
            {
                if (oldFolder.StartsWith(drive.RootDirectory.FullName))
                {
                    oldHitCounter++;
                }
                if (newFolder.StartsWith(drive.RootDirectory.FullName))
                {
                    newHitCounter++;
                }
            }
            if (oldHitCounter == newHitCounter && oldHitCounter > 0)
            {
                return true;
            }
            return false; // The files are on different drives, so they are not movable.
        }
        catch
        {
            return false; // If any exception occurs, we assume the move failed and return false.
        }
    }

    private static string? GetRealPath(string path)
    {
        // There's no good way to resolve the real path of a file in .NET on Linux and MacOS,
        // since .NET doesn't have a built-in way to do it and the behavior of Path.GetFullPath is inconsistent across platforms.
        // The best we can do is to call the "realpath" command-line utility, which is available on both Linux and MacOS.
        
        // Future implementation note: Dotnet 11 preview currently allows for the testing hardlinks, which would allow us to try creating a hardlink from launcher.ini
        // to a test file in the new location. If it succeeded, we'd know they were on the same drive. 
        
        // This should work on Linux and MacOS, but not Windows. It should never be called from Windows, but just in case:
        if (platform == Platform.Win32)
        {
            return null;
        }
        var startInfo = new ProcessStartInfo
        {
            FileName = "realpath",
            Arguments = path,
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        using (var process = new Process { StartInfo = startInfo })
        {
            try
            {
                process.Start();
                string realPath = process.StandardOutput.ReadToEnd().Trim();
                process.WaitForExit();
                if (process.ExitCode != 0)
                {
                    return null;
                }
                return realPath;
            }
            catch
            {
                return null; // If any exception occurs, we assume we can't resolve the path and return null.
            }
        }
    }
}
