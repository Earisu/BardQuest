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

    // Called at the END of every MainMenu.OnEnable (Cecil seam, injected after YARG's own PushScheme).
    // Ensures the BardQuest entry is present, and — when we are returning from a Fight — records the play
    // and re-opens the Hub automatically.
    public void OnMainMenuEnabled(MainMenu mainMenu)
    {
        MainMenuEntry.Ensure(this, mainMenu);

        // A launch is pending only between a Fight and the first main-menu enable after that play. This is
        // the robust return signal: MainMenu.OnEnable always fires when the Menu scene comes back (on the
        // completed-song path AND the quit path), and never on the intermediate Score scene. By now the
        // play's score is written to scores.db, so we can correlate and record it.
        if (_launcher?.Pending == null)
        {
            return;
        }

        // Capture the fought monster's hash before Correlate clears Pending, so the reopened Hub can put
        // the cursor back on it (like YARG's library keeps its position across a song).
        string foughtHash = _launcher.Pending.Value.SongHashHex;
        DomainQuest resume = RecordReturn();
        if (resume != null)
        {
            // The seam runs AFTER MainMenu.OnEnable has pushed YARG's nav scheme, so re-opening here lands
            // our Hub scheme ON TOP of the main menu's (input + music go to us). It also runs during scene
            // activation, before the first frame renders, so the canvas covers the menu with no flash.
            ReopenToHub(resume, foughtHash);
        }
    }

    // Correlate + record the just-finished play and return the quest to re-open (the updated quest on a
    // credited play, else the still-active quest on a quit/unfinished return). Clears the pending launch.
    private DomainQuest RecordReturn()
    {
        ProvenanceLink link = _launcher.Correlate();
        DomainQuest active = Controller?.ActiveQuest;
        if (link == null || active == null)
        {
            return active; // quit/unfinished/invalid → no credit, but still re-open the Hub
        }

        try
        {
            DomainQuest updated = QuestProgression.Record(active, link, QuestController.LibraryFor(active), _scores);
            QuestStore.Upsert(updated); // in-place replace: keeps the quest's slot, preserves other profiles
            Controller.Adopt(updated); // keep the controller's ActiveQuest current for further plays
            ModLog.Info($"Quest {updated.Id} recorded a linked play (now {updated.Links.Count} links).");
            return updated;
        }
        catch (Exception ex)
        {
            ModLog.Error("Quest record-on-return failed: " + ex);
            return active;
        }
    }

    // The read/launch orchestrator the UITK screens call.
    public QuestController Controller { get; private set; }

    private UI.BardQuestArt _art;
    private BardQuestCanvas _canvas;
    private SongEnricher _enricher; // BardQuest.Mod.Quest.SongEnricher, resolved via the `using BardQuest.Mod.Quest;` above
    private SongPreviewPlayer _preview;

    public void OpenCanvas()
    {
        // Rate on demand: entering BardQuest is what triggers the (fire-and-forget) rating build, so
        // YARG's own launch/scan is never burdened with it. EnsureRatings self-guards and never throws.
        Scan.ScanService.EnsureRatings();
        _art ??= new UI.BardQuestArt();
        _canvas ??= new BardQuestCanvas(_art);
        ShowSaves();
    }

    private void ShowSaves()
    {
        _canvas.ShowRoot(new UI.SavesScreen(
            _canvas, Controller, _art,
            openHub: ShowHub,
            openCreate: ShowCreate));
    }

    private void ShowCreate() => _canvas.Push(new UI.CreateQuestScreen(_canvas, Controller, _art, openHub: ShowHub));

    private void ShowHub(DomainQuest quest) => ShowHub(quest, null);

    private void ShowHub(DomainQuest quest, string selectHash)
    {
        _enricher ??= new SongEnricher();
        _preview ??= new SongPreviewPlayer();
        _canvas.Push(new UI.HubScreen(_canvas, Controller, _enricher, _preview, _art, quest, selectHash));
    }

    // Re-open BardQuest on a quest's Hub after returning from a Fight: rebuild the roster as the base
    // screen (so Back from the Hub lands there) then push the Hub for the quest just played, refreshed,
    // with the cursor restored to the monster just fought.
    private void ReopenToHub(DomainQuest quest, string selectHash)
    {
        ShowSaves();
        ShowHub(quest, selectHash);
    }

    private QuestLauncher _launcher;
    private ScoreSource _scores;

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // The only scene-load we care about is the launched song's Gameplay scene coming up: hide our
        // (DontDestroyOnLoad) canvas so it stops rendering over the game. We keep it visible through the
        // Menu -> Gameplay transition on purpose (showing the forest backdrop, covering YARG's main menu)
        // so there is no menu flash before the song. Recording + re-opening on return is driven from
        // OnMainMenuEnabled, not here.
        if (scene.buildIndex == (int)YARG.SceneIndex.Gameplay)
        {
            _canvas?.HideOverlay();
            _preview?.Stop(); // insurance: never let a song preview bleed into gameplay
            // Free the Hub's album textures now that its screens are gone (popped by PrepareForLaunch); they
            // are Unity native objects the GC won't reclaim, and the Hub re-enriches its small set on return.
            _enricher?.Teardown();
        }
    }
}
