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
