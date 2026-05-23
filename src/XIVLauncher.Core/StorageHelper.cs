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
            return null; // Use default storage on Windows.
        }

        var xdgStoragePath = new DirectoryInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), appName));       // new default
        var oldxdgStoragePath = new DirectoryInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "xlcore"));   // old xdg path for 1.4.0.3
        var oldStoragePath = new DirectoryInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".xlcore"));              // legacy path for xivlauncher
        if (xdgStoragePath.Exists)
        {
            try
            {
                if (!oldStoragePath.Exists)
                    Directory.CreateSymbolicLink(oldStoragePath.FullName, xdgStoragePath.FullName); // Create a symlink at ~/.xlcore to the new XDG path. Temporary legacy support.
            }
            catch (System.Exception)
            {
                Console.WriteLine($"Failed to create symlink at {oldStoragePath.FullName} pointing to {xdgStoragePath.FullName}");
            }
            return null; 
        }
        if (oldxdgStoragePath.Exists)
        {
            try
            {
                oldxdgStoragePath.MoveTo(xdgStoragePath.FullName); // Move XDG_DATA_HOME/xlcore to XDG_DATA_HOME/dev.goats.xivlauncher.
                try
                {
                    if (!oldStoragePath.Exists)
                        Directory.CreateSymbolicLink(oldStoragePath.FullName, xdgStoragePath.FullName); // Create a symlink at ~/.xlcore to the new XDG path. Temporary legacy support.
                }
                catch (Exception)
                {
                    Console.WriteLine($"Failed to create symlink at {oldStoragePath.FullName} pointing to {xdgStoragePath.FullName}");
                }
                return null;
            }
            catch (Exception)
            {
                Console.WriteLine($"Failed to move directory from {oldxdgStoragePath.FullName} to {xdgStoragePath.FullName}");
                return oldxdgStoragePath.FullName; // Return the old XDG path ~/.local/share/xlcore if the move failed.
            }
        }
        if (oldStoragePath.Exists)
        {
            var symLinkTarget = oldStoragePath.ResolveLinkTarget(true);
            // If ~/.xlcore is already a symlink, assume it's correctly set up and create a symlink at XDG_DATA_HOME/dev.goats.xivlauncher pointing to the same target.
            if (symLinkTarget is not null)
            {
                try
                {
                    Directory.CreateSymbolicLink(xdgStoragePath.FullName, symLinkTarget.FullName);
                }
                catch (System.Exception)
                {
                    return oldStoragePath.FullName; // Return the old storage path ~/.xlcore if the symlink creation failed.
                }
                return null;
            }

            try
            {
                var xlcore = oldStoragePath.FullName;           // Store the path. After the move, oldStoragePath will point to the new location.
                oldStoragePath.MoveTo(xdgStoragePath.FullName); // Move ~/.xlcore to new XDG path, since it's confirmed to be on the same drive.
                try
                {
                    Directory.CreateSymbolicLink(xlcore, xdgStoragePath.FullName); // Create a symlink from ~/.xlcore to the new XDG path. Temporary legacy support.
                }
                catch (System.Exception)
                {
                    Console.WriteLine($"Failed to create symlink at {xlcore} pointing to {xdgStoragePath.FullName}");
                }
                return null;
            }
            catch (System.Exception)
            {
                Console.WriteLine($"Failed to move directory from {oldStoragePath.FullName} to {xdgStoragePath.FullName}");
                return oldStoragePath.FullName; // Return the old storage path ~/.xlcore if the move failed.
            }
        }
        return null; // If no storage directory exists, return null to use the default path.
    }
}
