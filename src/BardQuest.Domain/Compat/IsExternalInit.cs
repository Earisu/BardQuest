// netstandard2.1 lacks IsExternalInit (needed for record/init). net10 already ships it, so this file
// is Compile-Removed for that TFM by the csproj.
namespace System.Runtime.CompilerServices
{
    internal static class IsExternalInit { }
}
