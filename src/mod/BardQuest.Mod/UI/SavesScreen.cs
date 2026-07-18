extern alias yargpkg;

using BardQuest.Domain.Quest;
using BardQuest.Mod.Quest;

using UnityEngine;
using UnityEngine.UIElements;

using YARG.Menu.Navigation;

using DomainQuest = BardQuest.Domain.Quest.Quest;
using MenuAction = yargpkg::YARG.Core.Input.MenuAction;

namespace BardQuest.Mod.UI;

// The Saves entry screen: three Zelda-style slots in a horizontal row, Left/Right cycles. Filled slots
// show quest standing; empty slots offer Create. Selection is index-based so it works on instruments
// without a dpad. The shell's AppHeader owns the logo and title.
public sealed class SavesScreen : IScreen
{
    private const int SlotCount = 3;

    private readonly BardQuestCanvas _canvas;
    private readonly QuestController _controller;
    private readonly BardQuestArt _art;
    private readonly Action<DomainQuest> _openHub;
    private readonly Action _openCreate;
    private readonly VisualElement[] _slotViews = new VisualElement[SlotCount];
    private readonly VisualElement[] _slotGlows = new VisualElement[SlotCount];
    private readonly DomainQuest?[] _slotQuests = new DomainQuest?[SlotCount];
    private int _selected;

    public VisualElement Root { get; }

    public string Title => "Your Quests";

    public bool ShowsAppHeader => true;

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
        var row = new VisualElement { style = { flexDirection = FlexDirection.Row } };
        Root.Add(row);

        IReadOnlyList<DomainQuest> quests = _controller.Quests();
        for (int i = 0; i < SlotCount; i++)
        {
            DomainQuest? q = i < quests.Count ? quests[i] : null;
            _slotQuests[i] = q;
            row.Add(BuildSlotWrap(q, i));
        }

        Highlight();
    }

    // Each slot is a parchment card layered over a soft gold selection glow. The glow sits behind the card
    // (added first, extended past its edges) and is shown only for the selected slot, so selection reads as
    // a halo rather than a hard border.
    private VisualElement BuildSlotWrap(DomainQuest? quest, int index)
    {
        var wrap = new VisualElement
        {
            style = { position = Position.Relative, marginLeft = 16, marginRight = 16 },
        };

        // The glow sprite is pre-sized for the 240x320 slot plus a 14px halo, its bright edge on the slot
        // boundary; drawn behind the parchment at native size so a thin soft rim hugs the card.
        var glow = new VisualElement
        {
            pickingMode = PickingMode.Ignore,
            style =
            {
                position = Position.Absolute, left = -14, top = -14, width = 268, height = 348,
                display = DisplayStyle.None,
                backgroundImage = new StyleBackground(Background.FromTexture2D(_art.SelectGlow())),
            },
        };
        wrap.Add(glow);
        wrap.Add(BuildSlot(quest));

        _slotGlows[index] = glow;
        _slotViews[index] = wrap;
        return wrap;
    }

    private VisualElement BuildSlot(DomainQuest? quest)
    {
        var slot = new VisualElement
        {
            style =
            {
                width = 240, height = 320,
                paddingTop = 22, paddingBottom = 22, paddingLeft = 22, paddingRight = 22,
                alignItems = Align.Center, justifyContent = Justify.Center,
            },
        };
        BardChrome.Parchment(slot, _art);

        if (quest == null)
        {
            slot.Add(new Label("+ Create")
            {
                style = { color = (Color)BardTheme.OldWood, fontSize = 26, unityFontStyleAndWeight = FontStyle.Bold },
            });
            return slot;
        }

        ActiveQuestView view = _controller.Resolve(quest);
        slot.Add(new Image
        {
            image = _art.ClassMedallion(view.Class),
            style = { width = 110, height = 110 },
        });
        var subrank = new Label($"{BardTheme.ClassName(view.Class)} {BardTheme.Roman(view.Subrank)}")
        {
            style = { color = (Color)BardTheme.Nightwood, fontSize = 22, marginTop = 8, unityFontStyleAndWeight = FontStyle.Bold },
        };
        BardFont.ApplyDisplay(subrank);
        slot.Add(subrank);
        slot.Add(new Label($"{quest.Instrument} · {quest.Difficulty}")
        {
            style = { color = (Color)BardTheme.OldWood, fontSize = 16, marginTop = 4 },
        });
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
            bool selected = i == _selected;
            _slotViews[i].style.translate = new Translate(0, selected ? -19 : 0);
            _slotGlows[i].style.display = selected ? DisplayStyle.Flex : DisplayStyle.None;
        }
    }

    public NavigationScheme BuildScheme() => new(
    [
        new(MenuAction.Left, "Menu.Common.Scroll", () => Move(-1)),
        new(MenuAction.Right, "Menu.Common.Scroll", () => Move(1)),
        new(MenuAction.Green, "Menu.Common.Confirm", Confirm),
        new(MenuAction.Red, "Menu.Common.Back", _canvas.Pop),
    ], false);

    public void OnPop() { }
}
