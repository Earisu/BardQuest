using System.Globalization;

namespace BardQuest.Updater;

// Parses/compares BardQuest release tags (semver with optional leading 'v').
public static class SemVer
{
    public static bool TryParse(string tag, out (int Major, int Minor, int Patch) version)
    {
        version = default;
        if (string.IsNullOrWhiteSpace(tag))
        {
            return false;
        }

        string s = tag.Trim();
        if (s.Length > 0 && (s[0] == 'v' || s[0] == 'V'))
        {
            s = s[1..];
        }

        string[] parts = s.Split('.');
        if (parts.Length != 3)
        {
            return false;
        }

        if (int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out int major)
            && int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out int minor)
            && int.TryParse(parts[2], NumberStyles.None, CultureInfo.InvariantCulture, out int patch))
        {
            version = (major, minor, patch);
            return true;
        }

        return false;
    }

    public static int Compare(string a, string b)
    {
        if (!TryParse(a, out (int Major, int Minor, int Patch) va))
        {
            throw new FormatException($"Not a semver tag: '{a}'");
        }

        if (!TryParse(b, out (int Major, int Minor, int Patch) vb))
        {
            throw new FormatException($"Not a semver tag: '{b}'");
        }

        int major = va.Major.CompareTo(vb.Major);
        if (major != 0)
        {
            return major;
        }

        int minor = va.Minor.CompareTo(vb.Minor);
        return minor != 0 ? minor : va.Patch.CompareTo(vb.Patch);
    }

    public static bool IsNewer(string candidate, string baseline) => Compare(candidate, baseline) > 0;
}
