using BardQuest.Domain.Quest;
using BardQuest.Mod.Quest;

using UnityEngine;
using UnityEngine.SceneManagement;

using YARG.Menu.Main;
// Alias, not a plain `using BardQuest.Domain.Quest;` type reference: this file lives in namespace
// BardQuest.Mod, and BardQuest.Mod.Quest is a nested sub-namespace of it, so a bare `Quest` would bind
// to that sub-namespace (CS0118) rather than the Domain record — same pitfall documented in QuestStore.cs.
using DomainQuest = BardQuest.Domain.Quest.Quest;

namespace BardQuest.Mod;

// Persistent lifecycle owner. Created once; survives scene loads.
public sealed class BardQuestManager : MonoBehaviour
{
    public static BardQuestManager Instance { get; private set; }

    public static BardQuestManager EnsureCreated()
    {
        if (Instance != null)
        {
            return Instance;
        }

        var go = new GameObject("BardQuestManager");
        DontDestroyOnLoad(go);
        Instance = go.AddComponent<BardQuestManager>();
        Instance._scores = new ScoreSource();
        Instance._launcher = new QuestLauncher(Instance._scores);
        Instance.Controller = new QuestController(Instance._scores, Instance._launcher);
        // Subscribe here (not in an OnEnable/OnDisable pair): this is a DontDestroyOnLoad singleton, created
        // once and never disabled, so it never needs to unsubscribe — and keeping the subscription off Unity
        // magic methods stops `dotnet format` (IDE0051) from deleting them as "unused private members".
        SceneManager.sceneLoaded += Instance.OnSceneLoaded;
        ModLog.Info("Manager created.");
        return Instance;
    }

    // Called on every MainMenu.OnEnable. Ensures the BardQuest entry is present (Task 4 fills this in).
    public void OnMainMenuEnabled(MainMenu mainMenu) => MainMenuEntry.Ensure(this, mainMenu);

    // The read/launch orchestrator the UITK screens call (Tasks 6-9).
    public QuestController Controller { get; private set; }

    private UI.BardQuestArt _art;
    private BardQuestCanvas _canvas;

    public void OpenCanvas()
    {
        // Rate on demand: entering BardQuest is what triggers the (fire-and-forget) rating build, so
        // YARG's own launch/scan is never burdened with it. EnsureRatings self-guards and never throws.
        Scan.ScanService.EnsureRatings();
        _art ??= new UI.BardQuestArt();
        _canvas ??= new BardQuestCanvas(_art);
        _canvas.ShowRoot(new UI.PlaceholderScreen(_canvas)); // replaced by SavesScreen in Task 7
    }

    private QuestLauncher _launcher;
    private ScoreSource _scores;

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // Ignore the scene load INTO Gameplay — that is the launch itself, not a return from it. Only a
        // scene load AWAY from Gameplay (Score on finish, Menu on quit) is a "return" worth correlating.
        // Without this guard, Correlate() would fire the instant Launch()'s own LoadScene(Gameplay) call
        // completes (sceneLoaded fires for the newly-loaded Gameplay scene too), clearing Pending before
        // the player has even played the song.
        if (scene.buildIndex == (int)YARG.SceneIndex.Gameplay || _launcher?.Pending == null)
        {
            return;
        }

        ProvenanceLink link = _launcher.Correlate();
        DomainQuest active = Controller?.ActiveQuest;
        if (link == null || active == null)
        {
            return; // quit/unfinished/invalid → no credit
        }

        try
        {
            DomainQuest updated = QuestProgression.Record(active, link, QuestController.LibraryFor(active), _scores);
            IReadOnlyList<DomainQuest> all =
            [
                .. QuestStore.Load(updated.ProfileId)
                                .Where(q => q.Id != updated.Id),
                updated,
            ];
            QuestStore.Save(all);
            Controller.Adopt(updated); // keep the controller's ActiveQuest current for further plays
            ModLog.Info($"Quest {updated.Id} recorded a linked play (now {updated.Links.Count} links).");
        }
        catch (Exception ex)
        {
            ModLog.Error("Quest record-on-return failed: " + ex);
        }
    }
}
