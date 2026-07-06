using Mono.Cecil;
using Mono.Cecil.Cil;

using Xunit;

using BardQuest.Updater;

namespace BardQuest.Updater.Tests;

public class SeamPatcherTests
{
    // Builds a synthetic assembly with a MainMenu-shaped type + OnEnable, patches it, asserts the seam.
    private static string BuildSyntheticManagedDir(string root)
    {
        _ = Directory.CreateDirectory(root);
        var asm = AssemblyDefinition.CreateAssembly(
            new AssemblyNameDefinition("Assembly-CSharp", new Version(1, 0)), "Assembly-CSharp", ModuleKind.Dll);
        ModuleDefinition module = asm.MainModule;

        var mainMenu = new TypeDefinition("YARG.Menu.Main", "MainMenu",
            TypeAttributes.Public | TypeAttributes.Class, module.TypeSystem.Object);
        module.Types.Add(mainMenu);

        var onEnable = new MethodDefinition("OnEnable",
            MethodAttributes.Public | MethodAttributes.HideBySig, module.TypeSystem.Void);
        ILProcessor il = onEnable.Body.GetILProcessor();
        il.Append(il.Create(OpCodes.Ret));
        mainMenu.Methods.Add(onEnable);

        // A stand-in Bootstrap type so the patcher has a call target to import.
        var bootstrap = new TypeDefinition("BardQuest", "Bootstrap",
            TypeAttributes.Public | TypeAttributes.Class, module.TypeSystem.Object);
        var onMenu = new MethodDefinition("OnMainMenuEnabled",
            MethodAttributes.Public | MethodAttributes.Static, module.TypeSystem.Void);
        onMenu.Parameters.Add(new ParameterDefinition(mainMenu));
        ILProcessor bil = onMenu.Body.GetILProcessor();
        bil.Append(bil.Create(OpCodes.Ret));
        bootstrap.Methods.Add(onMenu);
        module.Types.Add(bootstrap);

        asm.Write(Path.Combine(root, "Assembly-CSharp.dll"));
        return root;
    }

    [Fact]
    public void Patch_InjectsCallAndMarker_AndIsIdempotent()
    {
        string dir = BuildSyntheticManagedDir(Path.Combine(Path.GetTempPath(), "bq-seam-" + Guid.NewGuid()));
        try
        {
            SeamPatcher.Patch(dir);

            using var patched = ModuleDefinition.ReadModule(Path.Combine(dir, "Assembly-CSharp.dll"));
            Assert.True(SeamPatcher.IsPatched(patched));

            MethodDefinition onEnable = patched.GetType("YARG.Menu.Main.MainMenu").Methods.Single(m => m.Name == "OnEnable");
            Instruction first = onEnable.Body.Instructions[0];
            Instruction call = onEnable.Body.Instructions[1];
            Assert.Equal(OpCodes.Ldarg_0, first.OpCode);
            Assert.Equal(OpCodes.Call, call.OpCode);
            Assert.Equal("OnMainMenuEnabled", ((MethodReference)call.Operand).Name);

            // Backup exists, and re-running does not double-inject (patches from pristine backup).
            Assert.True(File.Exists(Path.Combine(dir, "Assembly-CSharp.dll.bardquest-bak")));
            SeamPatcher.Patch(dir);
            using var repatched = ModuleDefinition.ReadModule(Path.Combine(dir, "Assembly-CSharp.dll"));
            MethodDefinition oe2 = repatched.GetType("YARG.Menu.Main.MainMenu").Methods.Single(m => m.Name == "OnEnable");
            Assert.Equal(OpCodes.Ldarg_0, oe2.Body.Instructions[0].OpCode);
            Assert.Equal(OpCodes.Call, oe2.Body.Instructions[1].OpCode);
            // Exactly one call to our method.
            Assert.Equal(1, oe2.Body.Instructions.Count(i =>
                i.OpCode == OpCodes.Call && ((MethodReference)i.Operand).Name == "OnMainMenuEnabled"));
        }
        finally { Directory.Delete(dir, recursive: true); }
    }
}