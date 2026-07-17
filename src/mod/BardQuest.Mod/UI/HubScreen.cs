extern alias yargpkg;

using BardQuest.Domain.Progression;
using BardQuest.Domain.Quest;
using BardQuest.Mod.Quest;

using UnityEngine;
using UnityEngine.UIElements;

using YARG.Menu.Navigation;

using Attribute = BardQuest.Domain.Ratings.Attribute;
using DomainQuest = BardQuest.Domain.Quest.Quest;
using MenuAction = yargpkg::YARG.Core.Input.MenuAction;

namespace BardQuest.Mod.UI;

// The Hub: the journey path on top, then a player-stats panel, a monster list, and an encounter panel
// side by side. The monster list is the working set (or a single boss during a class-boss phase); the
// encounter panel shows the highlighted monster against the player's own attribute levels plus the XP a
// clean clear would award. Confirm launches the selected monster.
public sealed class HubScreen : IScreen
{
    private const int PanelHeight = 400;

    private static readonly Attribute[] Axes =
        [Attribute.Strength, Attribute.Endurance, Attribute.Technique, Attribute.Agility, Attribute.Dexterity];

    private readonly BardQuestCanvas _canvas;
    private readonly QuestController _controller;
    private readonly SongEnricher _enricher;
    private readonly SongPreviewPlayer _preview;
    private readonly BardQuestArt _art;
    private readonly DomainQuest _quest;
    private readonly JourneyPath _path;

    private readonly VisualElement _playerCol = new();
    private readonly VisualElement _monsterCol = new();
    private readonly VisualElement _encounterCol = new();

    private ActiveQuestView _view;
    private List<MonsterStatus> _monsters = [];
    private int _selected;
    private string _pendingSelectHash; // one-shot: restore this monster's highlight on the first Refresh

    public VisualElement Root { get; }

    public string Title => "Quest Hub";

    public HubScreen(
        BardQuestCanvas canvas, QuestController controller, SongEnricher enricher, SongPreviewPlayer preview,
        BardQuestArt art, DomainQuest quest, string initialSelectionHash = null)
    {
        _canvas = canvas;
        _controller = controller;
        _enricher = enricher;
        _preview = preview;
        _art = art;
        _quest = quest;
        _pendingSelectHash = initialSelectionHash;
        _path = new JourneyPath(art);

        Root = new VisualElement
        {
            style = { flexGrow = 1, flexDirection = FlexDirection.Column, paddingTop = 20, paddingLeft = 40, paddingRight = 40, paddingBottom = 20 },
        };

        // Top-align the three columns and let each size to its own content, so the parchment panels don't
        // stretch to the full row height (which ran their bottoms off-screen) and all three share a top edge.
        var mlower = new VisualElement { style = { flexGrow = 1, flexDirection = FlexDirection.Row, marginTop = 8, alignItems = Align.FlexStart } };
        // All three columns share one fixed height so the panels line up and the monster list keeps a stable
        // size regardless of how many songs (1..5) it holds.
        _playerCol.style.width = Length.Percent(26);
        _playerCol.style.height = PanelHeight;
        _playerCol.style.marginRight = 16;
        _playerCol.style.paddingTop = 24;
        _playerCol.style.paddingBottom = 24;
        _playerCol.style.paddingLeft = 22;
        _playerCol.style.paddingRight = 22;
        BardChrome.Parchment(_playerCol, _art);

        _monsterCol.style.width = Length.Percent(30);
        _monsterCol.style.height = PanelHeight;
        _monsterCol.style.marginRight = 16;
        _monsterCol.style.paddingTop = 24;
        _monsterCol.style.paddingBottom = 24;
        _monsterCol.style.paddingLeft = 22;
        _monsterCol.style.paddingRight = 22;
        BardChrome.Parchment(_monsterCol, _art);

        _encounterCol.style.flexGrow = 1;
        _encounterCol.style.height = PanelHeight;
        _encounterCol.style.paddingTop = 32;
        _encounterCol.style.paddingBottom = 32;
        _encounterCol.style.paddingLeft = 40;
        _encounterCol.style.paddingRight = 40;
        BardChrome.Panel(_encounterCol, _art);

        mlower.Add(_playerCol);
        mlower.Add(_monsterCol);
        mlower.Add(_encounterCol);

        Root.Add(_path.Root);
        Root.Add(mlower);

        Refresh();
    }

