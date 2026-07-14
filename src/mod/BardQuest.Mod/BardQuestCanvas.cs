using BardQuest.Mod.UI;

using UnityEngine;
using UnityEngine.UIElements;

using YARG.Menu.Navigation;

namespace BardQuest.Mod;

// The full-screen BardQuest surface: a persistent UIDocument hosting a stack of screens over a shared
// forest backdrop. Each pushed screen installs its own navigation scheme; popping the last one hides the
// canvas and returns control to YARG's menu.
public sealed class BardQuestCanvas
{
    private readonly UIDocument _doc;
    private readonly VisualElement _content;
    private readonly List<IScreen> _stack = [];
    private readonly BardQuestArt _art;
    private bool _visible;

    public BardQuestCanvas(BardQuestArt art)
    {
        _art = art;
        var go = new GameObject("BardQuestCanvas");
        UnityEngine.Object.DontDestroyOnLoad(go);

        PanelSettings panel = ScriptableObject.CreateInstance<PanelSettings>();
        // Scale the UI with the screen (default is ConstantPixelSize, which renders tiny on a 4K TV).
        // The whole layout is authored against a 1920x1080 reference.
        panel.scaleMode = PanelScaleMode.ScaleWithScreenSize;
        panel.referenceResolution = new Vector2Int(1920, 1080);
        panel.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
        panel.match = 0.5f;
        _doc = go.AddComponent<UIDocument>();
        _doc.panelSettings = panel;

        VisualElement root = _doc.rootVisualElement;
        root.style.flexGrow = 1;
        root.style.backgroundColor = (Color)BardTheme.Nightwood;
        root.style.backgroundImage = new StyleBackground(_art.Backdrop());
        // YARG ships no UITK theme; supply a font or nothing renders.
        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf")
                 ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
        root.style.unityFont = font;

        _content = new VisualElement { style = { flexGrow = 1 } };
        root.Add(_content);
        root.style.display = DisplayStyle.None;
    }

    public void ShowRoot(IScreen root)
    {
        while (_stack.Count > 0)
        {
            PopInternal();
        }

        Show();
        Push(root);
    }

    public void Push(IScreen screen)
    {
        _stack.Add(screen);
        _content.Add(screen.Root);
        for (int i = 0; i < _stack.Count - 1; i++)
        {
            _stack[i].Root.style.display = DisplayStyle.None;
        }

        Navigator.Instance.PushScheme(screen.BuildScheme());
    }

    public void Pop()
    {
        PopInternal();
        if (_stack.Count == 0)
        {
            Hide();
        }
        else
        {
            _stack[^1].Root.style.display = DisplayStyle.Flex;
        }
    }

    // Prepare to launch a song into Gameplay (called from Fight). Three things matter through the
    // Menu -> Gameplay transition:
    //   1. Our screens' NavigationSchemes must come OFF YARG's shared Navigator stack cleanly, in sync
    //      with our own _stack. The Menu scene fully unloads on launch, and YARG's MainMenu.OnDisable
    //      pops whatever is on top of that stack — so any scheme of ours left on top would be popped out
    //      from under us, desyncing the two stacks. We pop our own first.
    //   2. The menu MusicPlayer must stay silent, or its random track bleeds over the song. YARG keys the
    //      MusicPlayer off the TOP scheme's AllowsMusicPlayer, so we push a single guard scheme with it
    //      false. MainMenu.OnDisable pops this guard as the Menu scene unloads.
    //   3. We deliberately DO NOT hide the canvas here — it keeps showing (now just the forest backdrop,
    //      the screens having been popped) so YARG's main menu is never revealed for a jarring flash
    //      before the song. The canvas is hidden only once the Gameplay scene is up (HideOverlay, called
    //      from BardQuestManager.OnSceneLoaded), and re-opened fresh from an empty _stack on return.
    public void PrepareForLaunch()
    {
        while (_stack.Count > 0)
        {
            PopInternal();
        }

        Navigator.Instance.PushScheme(new NavigationScheme(new List<NavigationScheme.Entry>(), false));
    }

    // Hide the canvas as the launched Gameplay scene comes up, so the DontDestroyOnLoad UIDocument does
    // not render over the game. No scheme changes — the guard from PrepareForLaunch is torn down by YARG.
    public void HideOverlay() => Hide();

    private void PopInternal()
    {
        if (_stack.Count == 0)
        {
            return;
        }

        IScreen top = _stack[^1];
        _stack.RemoveAt(_stack.Count - 1);
        _content.Remove(top.Root);
        Navigator.Instance.PopScheme();
    }

    private void Show()
    {
        if (_visible)
        {
            return;
        }

        _doc.rootVisualElement.style.display = DisplayStyle.Flex;
        _visible = true;
    }

    private void Hide()
    {
        if (!_visible)
        {
            return;
        }

        _doc.rootVisualElement.style.display = DisplayStyle.None;
        _visible = false;
    }
}
