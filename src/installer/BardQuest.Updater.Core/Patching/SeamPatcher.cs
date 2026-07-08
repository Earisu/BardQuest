using Mono.Cecil;
using Mono.Cecil.Cil;

namespace BardQuest.Updater.Core.Patching;

public static class SeamPatcher
{
    private const string MarkerNamespace = "BardQuest";
    private const string MarkerType = "BardQuestSeam_v2";
    private const string BootstrapType = "BardQuest.Bootstrap";
    private const string BootstrapMethod = "OnMainMenuEnabled";
    private const string TargetType = "YARG.Menu.Main.MainMenu";
    private const string TargetMethod = "OnEnable";
    private const string ScanTargetType = "YARG.Song.SongContainer";
    private const string ScanTargetMethod = "FillContainers";
    private const string ScanBootstrapMethod = "OnLibraryRefreshed";

    public static bool IsPatched(ModuleDefinition module) =>
        module.Types.Any(t => t.Namespace == MarkerNamespace && t.Name == MarkerType);

    public static void Patch(string managedDir)
    {
        string live = Path.Combine(managedDir, "Assembly-CSharp.dll");
        string backup = live + ".bardquest-bak";
        if (!File.Exists(backup))
        {
            File.Copy(live, backup);
        }

        var resolver = new DefaultAssemblyResolver();
        resolver.AddSearchDirectory(managedDir);
        var readerParams = new ReaderParameters { AssemblyResolver = resolver, ReadWrite = false };

        // Always read the pristine backup so re-running patches clean.
        using var module = ModuleDefinition.ReadModule(backup, readerParams);

        TypeDefinition target = module.GetType(TargetType)
            ?? throw new InvalidOperationException($"Type not found: {TargetType}");
        MethodDefinition onEnable = target.Methods.SingleOrDefault(m => m.Name == TargetMethod && m.Parameters.Count == 0)
            ?? throw new InvalidOperationException($"Method not found: {TargetType}::{TargetMethod}()");

        MethodReference bootstrapRef = ResolveBootstrapMethod(module, resolver, managedDir, BootstrapMethod, paramCount: 1);

        ILProcessor il = onEnable.Body.GetILProcessor();
        Instruction first = onEnable.Body.Instructions[0];
        il.InsertBefore(first, il.Create(OpCodes.Ldarg_0));
        il.InsertBefore(first, il.Create(OpCodes.Call, bootstrapRef));

        // Second seam: kick BardQuest's rating build at the end of YARG's library refresh.
        MethodDefinition fillContainers = module.GetType(ScanTargetType)
            ?.Methods.SingleOrDefault(m => m.Name == ScanTargetMethod && m.Parameters.Count == 0)
            ?? throw new InvalidOperationException($"Method not found: {ScanTargetType}::{ScanTargetMethod}()");

        MethodReference onRefreshRef = ResolveBootstrapMethod(module, resolver, managedDir, ScanBootstrapMethod, paramCount: 0);

        ILProcessor scanIl = fillContainers.Body.GetILProcessor();
        RouteAllExitsThroughCall(fillContainers, scanIl, onRefreshRef);

        // Idempotency marker.
        module.Types.Add(new TypeDefinition(MarkerNamespace, MarkerType,
            TypeAttributes.NotPublic | TypeAttributes.Class, module.TypeSystem.Object));

        module.Write(live);
    }

    // Injects a call at EVERY exit of the method body, instead of naively inserting before each `ret`.
    // A naive InsertBefore(ret, call) is unreachable dead code when a `leave` (e.g. the normal exit of
    // a try/finally) jumps straight to that `ret`: the `leave` still targets the original `ret`
    // instruction, bypassing the call entirely. This routine retargets every branch/leave/switch operand
    // and every exception-handler boundary that pointed at the `ret` to point at the injected `call`
    // instead, so all control flow is forced through the call before it can reach the `ret`.
    private static void RouteAllExitsThroughCall(MethodDefinition method, ILProcessor il, MethodReference callee)
    {
        // Snapshot the `ret` list before mutating the body.
        List<Instruction> rets = [.. method.Body.Instructions.Where(i => i.OpCode == OpCodes.Ret)];
        foreach (Instruction ret in rets)
        {
            Instruction call = il.Create(OpCodes.Call, callee);
            il.InsertBefore(ret, call);

            // Retarget branch/leave/switch operands that pointed at this ret to the new call.
            foreach (Instruction ins in method.Body.Instructions)
            {
                if (ins.Operand is Instruction target && target == ret)
                {
                    ins.Operand = call;
                }
                else if (ins.Operand is Instruction[] targets)
                {
                    for (int i2 = 0; i2 < targets.Length; i2++)
                    {
                        if (targets[i2] == ret)
                        {
                            targets[i2] = call;
                        }
                    }
                }
            }

            // Retarget exception-handler boundaries that pointed at this ret to the new call, so the
            // call ends up outside every try/handler region (Try/Handler/Filter End are exclusive
            // upper bounds) rather than inside the finally/catch it was meant to run after.
            foreach (ExceptionHandler eh in method.Body.ExceptionHandlers)
            {
                if (eh.TryStart == ret)
                {
                    eh.TryStart = call;
                }

                if (eh.TryEnd == ret)
                {
                    eh.TryEnd = call;
                }

                if (eh.HandlerStart == ret)
                {
                    eh.HandlerStart = call;
                }

                if (eh.HandlerEnd == ret)
                {
                    eh.HandlerEnd = call;
                }

                if (eh.FilterStart == ret)
                {
                    eh.FilterStart = call;
                }
            }
        }
    }

