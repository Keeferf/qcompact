using Microsoft.Win32;

namespace QCompact;

public static class WslDiskLocator
{
    public const string DefaultRegistryRoot = @"Software\Microsoft\Windows\CurrentVersion\Lxss";

    public static IReadOnlyList<WslDisk> Find(string registryRoot = DefaultRegistryRoot)
    {
        var disks = new List<WslDisk>();
        if (!OperatingSystem.IsWindows())
        {
            return disks;
        }

        using var root = Registry.CurrentUser.OpenSubKey(registryRoot);
        if (root is null)
        {
            return disks;
        }

        foreach (var subKeyName in root.GetSubKeyNames())
        {
            using var sub = root.OpenSubKey(subKeyName);
            if (sub is null)
            {
                continue;
            }

            if (sub.GetValue("DistributionName") is not string distro || string.IsNullOrEmpty(distro))
            {
                continue;
            }

            if (sub.GetValue("BasePath") is not string basePath || string.IsNullOrEmpty(basePath))
            {
                continue;
            }

            var vhdx = Path.Combine(basePath, "ext4.vhdx");
            disks.Add(new WslDisk(distro, basePath, vhdx, File.Exists(vhdx)));
        }

        return disks;
    }
}
