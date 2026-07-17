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

// The new-quest form: a centered parchment card. Instrument is fixed (the active profile's, display-only);
// Pace and Difficulty are segmented rows. Navigation is a YARG-style wizard: the arrows change the focused
// field's value, Confirm advances to the next step (Pace -> Difficulty -> Begin -> create), and Back steps
// to the previous field (and exits to Saves from the first). Difficulty defaults to the profile's current
// difficulty. The shell's AppHeader owns the logo and title.
public sealed class CreateQuestScreen : IScreen
{
    private const int FieldPace = 0;
    private const int FieldDifficulty = 1;
    private const int FieldBegin = 2;
    private const int FieldCount = 3;

    private static readonly QuestPace[] Paces = [QuestPace.Sprint, QuestPace.Journey, QuestPace.Odyssey];
    private static readonly Difficulty[] Difficulties =
    [
        Difficulty.Beginner, Difficulty.Easy, Difficulty.Medium, Difficulty.Hard, Difficulty.Expert, Difficulty.ExpertPlus,
    ];

    private readonly BardQuestCanvas _canvas;
    private readonly QuestController _controller;
    private readonly BardQuestArt _art;
    private readonly Action<DomainQuest> _openHub;
    private readonly bool _hasProfile;

    private readonly VisualElement[] _fieldFrames = new VisualElement[FieldCount];
    private readonly VisualElement _paceSegmentsHost = new() { style = { flexDirection = FlexDirection.Row } };
    private readonly VisualElement _diffSegmentsHost = new() { style = { flexDirection = FlexDirection.Row } };
    private readonly VisualElement _paceChevronLeft;
    private readonly VisualElement _paceChevronRight;
    private readonly VisualElement _diffChevronLeft;
    private readonly VisualElement _diffChevronRight;
    private readonly VisualElement _beginGlow;

    private int _paceIdx;
    private int _diffIdx;
    private int _field;

    public VisualElement Root { get; }

    public string Title => "New Quest";

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

        var card = new VisualElement
        {
            style =
            {
                alignSelf = Align.Center, alignItems = Align.Center,
                paddingTop = 44, paddingBottom = 44, paddingLeft = 64, paddingRight = 64,
            },
        };
        BardChrome.Parchment(card, _art);
        Root.Add(card);

        card.Add(new Label($"Instrument: {instrumentName}")
        {
            style =
            {
                color = (Color)BardTheme.Nightwood, fontSize = 22, marginBottom = 20,
                unityFontStyleAndWeight = FontStyle.Bold,
            },
        });