    // Locates a static method on BardQuest.Bootstrap by name+arity: prefers the real BardQuest.Mod.dll
    // deployed alongside; falls back to a same-module Bootstrap (the synthetic test assembly defines
    // Bootstrap in-module).
    private static MethodReference ResolveBootstrapMethod(
        ModuleDefinition module, IAssemblyResolver resolver, string managedDir, string methodName, int paramCount)
    {
        string modPath = Path.Combine(managedDir, "BardQuest.Mod.dll");
        if (File.Exists(modPath))
        {
            var mod = ModuleDefinition.ReadModule(modPath, new ReaderParameters { AssemblyResolver = resolver });
            TypeDefinition bootstrap = mod.GetType(BootstrapType)
                ?? throw new InvalidOperationException($"Type not found in BardQuest.Mod: {BootstrapType}");
            MethodDefinition method = bootstrap.Methods.Single(m => m.Name == methodName && m.Parameters.Count == paramCount);
            return module.ImportReference(method);
        }

        TypeDefinition inModule = module.GetType(BootstrapType)
            ?? throw new InvalidOperationException("BardQuest.Mod.dll not found and no in-module Bootstrap.");
        return module.ImportReference(inModule.Methods.Single(m => m.Name == methodName && m.Parameters.Count == paramCount));
    }

    public static void Restore(string managedDir)
    {
        foreach (string backup in Directory.GetFiles(managedDir, "*.bardquest-bak"))
        {
            string live = backup[..^".bardquest-bak".Length];
            File.Copy(backup, live, overwrite: true);
            File.Delete(backup);
        }
    }

    // True if the live Assembly-CSharp.dll in managedDir carries the seam marker.
    public static bool IsManagedDirPatched(string managedDir)
    {
        string live = Path.Combine(managedDir, "Assembly-CSharp.dll");
        if (!File.Exists(live))
        {
            return false;
        }

        using var module = ModuleDefinition.ReadModule(live);
        return IsPatched(module);
    }

    // Launcher-clobber-safe AND marker-version-safe patch entry point.
    public static void EnsurePatched(string managedDir)
    {
        string live = Path.Combine(managedDir, "Assembly-CSharp.dll");
        string backup = live + ".bardquest-bak";

        // 1. Already on the current marker → nothing to do.
        if (IsManagedDirPatched(managedDir))
        {
            return;
        }

        // 2. An OLDER BardQuest marker is present → this is a v1→v2 field update. Restore the
        //    pristine DLL from backup, then re-patch, so the bootstrap seam is never injected
        //    twice on top of an already-seamed live DLL.
        if (HasAnyBardQuestMarker(live))
        {
            if (!File.Exists(backup))
            {
                throw new InvalidOperationException(
                    "BardQuest is patched into this install but its pristine backup (.bardquest-bak) is " +
                    "missing, so it cannot be safely re-patched. Reinstall BardQuest to recover.");
            }

            Restore(managedDir);
            Patch(managedDir);
            return;
        }

        // 3. No BardQuest marker: a fresh (launcher-replaced) build. Discard any stale backup so the
        // current live becomes the pristine baseline, then patch.
        if (File.Exists(backup))
        {
            File.Delete(backup);
        }

        Patch(managedDir);
    }

    private static bool HasAnyBardQuestMarker(string dll)
    {
        if (!File.Exists(dll))
        {
            return false;
        }

        using var module = ModuleDefinition.ReadModule(dll);
        return module.Types.Any(t => t.Namespace == MarkerNamespace && t.Name.StartsWith("BardQuestSeam_"));
    }
}
