using Mono.Cecil;
using Mono.Cecil.Cil;

using Xunit;

namespace BardQuest.Updater.Tests;

public class SeamPatcherEnsureTests
{
    // Writes a MainMenu-shaped Assembly-CSharp.dll carrying a version marker string we can read back.
    private static void WriteAssembly(string managedDir, string versionTypeName)
    {
        _ = Directory.CreateDirectory(managedDir);
        var asm = AssemblyDefinition.CreateAssembly(
            new AssemblyNameDefinition("Assembly-CSharp", new Version(1, 0)), "Assembly-CSharp", ModuleKind.Dll);
        ModuleDefinition module = asm.MainModule;

        var mainMenu = new TypeDefinition("YARG.Menu.Main", "MainMenu",
            TypeAttributes.Public | TypeAttributes.Class, module.TypeSystem.Object);
        var onEnable = new MethodDefinition("OnEnable",
            MethodAttributes.Public | MethodAttributes.HideBySig, module.TypeSystem.Void);
        onEnable.Body.GetILProcessor().Append(Instruction.Create(OpCodes.Ret));
        mainMenu.Methods.Add(onEnable);
        module.Types.Add(mainMenu);

        var bootstrap = new TypeDefinition("BardQuest", "Bootstrap",
            TypeAttributes.Public | TypeAttributes.Class, module.TypeSystem.Object);
        var onMenu = new MethodDefinition("OnMainMenuEnabled",
            MethodAttributes.Public | MethodAttributes.Static, module.TypeSystem.Void);
        onMenu.Parameters.Add(new ParameterDefinition(mainMenu));
        onMenu.Body.GetILProcessor().Append(Instruction.Create(OpCodes.Ret));
        bootstrap.Methods.Add(onMenu);
        module.Types.Add(bootstrap);

        // A unique marker type so tests can tell which build the live DLL currently is.
        module.Types.Add(new TypeDefinition("BuildTag", versionTypeName,
            TypeAttributes.NotPublic | TypeAttributes.Class, module.TypeSystem.Object));

        asm.Write(Path.Combine(managedDir, "Assembly-CSharp.dll"));
    }

    private static bool HasType(string dll, string ns, string name)
    {
        using var m = ModuleDefinition.ReadModule(dll);
        return m.Types.Any(t => t.Namespace == ns && t.Name == name);
    }

    [Fact]
    public void EnsurePatched_AfterLauncherReplacesDll_DoesNotRevertToStaleBackup()
    {
        string managed = Path.Combine(Path.GetTempPath(), "bq-ensure-" + Guid.NewGuid());
        try
        {
            // 1. Install onto the "old" YARG build -> creates .bardquest-bak from that old build.
            WriteAssembly(managed, "OldBuild");
            SeamPatcher.EnsurePatched(managed);
            Assert.True(SeamPatcher.IsManagedDirPatched(managed));

            // 2. Launcher replaces Assembly-CSharp.dll with a NEW pristine build (seam gone, backup now stale).
            WriteAssembly(managed, "NewBuild");
            Assert.False(SeamPatcher.IsManagedDirPatched(managed));

            // 3. EnsurePatched must re-patch the NEW build, not revert to the OLD backup.
            SeamPatcher.EnsurePatched(managed);
            string live = Path.Combine(managed, "Assembly-CSharp.dll");
            Assert.True(SeamPatcher.IsManagedDirPatched(managed));
            Assert.True(HasType(live, "BuildTag", "NewBuild"));   // still the new build
            Assert.False(HasType(live, "BuildTag", "OldBuild"));  // NOT reverted to old
        }
        finally { Directory.Delete(managed, recursive: true); }
    }

    [Fact]
    public void EnsurePatched_WhenAlreadyPatched_IsNoOp()
    {
        string managed = Path.Combine(Path.GetTempPath(), "bq-ensure2-" + Guid.NewGuid());
        try
        {
            WriteAssembly(managed, "Build");
            SeamPatcher.EnsurePatched(managed);
            SeamPatcher.EnsurePatched(managed); // second call must not throw or double-patch
            Assert.True(SeamPatcher.IsManagedDirPatched(managed));
        }
        finally { Directory.Delete(managed, recursive: true); }
    }
}