        var paceRow = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center } };
        paceRow.Add(_paceChevronLeft = BuildChevron(pointLeft: true));
        paceRow.Add(_paceSegmentsHost);
        paceRow.Add(_paceChevronRight = BuildChevron(pointLeft: false));
        _fieldFrames[FieldPace] = BuildFieldFrame("Pace", paceRow);
        card.Add(_fieldFrames[FieldPace]);

        var diffRow = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center } };
        diffRow.Add(_diffChevronLeft = BuildChevron(pointLeft: true));
        diffRow.Add(_diffSegmentsHost);
        diffRow.Add(_diffChevronRight = BuildChevron(pointLeft: false));
        _fieldFrames[FieldDifficulty] = BuildFieldFrame("Difficulty", diffRow);
        card.Add(_fieldFrames[FieldDifficulty]);

        var beginBanner = new VisualElement
        {
            style =
            {
                width = 300, height = 64, alignItems = Align.Center, justifyContent = Justify.Center,
            },
        };
        BardChrome.BannerPrimary(beginBanner, _art, 64);
        var beginLabel = new Label("Begin Quest")
        {
            style = { color = (Color)BardTheme.Parchment, fontSize = 22, unityFontStyleAndWeight = FontStyle.Bold },
        };
        BardFont.ApplyDisplay(beginLabel);
        beginBanner.Add(beginLabel);

        // Begin reads as a CTA, not a form field: instead of the shared focus ring it lifts and shows a vivid
        // gold glow behind the plate when focused, matching the Saves slots. On the light parchment the glow
        // has to win on saturation, not brightness (nothing reads brighter than cream), so it is a strong,
        // high-opacity amber-gold. The sprite is sized for the 300x64 banner plus an 18px halo.
        var beginWrap = new VisualElement { style = { marginTop = 16, position = Position.Relative } };
        _beginGlow = new VisualElement
        {
            pickingMode = PickingMode.Ignore,
            style =
            {
                position = Position.Absolute, left = -18, top = -18, width = 336, height = 100,
                display = DisplayStyle.None,
                backgroundImage = new StyleBackground(Background.FromTexture2D(_art.BeginGlow())),
            },
        };
        beginWrap.Add(_beginGlow);
        beginWrap.Add(beginBanner);
        _fieldFrames[FieldBegin] = beginWrap;
        card.Add(beginWrap);

        RenderPaceSegments();
        RenderDiffSegments();
        Highlight();
    }

    // A labeled block with a border ring that lights gold when its field holds focus. Caption is omitted
    // for the Begin field, whose banner art already reads as a single control.
    private static VisualElement BuildFieldFrame(string? caption, VisualElement content)
    {
        var frame = new VisualElement
        {
            style =
            {
                alignItems = Align.Center, marginTop = 16,
                paddingTop = 8, paddingBottom = 8, paddingLeft = 18, paddingRight = 18,
                borderTopWidth = 3, borderBottomWidth = 3, borderLeftWidth = 3, borderRightWidth = 3,
                borderTopLeftRadius = 10, borderTopRightRadius = 10, borderBottomLeftRadius = 10, borderBottomRightRadius = 10,
                borderTopColor = Color.clear, borderBottomColor = Color.clear, borderLeftColor = Color.clear, borderRightColor = Color.clear,
            },
        };

        if (caption != null)
        {
            var captionLabel = new Label(caption.ToUpperInvariant())
            {
                style = { color = (Color)BardTheme.OldWood, fontSize = 14, letterSpacing = 2, marginBottom = 6 },
            };
            BardFont.ApplyDisplay(captionLabel);
            frame.Add(captionLabel);
        }

        frame.Add(content);
        return frame;
    }

    // A left- or right-pointing chevron drawn from two rotated borders, so it renders regardless of which
    // glyphs the runtime font ships (AppHeader's back chevron uses the same trick for the same reason).
    private static VisualElement BuildChevron(bool pointLeft) => new()
    {
        pickingMode = PickingMode.Ignore,
        style =
        {
            width = 14, height = 14, marginLeft = 10, marginRight = 10,
            borderRightWidth = 4, borderBottomWidth = 4,
            borderRightColor = (Color)BardTheme.Gilt, borderBottomColor = (Color)BardTheme.Gilt,
            rotate = new Rotate(new Angle(pointLeft ? 135f : -45f, AngleUnit.Degree)),
            visibility = Visibility.Hidden,
        },
    };

    private void RenderPaceSegments()
    {
        _paceSegmentsHost.Clear();
        for (int i = 0; i < Paces.Length; i++)
        {
            _paceSegmentsHost.Add(BuildSegment(BardTheme.PaceName(Paces[i]), i == _paceIdx));
        }
    }

    private void RenderDiffSegments()
    {
        _diffSegmentsHost.Clear();
        for (int i = 0; i < Difficulties.Length; i++)
        {
            _diffSegmentsHost.Add(BuildSegment(Difficulties[i].ToString(), i == _diffIdx));
        }
    }

    // The current option in a segmented row: green fill + gold border, light ink. The rest sit muted: no
    // fill, dark ink on the parchment beneath.
    private static VisualElement BuildSegment(string text, bool lit) => new Label(text)
    {
        style =
        {
            color = lit ? (Color)BardTheme.Parchment : (Color)BardTheme.OldWood,
            // Constant weight so a segment keeps its width lit or not — otherwise re-rendering on a value
            // change would resize the row and re-centre the content-sized card, making it jitter.
            fontSize = 18, unityFontStyleAndWeight = FontStyle.Bold,
            marginLeft = 4, marginRight = 4,
            paddingTop = 6, paddingBottom = 6, paddingLeft = 14, paddingRight = 14,
            backgroundColor = lit ? (Color)BardTheme.Glowmoss : Color.clear,
            borderTopWidth = 2, borderBottomWidth = 2, borderLeftWidth = 2, borderRightWidth = 2,
            borderTopColor = lit ? (Color)BardTheme.Gilt : Color.clear,
            borderBottomColor = lit ? (Color)BardTheme.Gilt : Color.clear,
            borderLeftColor = lit ? (Color)BardTheme.Gilt : Color.clear,
            borderRightColor = lit ? (Color)BardTheme.Gilt : Color.clear,
            borderTopLeftRadius = 6, borderTopRightRadius = 6, borderBottomLeftRadius = 6, borderBottomRightRadius = 6,
        },
    };

    // The arrows step the focused field's value (both axes, so any instrument's dpad works); a no-op on Begin.
    private void ChangeValue(int delta)
    {
        switch (_field)
        {
            case FieldPace:
                _paceIdx = (_paceIdx + delta + Paces.Length) % Paces.Length;
                RenderPaceSegments();
                break;
            case FieldDifficulty:
                _diffIdx = (_diffIdx + delta + Difficulties.Length) % Difficulties.Length;
                RenderDiffSegments();
                break;
        }
    }

    // Confirm walks the wizard forward: Pace -> Difficulty -> Begin, then creates on Begin.
    private void Advance()
    {
        if (_field != FieldBegin)
        {
            _field++;
            Highlight();
            return;
        }

        BeginQuest();
    }

    // Back walks the wizard backward one field, and exits to Saves from the first.
    private void Retreat()
    {
        if (_field == FieldPace)
        {
            _canvas.Pop();
            return;
        }

        _field--;
        Highlight();
    }

    private void BeginQuest()
    {
        // No active YARG profile → Create would throw out of this nav callback. Guard instead: there is
        // nothing to scope a quest to, so leave the form as-is (Back returns to Saves).
        if (!_hasProfile)
        {
            ModLog.Warn("CreateQuestScreen: no active YARG profile; cannot begin a quest.");
            return;
        }

        DomainQuest quest = _controller.Create(Paces[_paceIdx], Difficulties[_diffIdx]);
        _openHub(quest);
    }

    private void Highlight()
    {
        // Pace and Difficulty light a gold focus ring; Begin instead lifts and glows (handled below), so its
        // frame keeps a clear ring.
        for (int i = 0; i < _fieldFrames.Length; i++)
        {
            Color ring = i == _field && i != FieldBegin ? (Color)BardTheme.Gilt : Color.clear;
            _fieldFrames[i].style.borderTopColor = ring;
            _fieldFrames[i].style.borderBottomColor = ring;
            _fieldFrames[i].style.borderLeftColor = ring;
            _fieldFrames[i].style.borderRightColor = ring;
        }

        bool beginFocused = _field == FieldBegin;
        _beginGlow.style.display = beginFocused ? DisplayStyle.Flex : DisplayStyle.None;
        _fieldFrames[FieldBegin].style.translate = new Translate(0, beginFocused ? -10 : 0);

        SetChevronsVisible(_paceChevronLeft, _paceChevronRight, _field == FieldPace);
        SetChevronsVisible(_diffChevronLeft, _diffChevronRight, _field == FieldDifficulty);
    }

    private static void SetChevronsVisible(VisualElement left, VisualElement right, bool visible)
    {
        Visibility v = visible ? Visibility.Visible : Visibility.Hidden;
        left.style.visibility = v;
        right.style.visibility = v;
    }

    public NavigationScheme BuildScheme() => new(
    [
        new(MenuAction.Up, "Menu.Common.Up", () => ChangeValue(-1)),
        new(MenuAction.Down, "Menu.Common.Down", () => ChangeValue(1)),
        new(MenuAction.Left, "Menu.Common.Scroll", () => ChangeValue(-1)),
        new(MenuAction.Right, "Menu.Common.Scroll", () => ChangeValue(1)),
        new(MenuAction.Green, "Menu.Common.Confirm", Advance),
        new(MenuAction.Red, "Menu.Common.Back", Retreat),
    ], false);

    public void OnPop() { }
}
