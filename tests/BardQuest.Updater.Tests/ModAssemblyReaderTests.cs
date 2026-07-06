using Mono.Cecil;

using Xunit;

namespace BardQuest.Updater.Tests;

public class ModAssemblyReaderTests
{
    private static string WriteAssembly(string dir, (string Key, string Value)[] metadata)
    {
        _ = Directory.CreateDirectory(dir);
        string path = Path.Combine(dir, "BardQuest.Mod.dll");
        var asm = AssemblyDefinition.CreateAssembly(
            new AssemblyNameDefinition("BardQuest.Mod", new Version(1, 0)), "BardQuest.Mod", ModuleKind.Dll);
        ModuleDefinition module = asm.MainModule;

        MethodReference ctor = module.ImportReference(
            typeof(System.Reflection.AssemblyMetadataAttribute).GetConstructor([typeof(string), typeof(string)]));
        TypeReference stringType = module.ImportReference(typeof(string));

        foreach ((string key, string value) in metadata)
        {
            var ca = new CustomAttribute(ctor);
            ca.ConstructorArguments.Add(new CustomAttributeArgument(stringType, key));
            ca.ConstructorArguments.Add(new CustomAttributeArgument(stringType, value));
            asm.CustomAttributes.Add(ca);
        }

        asm.Write(path);
        return path;
    }

    [Fact]
    public void Read_ReturnsBothMarkers_WhenPresent()
    {
        string dir = Path.Combine(Path.GetTempPath(), "bq-mar-" + Guid.NewGuid());
        try
        {
            string dll = WriteAssembly(dir,
            [
                ("BardQuestModVersion", "1.2.0"),
                ("BardQuestYargTarget", "v0.15.0"),
                ("SomethingElse", "ignored"),
            ]);

            ModAssemblyInfo info = ModAssemblyReader.Read(dll);

            Assert.Equal("1.2.0", info.ModVersion);
            Assert.Equal("v0.15.0", info.YargTarget);
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    [Fact]
    public void Read_ReturnsNulls_WhenMarkersAbsent()
    {
        string dir = Path.Combine(Path.GetTempPath(), "bq-mar-" + Guid.NewGuid());
        try
        {
            string dll = WriteAssembly(dir, []);
            ModAssemblyInfo info = ModAssemblyReader.Read(dll);
            Assert.Null(info.ModVersion);
            Assert.Null(info.YargTarget);
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    [Fact]
    public void Read_ReturnsDefault_WhenFileMissing()
    {
        ModAssemblyInfo info = ModAssemblyReader.Read(
            Path.Combine(Path.GetTempPath(), "bq-missing-" + Guid.NewGuid(), "BardQuest.Mod.dll"));
        Assert.Null(info.ModVersion);
        Assert.Null(info.YargTarget);
    }
}
