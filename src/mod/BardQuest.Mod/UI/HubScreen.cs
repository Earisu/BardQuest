// src/mod/BardQuest.Mod/UI/HubScreen.cs
extern alias yargpkg;

using BardQuest.Domain.Progression;
using BardQuest.Domain.Quest;
using BardQuest.Domain.Ratings;
using BardQuest.Mod.Quest;

using UnityEngine;
using UnityEngine.UIElements;

using YARG.Menu.Navigation;

using Attribute = BardQuest.Domain.Ratings.Attribute;
using DomainQuest = BardQuest.Domain.Quest.Quest;
using MenuAction = yargpkg::YARG.Core.Input.MenuAction;

namespace BardQuest.Mod.UI;

// The Hub: a master-detail dashboard over one quest's ActiveQuestView. The left list is the working set
// (or a single boss during a class-boss phase); the right panel shows the highlighted monster's encounter
// against the player's own attribute levels. Confirm launches the selected monster.
public sealed class HubScreen : IScreen
{
    private static readonly Attribute[] Axes =
        [Attribute.Strength, Attribute.Endurance, Attribute.Technique, Attribute.Agility, Attribute.Dexterity];

    private readonly BardQuestCanvas _canvas;
    private readonly QuestController _controller;
    private readonly SongEnricher _enricher;
    private readonly BardQuestArt _art;
    private readonly DomainQuest _quest;

    private ActiveQuestView _view;
    private List<MonsterStatus> _monsters = new();
    private readonly VisualElement _listCol = new();
    private readonly VisualElement _detailCol = new();
    private int _selected;

    public VisualElement Root { get; }

    public HubScreen(
        BardQuestCanvas canvas, QuestController controller, SongEnricher enricher, BardQuestArt art, DomainQuest quest)
    {
        _canvas = canvas;
        _controller = controller;
        _enricher = enricher;
        _art = art;
        _quest = quest;

        Root = new VisualElement
        {
            style = { flexGrow = 1, flexDirection = FlexDirection.Row, paddingTop = 40, paddingLeft = 40, paddingRight = 40 },
        };
        _listCol.style.width = Length.Percent(42);
        _listCol.style.marginRight = 24;
        _detailCol.style.flexGrow = 1;
        _detailCol.style.backgroundColor = (Color)BardTheme.Mossdeep;
        _detailCol.style.paddingTop = 20;
        _detailCol.style.paddingLeft = 20;
        _detailCol.style.paddingRight = 20;
        Root.Add(_listCol);
        Root.Add(_detailCol);

        Refresh();
    }

    // Re-resolve and rebuild (called on construct and after a play returns).
    public void Refresh()
    {
        _view = _controller.Resolve(_quest);
        _monsters = _view.AtClassBoss && _view.Boss != null
            ? new List<MonsterStatus> { _view.Boss }
            : new List<MonsterStatus>(_view.WorkingSet);
        _selected = Mathf.Clamp(_selected, 0, Math.Max(0, _monsters.Count - 1));
        BuildLeft();
        BuildDetail();
    }

