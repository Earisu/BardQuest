extern alias yargpkg;

using BardQuest.Domain.Progression;
using BardQuest.Mod.Quest;

using UnityEngine;
using UnityEngine.UIElements;

using YARG.Core;
using YARG.Menu.Navigation;

using DomainQuest = BardQuest.Domain.Quest.Quest;
using MenuAction = yargpkg::YARG.Core.Input.MenuAction;
using RtYargProfile = yargpkg::YARG.Core.Game.YargProfile;

namespace BardQuest.Mod.UI;

// The new-quest form. Instrument is fixed (the active profile's, display-only); pace and difficulty are
// cycled with Confirm. Difficulty defaults to the profile's current difficulty.
public sealed class CreateQuestScreen : IScreen
{
    private static readonly QuestPace[] Paces = [QuestPace.Sprint, QuestPace.Journey, QuestPace.Odyssey];
    private static readonly Difficulty[] Difficulties =
    [
        Difficulty.Beginner, Difficulty.Easy, Difficulty.Medium, Difficulty.Hard, Difficulty.Expert, Difficulty.ExpertPlus,
    ];

    private readonly BardQuestCanvas _canvas;
    private readonly QuestController _controller;
    private readonly BardQuestArt _art;
    private readonly Action<DomainQuest> _openHub;
    private readonly Label[] _rowLabels = new Label[3]; // 0 pace, 1 difficulty, 2 begin
    private readonly bool _hasProfile;
    private int _paceIdx;
    private int _diffIdx;
    private int _row;

    public VisualElement Root { get; }

    public CreateQuestScreen(BardQuestCanvas canvas, QuestController controller, BardQuestArt art, Action<DomainQuest> openHub)
    {
        _canvas = canvas;
        _controller = controller;
        _art = art;
        _openHub = openHub;

        RtYargProfile? profile = controller.ActiveProfile();
        _hasProfile = profile != null;
        // Bridge runtime -> vendored Difficulty by integer: the two Difficulty enums are distinct CLR
        // types (two-YARG.Core split) but byte-identical in layout, so an int round-trip is safe.
        Difficulty current = profile != null ? (Difficulty)(int)profile.CurrentDifficulty : Difficulty.Expert;
        _diffIdx = Math.Max(0, Array.IndexOf(Difficulties, current));

        // Instrument is display-only here: no bridge needed, just the runtime enum's name.
        string instrumentName = profile?.CurrentInstrument.ToString() ?? "Unknown";

        Root = new VisualElement
        {
            style =
            {
                flexGrow = 1, flexDirection = FlexDirection.Column,
                alignItems = Align.Center, justifyContent = Justify.Center,
            },
        };
        Root.Add(new Image
        {
            image = _art.Logo(),
            style = { width = 400, height = 400, marginBottom = 4 },
        });
        Root.Add(new Label("NEW QUEST")
        {
            style = { color = (Color)BardTheme.Gilt, fontSize = 40, marginBottom = 24, unityFontStyleAndWeight = FontStyle.Bold },
        });
        Root.Add(new Label($"Instrument: {instrumentName}")
        {
            style = { color = (Color)BardTheme.Parchment, fontSize = 22, marginBottom = 16 },
        });

        _rowLabels[0] = AddRow($"Pace: {BardTheme.PaceName(Paces[_paceIdx])}");
        _rowLabels[1] = AddRow($"Difficulty: {Difficulties[_diffIdx]}");
        _rowLabels[2] = AddRow("Begin Quest");
        Highlight();
    }

    private Label AddRow(string text)
    {
        var l = new Label(text)
        {
            style =
            {
                color = (Color)BardTheme.Parchment, fontSize = 26, marginTop = 10,
                paddingLeft = 20, paddingRight = 20, paddingTop = 6, paddingBottom = 6,
            },
        };
        Root.Add(l);
        return l;
    }

    private void Move(int delta)
    {
        _row = (_row + delta + _rowLabels.Length) % _rowLabels.Length;
        Highlight();
    }

    private void Confirm()
    {
        switch (_row)
        {
            case 0:
                _paceIdx = (_paceIdx + 1) % Paces.Length;
                _rowLabels[0].text = $"Pace: {BardTheme.PaceName(Paces[_paceIdx])}";
                break;
            case 1:
                _diffIdx = (_diffIdx + 1) % Difficulties.Length;
                _rowLabels[1].text = $"Difficulty: {Difficulties[_diffIdx]}";
                break;
            default:
                // No active YARG profile → Create would throw out of this nav callback. Guard instead:
                // there is nothing to scope a quest to, so leave the form as-is (Back returns to Saves).
                if (!_hasProfile)
                {
                    ModLog.Warn("CreateQuestScreen: no active YARG profile; cannot begin a quest.");
                    break;
                }

                DomainQuest quest = _controller.Create(Paces[_paceIdx], Difficulties[_diffIdx]);
                _openHub(quest);
                break;
        }
    }

    private void Highlight()
    {
        for (int i = 0; i < _rowLabels.Length; i++)
        {
            _rowLabels[i].style.color = i == _row ? (Color)BardTheme.Glowmoss : (Color)BardTheme.Parchment;
        }
    }

    public NavigationScheme BuildScheme() => new(
    [
        new(MenuAction.Up, "Menu.Common.Up", () => Move(-1)),
        new(MenuAction.Down, "Menu.Common.Down", () => Move(1)),
        new(MenuAction.Green, "Menu.Common.Confirm", Confirm),
        new(MenuAction.Red, "Menu.Common.Back", _canvas.Pop),
    ], false);
}
