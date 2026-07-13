using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

using UnityEngine;

// Alias, not a plain `using BardQuest.Domain.Quest;`: this file lives in the BardQuest.Mod.Quest
// namespace, so a bare `Quest` binds to the enclosing namespace itself (CS0118), not the Domain record.
using DomainQuest = BardQuest.Domain.Quest.Quest;

namespace BardQuest.Mod.Quest;

// Reads/writes BardQuest's quest saves as human-readable JSON under <persistentDataPath>/bardquest/.
// A format-version marker gates forward migration. Atomic temp+rename so a crash mid-write can't corrupt
// the save set. Only ever writes under bardquest/ — never YARG's own data.
public static class QuestStore
{
    private const int FormatVersion = 1;

    private sealed class SaveFile
    {
        public int Version { get; set; } = FormatVersion;
        public List<DomainQuest> Quests { get; set; } = [];
    }

    private static readonly JsonSerializerSettings Settings = new()
    {
        Formatting = Formatting.Indented,
        ContractResolver = new Newtonsoft.Json.Serialization.CamelCasePropertyNamesContractResolver(),
        Converters = { new StringEnumConverter() },
    };

    private static string Path()
    {
        string dir = System.IO.Path.Combine(Application.persistentDataPath, "bardquest");
        _ = Directory.CreateDirectory(dir);
        return System.IO.Path.Combine(dir, "saves.json");
    }

    public static IReadOnlyList<DomainQuest> Load(Guid profileId)
        => [.. ReadAll().Where(q => q.ProfileId == profileId)];

    // Every profile's quests live in one saves.json. Reads back the full set so a per-profile Save can
    // preserve the other profiles' quests instead of clobbering them.
    private static IReadOnlyList<DomainQuest> ReadAll()
    {
        string path = Path();
        if (!File.Exists(path))
        {
            return [];
        }

        try
        {
            SaveFile? file = JsonConvert.DeserializeObject<SaveFile>(File.ReadAllText(path), Settings);
            if (file == null || file.Version != FormatVersion)
            {
                return []; // unknown/older format → treat as empty (migration lever; no destructive rewrite)
            }

            return file.Quests;
        }
        catch (Exception ex)
        {
            ModLog.Error("QuestStore load failed: " + ex);
            return [];
        }
    }

    // Replaces the complete quest set for one profile, leaving every other profile's quests untouched.
    // Callers pass this profile's full list (from Load(profileId), then modified); we re-merge it with the
    // quests that belong to other profiles so a play on one YARG profile can't erase another's saves.
    public static void Save(Guid profileId, IReadOnlyList<DomainQuest> quests)
    {
        IReadOnlyList<DomainQuest> merged =
        [
            .. ReadAll().Where(q => q.ProfileId != profileId),
            .. quests,
        ];

        string path = Path();
        string tmp = path + ".tmp";
        var file = new SaveFile { Version = FormatVersion, Quests = [.. merged] };
        File.WriteAllText(tmp, JsonConvert.SerializeObject(file, Settings));
        if (File.Exists(path))
        {
            File.Delete(path);
        }

        File.Move(tmp, path);
    }
}
