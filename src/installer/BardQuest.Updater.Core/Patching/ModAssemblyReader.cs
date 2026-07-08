using Mono.Cecil;

namespace BardQuest.Updater.Core.Patching;

// Reads the BardQuestModVersion / BardQuestYargTarget assembly-metadata markers
// from a BardQuest mod DLL using Mono.Cecil (no managed load of the assembly).
public static class ModAssemblyReader
{
    private const string MetadataAttribute = "System.Reflection.AssemblyMetadataAttribute";
    public const string ModVersionKey = "BardQuestModVersion";
    public const string YargTargetKey = "BardQuestYargTarget";

    // Reads both markers from dllPath. Returns default (both null) if the file is
    // absent or unreadable, or for any marker not present.
    public static ModAssemblyInfo Read(string dllPath)
    {
        if (!File.Exists(dllPath))
        {
            return default;
        }

        try
        {
            using var asm = AssemblyDefinition.ReadAssembly(dllPath);
            string? modVersion = null;
            string? yargTarget = null;

            foreach (CustomAttribute attr in asm.CustomAttributes)
            {
                if (attr.AttributeType.FullName != MetadataAttribute
                    || attr.ConstructorArguments.Count != 2)
                {
                    continue;
                }

                string? key = attr.ConstructorArguments[0].Value as string;
                string? value = NullIfBlank(attr.ConstructorArguments[1].Value as string);
                if (key == ModVersionKey)
                {
                    modVersion = value;
                }
                else if (key == YargTargetKey)
                {
                    yargTarget = value;
                }
            }

            return new ModAssemblyInfo(modVersion, yargTarget);
        }
        catch (Exception ex) when (ex is IOException or BadImageFormatException or UnauthorizedAccessException)
        {
            return default;
        }
    }

    private static string? NullIfBlank(string? s) => string.IsNullOrWhiteSpace(s) ? null : s;
}
