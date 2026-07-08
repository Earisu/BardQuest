namespace BardQuest.Updater.Core.Patching;

// The version + YARG-target markers baked into BardQuest.Mod.dll as AssemblyMetadata.
public readonly record struct ModAssemblyInfo(string? ModVersion, string? YargTarget);
