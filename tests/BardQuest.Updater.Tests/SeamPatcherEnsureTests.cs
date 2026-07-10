using BardQuest.Updater.Core.Patching;

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

    private static int CallCountIn(string dll, string ns, string type, string method, string calleeName)
    {
        using var m = ModuleDefinition.ReadModule(dll);
        MethodDefinition target = m.GetType(ns + "." + type).Methods.Single(x => x.Name == method);
        return target.Body.Instructions.Count(i =>
            i.OpCode == OpCodes.Call && ((MethodReference)i.Operand).Name == calleeName);
    }

    // Hand-crafts an OLD-marker live DLL over the pristine build: one bootstrap seam already injected
    // plus a superseded BardQuestSeam_v1 marker, exactly as a prior BardQuest version would have left it.
    private static void SeamWithOldMarker(string live)
    {
        using var m = ModuleDefinition.ReadModule(live, new ReaderParameters { ReadWrite = true });
        MethodDefinition onEnable = m.GetType("YARG.Menu.Main.MainMenu").Methods.Single(x => x.Name == "OnEnable");
        MethodDefinition onMenu = m.GetType("BardQuest.Bootstrap").Methods.Single(x => x.Name == "OnMainMenuEnabled");
        ILProcessor il = onEnable.Body.GetILProcessor();
        Instruction firstInstr = onEnable.Body.Instructions[0];
        il.InsertBefore(firstInstr, il.Create(OpCodes.Ldarg_0));
        il.InsertBefore(firstInstr, il.Create(OpCodes.Call, onMenu));
        m.Types.Add(new TypeDefinition("BardQuest", "BardQuestSeam_v1",
            TypeAttributes.NotPublic | TypeAttributes.Class, m.TypeSystem.Object));
        m.Write();
    }

    // A superseded-marker install (older BardQuest) with its pristine backup intact must re-patch
    // cleanly from the backup — NOT inject a second bootstrap call on top of the already-seamed live.
    [Fact]
    public void EnsurePatched_FromOlderMarkerVersion_DoesNotDoubleInjectBootstrap()
    {
        string managed = Path.Combine(Path.GetTempPath(), "bq-oldmarker-" + Guid.NewGuid());
        try
        {
            WriteAssembly(managed, "Base");
            string live = Path.Combine(managed, "Assembly-CSharp.dll");
            File.Copy(live, live + ".bardquest-bak"); // pristine backup, as a real old install left it
            SeamWithOldMarker(live);

            Assert.False(SeamPatcher.IsManagedDirPatched(managed)); // not the current marker
            Assert.Equal(1, CallCountIn(live, "YARG.Menu.Main", "MainMenu", "OnEnable", "OnMainMenuEnabled"));

            SeamPatcher.EnsurePatched(managed);

            Assert.True(SeamPatcher.IsManagedDirPatched(managed));
            // Exactly one bootstrap call (NOT two) after the version migration.
            Assert.Equal(1, CallCountIn(live, "YARG.Menu.Main", "MainMenu", "OnEnable", "OnMainMenuEnabled"));
        }
        finally { Directory.Delete(managed, recursive: true); }
    }

    // A superseded-marker install whose pristine backup was lost out-of-band (antivirus, manual cleanup)
    // while the already-seamed live survives: EnsurePatched must refuse to patch on top of it (which would
    // double-inject the bootstrap seam) and throw instead.
    [Fact]
    public void EnsurePatched_MarkerPresentButBackupMissing_Throws()
    {
        string managed = Path.Combine(Path.GetTempPath(), "bq-nobak-" + Guid.NewGuid());
        try
        {
            WriteAssembly(managed, "Base");
            string live = Path.Combine(managed, "Assembly-CSharp.dll");
            string backup = live + ".bardquest-bak";
            File.Copy(live, backup);
            SeamWithOldMarker(live);
            File.Delete(backup); // simulate out-of-band backup loss

            Assert.False(SeamPatcher.IsManagedDirPatched(managed));

            _ = Assert.Throws<InvalidOperationException>(() => SeamPatcher.EnsurePatched(managed));

            // Must not have double-injected the bootstrap seam, and must not have been (re-)patched.
            Assert.Equal(1, CallCountIn(live, "YARG.Menu.Main", "MainMenu", "OnEnable", "OnMainMenuEnabled"));
            Assert.False(SeamPatcher.IsManagedDirPatched(managed));
        }
        finally { Directory.Delete(managed, recursive: true); }
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
    public void Restore_AfterPatch_RevertsLiveDllAndDeletesBackup()
    {
        string managed = Path.Combine(Path.GetTempPath(), "bq-restore-" + Guid.NewGuid());
        try
        {
            WriteAssembly(managed, "Pristine");
            string live = Path.Combine(managed, "Assembly-CSharp.dll");
            string backup = live + ".bardquest-bak";

            Assert.False(SeamPatcher.IsManagedDirPatched(managed));

            SeamPatcher.Patch(managed);
            Assert.True(SeamPatcher.IsManagedDirPatched(managed));
            Assert.True(File.Exists(backup));

            SeamPatcher.Restore(managed);
            Assert.False(SeamPatcher.IsManagedDirPatched(managed));
            Assert.True(HasType(live, "BuildTag", "Pristine")); // live restored to the pre-patch build
            Assert.False(File.Exists(backup));                  // backup consumed by Restore
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
