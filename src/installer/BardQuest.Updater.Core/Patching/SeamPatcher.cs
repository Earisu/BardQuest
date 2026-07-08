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

        MethodReference bootstrapRef = ResolveBootstrapMethod(module, resolver, managedDir, target);

        ILProcessor il = onEnable.Body.GetILProcessor();
        Instruction first = onEnable.Body.Instructions[0];
        il.InsertBefore(first, il.Create(OpCodes.Ldarg_0));
        il.InsertBefore(first, il.Create(OpCodes.Call, bootstrapRef));

        // Second seam: kick BardQuest's rating build at the end of YARG's library refresh.
        MethodDefinition fillContainers = module.GetType(ScanTargetType)
            ?.Methods.SingleOrDefault(m => m.Name == ScanTargetMethod && m.Parameters.Count == 0)
            ?? throw new InvalidOperationException($"Method not found: {ScanTargetType}::{ScanTargetMethod}()");

        MethodReference onRefreshRef = ResolveMod0ArgMethod(module, resolver, managedDir, ScanBootstrapMethod);

        ILProcessor scanIl = fillContainers.Body.GetILProcessor();
        foreach (Instruction ret in fillContainers.Body.Instructions.Where(i => i.OpCode == OpCodes.Ret).ToList())
        {
            scanIl.InsertBefore(ret, scanIl.Create(OpCodes.Call, onRefreshRef));
        }

        // Idempotency marker.
        module.Types.Add(new TypeDefinition(MarkerNamespace, MarkerType,
            TypeAttributes.NotPublic | TypeAttributes.Class, module.TypeSystem.Object));

        module.Write(live);
    }

    private static MethodReference ResolveBootstrapMethod(
        ModuleDefinition module, IAssemblyResolver resolver, string managedDir, TypeDefinition mainMenu)
    {
        // Prefer the real BardQuest.Mod.dll deployed alongside; fall back to a same-module Bootstrap
        // (the synthetic test assembly defines Bootstrap in-module).
        string modPath = Path.Combine(managedDir, "BardQuest.Mod.dll");
        if (File.Exists(modPath))
        {
            var mod = ModuleDefinition.ReadModule(modPath, new ReaderParameters { AssemblyResolver = resolver });
            TypeDefinition bootstrap = mod.GetType(BootstrapType)
                ?? throw new InvalidOperationException($"Type not found in BardQuest.Mod: {BootstrapType}");
            MethodDefinition method = bootstrap.Methods.Single(m => m.Name == BootstrapMethod && m.Parameters.Count == 1);
            return module.ImportReference(method);
        }

        TypeDefinition inModule = module.GetType(BootstrapType);
        return inModule != null
            ? module.ImportReference(inModule.Methods.Single(m => m.Name == BootstrapMethod))
            : throw new InvalidOperationException("BardQuest.Mod.dll not found and no in-module Bootstrap.");
    }

    private static MethodReference ResolveMod0ArgMethod(
        ModuleDefinition module, IAssemblyResolver resolver, string managedDir, string methodName)
    {
        string modPath = Path.Combine(managedDir, "BardQuest.Mod.dll");
        if (File.Exists(modPath))
        {
            var mod = ModuleDefinition.ReadModule(modPath, new ReaderParameters { AssemblyResolver = resolver });
            TypeDefinition bootstrap = mod.GetType(BootstrapType)
                ?? throw new InvalidOperationException($"Type not found in BardQuest.Mod: {BootstrapType}");
            MethodDefinition method = bootstrap.Methods.Single(m => m.Name == methodName && m.Parameters.Count == 0);
            return module.ImportReference(method);
        }

        TypeDefinition inModule = module.GetType(BootstrapType)
            ?? throw new InvalidOperationException("BardQuest.Mod.dll not found and no in-module Bootstrap.");
        return module.ImportReference(inModule.Methods.Single(m => m.Name == methodName && m.Parameters.Count == 0));
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
        if (IsManagedDirPatched(managedDir)) // already carries the current (v2) marker
        {
            return;
        }

        string live = Path.Combine(managedDir, "Assembly-CSharp.dll");
        string backup = live + ".bardquest-bak";

        if (HasAnyBardQuestMarker(live) && File.Exists(backup))
        {
            // Our own OLDER patch (e.g. v1) over a pristine backup: restore the pristine DLL, then
            // patch it fresh — never inject on top of an already-patched live (would double the seam).
            Restore(managedDir);
            Patch(managedDir);
            return;
        }

        // No BardQuest marker: a fresh (launcher-replaced) build. Discard any stale backup so the
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
