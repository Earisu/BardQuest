extern alias yargpkg;

using BardQuest.Domain.Quest;
using BardQuest.Mod.Quest;

using UnityEngine;
using UnityEngine.UIElements;

using YARG.Menu.Navigation;

using DomainQuest = BardQuest.Domain.Quest.Quest;
using MenuAction = yargpkg::YARG.Core.Input.MenuAction;

namespace BardQuest.Mod.UI;

// The Saves entry screen: three Zelda-style slots (horizontal, Up/Down cycles). Filled slots show quest
// standing; empty slots offer Create. Selection is index-based so it works on instruments without a dpad.
public sealed class SavesScreen : IScreen
{
    private const int SlotCount = 3;

    private readonly BardQuestCanvas _canvas;
    private readonly QuestController _controller;
    private readonly BardQuestArt _art;
    private readonly Action<DomainQuest> _openHub;
    private readonly Action _openCreate;
    private readonly VisualElement[] _slotViews = new VisualElement[SlotCount];
    private readonly DomainQuest?[] _slotQuests = new DomainQuest?[SlotCount];
    private int _selected;

    public VisualElement Root { get; }

    public SavesScreen(
        BardQuestCanvas canvas, QuestController controller, BardQuestArt art,
        Action<DomainQuest> openHub, Action openCreate)
    {
        _canvas = canvas;
        _controller = controller;
        _art = art;
        _openHub = openHub;
        _openCreate = openCreate;

        Root = new VisualElement
        {
            style =
            {
                flexGrow = 1, flexDirection = FlexDirection.Column,
                alignItems = Align.Center, justifyContent = Justify.Center,
            },
        };
        Root.Add(new Label("YOUR QUESTS")
        {
            style = { color = (Color)BardTheme.Gilt, fontSize = 40, marginBottom = 24, unityFontStyleAndWeight = FontStyle.Bold },
        });

        var row = new VisualElement { style = { flexDirection = FlexDirection.Row } };
        Root.Add(row);

        IReadOnlyList<DomainQuest> quests = _controller.Quests();
        for (int i = 0; i < SlotCount; i++)
        {
            DomainQuest? q = i < quests.Count ? quests[i] : null;
            _slotQuests[i] = q;
            VisualElement slot = BuildSlot(q);
            _slotViews[i] = slot;
            row.Add(slot);
        }

        Highlight();
    }

    private VisualElement BuildSlot(DomainQuest? quest)
    {
        var slot = new VisualElement
        {
            style =
            {
                width = 240, height = 320, marginLeft = 12, marginRight = 12,
                backgroundColor = (Color)BardTheme.Mossdeep,
                alignItems = Align.Center, justifyContent = Justify.Center,
                borderTopWidth = 3, borderBottomWidth = 3, borderLeftWidth = 3, borderRightWidth = 3,
            },
        };
        SetBorder(slot, BardTheme.OldWood);

        if (quest == null)
        {
            slot.Add(new Label("+ Create")
            {
                style = { color = (Color)BardTheme.Parchment, fontSize = 26 },
            });
            return slot;
        }

        ActiveQuestView view = _controller.Resolve(quest);
        slot.Add(new Image
        {
            image = _art.ClassMedallion(view.Class),
            style = { width = 110, height = 110 },
        });
        slot.Add(new Label($"{BardTheme.ClassName(view.Class)} {BardTheme.Roman(view.Subrank)}")
        {
            style = { color = (Color)BardTheme.Gilt, fontSize = 22, marginTop = 8 },
        });
        slot.Add(new Label($"{quest.Instrument} · {quest.Difficulty}")
        {
            style = { color = (Color)BardTheme.Parchment, fontSize = 16, marginTop = 4 },
        });
        var radar = new PentagonRadar { style = { marginTop = 8 } };
        radar.SetLevels(view.Profile.Axes);
        slot.Add(radar);
        return slot;
    }

    private void Move(int delta)
    {
        _selected = (_selected + delta + SlotCount) % SlotCount;
        Highlight();
    }

    private void Confirm()
    {
        DomainQuest? q = _slotQuests[_selected];
        if (q == null)
        {
            _openCreate();
        }
        else
        {
            _openHub(q);
        }
    }

    private void Highlight()
    {
        for (int i = 0; i < SlotCount; i++)
        {
            SetBorder(_slotViews[i], i == _selected ? BardTheme.Glowmoss : BardTheme.OldWood);
        }
    }

    private static void SetBorder(VisualElement e, Color32 c)
    {
        var col = (Color)c;
        e.style.borderTopColor = col;
        e.style.borderBottomColor = col;
        e.style.borderLeftColor = col;
        e.style.borderRightColor = col;
    }

    public NavigationScheme BuildScheme() => new(
    [
        new(MenuAction.Up, "Menu.Common.Up", () => Move(-1)),
        new(MenuAction.Down, "Menu.Common.Down", () => Move(1)),
        new(MenuAction.Green, "Menu.Common.Confirm", Confirm),
        new(MenuAction.Red, "Menu.Common.Back", _canvas.Pop),
    ], false);
}
