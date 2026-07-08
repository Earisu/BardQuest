using BardQuest.Updater.Core.Patching;
using BardQuest.Updater.Core.Updating;

using Mono.Cecil;

using Xunit;

namespace BardQuest.Updater.Tests;

public class ModUpdateApplierTests
{
    // Builds an "extracted release" dir: a real BardQuest.Mod.dll with a baked YargTarget
    // marker, plus empty stand-ins for the other required mod DLLs.
    private static string BuildExtractedRelease(string root, string yargTarget)
    {
        _ = Directory.CreateDirectory(root);
        var asm = AssemblyDefinition.CreateAssembly(
            new AssemblyNameDefinition("BardQuest.Mod", new Version(1, 0)), "BardQuest.Mod", ModuleKind.Dll);
        ModuleDefinition module = asm.MainModule;
        MethodReference ctor = module.ImportReference(
            typeof(System.Reflection.AssemblyMetadataAttribute).GetConstructor([typeof(string), typeof(string)]));
        TypeReference stringType = module.ImportReference(typeof(string));

        void AddMeta(string key, string value)
        {
            var ca = new CustomAttribute(ctor);
            ca.ConstructorArguments.Add(new CustomAttributeArgument(stringType, key));
            ca.ConstructorArguments.Add(new CustomAttributeArgument(stringType, value));
            asm.CustomAttributes.Add(ca);
        }

        AddMeta("BardQuestModVersion", "1.5.0");
        AddMeta("BardQuestYargTarget", yargTarget);
        asm.Write(Path.Combine(root, "BardQuest.Mod.dll"));

        foreach (string name in ModDeployer.ModDllNames)
        {
            if (name != "BardQuest.Mod.dll")
            {
                File.WriteAllBytes(Path.Combine(root, name), []);
            }
        }

        return root;
    }

    [Fact]
    public void GateAndApply_MissingFiles_WhenExtractedDirEmpty()
    {
        string extracted = Path.Combine(Path.GetTempPath(), "bq-empty-" + Guid.NewGuid());
        string managed = Path.Combine(Path.GetTempPath(), "bq-mgd-" + Guid.NewGuid());
        _ = Directory.CreateDirectory(extracted);
        try
        {
            ApplyResult r = ModUpdateApplier.GateAndApply(extracted, installTag: "v0.15.0", managedDir: managed);
            Assert.Equal(ApplyOutcome.MissingFiles, r.Outcome);
            Assert.False(Directory.Exists(managed)); // never touched the install
        }
        finally { Directory.Delete(extracted, recursive: true); }
    }

    [Fact]
    public void GateAndApply_Incompatible_WhenYargTargetMismatches()
    {
        string extracted = BuildExtractedRelease(
            Path.Combine(Path.GetTempPath(), "bq-rel-" + Guid.NewGuid()), yargTarget: "v0.15.0");
        string managed = Path.Combine(Path.GetTempPath(), "bq-mgd-" + Guid.NewGuid());
        try
        {
            ApplyResult r = ModUpdateApplier.GateAndApply(extracted, installTag: "v0.14.0", managedDir: managed);
            Assert.Equal(ApplyOutcome.Incompatible, r.Outcome);
            Assert.Equal("v0.15.0", r.ModTarget);
            Assert.Equal("1.5.0", r.Version);
            Assert.False(Directory.Exists(managed)); // never touched the install
        }
        finally { Directory.Delete(extracted, recursive: true); }
    }
}
