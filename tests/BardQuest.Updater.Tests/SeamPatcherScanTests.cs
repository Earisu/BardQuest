using BardQuest.Updater.Core.Patching;

using Mono.Cecil;
using Mono.Cecil.Cil;

using Xunit;

namespace BardQuest.Updater.Tests;

public class SeamPatcherScanTests
{
    // A synthetic Assembly-CSharp with BOTH seam targets (MainMenu.OnEnable, SongContainer.FillContainers)
    // and an in-module Bootstrap exposing both injected methods.
    private static void WriteAssembly(string managedDir)
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

        var songContainer = new TypeDefinition("YARG.Song", "SongContainer",
            TypeAttributes.Public | TypeAttributes.Abstract | TypeAttributes.Sealed, module.TypeSystem.Object);
        var fill = new MethodDefinition("FillContainers",
            MethodAttributes.Private | MethodAttributes.Static, module.TypeSystem.Void);
        fill.Body.GetILProcessor().Append(Instruction.Create(OpCodes.Ret));
        songContainer.Methods.Add(fill);
        module.Types.Add(songContainer);

        var bootstrap = new TypeDefinition("BardQuest", "Bootstrap",
            TypeAttributes.Public | TypeAttributes.Class, module.TypeSystem.Object);
        var onMenu = new MethodDefinition("OnMainMenuEnabled",
            MethodAttributes.Public | MethodAttributes.Static, module.TypeSystem.Void);
        onMenu.Parameters.Add(new ParameterDefinition(mainMenu));
        onMenu.Body.GetILProcessor().Append(Instruction.Create(OpCodes.Ret));
        bootstrap.Methods.Add(onMenu);
        var onRefresh = new MethodDefinition("OnLibraryRefreshed",
            MethodAttributes.Public | MethodAttributes.Static, module.TypeSystem.Void);
        onRefresh.Body.GetILProcessor().Append(Instruction.Create(OpCodes.Ret));
        bootstrap.Methods.Add(onRefresh);
        module.Types.Add(bootstrap);

        asm.Write(Path.Combine(managedDir, "Assembly-CSharp.dll"));
    }

    private static int CallCountIn(string dll, string ns, string type, string method, string calleeName)
    {
        using var m = ModuleDefinition.ReadModule(dll);
        MethodDefinition target = m.GetType(ns + "." + type).Methods.Single(x => x.Name == method);
        return target.Body.Instructions.Count(i =>
            i.OpCode == OpCodes.Call && ((MethodReference)i.Operand).Name == calleeName);
    }

    [Fact]
    public void Patch_InjectsCallIntoFillContainers()
    {
        string managed = Path.Combine(Path.GetTempPath(), "bq-scan-" + Guid.NewGuid());
        try
        {
            WriteAssembly(managed);
            string live = Path.Combine(managed, "Assembly-CSharp.dll");

            Assert.Equal(0, CallCountIn(live, "YARG.Song", "SongContainer", "FillContainers", "OnLibraryRefreshed"));

            SeamPatcher.Patch(managed);

            Assert.True(SeamPatcher.IsManagedDirPatched(managed));
            Assert.Equal(1, CallCountIn(live, "YARG.Song", "SongContainer", "FillContainers", "OnLibraryRefreshed"));
            // The original bootstrap seam is still injected too.
            Assert.Equal(1, CallCountIn(live, "YARG.Menu.Main", "MainMenu", "OnEnable", "OnMainMenuEnabled"));
        }
        finally { Directory.Delete(managed, recursive: true); }
    }

    [Fact]
    public void Restore_RemovesBothSeams()
    {
        string managed = Path.Combine(Path.GetTempPath(), "bq-scan2-" + Guid.NewGuid());
        try
        {
            WriteAssembly(managed);
            string live = Path.Combine(managed, "Assembly-CSharp.dll");
            SeamPatcher.Patch(managed);
            SeamPatcher.Restore(managed);
            Assert.Equal(0, CallCountIn(live, "YARG.Song", "SongContainer", "FillContainers", "OnLibraryRefreshed"));
            Assert.False(SeamPatcher.IsManagedDirPatched(managed));
        }
        finally { Directory.Delete(managed, recursive: true); }
    }

    // Simulate a v1-era install: pristine backup + a live DLL carrying the OLD marker and the single
    // bootstrap seam. Updating (EnsurePatched) must NOT double-inject the bootstrap call.
    [Fact]
    public void EnsurePatched_FromOlderMarkerVersion_DoesNotDoubleInjectBootstrap()
    {
        string managed = Path.Combine(Path.GetTempPath(), "bq-v1v2-" + Guid.NewGuid());
        try
        {
            WriteAssembly(managed);
            string live = Path.Combine(managed, "Assembly-CSharp.dll");
            File.Copy(live, live + ".bardquest-bak"); // pristine backup, as a real v1 install left it

            // Hand-craft a "v1" live: one bootstrap call injected + a v1 marker type.
            using (var m = ModuleDefinition.ReadModule(live, new ReaderParameters { ReadWrite = true }))
            {
                MethodDefinition onEnable =
                    m.GetType("YARG.Menu.Main.MainMenu").Methods.Single(x => x.Name == "OnEnable");
                MethodDefinition onMenu =
                    m.GetType("BardQuest.Bootstrap").Methods.Single(x => x.Name == "OnMainMenuEnabled");
                ILProcessor il = onEnable.Body.GetILProcessor();
                Instruction firstInstr = onEnable.Body.Instructions[0];
                il.InsertBefore(firstInstr, il.Create(OpCodes.Ldarg_0));
                il.InsertBefore(firstInstr, il.Create(OpCodes.Call, onMenu));
                m.Types.Add(new TypeDefinition("BardQuest", "BardQuestSeam_v1",
                    TypeAttributes.NotPublic | TypeAttributes.Class, m.TypeSystem.Object));
                m.Write();
            }

            Assert.False(SeamPatcher.IsManagedDirPatched(managed)); // not the current (v2) marker
            Assert.Equal(1, CallCountIn(live, "YARG.Menu.Main", "MainMenu", "OnEnable", "OnMainMenuEnabled"));

            SeamPatcher.EnsurePatched(managed);

            Assert.True(SeamPatcher.IsManagedDirPatched(managed));
            // Exactly one bootstrap call (NOT two), and the new scan seam present.
            Assert.Equal(1, CallCountIn(live, "YARG.Menu.Main", "MainMenu", "OnEnable", "OnMainMenuEnabled"));
            Assert.Equal(1, CallCountIn(live, "YARG.Song", "SongContainer", "FillContainers", "OnLibraryRefreshed"));
        }
        finally { Directory.Delete(managed, recursive: true); }
    }
}
