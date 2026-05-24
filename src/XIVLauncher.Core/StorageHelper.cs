using System.IO;
using System.Diagnostics;

using XIVLauncher.Common;

namespace XIVLauncher.Core;

public static class StorageHelper
{
    private static Platform platform;

    private static string xdgPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), Program.APP_NAME);

    private static string oldPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".xlcore");

    public static string? GetStoragePath()
    {
        if (!string.IsNullOrEmpty(CoreEnvironmentSettings.UserDir))
        {
            return CoreEnvironmentSettings.UserDir;
        }

        if (OperatingSystem.IsWindows())
        {
            return null; // Use default storage on Windows.
        }

        var xdgStoragePath = new DirectoryInfo(xdgPath);
        var oldStoragePath = new DirectoryInfo(oldPath);
       
        if (xdgStoragePath.Exists)
        {
            return null; 
        }

        if (oldStoragePath.Exists)
        {
            try
            {
                oldStoragePath.MoveTo(xdgStoragePath.FullName); // Move ~/.xlcore to new XDG path.
                return null;
            }
            catch (System.Exception)
            {
                Console.WriteLine($"Warning: Failed to move directory from {oldStoragePath.FullName} to {xdgStoragePath.FullName}");
                return oldStoragePath.FullName; // Return the old storage path ~/.xlcore if the move failed.
            }
        }
        return null; // If no storage directory exists, return null to use the default path.
    }

    public static void MakeSymlink(string storagePath)
    {
        if (OperatingSystem.IsWindows() || !string.IsNullOrEmpty(CoreEnvironmentSettings.UserDir))
        {
            return; // Do not create symlink on Windows, or if a custom user directory is set.
        }

        // This should only happen if XDG_DATA_HOME is on a separate volume from HOME.
        // Make a symlink at XDG_DATA_HOME/dev.goats.xivlauncher pointing to ~/.xlcore
        if (Path.Combine(storagePath) == Path.Combine(oldPath))
        {
            var oldTarget = Directory.ResolveLinkTarget(oldPath, true)?.FullName ?? oldPath;
            try
            {
                Directory.CreateSymbolicLink(xdgPath, oldTarget);
            }
            catch (System.Exception)
            {
                Console.WriteLine($"Warning: Failed to create symlink at {xdgPath} to {oldTarget}");
            }
            return;
        }

        // Catch an edge case where the user did some unusual manual symlinking.
        if (Directory.Exists(oldPath))
        {
            return;
        }

        // Make a symlink at ~/.xlcore pointing to the storage path
        var storageTarget = Directory.ResolveLinkTarget(storagePath, true)?.FullName ?? storagePath;
        try
        {
            Directory.CreateSymbolicLink(oldPath, storageTarget);
        }
        catch (System.Exception)
        {
            Console.WriteLine($"Warning:Failed to create symlink at {oldPath} to {storageTarget}");
        }
    }
}
