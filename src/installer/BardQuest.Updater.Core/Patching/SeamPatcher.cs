using Mono.Cecil;
using Mono.Cecil.Cil;

namespace BardQuest.Updater.Core.Patching;

public static class SeamPatcher
{
    private const string MarkerNamespace = "BardQuest";
    // Idempotency/identity marker for the patched DLL. Kept stable during development — we reset the
    // sandbox to pristine (restore) and re-patch rather than relying on version-migration. Bump only at
    // a real release when the injected seam shape changes, so shipped installs re-patch from backup.
    private const string MarkerType = "BardQuestSeam_v2";
    private const string BootstrapType = "BardQuest.Bootstrap";
    private const string BootstrapMethod = "OnMainMenuEnabled";
    private const string TargetType = "YARG.Menu.Main.MainMenu";
    private const string TargetMethod = "OnEnable";

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

        // Inject at the END of OnEnable (just before its ret), NOT the start. MainMenu.OnEnable pushes
        // YARG's own navigation scheme in its body; by running our bootstrap after that push, the mod can
        // synchronously re-open its Hub (on the return-from-song path) and have its scheme land ON TOP of
        // the main menu's — no fragile one-frame deferral, and the canvas shows before the first render so
        // there is no menu flash. For a trivial ret-only body (the synthetic test) this is identical to
        // injecting at the start. Branch-free bodies like MainMenu.OnEnable have no jump targeting the ret.
        ILProcessor il = onEnable.Body.GetILProcessor();
        Instruction ret = onEnable.Body.Instructions[^1];
        il.InsertBefore(ret, il.Create(OpCodes.Ldarg_0));
        il.InsertBefore(ret, il.Create(OpCodes.Call, bootstrapRef));

        // Idempotency marker.
        module.Types.Add(new TypeDefinition(MarkerNamespace, MarkerType,
            TypeAttributes.NotPublic | TypeAttributes.Class, module.TypeSystem.Object));

        module.Write(live);
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

        // 2. An OLDER BardQuest marker is present → this is a marker-version field update. Restore the
        //    pristine DLL from backup, then re-patch, so the seam is never injected twice on top of an
        //    already-seamed live DLL (and any seam dropped between versions is cleanly gone).
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
