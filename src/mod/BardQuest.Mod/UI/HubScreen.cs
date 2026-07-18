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

// The Hub: the journey path on top, then a player-stats panel and a foe panel side by side. The foe is the
// first undefeated monster in the working set (or the exclusive boss during a class-boss phase); the
// encounter panel shows it against the player's own attribute levels plus the XP a clean clear would award.
// Confirm launches the foe.
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
    private readonly VisualElement _encounterCol = new();

    private ActiveQuestView _view;
    private List<MonsterStatus> _monsters = [];
    private MonsterStatus _target;      // the single foe: first undefeated in the working set, or the boss
    private string _pendingSelectHash;  // one-shot: on the first Refresh after a fight, prefer the just-played
                                        // song as the target if it is still undefeated

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
        // Build() recreates the path's internal nodes/state every Refresh, but the JourneyPath instance and
        // this subscription persist across the Hub's lifetime — subscribing once here (not per-Refresh)
        // avoids stacking duplicate handlers.
        _path.SelectionChanged += OnJourneySelectionChanged;

        Root = new VisualElement
        {
            style = { flexGrow = 1, flexDirection = FlexDirection.Column, paddingTop = 20, paddingLeft = 40, paddingRight = 40, paddingBottom = 20 },
        };

        // Top-align the two columns and let each size to its own content, so the parchment panels don't
        // stretch to the full row height (which ran their bottoms off-screen) and both share a top edge.
        var mlower = new VisualElement { style = { flexGrow = 1, flexDirection = FlexDirection.Row, marginTop = 8, alignItems = Align.FlexStart } };
        // Both columns share one fixed height so the panels line up.
        _playerCol.style.width = Length.Percent(38);
        _playerCol.style.height = PanelHeight;
        _playerCol.style.marginRight = 16;
        _playerCol.style.paddingTop = 24;
        _playerCol.style.paddingBottom = 24;
        _playerCol.style.paddingLeft = 22;
        _playerCol.style.paddingRight = 22;
        BardChrome.Parchment(_playerCol, _art);

        _encounterCol.style.flexGrow = 1;
        _encounterCol.style.height = PanelHeight;
        _encounterCol.style.paddingTop = 32;
        _encounterCol.style.paddingBottom = 32;
        _encounterCol.style.paddingLeft = 40;
        _encounterCol.style.paddingRight = 40;
        BardChrome.Panel(_encounterCol, _art);

        mlower.Add(_playerCol);
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

        _target = ResolveTarget();
        _pendingSelectHash = null; // one-shot consumed

        _path.Build(_view.Class, _view.Subrank);
        BuildPlayerPanel();
        BuildEncounterPanel();
    }

    // The foe under the cursor: the exclusive boss at a class-boss phase; otherwise the first undefeated
    // monster in the working set. If the just-fought song (pendingSelectHash) is still undefeated, prefer it
    // so a failed attempt re-presents the same foe rather than skipping ahead.
    private MonsterStatus ResolveTarget()
    {
        if (_view.AtClassBoss && _view.Boss != null)
        {
            return _view.Boss;
        }

        if (_pendingSelectHash != null)
        {
            MonsterStatus just = _monsters.FirstOrDefault(
                m => !m.Defeated && string.Equals(m.Hash, _pendingSelectHash, StringComparison.OrdinalIgnoreCase));
            if (just != null)
            {
                return just;
            }
        }

        return _monsters.FirstOrDefault(m => !m.Defeated) ?? _monsters.FirstOrDefault();
    }

    // Fires only from ◄►, never from Refresh (JourneyPath.Build sets its Selected field directly rather than
    // through MoveSelection). Swaps the duel for a summary panel on non-current nodes.
    private void OnJourneySelectionChanged()
    {
        ClassNode node = _path.SelectedNode;
        switch (node.State)
        {
            case ClassNodeState.Current:
                _playerCol.style.display = DisplayStyle.Flex;
                BardChrome.Panel(_encounterCol, _art);
                BuildEncounterPanel();
                break;
            case ClassNodeState.Cleared:
                _preview.Stop();
                _playerCol.style.display = DisplayStyle.None;
                BardChrome.Parchment(_encounterCol, _art);
                BuildConqueredPanel(node);
                break;
            default: // Locked
                _preview.Stop();
                _playerCol.style.display = DisplayStyle.None;
                BardChrome.Parchment(_encounterCol, _art);
                BuildLockedPanel(node);
                break;
        }
    }

    // Cleared node summary: the class medallion plus a "Conquered" heading, filling the row (the YOU panel
    // is hidden, so this panel's flexGrow:1 takes the space).
    private void BuildConqueredPanel(ClassNode node)
    {
        _encounterCol.Clear();
        var wrap = new VisualElement
        {
            style = { flexGrow = 1, alignItems = Align.Center, justifyContent = Justify.Center },
        };
        wrap.Add(new Image
        {
            image = _art.ClassMedallion(node.Class),
            style = { width = 120, height = 120, marginBottom = 16 },
        });
        var heading = new Label($"{BardTheme.ClassName(node.Class)} — Conquered")
        {
            style = { color = (Color)BardTheme.Nightwood, fontSize = 24, unityFontStyleAndWeight = FontStyle.Bold, unityTextAlign = TextAnchor.MiddleCenter },
        };
        BardFont.ApplyDisplay(heading);
        wrap.Add(heading);
        _encounterCol.Add(wrap);
    }

    // Locked node summary: a plain message, no monster content (the player hasn't reached this class yet).
    private void BuildLockedPanel(ClassNode node)
    {
        _encounterCol.Clear();
        var wrap = new VisualElement
        {
            style = { flexGrow = 1, alignItems = Align.Center, justifyContent = Justify.Center },
        };
        var message = new Label($"Reach {BardTheme.ClassName(node.Class)} to unlock")
        {
            style = { color = (Color)BardTheme.Nightwood, fontSize = 22, unityFontStyleAndWeight = FontStyle.Bold, unityTextAlign = TextAnchor.MiddleCenter },
        };
        BardFont.ApplyDisplay(message);
        wrap.Add(message);
        _encounterCol.Add(wrap);
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

    private void BuildEncounterPanel()
    {
        _encounterCol.Clear();
        if (_target == null)
        {
            _preview.Stop();
            _encounterCol.Add(new Label(_view.IsComplete ? "The quest is complete." : "No monsters delivered.")
            {
                style = { color = (Color)BardTheme.Nightwood, fontSize = 22 },
            });
            return;
        }

        MonsterStatus m = _target;
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

    private void Confirm()
    {
        // Cleared/Locked nodes have no fightable foe; a class other than the current one is browse-only.
        if (_path.SelectedNode.State != ClassNodeState.Current || _target == null || _target.Defeated)
        {
            return;
        }

        string hash = _target.Hash;

        if (!_controller.CanLaunch(hash))
        {
            ModLog.Warn($"HubScreen: song {hash} is no longer in the library; not launching.");
            return;
        }

        _preview.Stop();
        _canvas.PrepareForLaunch();
        _controller.Launch(_quest, hash);
    }

    private void Back() => _canvas.Pop();

    // Fires on every pop (Red action, header back button, or bulk teardown) — silence the preview so it can't
    // loop over the main menu after we leave.
    public void OnPop() => _preview.Stop();

    public NavigationScheme BuildScheme() => new(
    [
        new(MenuAction.Left, "Menu.Common.Scroll", () => _path.MoveSelection(-1)),
        new(MenuAction.Right, "Menu.Common.Scroll", () => _path.MoveSelection(1)),
        new(MenuAction.Green, "Menu.Common.Confirm", Confirm),
        new(MenuAction.Red, "Menu.Common.Back", Back),
    ], false);
}
