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
        // Mirror the real FillContainers' shape: a try/finally (disposing an enumerator) whose
        // normal exit is a `leave` targeting the method's final `ret` directly, with the `ret`
        // sitting AFTER the handler's `endfinally`. A naive before-ret insert produces dead code
        // here exactly like the real method, because the `leave` still jumps straight past it.
        ILProcessor fillIl = fill.Body.GetILProcessor();
        var tryStart = Instruction.Create(OpCodes.Nop);
        var ret = Instruction.Create(OpCodes.Ret);
        var leave = Instruction.Create(OpCodes.Leave_S, ret);
        var handlerStart = Instruction.Create(OpCodes.Nop);
        var endFinally = Instruction.Create(OpCodes.Endfinally);
        fillIl.Append(tryStart);
        fillIl.Append(leave);
        fillIl.Append(handlerStart);
        fillIl.Append(endFinally);
        fillIl.Append(ret);
        fill.Body.ExceptionHandlers.Add(new ExceptionHandler(ExceptionHandlerType.Finally)
        {
            TryStart = tryStart,
            TryEnd = handlerStart,
            HandlerStart = handlerStart,
            HandlerEnd = ret,
        });
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

    // Simulate an OLDER BardQuest marker present but its pristine backup missing (e.g. deleted
    // out-of-band by antivirus/manual cleanup) while the already-seamed live DLL survives.
    // EnsurePatched must refuse to patch on top of the already-seamed live (which would double-inject
    // the bootstrap seam) and must throw instead.
    [Fact]
    public void EnsurePatched_MarkerPresentButBackupMissing_Throws()
    {
        string managed = Path.Combine(Path.GetTempPath(), "bq-nobak-" + Guid.NewGuid());
        try
        {
            WriteAssembly(managed);
            string live = Path.Combine(managed, "Assembly-CSharp.dll");
            string backup = live + ".bardquest-bak";
            File.Copy(live, backup); // pristine backup, as a real v1 install would have left

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

            File.Delete(backup); // simulate out-of-band backup loss

            Assert.False(SeamPatcher.IsManagedDirPatched(managed)); // not the current (v2) marker

            _ = Assert.Throws<InvalidOperationException>(() => SeamPatcher.EnsurePatched(managed));

            // Must not have double-injected the bootstrap seam, and must not have been (re-)patched.
            Assert.Equal(1, CallCountIn(live, "YARG.Menu.Main", "MainMenu", "OnEnable", "OnMainMenuEnabled"));
            Assert.False(SeamPatcher.IsManagedDirPatched(managed));
        }
        finally { Directory.Delete(managed, recursive: true); }
    }

    // Structural regression test for the leave-bypass bug: a naive InsertBefore(ret, call) leaves the
    // `leave` inside the synthetic try/finally still targeting the original `ret`, so the injected
    // call is unreachable dead code. After Patch, EVERY exit must be routed through the call:
    // no ret is reachable except by falling through the call, and no branch/leave/switch/handler
    // boundary still points at a ret.
    [Fact]
    public void Patch_RoutesEveryExitThroughTheFillContainersSeam()
    {
        string managed = Path.Combine(Path.GetTempPath(), "bq-scan3-" + Guid.NewGuid());
        try
        {
            WriteAssembly(managed);
            string live = Path.Combine(managed, "Assembly-CSharp.dll");

            SeamPatcher.Patch(managed);

            using var m = ModuleDefinition.ReadModule(live);
            MethodDefinition fill = m.GetType("YARG.Song.SongContainer").Methods.Single(x => x.Name == "FillContainers");
            var instructions = fill.Body.Instructions.ToList();
            var rets = instructions.Where(i => i.OpCode == OpCodes.Ret).ToList();
            Assert.NotEmpty(rets);

            // 1. Every ret is immediately preceded by the injected call.
            foreach (Instruction ret in rets)
            {
                int index = instructions.IndexOf(ret);
                Assert.True(index > 0, "ret must not be the first instruction in the method");
                Instruction previous = instructions[index - 1];
                Assert.Equal(OpCodes.Call, previous.OpCode);
                Assert.Equal("OnLibraryRefreshed", ((MethodReference)previous.Operand).Name);
            }

            // 2. No instruction operand (branch/leave/switch target) still points at a ret.
            foreach (Instruction ins in instructions)
            {
                if (ins.Operand is Instruction target)
                {
                    Assert.DoesNotContain(target, rets);
                }
                else if (ins.Operand is Instruction[] targets)
                {
                    foreach (Instruction t in targets)
                    {
                        Assert.DoesNotContain(t, rets);
                    }
                }
            }

            // 3. No exception-handler boundary still points at a ret (it would place the call
            // inside the finally region, which is invalid IL, instead of at the true method exit).
            foreach (ExceptionHandler eh in fill.Body.ExceptionHandlers)
            {
                Assert.DoesNotContain(eh.TryStart, rets);
                Assert.DoesNotContain(eh.TryEnd, rets);
                Assert.DoesNotContain(eh.HandlerStart, rets);
                Assert.DoesNotContain(eh.HandlerEnd, rets);
                if (eh.FilterStart is not null)
                {
                    Assert.DoesNotContain(eh.FilterStart, rets);
                }
            }
        }
        finally { Directory.Delete(managed, recursive: true); }
    }
}
