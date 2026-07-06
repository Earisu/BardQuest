extern alias yargpkg;
using UnityEngine;
using UnityEngine.UIElements;

using YARG.Menu.Navigation;

using MenuAction = yargpkg::YARG.Core.Input.MenuAction;

namespace BardQuest.Mod;

public sealed class BardQuestCanvas
{
    private static readonly Color32 Bg = new(0x14, 0x10, 0x22, 0xF2);   // BardQuest dark
    private static readonly Color32 Fg = new(0xE8, 0xE2, 0xF7, 0xFF);

    private readonly GameObject _go;
    private readonly UIDocument _doc;
    private NavigationScheme _scheme;
    private bool _visible;

    public BardQuestCanvas()
    {
        _go = new GameObject("BardQuestCanvas");
        UnityEngine.Object.DontDestroyOnLoad(_go);

        PanelSettings panel = ScriptableObject.CreateInstance<PanelSettings>();
        _doc = _go.AddComponent<UIDocument>();
        _doc.panelSettings = panel;

        VisualElement root = _doc.rootVisualElement;
        root.style.flexGrow = 1;
        root.style.backgroundColor = (Color)Bg;
        // YARG ships no UITK theme; supply an explicit font or nothing renders.
        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf")
                   ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
        root.style.unityFont = font;

        var title = new Label("BARDQUEST") { style =
            {
                color = (Color)Fg, fontSize = 48, unityFontStyleAndWeight = FontStyle.Bold, marginTop = 60,
                marginLeft = 60
            }
        };
        root.Add(title);

        var hint = new Label("Empty canvas — Phase 0. Press Back to return.") { style =
            {
                color = (Color)Fg, fontSize = 22, marginLeft = 60
            }
        };
        root.Add(hint);

        root.style.display = DisplayStyle.None;
    }

    public void Show()
    {
        if (_visible)
        {
            return;
        }

        _doc.rootVisualElement.style.display = DisplayStyle.Flex;
        _scheme = new NavigationScheme(
        [
            new NavigationScheme.Entry(MenuAction.Red, "Menu.Common.Back", Hide),
        ], false);
        Navigator.Instance.PushScheme(_scheme);
        _visible = true;
    }

    public void Hide()
    {
        if (!_visible)
        {
            return;
        }

        Navigator.Instance.PopScheme();
        _doc.rootVisualElement.style.display = DisplayStyle.None;
        _visible = false;
    }
}
