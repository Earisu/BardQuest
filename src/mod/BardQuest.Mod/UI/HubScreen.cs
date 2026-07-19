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

// The Hub as a "wave board": a frameless player header, then the body split between the wave of up to five
// selectable foes (left) and the selected foe's detail card (right). Up/Down move the cursor through the
// wave; Green fights the selected foe; Red leaves.
public sealed class HubScreen : IScreen
{
    private static readonly Attribute[] Axes =
        [Attribute.Strength, Attribute.Endurance, Attribute.Technique, Attribute.Agility, Attribute.Dexterity];

    private readonly BardQuestCanvas _canvas;
    private readonly QuestController _controller;
    private readonly SongEnricher _enricher;
    private readonly SongPreviewPlayer _preview;
    private readonly BardQuestArt _art;
    private readonly DomainQuest _quest;

    private readonly VisualElement _header = new();
    private readonly VisualElement _listCol = new();
    private readonly VisualElement _detailCol = new();

    private ActiveQuestView _view;
    private List<MonsterStatus> _rows = [];
    private int _cursor;
    private string _pendingSelectHash; // one-shot: after a fight, prefer the just-played song as the cursor row

    public VisualElement Root { get; }

    public string Title => "Quest Hub";

    public bool ShowsAppHeader => false;

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

        // A screen padding keeps every zone off the screen edge; the margins below then separate the header
        // from the body and the two frames from each other. The bottom padding is larger than the top: the UI
        // scales the 1080 reference to the display, so filling to a tight bottom edge crops on non-16:9
        // screens — the extra clearance keeps the frame bottoms on-screen.
        Root = new VisualElement
        {
            style = { flexGrow = 1, flexDirection = FlexDirection.Column, paddingTop = 40, paddingLeft = 40, paddingRight = 40, paddingBottom = 68 },
        };

        // Zone A — the player. Frameless (the app header is suppressed for the Hub): the stat strip sits on
        // the backdrop, so its text is light. Content rebuilt each Refresh.
        _header.style.flexShrink = 0;
        _header.style.marginBottom = 28;

        // Body — the wave (left) and the selected foe's detail card (right). It grows to fill the height left
        // under the header, and the columns stretch to fill it (default Align.Stretch), so the frames take the
        // whole screen with no dead space at the bottom.
        var body = new VisualElement { style = { flexGrow = 1, flexDirection = FlexDirection.Row } };

        _listCol.style.width = Length.Percent(42);
        _listCol.style.marginRight = 28; // gap between the two frames
        _listCol.style.paddingTop = 34;
        _listCol.style.paddingBottom = 34;
        _listCol.style.paddingLeft = 30;
        _listCol.style.paddingRight = 30;
        BardChrome.ListFrame(_listCol, _art); // retune padding when list_frame.png lands

        // The detail card wears the ornate Panel frame; its 9-slice border renders thick, so the content is
        // inset well past it — otherwise the album/title burrow under the frame.
        _detailCol.style.flexGrow = 1;
        _detailCol.style.paddingTop = 96;
        _detailCol.style.paddingBottom = 72;
        _detailCol.style.paddingLeft = 84;
        _detailCol.style.paddingRight = 84;
        BardChrome.Panel(_detailCol, _art);

        body.Add(_listCol);
        body.Add(_detailCol);

        Root.Add(_header);
        Root.Add(body);

