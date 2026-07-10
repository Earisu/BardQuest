using BardQuest.Domain.Ratings;

using UnityEngine;

namespace BardQuest.Mod.Scan;

// Resolves BardQuest's own cache file under YARG's persistent data dir and (de)serializes it via
// the pure Domain RatingCache. BardQuest only ever writes under <persistentDataPath>/bardquest/.
public static class RatingCacheFile
{
    public static string Path()
    {
        string dir = System.IO.Path.Combine(Application.persistentDataPath, "bardquest");
        _ = Directory.CreateDirectory(dir);
        return System.IO.Path.Combine(dir, "ratingcache.bin");
    }

    public static Dictionary<string, List<ChartMetrics>> Load()
    {
        string path = Path();
        if (!File.Exists(path))
        {
            return [];
        }

        using FileStream fs = File.OpenRead(path);
        return RatingCache.Deserialize(fs) ?? [];
    }

    public static void Save(IReadOnlyDictionary<string, IReadOnlyList<ChartMetrics>> byHash) => Save(Path(), byHash);

    // Path must be resolved on the main thread (Application.persistentDataPath is a main-thread-only
    // Unity API) and passed in — this overload itself never touches Path()/Application, so it's safe
    // to call from a background build thread.
    public static void Save(string path, IReadOnlyDictionary<string, IReadOnlyList<ChartMetrics>> byHash)
    {
        string tmp = path + ".tmp";
        using (FileStream fs = File.Create(tmp))
        {
            RatingCache.Serialize(byHash, fs);
        }

        if (File.Exists(path))
        {
            File.Delete(path);
        }

        File.Move(tmp, path); // atomic-ish replace so a crash mid-write can't corrupt the cache
    }
}
