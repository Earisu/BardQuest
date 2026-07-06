using UnityEngine;
using YARG.Menu.Main;

namespace BardQuest.Mod
{
    // Persistent lifecycle owner. Created once; survives scene loads.
    public sealed class BardQuestManager : MonoBehaviour
    {
        public static BardQuestManager Instance { get; private set; }

        public static BardQuestManager EnsureCreated()
        {
            if (Instance != null) return Instance;
            var go = new GameObject("BardQuestManager");
            DontDestroyOnLoad(go);
            Instance = go.AddComponent<BardQuestManager>();
            ModLog.Info("Manager created.");
            return Instance;
        }

        // Called on every MainMenu.OnEnable. Ensures the BardQuest entry is present (Task 4 fills this in).
        public void OnMainMenuEnabled(MainMenu mainMenu)
        {
            MainMenuEntry.Ensure(this, mainMenu);
        }

        private BardQuestCanvas _canvas;

        public void OpenCanvas()
        {
            _canvas ??= new BardQuestCanvas();
            _canvas.Show();
        }
    }
}