    private void BuildLeft()
    {
        _listCol.Clear();

        // Bard status header.
        var header = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center, marginBottom = 16 } };
        header.Add(new Image { image = _art.ClassMedallion(_view.Class), style = { width = 72, height = 72, marginRight = 12 } });
        var titleCol = new VisualElement();
        titleCol.Add(new Label(_view.IsComplete
            ? "LEGENDWEAVER — Quest Complete!"
            : $"{BardTheme.ClassName(_view.Class)} {BardTheme.Roman(_view.Subrank)}")
        {
            style = { color = (Color)BardTheme.Gilt, fontSize = 26, unityFontStyleAndWeight = FontStyle.Bold },
        });
        titleCol.Add(XpBar());
        header.Add(titleCol);
        _listCol.Add(header);

        var radar = new PentagonRadar();
        radar.SetLevels(_view.Profile.Axes);
        _listCol.Add(radar);

        string section = _view.AtClassBoss ? "— CLASS BOSS —" : _view.AtMiniBoss ? "— ELITE —" : "— Monsters —";
        _listCol.Add(new Label(section)
        {
            style = { color = (Color)BardTheme.Parchment, fontSize = 18, marginTop = 12, marginBottom = 6 },
        });

        for (int i = 0; i < _monsters.Count; i++)
        {
            _listCol.Add(BuildRow(_monsters[i], i));
        }
    }

    private VisualElement XpBar()
    {
        // Fraction of the way through the current class band, by score.
        (double lo, double hi) = ClassDerivation.Range(_view.Class);
        float frac = hi > lo ? Mathf.Clamp01((float)((_view.Profile.Score - lo) / (hi - lo))) : 1f;
        var track = new VisualElement
        {
            style = { width = 220, height = 12, backgroundColor = (Color)BardTheme.Nightwood, marginTop = 6 },
        };
        track.Add(new VisualElement
        {
            style = { width = Length.Percent(frac * 100f), height = 12, backgroundColor = (Color)BardTheme.Glowmoss },
        });
        return track;
    }

    private VisualElement BuildRow(MonsterStatus m, int index)
    {
        SongEnricher.SongInfo? info = _enricher.Lookup(m.Hash);
        var row = new VisualElement
        {
            style =
            {
                flexDirection = FlexDirection.Row, alignItems = Align.Center,
                paddingTop = 6, paddingBottom = 6, paddingLeft = 8, marginBottom = 4,
                backgroundColor = index == _selected ? (Color)BardTheme.OldWood : (Color)BardTheme.Mossdeep,
            },
        };
        row.Add(new Label(info?.Title ?? m.Hash[..Math.Min(8, m.Hash.Length)])
        {
            style = { color = (Color)BardTheme.Parchment, fontSize = 18, flexGrow = 1 },
        });
        row.Add(new Label(m.Profile.ToRank().ToString())
        {
            style = { color = (Color)BardTheme.Gilt, fontSize = 16, marginRight = 8 },
        });
        row.Add(new Label(Marker(m))
        {
            style = { color = (Color)(m.Defeated ? BardTheme.Glowmoss : BardTheme.Ember), fontSize = 16 },
        });
        return row;
    }

    private static string Marker(MonsterStatus m)
    {
        string type = m.Type switch
        {
            MonsterType.Elite => "Elite",
            MonsterType.Boss => "Boss",
            MonsterType.Rare => "Rare",
            _ => "",
        };
        string state = m.Defeated ? "cleared" : "";
        return string.Join(" ", new[] { type, state }.Where(s => s.Length > 0));
    }

    private void BuildDetail()
    {
        _detailCol.Clear();
        if (_monsters.Count == 0)
        {
            _detailCol.Add(new Label(_view.IsComplete ? "The quest is complete." : "No monsters delivered.")
            {
                style = { color = (Color)BardTheme.Parchment, fontSize = 22 },
            });
            return;
        }

        MonsterStatus m = _monsters[_selected];
        SongEnricher.SongInfo? info = _enricher.Lookup(m.Hash);

        // Framed album art (frame overlays the album at a fixed size).
        var frameStack = new VisualElement { style = { width = 220, height = 220, alignSelf = Align.Center } };
        var album = new Image
        {
            image = info?.Album,
            // YARGImage.LoadTexture decodes bottom-up (YARG's own uGUI covers flip it with a negative
            // uvRect); UITK does not, so flip the element vertically or the cover renders upside down.
            style = { position = Position.Absolute, left = 16, top = 16, width = 188, height = 188 },
        };
        if (info?.Album != null)
        {
            album.style.scale = new Scale(new Vector2(1f, -1f));
        }
        else
        {
            album.style.backgroundColor = (Color)BardTheme.Nightwood;
        }

        frameStack.Add(album);
        frameStack.Add(new Image
        {
            image = _art.MonsterFrame(m.Type),
            style = { position = Position.Absolute, left = 0, top = 0, width = 220, height = 220 },
        });
        _detailCol.Add(frameStack);

        _detailCol.Add(new Label(info?.Title ?? "Unknown")
        {
            style = { color = (Color)BardTheme.Parchment, fontSize = 24, marginTop = 12, unityTextAlign = TextAnchor.MiddleCenter },
        });
        _detailCol.Add(new Label(info?.Artist ?? "")
        {
            style = { color = (Color)BardTheme.Gilt, fontSize = 16, marginBottom = 12, unityTextAlign = TextAnchor.MiddleCenter },
        });

        foreach (Attribute a in Axes)
        {
            _detailCol.Add(CompareBar(a, m.Profile[a], _view.Profile[a].Level));
        }

        _detailCol.Add(new Label(m.Defeated ? "Confirm to replay" : "Confirm to FIGHT")
        {
            style = { color = (Color)BardTheme.Glowmoss, fontSize = 22, marginTop = 16, unityTextAlign = TextAnchor.MiddleCenter },
        });
    }

    // One axis: the song's demand (colored) over the player's current level (faint) on a 0..10 track.
    private VisualElement CompareBar(Attribute a, double songScore, int playerLevel)
    {
        var wrap = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center, marginTop = 4 } };
        wrap.Add(new Label(BardTheme.AxisName(a))
        {
            style = { color = (Color)BardTheme.Parchment, fontSize = 14, width = 90 },
        });
        var track = new VisualElement { style = { flexGrow = 1, height = 14, backgroundColor = (Color)BardTheme.Nightwood } };
        track.Add(new VisualElement
        {
            style = { position = Position.Absolute, left = 0, top = 0, height = 14, width = Length.Percent(Mathf.Clamp01(playerLevel / 10f) * 100f), backgroundColor = new Color(1, 1, 1, 0.18f) },
        });
        track.Add(new VisualElement
        {
            style = { position = Position.Absolute, left = 0, top = 0, height = 14, width = Length.Percent(Mathf.Clamp01((float)songScore / 10f) * 100f), backgroundColor = BardTheme.AxisColor(a) },
        });
        wrap.Add(track);
        return wrap;
    }

    private void Move(int delta)
    {
        if (_monsters.Count == 0)
        {
            return;
        }

        _selected = (_selected + delta + _monsters.Count) % _monsters.Count;
        BuildLeft();
        BuildDetail();
    }

    private void Confirm()
    {
        if (_monsters.Count == 0)
        {
            return;
        }

        // Cleanly tear our screens off YARG's Navigator and push a music-suppressing guard so the menu
        // track never bleeds over the song (see PrepareForLaunch). On return, BardQuestManager records the
        // play and re-opens this Hub for the quest automatically — no re-entering the mod by hand.
        _canvas.PrepareForLaunch();
        _controller.Launch(_quest, _monsters[_selected].Hash);
    }

    public NavigationScheme BuildScheme() => new(new List<NavigationScheme.Entry>
    {
        new(MenuAction.Up, "Menu.Common.Up", () => Move(-1)),
        new(MenuAction.Down, "Menu.Common.Down", () => Move(1)),
        new(MenuAction.Green, "Menu.Common.Confirm", Confirm),
        new(MenuAction.Red, "Menu.Common.Back", _canvas.Pop),
    }, false);
}