    // Re-resolve and rebuild (called on construct and after a play returns).
    public void Refresh()
    {
        _view = _controller.Resolve(_quest);
        _monsters = _view.AtClassBoss && _view.Boss != null
            ? [_view.Boss]
            : [.. _view.WorkingSet];

        // Restore the highlight to a specific monster (the one just fought), like YARG's library keeps its
        // cursor across a song. One-shot; falls back to the clamped index if that monster is gone (e.g. the
        // working set redelivered or collapsed to a boss/Elite).
        if (_pendingSelectHash != null)
        {
            int idx = _monsters.FindIndex(m => string.Equals(m.Hash, _pendingSelectHash, StringComparison.OrdinalIgnoreCase));
            if (idx >= 0)
            {
                _selected = idx;
            }

            _pendingSelectHash = null;
        }

        _selected = Mathf.Clamp(_selected, 0, Math.Max(0, _monsters.Count - 1));
        _path.Build(_view.Class, _view.Subrank);
        BuildPlayerPanel();
        BuildMonsterPanel();
        BuildEncounterPanel();
    }

    private void BuildPlayerPanel()
    {
        _playerCol.Clear();

        var header = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center, marginBottom = 10 } };
        header.Add(new Image
        {
            image = _art.ClassMedallion(_view.Class),
            style = { width = 64, height = 64, marginRight = 12 },
        });
        var title = new Label(_view.IsComplete
            ? "LEGENDWEAVER"
            : $"{BardTheme.ClassName(_view.Class)} {BardTheme.Roman(_view.Subrank)}")
        {
            style = { color = (Color)BardTheme.Nightwood, fontSize = 22, unityFontStyleAndWeight = FontStyle.Bold, flexShrink = 1, whiteSpace = WhiteSpace.Normal },
        };
        BardFont.ApplyDisplay(title);
        header.Add(title);
        _playerCol.Add(header);
        _playerCol.Add(ClassXpBar());

        var sectionBanner = new VisualElement
        {
            style = { height = 36, marginTop = 14, marginBottom = 10, alignItems = Align.Center, justifyContent = Justify.Center },
        };
        BardChrome.BannerSecondary(sectionBanner, _art, 36);
        sectionBanner.Add(new Label("— Your Stats —")
        {
            style = { color = (Color)BardTheme.Parchment, fontSize = 16, unityFontStyleAndWeight = FontStyle.Bold },
        });
        _playerCol.Add(sectionBanner);

        var curve = LevelCurve.ForPace(_quest.Pace);
        foreach (Attribute a in Axes)
        {
            _playerCol.Add(AttributeStatRow(a, curve));
        }
    }

    // Fraction of the way through the current class band, by score.
    private VisualElement ClassXpBar()
    {
        (double lo, double hi) = ClassDerivation.Range(_view.Class);
        float frac = hi > lo ? Mathf.Clamp01((float)((_view.Profile.Score - lo) / (hi - lo))) : 1f;
        var wrap = new VisualElement();
        wrap.Add(new Label($"{Mathf.RoundToInt(frac * 100f)}% to next rank")
        {
            style = { color = (Color)BardTheme.OldWood, fontSize = 13, marginBottom = 4 },
        });
        var track = new VisualElement
        {
            style = { height = 12, backgroundColor = (Color)BardTheme.OldWood },
        };
        track.Add(new VisualElement
        {
            style = { width = Length.Percent(frac * 100f), height = 12, backgroundColor = (Color)BardTheme.Glowmoss },
        });
        wrap.Add(track);
        return wrap;
    }

    // One axis of the player's own sheet: icon, name, a level badge, and a fill bar to the next level.
    private VisualElement AttributeStatRow(Attribute a, LevelCurve curve)
    {
        AttributeState state = _view.Profile[a];
        (_, double into, double needed) = curve.Progress(state.Xp);
        float frac = needed > 0 ? Mathf.Clamp01((float)(into / needed)) : 0f;

        var row = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center, marginTop = 8 } };
        row.Add(new Image
        {
            image = _art.AttributeIcon(a),
            style = { width = 20, height = 20, marginRight = 6 },
        });
        row.Add(new Label(BardTheme.AxisName(a))
        {
            style = { color = (Color)BardTheme.Nightwood, fontSize = 13, width = 68 },
        });

        var badge = new VisualElement
        {
            style =
            {
                width = 26, height = 22, marginRight = 8, alignItems = Align.Center, justifyContent = Justify.Center,
                backgroundColor = (Color)BardTheme.OldWood,
                borderTopLeftRadius = 6, borderTopRightRadius = 6, borderBottomLeftRadius = 6, borderBottomRightRadius = 6,
            },
        };
        badge.Add(new Label(state.Level.ToString())
        {
            style = { color = (Color)BardTheme.Parchment, fontSize = 13, unityFontStyleAndWeight = FontStyle.Bold },
        });
        row.Add(badge);

        var track = new VisualElement { style = { flexGrow = 1, height = 10, backgroundColor = new Color(0f, 0f, 0f, 0.18f) } };
        track.Add(new VisualElement
        {
            style = { width = Length.Percent(frac * 100f), height = 10, backgroundColor = BardTheme.AxisColor(a) },
        });
        row.Add(track);
        return row;
    }

    private void BuildMonsterPanel()
    {
        _monsterCol.Clear();

        string section = _view.AtClassBoss ? "— CLASS BOSS —"
            : _view.AtMiniBoss ? "— ELITE —"
            : $"— {BardTheme.ClassName(_view.Class)} {BardTheme.Roman(_view.Subrank)} · Monsters —";
        var sectionBanner = new VisualElement
        {
            style = { height = 40, marginBottom = 10, alignItems = Align.Center, justifyContent = Justify.Center },
        };
        BardChrome.BannerSecondary(sectionBanner, _art, 40);
        sectionBanner.Add(new Label(section)
        {
            style = { color = (Color)BardTheme.Parchment, fontSize = 16, unityFontStyleAndWeight = FontStyle.Bold },
        });
        _monsterCol.Add(sectionBanner);

        for (int i = 0; i < _monsters.Count; i++)
        {
            _monsterCol.Add(BuildRow(_monsters[i], i));
        }
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
        row.Add(new Image
        {
            image = _art.RankBadge(m.Profile.ToRank()),
            style = { width = 30, height = 30, marginRight = 8 },
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

    private void BuildEncounterPanel()
    {
        _encounterCol.Clear();
        if (_monsters.Count == 0)
        {
            _preview.Stop();
            _encounterCol.Add(new Label(_view.IsComplete ? "The quest is complete." : "No monsters delivered.")
            {
                style = { color = (Color)BardTheme.Nightwood, fontSize = 22 },
            });
            return;
        }

        MonsterStatus m = _monsters[_selected];
        SongEnricher.SongInfo? info = _enricher.Lookup(m.Hash);

        // Preview the highlighted song, like YARG's Music Library. No-op if it is already previewing this
        // song; debounced inside the player so scrolling Up/Down only previews the settled selection.
        _preview.Play(m.Hash);

        var cardTop = new VisualElement { style = { flexDirection = FlexDirection.Row } };

        const float frameSize = 170f;
        var frameStack = new VisualElement { style = { width = frameSize, height = frameSize, flexShrink = 0 } };
        var album = new Image
        {
            image = info?.Album,
            // YARGImage.LoadTexture decodes bottom-up (YARG's own uGUI covers flip it with a negative
            // uvRect); UITK does not, so flip the element vertically or the cover renders upside down.
            // Inset ~17.5% each side to match the frame art's transparent window (~65% of the frame), so
            // the cover fills the opening instead of poking past the border.
            style = { position = Position.Absolute, left = 30, top = 30, width = 110, height = 110 },
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
            style = { position = Position.Absolute, left = 0, top = 0, width = frameSize, height = frameSize },
        });
        cardTop.Add(frameStack);

        var infoStack = new VisualElement { style = { flexGrow = 1, marginLeft = 20, justifyContent = Justify.Center } };
        infoStack.Add(new Label(info?.Title ?? "Unknown")
        {
            style = { color = (Color)BardTheme.Nightwood, fontSize = 22, unityFontStyleAndWeight = FontStyle.Bold },
        });
        infoStack.Add(new Label(info?.Artist ?? "")
        {
            style = { color = (Color)BardTheme.OldWood, fontSize = 15, marginBottom = 10 },
        });

        var columnHeader = new VisualElement { style = { flexDirection = FlexDirection.Row, marginBottom = 4 } };
        columnHeader.Add(new VisualElement { style = { width = 96 } });
        columnHeader.Add(new Label("Demand vs. you")
        {
            style = { color = (Color)BardTheme.OldWood, fontSize = 11, flexGrow = 1 },
        });
        columnHeader.Add(new Label("XP on clear")
        {
            style = { color = (Color)BardTheme.OldWood, fontSize = 11, width = 48, unityTextAlign = TextAnchor.MiddleRight },
        });
        infoStack.Add(columnHeader);

        var playerLevels = new Dictionary<Attribute, int>(Axes.Length);
        foreach (Attribute a in Axes)
        {
            playerLevels[a] = _view.Profile[a].Level;
        }

        IReadOnlyDictionary<Attribute, double> rewards = RewardProjection.ForCleanClear(m.Profile, playerLevels);
        foreach (Attribute a in Axes)
        {
            infoStack.Add(CompareBar(a, m.Profile[a], playerLevels[a], rewards[a]));
        }

        cardTop.Add(infoStack);
        _encounterCol.Add(cardTop);

        var cta = new VisualElement
        {
            style =
            {
                height = 56, marginTop = 18,
                alignItems = Align.Center, justifyContent = Justify.Center,
            },
        };
        BardChrome.BannerPrimary(cta, _art, 56);
        cta.Add(new Label(m.Defeated ? "Already cleared" : "Confirm to FIGHT")
        {
            style =
            {
                color = (Color)(m.Defeated ? BardTheme.Gilt : BardTheme.Parchment),
                fontSize = 20, unityFontStyleAndWeight = FontStyle.Bold, unityTextAlign = TextAnchor.MiddleCenter,
            },
        });
        _encounterCol.Add(cta);
    }

    // One axis: the song's demand (colored) over the player's current level (faint) on a 0..10 track, plus
    // the XP a clean clear of this axis would award at the player's current levels.
    private VisualElement CompareBar(Attribute a, double songScore, int playerLevel, double reward)
    {
        var wrap = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center, marginTop = 4 } };
        wrap.Add(new Image
        {
            image = _art.AttributeIcon(a),
            style = { width = 22, height = 22, marginRight = 6 },
        });
        wrap.Add(new Label(BardTheme.AxisName(a))
        {
            style = { color = (Color)BardTheme.OldWood, fontSize = 14, width = 68 },
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
        wrap.Add(new Label($"+{Math.Round(reward)}")
        {
            style = { color = (Color)BardTheme.Glowmoss, fontSize = 14, width = 48, unityFontStyleAndWeight = FontStyle.Bold, unityTextAlign = TextAnchor.MiddleRight },
        });
        return wrap;
    }

    private void Move(int delta)
    {
        if (_monsters.Count == 0)
        {
            return;
        }

        _selected = (_selected + delta + _monsters.Count) % _monsters.Count;
        BuildMonsterPanel();
        BuildEncounterPanel();
    }

    private void Confirm()
    {
        if (_monsters.Count == 0)
        {
            return;
        }

        MonsterStatus target = _monsters[_selected];

        // A cleared monster cannot be re-fought: no replay, so it can never re-award XP for a song already
        // beaten. Confirm is a no-op on it (the detail panel shows it as cleared instead of a fight prompt).
        if (target.Defeated)
        {
            return;
        }

        string hash = target.Hash;

        // Guard BEFORE tearing anything down: if this monster's song has left the library (a stale rating
        // cache), Launch would bail with no scene load, leaving the canvas on an input-less guard scheme —
        // a soft-lock. Bail here instead, keeping the Hub fully interactive.
        if (!_controller.CanLaunch(hash))
        {
            ModLog.Warn($"HubScreen: song {hash} is no longer in the library; not launching.");
            return;
        }

        _preview.Stop(); // silence the preview so it can't bleed into gameplay

        // Cleanly tear our screens off YARG's Navigator and push a music-suppressing guard so the menu
        // track never bleeds over the song (see PrepareForLaunch). On return, BardQuestManager records the
        // play and re-opens this Hub for the quest automatically — no re-entering the mod by hand.
        _canvas.PrepareForLaunch();
        _controller.Launch(_quest, hash);
    }

    private void Back() => _canvas.Pop();

    // Fires on every pop (Red action, header back button, or bulk teardown) — silence the preview so it can't
    // loop over the main menu after we leave.
    public void OnPop() => _preview.Stop();

    public NavigationScheme BuildScheme() => new(
    [
        new(MenuAction.Up, "Menu.Common.Up", () => Move(-1)),
        new(MenuAction.Down, "Menu.Common.Down", () => Move(1)),
        new(MenuAction.Left, "Menu.Common.Scroll", () => Move(-1)),
        new(MenuAction.Right, "Menu.Common.Scroll", () => Move(1)),
        new(MenuAction.Green, "Menu.Common.Confirm", Confirm),
        new(MenuAction.Red, "Menu.Common.Back", Back),
    ], false);
}
