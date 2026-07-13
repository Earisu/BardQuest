// netstandard2.1 lacks IsExternalInit (needed for record/init/record struct). Mirrors
// BardQuest.Domain/Compat/IsExternalInit.cs — each assembly compiling init-only members needs its own
// copy; Domain's is `internal` to its own assembly and not visible here.
namespace System.Runtime.CompilerServices;

internal static class IsExternalInit { }
