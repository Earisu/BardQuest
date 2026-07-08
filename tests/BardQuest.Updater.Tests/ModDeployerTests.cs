using BardQuest.Updater.Core.Patching;

using Xunit;

namespace BardQuest.Updater.Tests;

public class ModDeployerTests
{
    [Fact]
    public void Copy_CopiesAllThreeDlls_ThenDeleteRemovesThem()
    {
        string root = Path.Combine(Path.GetTempPath(), "bq-deploy-" + Guid.NewGuid());
        string srcDir = Path.Combine(root, "src");
        string managed = Path.Combine(root, "managed");
        _ = Directory.CreateDirectory(srcDir);
        _ = Directory.CreateDirectory(managed);
        try
        {
            foreach (string name in ModDeployer.ModDllNames)
            {
                File.WriteAllText(Path.Combine(srcDir, name), "stub");
            }

            ModDeployer.Copy(srcDir, managed);
            foreach (string name in ModDeployer.ModDllNames)
            {
                Assert.True(File.Exists(Path.Combine(managed, name)));
            }

            ModDeployer.Delete(managed);
            foreach (string name in ModDeployer.ModDllNames)
            {
                Assert.False(File.Exists(Path.Combine(managed, name)));
            }
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public void Copy_Throws_WhenSourceDllMissing()
    {
        string root = Path.Combine(Path.GetTempPath(), "bq-deploy-" + Guid.NewGuid());
        string srcDir = Path.Combine(root, "src");
        string managed = Path.Combine(root, "managed");
        _ = Directory.CreateDirectory(srcDir);
        _ = Directory.CreateDirectory(managed);
        try
        {
            _ = Assert.Throws<FileNotFoundException>(() => ModDeployer.Copy(srcDir, managed));
        }
        finally { Directory.Delete(root, recursive: true); }
    }
}
