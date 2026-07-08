namespace BardQuest.Updater.Core.Compatibility;

// Decides whether a mod build (compiled against a specific YARG version) may be
// installed into a given YARG install, comparing the mod's baked YargTarget to
// the install's tag. Unknown on either side is "unverified" (allowed), not blocked.
public static class YargCompat
{
    // Trim, drop a leading 'v'/'V', lowercase. Blank -> null.
    public static string? Normalize(string? tag)
    {
        if (string.IsNullOrWhiteSpace(tag))
        {
            return null;
        }

        string s = tag.Trim();
        if (s.Length > 0 && (s[0] == 'v' || s[0] == 'V'))
        {
            s = s[1..];
        }

        return s.ToLowerInvariant();
    }

    public static Compatibility Evaluate(string? modTarget, string? installTag)
    {
        string? a = Normalize(modTarget);
        string? b = Normalize(installTag);
        return a is null || b is null ? Compatibility.Unverified : a == b ? Compatibility.Compatible : Compatibility.Incompatible;
    }
}
