using Mono.Cecil;
using Mono.Cecil.Cil;

namespace BardQuest.Installer;

public static class SeamPatcher
{
    private const string MarkerNamespace = "BardQuest";
    private const string MarkerType = "BardQuestSeam_v1";
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

        var target = module.GetType(TargetType)
            ?? throw new InvalidOperationException($"Type not found: {TargetType}");
        var onEnable = target.Methods.SingleOrDefault(m => m.Name == TargetMethod && m.Parameters.Count == 0)
            ?? throw new InvalidOperationException($"Method not found: {TargetType}::{TargetMethod}()");

        var bootstrapRef = ResolveBootstrapMethod(module, resolver, managedDir, target);

        var il = onEnable.Body.GetILProcessor();
        var first = onEnable.Body.Instructions[0];
        il.InsertBefore(first, il.Create(OpCodes.Ldarg_0));
        il.InsertBefore(first, il.Create(OpCodes.Call, bootstrapRef));

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
            var bootstrap = mod.GetType(BootstrapType)
                ?? throw new InvalidOperationException($"Type not found in BardQuest.Mod: {BootstrapType}");
            var method = bootstrap.Methods.Single(m => m.Name == BootstrapMethod && m.Parameters.Count == 1);
            return module.ImportReference(method);
        }

        var inModule = module.GetType(BootstrapType);
        return inModule != null
            ? module.ImportReference(inModule.Methods.Single(m => m.Name == BootstrapMethod))
            : throw new InvalidOperationException("BardQuest.Mod.dll not found and no in-module Bootstrap.");
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
}