        Refresh();
    }

    // Re-resolve and rebuild (called on construct and after a play returns).
    public void Refresh()
    {
        _view = _controller.Resolve(_quest);
        _rows = _view.AtClassBoss && _view.Boss != null
            ? [_view.Boss]
            : [.. _view.WorkingSet];
        _cursor = ResolveCursor();
        _pendingSelectHash = null; // one-shot consumed

        BuildHeader();
        BuildList();
        BuildDetail();
        PreviewSelected();
    }

    // Where the cursor lands when the board (re)builds: the just-fought song if still undefeated, else the
    // first undefeated row, else the first row.
    private int ResolveCursor()
    {
        if (_rows.Count == 0)
        {
            return 0;
        }

        if (_pendingSelectHash != null)
        {
            int just = _rows.FindIndex(
                m => !m.Defeated && string.Equals(m.Hash, _pendingSelectHash, StringComparison.OrdinalIgnoreCase));
            if (just >= 0)
            {
                return just;
            }
        }

        int undefeated = _rows.FindIndex(m => !m.Defeated);
        return undefeated >= 0 ? undefeated : 0;
    }

    private MonsterStatus Selected() => _rows.Count > 0 ? _rows[_cursor] : null;

    private void Move(int delta)
    {
        if (_rows.Count == 0)
        {
            return;
        }

        _cursor = Mathf.Clamp(_cursor + delta, 0, _rows.Count - 1);
        BuildList();
        BuildDetail();
        PreviewSelected();
    }

    // Preview the selected song, like YARG's Music Library (debounced inside the player). Silent when there
    // is no foe (a completed quest).
    private void PreviewSelected()
    {
        MonsterStatus sel = Selected();
        if (sel == null)
        {
            _preview.Stop();
        }
        else
        {
            _preview.Play(sel.Hash);
        }
    }

    // Zone A — the player: class/rank medallion (top-left), class + subrank, the XP-to-rank bar, and the five
    // attribute levels as plain icon + value.
    private void BuildHeader()
    {
        _header.Clear();
        var row = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center } };

        row.Add(new Image
        {
            image = _art.ClassMedallion(_view.Class),
            pickingMode = PickingMode.Ignore,
            style = { width = 84, height = 84, marginRight = 18, flexShrink = 0 },
        });

        var idCol = new VisualElement { style = { flexGrow = 1, justifyContent = Justify.Center } };
        var classLabel = new Label(_view.IsComplete
            ? "LEGENDWEAVER"
            : $"{BardTheme.ClassName(_view.Class)} {BardTheme.Roman(_view.Subrank)}")
        {
            style = { color = (Color)BardTheme.Gilt, fontSize = 30, letterSpacing = 2, unityFontStyleAndWeight = FontStyle.Bold },
        };
        BardFont.ApplyDisplay(classLabel);
        idCol.Add(classLabel);
        idCol.Add(ClassXpBar());
        row.Add(idCol);

        var attrRow = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center, flexShrink = 0 } };
        foreach (Attribute a in Axes)
        {
            attrRow.Add(AttributeCell(a));
        }

        row.Add(attrRow);
        _header.Add(row);
    }

    // Score progress within the current class band — the one surviving StatBar.
    private VisualElement ClassXpBar()
    {
        (double lo, double hi) = ClassDerivation.Range(_view.Class);
        float frac = hi > lo ? Mathf.Clamp01((float)((_view.Profile.Score - lo) / (hi - lo))) : 1f;
        var wrap = new VisualElement { style = { maxWidth = 340, marginTop = 6 } };
        wrap.Add(new Label($"{Mathf.RoundToInt(frac * 100f)}% to next rank")
        {
            style = { color = (Color)BardTheme.Parchment, fontSize = 12, marginBottom = 3 },
        });
        wrap.Add(StatBar.Build(_art, frac, (Color)BardTheme.Glowmoss, 12f));
        return wrap;
    }

    // One attribute as a plain icon + its level value (no ring).
    private VisualElement AttributeCell(Attribute a)
    {
        var cell = new VisualElement { style = { alignItems = Align.Center, marginLeft = 18 } };
        cell.Add(new Image
        {
            image = _art.AttributeIcon(a),
            pickingMode = PickingMode.Ignore,
            style = { width = 40, height = 40 },
        });
        cell.Add(new Label(_view.Profile[a].Level.ToString())
        {
            style = { color = BardTheme.AxisColor(a), fontSize = 18, unityFontStyleAndWeight = FontStyle.Bold, marginTop = 2 },
        });
        return cell;
    }

    // Zone B — the wave: up to five selectable foe rows (or the lone boss). Light text (the frame's interior
    // is dark/the backdrop shows through the real frame's hollow center).
    private void BuildList()
    {
        _listCol.Clear();
        _listCol.Add(new Label(_view.AtClassBoss ? "— The Boss —" : "— The Wave —")
        {
            style = { color = (Color)BardTheme.Gilt, fontSize = 16, unityFontStyleAndWeight = FontStyle.Bold, unityTextAlign = TextAnchor.MiddleCenter, marginBottom = 12 },
        });

        if (_rows.Count == 0)
        {
            _listCol.Add(new Label(_view.IsComplete ? "Your legend is complete" : "No monsters delivered")
            {
                style = { color = (Color)BardTheme.Parchment, fontSize = 16, unityTextAlign = TextAnchor.MiddleCenter, whiteSpace = WhiteSpace.Normal },
            });
            return;
        }

        for (int i = 0; i < _rows.Count; i++)
        {
            _listCol.Add(WaveRow(_rows[i], i == _cursor));
        }
    }

    private VisualElement WaveRow(MonsterStatus m, bool selected)
    {
        SongEnricher.SongInfo? info = _enricher.Lookup(m.Hash);
        var row = new VisualElement
        {
            style =
            {
                flexDirection = FlexDirection.Row, alignItems = Align.Center,
                paddingTop = 8, paddingBottom = 8, paddingLeft = 10, paddingRight = 10, marginBottom = 6,
                borderTopLeftRadius = 8, borderTopRightRadius = 8, borderBottomLeftRadius = 8, borderBottomRightRadius = 8,
                opacity = m.Defeated ? 0.45f : 1f,
            },
        };
        if (selected)
        {
            row.style.backgroundColor = new Color(1f, 0.85f, 0.4f, 0.22f); // drawn cursor highlight
        }

        row.Add(new Image
        {
            image = _art.RankBadge(m.Profile.ToRank()),
            pickingMode = PickingMode.Ignore,
            style = { width = 42, height = 42, marginRight = 12, flexShrink = 0 },
        });

        var textCol = new VisualElement { style = { flexGrow = 1, flexShrink = 1, overflow = Overflow.Hidden } };
        textCol.Add(new Label(info?.Title ?? "Unknown")
        {
            style = { color = (Color)BardTheme.Parchment, fontSize = 16, unityFontStyleAndWeight = FontStyle.Bold, whiteSpace = WhiteSpace.NoWrap, overflow = Overflow.Hidden, textOverflow = TextOverflow.Ellipsis },
        });
        textCol.Add(new Label(info?.Artist ?? "")
        {
            style = { color = (Color)BardTheme.Gilt, fontSize = 12, whiteSpace = WhiteSpace.NoWrap, overflow = Overflow.Hidden, textOverflow = TextOverflow.Ellipsis },
        });
        row.Add(textCol);

        if (m.Type != MonsterType.Regular)
        {
            row.Add(new Label(TypeTag(m.Type))
            {
                style = { color = (Color)BardTheme.Ember, fontSize = 12, unityFontStyleAndWeight = FontStyle.Bold, marginLeft = 8, flexShrink = 0 },
            });
        }

        if (m.Defeated)
        {
            row.Add(new Label("✓")
            {
                style = { color = (Color)BardTheme.Glowmoss, fontSize = 18, unityFontStyleAndWeight = FontStyle.Bold, marginLeft = 8, flexShrink = 0 },
            });
        }

        return row;
    }

    private static string TypeTag(MonsterType type) => type switch
    {
        MonsterType.Elite => "ELITE",
        MonsterType.Boss => "BOSS",
        MonsterType.Rare => "RARE",
        _ => "REGULAR",
    };

    // Zone C — STUB. Task 4 replaces this with the full detail card (album/frame, shield rank + type, plain
    // demand cells, green FIGHT pip). For now it names the selected foe and shows the FIGHT plate so the
    // Green binding is verifiable. The Panel interior is light parchment, so its text is dark.
    private void BuildDetail()
    {
        _detailCol.Clear();
        MonsterStatus m = Selected();
        if (m == null)
        {
            _detailCol.Add(new Label(_view.IsComplete ? "The quest is complete." : "No foe selected.")
            {
                style = { color = (Color)BardTheme.Nightwood, fontSize = 20, unityTextAlign = TextAnchor.MiddleCenter },
            });
            return;
        }

        SongEnricher.SongInfo? info = _enricher.Lookup(m.Hash);
        var title = new Label(info?.Title ?? "Unknown")
        {
            style = { color = (Color)BardTheme.Nightwood, fontSize = 24, unityFontStyleAndWeight = FontStyle.Bold, unityTextAlign = TextAnchor.MiddleCenter, whiteSpace = WhiteSpace.Normal },
        };
        BardFont.ApplyDisplay(title);
        _detailCol.Add(title);
        _detailCol.Add(new Label(info?.Artist ?? "")
        {
            style = { color = (Color)BardTheme.OldWood, fontSize = 16, unityTextAlign = TextAnchor.MiddleCenter, marginBottom = 20 },
        });

        var cta = new VisualElement { style = { height = 60, marginTop = 16, alignItems = Align.Center, justifyContent = Justify.Center } };
        BardChrome.BannerPrimary(cta, _art, 60);
        cta.Add(new Label(m.Defeated ? "Already cleared" : "FIGHT")
        {
            style = { color = (Color)(m.Defeated ? BardTheme.Gilt : BardTheme.Parchment), fontSize = 22, unityFontStyleAndWeight = FontStyle.Bold },
        });
        _detailCol.Add(cta);
    }

    private void Confirm()
    {
        MonsterStatus sel = Selected();
        if (sel == null || sel.Defeated)
        {
            return;
        }

        if (!_controller.CanLaunch(sel.Hash))
        {
            ModLog.Warn($"HubScreen: song {sel.Hash} is no longer in the library; not launching.");
            return;
        }

        _preview.Stop();
        _canvas.PrepareForLaunch();
        _controller.Launch(_quest, sel.Hash);
    }

    private void Back() => _canvas.Pop();

    // Fires on every pop (Red action or bulk teardown) — silence the preview so it can't loop over the main
    // menu after we leave.
    public void OnPop() => _preview.Stop();

    public NavigationScheme BuildScheme() => new(
    [
        new(MenuAction.Up, "Menu.Common.Scroll", () => Move(-1)),
        new(MenuAction.Down, "Menu.Common.Scroll", () => Move(1)),
        new(MenuAction.Green, "Menu.Common.Confirm", Confirm),
        new(MenuAction.Red, "Menu.Common.Back", Back),
    ], false);
}
