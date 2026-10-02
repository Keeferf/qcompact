using Microsoft.Win32;

namespace QCompact.Tests;

public class WslDiskLocatorTests
{
    [Fact]
    public void MapsRegistryBasePathToVhdx()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var root = $"Software\\qcompact-test-{Guid.NewGuid():N}";
        var basePath = Path.Combine(Path.GetTempPath(), $"qcompact-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(basePath);
        File.WriteAllText(Path.Combine(basePath, "ext4.vhdx"), string.Empty);

        try
        {
            using (var key = Registry.CurrentUser.CreateSubKey($"{root}\\{Guid.NewGuid()}"))
            {
                Assert.NotNull(key);
                key!.SetValue("DistributionName", "TestDistro");
                key.SetValue("BasePath", basePath);
            }

            var disks = WslDiskLocator.Find(root);

            var disk = Assert.Single(disks);
            Assert.Equal("TestDistro", disk.Distro);
            Assert.True(disk.Exists);
            Assert.Equal(Path.Combine(basePath, "ext4.vhdx"), disk.Vhdx);
        }
        finally
        {
            Registry.CurrentUser.DeleteSubKeyTree(root, throwOnMissingSubKey: false);
            try
            {
                Directory.Delete(basePath, recursive: true);
            }
            catch
            {
                // Best effort.
            }
        }
    }

    [Fact]
    public void MissingRegistryRootReturnsEmpty()
    {
        var disks = WslDiskLocator.Find($"Software\\qcompact-missing-{Guid.NewGuid():N}");

        Assert.Empty(disks);
    }
}
