extern alias yargpkg;

using BardQuest.Domain.Progression;
using BardQuest.Domain.Quest;
using BardQuest.Mod.Quest;

using UnityEngine;
using UnityEngine.UIElements;

using YARG.Menu.Data;
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

    private static Texture2D _dividerTex;

    // The header's class medallion: a large round badge overhanging the header's far-left edge (as if it sits
    // on it), vertically centered.
    private const float ClassBadge = 170f;
    private const float ClassBadgeLeft = -50f;

    // How far the wood board bleeds toward the panel edge (tunable). It must be small enough that the board
    // reaches under the frame's inner rail — otherwise the backdrop shows in the gap between them. Content
    // breathing room lives on the board's own padding instead.
    private const float FrameBleed = 16f;

    private readonly BardQuestCanvas _canvas;
    private readonly QuestController _controller;
    private readonly SongEnricher _enricher;
    private readonly SongPreviewPlayer _preview;
    private readonly BardQuestArt _art;
    private readonly DomainQuest _quest;

    private readonly VisualElement _header = new();
    private readonly VisualElement _headerBoard = new(); // wood interior of the header frame
    private readonly Image _classBadge = new(); // oversized class medallion, overhanging the header's far left
    private readonly VisualElement _listCol = new();
    private readonly VisualElement _listBoard = new(); // wood interior of the list frame
    private readonly VisualElement _detailCol = new();
    private readonly Label _callout = new();

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

        // Zone A — the player. Same wooden frame + wood-board interior as the wave list (Zone B), but the left
        // corners are dropped in favor of a large class medallion (_classBadge) overhanging the far left; the
        // board's left padding clears that medallion. Content rebuilt on the board.
        _header.style.flexShrink = 0;
        _header.style.marginBottom = 28;
        _header.style.paddingTop = FrameBleed;
        _header.style.paddingBottom = FrameBleed;
        _header.style.paddingLeft = FrameBleed;
        _header.style.paddingRight = FrameBleed;
        BardChrome.FrameWood(_header, _art);
        WoodBoard(_headerBoard);
        _headerBoard.style.paddingTop = 2;
        _headerBoard.style.paddingBottom = 2;
        _headerBoard.style.paddingLeft = 130; // clear the class medallion on the far left
        _headerBoard.style.paddingRight = 26;
        _header.Add(_headerBoard);

        // The class medallion: large, on the far left, a bit taller than the header and vertically centered
        // (its image is set per-class in BuildHeader).
        _classBadge.pickingMode = PickingMode.Ignore;
        _classBadge.style.position = Position.Absolute;
        _classBadge.style.left = ClassBadgeLeft;
        _classBadge.style.top = Length.Percent(50);
        _classBadge.style.translate = new Translate(0, Length.Percent(-50));
        _classBadge.style.width = ClassBadge;
        _classBadge.style.height = ClassBadge;
        _header.Add(_classBadge);

        // Body — the wave (left) and the selected foe's detail card (right). It grows to fill the height left
        // under the header, and the columns stretch to fill it (default Align.Stretch), so the frames take the
        // whole screen with no dead space at the bottom.
        var body = new VisualElement { style = { flexGrow = 1, flexDirection = FlexDirection.Row } };

        _listCol.style.width = Length.Percent(42);
        _listCol.style.marginRight = 28; // gap between the two frames
        _listCol.style.paddingTop = FrameBleed;
        _listCol.style.paddingBottom = FrameBleed;
        _listCol.style.paddingLeft = FrameBleed;
        _listCol.style.paddingRight = FrameBleed;
        BardChrome.FrameWood(_listCol, _art);
        WoodBoard(_listBoard);
        _listBoard.style.paddingTop = 18;
        _listBoard.style.paddingBottom = 18;
        _listBoard.style.paddingLeft = 16;
        _listBoard.style.paddingRight = 16;
        _listCol.Add(_listBoard);

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

        // A slim objective line between the header and the body — shown only for an Elite, a class boss, or
        // completion (see UpdateCallout); hidden during the ordinary grind.
        _callout.style.unityTextAlign = TextAnchor.MiddleCenter;
        _callout.style.color = (Color)BardTheme.Gilt;
        _callout.style.fontSize = 18;
        _callout.style.unityFontStyleAndWeight = FontStyle.Bold;
        _callout.style.marginBottom = 12;
        _callout.style.flexShrink = 0;
        _callout.style.display = DisplayStyle.None;
        BardFont.ApplyDisplay(_callout);

        Root.Add(_header);
        Root.Add(_callout);
        Root.Add(body);

        Refresh();
    }

    // The wood interior of a frame: fills the frame's hollow center (inset past the 9-slice border by the
    // parent's padding). Its background survives Clear(); Build* rebuild content into it. Because it sits
    // within the frame's opaque border, the wood never bleeds past the ornate outer silhouette.
    private void WoodBoard(VisualElement board)
    {
        board.style.flexGrow = 1;
        board.style.backgroundImage = new StyleBackground(_art.WoodPanel());
        // Round the corners so the board nestles into the frame's rounded corners instead of poking out square.
        board.style.borderTopLeftRadius = 22;
        board.style.borderTopRightRadius = 22;
        board.style.borderBottomLeftRadius = 22;
        board.style.borderBottomRightRadius = 22;
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
        UpdateCallout();
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

    // Zone A — the player, split into six parts (the class, then the five attributes) separated by vertical
    // dividers that fade out toward their top and bottom edges.
    private void BuildHeader()
    {
        _headerBoard.Clear();
        var row = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Stretch, justifyContent = Justify.Center } };

        row.Add(ClassPart());
        var curve = LevelCurve.ForPace(_quest.Pace);
        foreach (Attribute a in Axes)
        {
            row.Add(Divider());
            row.Add(AttributePart(a, curve));
        }

        _headerBoard.Add(row);
        _classBadge.image = _art.ClassMedallion(_view.Class);
    }

    // Part 1: to the right of the class medallion — the class name, a "Rank" row of leaf pips (the subrank),
    // the global XP-to-rank bar, and the percentage beneath. Left-aligned so it reads off the medallion.
    private VisualElement ClassPart()
    {
        (double lo, double hi) = ClassDerivation.Range(_view.Class);
        float frac = hi > lo ? Mathf.Clamp01((float)((_view.Profile.Score - lo) / (hi - lo))) : 1f;

        var part = new VisualElement { style = { flexGrow = 1, flexShrink = 1, alignItems = Align.FlexStart, justifyContent = Justify.Center, paddingLeft = 10, paddingRight = 10 } };

        var name = new Label(_view.IsComplete ? "LEGENDWEAVER" : BardTheme.ClassName(_view.Class))
        {
            style = { color = (Color)BardTheme.Gilt, fontSize = 26, unityFontStyleAndWeight = FontStyle.Bold, whiteSpace = WhiteSpace.Normal, flexShrink = 1, marginBottom = 4 },
        };
        BardFont.ApplyDisplay(name);
        part.Add(name);

        part.Add(RankLeaves());

        VisualElement bar = XpBar.Build(frac, (Color)BardTheme.Leaf);
        bar.style.width = 176;
        part.Add(bar);
        part.Add(new Label($"{Mathf.RoundToInt(frac * 100f)}% to next rank")
        {
            style = { color = (Color)BardTheme.Parchment, fontSize = 14, marginTop = 4 },
        });
        return part;
    }

    // The subrank shown as leaf pips: the label "Rank" then three leaves (subranks I/II/III), filled up to the
    // current subrank and dimmed beyond. Replaces the old Roman-numeral subrank next to the class name.
    private VisualElement RankLeaves()
    {
        const int total = 3; // subranks I, II, III
        int filled = _view.IsComplete ? total : Mathf.Clamp(_view.Subrank + 1, 0, total);

        var row = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center, marginBottom = 4 } };
        var rankLabel = new Label("Rank")
        {
            style = { color = (Color)BardTheme.Leaf, fontSize = 15, unityFontStyleAndWeight = FontStyle.Bold, marginRight = 8 },
        };
        BardFont.ApplyDisplay(rankLabel);
        row.Add(rankLabel);
        for (int i = 0; i < total; i++)
        {
            row.Add(new Image
            {
                image = i < filled ? _art.RankLeafFull() : _art.RankLeafEmpty(),
                pickingMode = PickingMode.Ignore,
                style = { width = 26, height = 26, marginRight = 3, flexShrink = 0 },
            });
        }

        return row;
    }

    // Parts 2-6: one attribute — its icon beside the axis name (in the axis color) and the level "/10", then a
    // glassy XP bar tinted the axis color, and current/next XP below.
    private VisualElement AttributePart(Attribute a, LevelCurve curve)
    {
        AttributeState state = _view.Profile[a];
        (_, double into, double needed) = curve.Progress(state.Xp);
        float frac = needed > 0 ? Mathf.Clamp01((float)(into / needed)) : 0f;
        Color color = BardTheme.AxisColor(a);

        var part = new VisualElement { style = { flexGrow = 1, flexShrink = 1, justifyContent = Justify.Center, paddingLeft = 10, paddingRight = 10 } };

        var topRow = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center, marginBottom = 4 } };
        topRow.Add(new Image
        {
            image = _art.AttributeIcon(a),
            pickingMode = PickingMode.Ignore,
            style = { width = 54, height = 54, marginRight = 12, flexShrink = 0 },
        });

        var textCol = new VisualElement { style = { flexGrow = 1, flexShrink = 1 } };
        var nameLabel = new Label(BardTheme.AxisName(a).ToUpperInvariant())
        {
            style = { color = color, fontSize = 17, unityFontStyleAndWeight = FontStyle.Bold, marginBottom = 2 },
        };
        BardFont.ApplyDisplay(nameLabel);
        textCol.Add(nameLabel);

        var valueRow = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.FlexEnd } };
        valueRow.Add(new Label(state.Level.ToString())
        {
            style = { color = (Color)BardTheme.Parchment, fontSize = 31, unityFontStyleAndWeight = FontStyle.Bold },
        });
        valueRow.Add(new Label("/10")
        {
            style = { color = (Color)BardTheme.Parchment, fontSize = 15, marginLeft = 4, marginBottom = 4 },
        });
        textCol.Add(valueRow);
        topRow.Add(textCol);
        part.Add(topRow);

        part.Add(XpBar.Build(frac, color));
        part.Add(new Label($"{Mathf.RoundToInt((float)into):n0} / {Mathf.RoundToInt((float)needed):n0} XP")
        {
            style = { color = (Color)BardTheme.Parchment, fontSize = 13, marginTop = 5 },
        });
        return part;
    }

    // A vertical divider between header parts, fading out toward its top and bottom edges (a generated gradient
    // texture stretched to the header's height).
    private static VisualElement Divider() => new()
    {
        style =
        {
            width = 2, marginLeft = 10, marginRight = 10, flexShrink = 0,
            backgroundImage = new StyleBackground(Background.FromTexture2D(DividerTexture())),
        },
    };

    private static Texture2D DividerTexture()
    {
        if (_dividerTex != null)
        {
            return _dividerTex;
        }

        const int h = 64;
        var tex = new Texture2D(1, h, TextureFormat.RGBA32, false);
        var px = new Color32[h];
        for (int y = 0; y < h; y++)
        {
            float a = Mathf.Sin(y / (float)(h - 1) * Mathf.PI); // 0 at both ends, 1 in the middle
            px[y] = new Color32(214, 183, 122, (byte)(a * 75f));
        }

        tex.SetPixels32(px);
        tex.Apply();
        _dividerTex = tex;
        return tex;
    }

    // Zone B — the wave: up to five selectable foe rows (or the lone boss). Light text (the frame's interior
    // is dark/the backdrop shows through the real frame's hollow center).
    private void BuildList()
    {
        _listBoard.Clear();
        _listBoard.Add(new Label(_view.AtClassBoss ? "— The Boss —" : "— The Wave —")
        {
            style = { color = (Color)BardTheme.Gilt, fontSize = 16, unityFontStyleAndWeight = FontStyle.Bold, unityTextAlign = TextAnchor.MiddleCenter, marginBottom = 12 },
        });

        if (_rows.Count == 0)
        {
            _listBoard.Add(new Label(_view.IsComplete ? "Your legend is complete" : "No monsters delivered")
            {
                style = { color = (Color)BardTheme.Parchment, fontSize = 16, unityTextAlign = TextAnchor.MiddleCenter, whiteSpace = WhiteSpace.Normal },
            });
            return;
        }

        for (int i = 0; i < _rows.Count; i++)
        {
            _listBoard.Add(WaveRow(_rows[i], i == _cursor));
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

    // Zone C — the selected foe: a big album/monster-frame pinned top-left, with title, artist, shield rank +
    // type, and the five demands (icon + 10 pills) in the column to its right; the green FIGHT plate pinned at
    // the bottom. The Panel interior is light parchment, so its text is dark.
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

        var topRow = new VisualElement { style = { flexDirection = FlexDirection.Row, flexShrink = 0 } };

        // Big album pinned top-left, in the monster frame. The detail column's padding already insets it past
        // the Panel's leafy corners, so it clears the vines.
        const float frameSize = 480f;
        var frameStack = new VisualElement { style = { width = frameSize, height = frameSize, flexShrink = 0, alignSelf = Align.FlexStart } };
        var album = new Image
        {
            image = info?.Album,
            // YARGImage decodes bottom-up; flip vertically. Inset to the frame art's transparent window (~65%).
            style = { position = Position.Absolute, left = 84, top = 84, width = 312, height = 312 },
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
        topRow.Add(frameStack);

        // Info column to the right of the album, centered against the tall album: title, artist, rank + type,
        // then the demand pills.
        var infoCol = new VisualElement { style = { flexGrow = 1, flexShrink = 1, marginLeft = 24, justifyContent = Justify.Center, alignItems = Align.Center } };
        var title = new Label(info?.Title ?? "Unknown")
        {
            style = { color = (Color)BardTheme.Nightwood, fontSize = 26, unityFontStyleAndWeight = FontStyle.Bold, whiteSpace = WhiteSpace.Normal, unityTextAlign = TextAnchor.MiddleCenter },
        };
        BardFont.ApplyDisplay(title);
        infoCol.Add(title);
        infoCol.Add(new Label(info?.Artist ?? "")
        {
            style = { color = (Color)BardTheme.OldWood, fontSize = 16, whiteSpace = WhiteSpace.Normal, unityTextAlign = TextAnchor.MiddleCenter, marginBottom = 10 },
        });

        var meta = new VisualElement { style = { flexDirection = FlexDirection.Column, alignItems = Align.Center, marginBottom = 4 } };
        meta.Add(new Image
        {
            image = _art.RankBadge(m.Profile.ToRank()),
            pickingMode = PickingMode.Ignore,
            style = { width = 48, height = 48, flexShrink = 0 },
        });
        meta.Add(new Label(TypeTag(m.Type))
        {
            style = { color = (Color)BardTheme.Ember, fontSize = 16, unityFontStyleAndWeight = FontStyle.Bold, marginTop = 4 },
        });
        infoCol.Add(meta);

        // The demands live in a frame whose corners round INWARD (concave), drawn with Painter2D — a CSS
        // borderRadius only rounds outward. An inner padded box holds the pills; the outer element sizes to it
        // and strokes the concave border around the full box.
        var attrFrame = new VisualElement { style = { marginTop = 12 } };
        attrFrame.generateVisualContent += ctx => DrawInwardBorder(ctx, (Color)BardTheme.OldWood, 2.5f, 14f);
        var attrInner = new VisualElement { style = { paddingTop = 10, paddingBottom = 12, paddingLeft = 18, paddingRight = 18 } };
        foreach (Attribute a in Axes)
        {
            attrInner.Add(DemandPills(a, m.Profile[a]));
        }

        attrFrame.Add(attrInner);
        infoCol.Add(attrFrame);

        topRow.Add(infoCol);
        _detailCol.Add(topRow);

        _detailCol.Add(new VisualElement { style = { flexGrow = 1 } }); // push the FIGHT plate to the bottom

        var cta = new VisualElement { style = { height = 64, flexDirection = FlexDirection.Row, alignItems = Align.Center, justifyContent = Justify.Center } };
        BardChrome.BannerPrimary(cta, _art, 64);
        if (!m.Defeated)
        {
            cta.Add(GreenGlyph());
        }

        cta.Add(new Label(m.Defeated ? "Already cleared" : "FIGHT")
        {
            style = { color = (Color)(m.Defeated ? BardTheme.Gilt : BardTheme.Parchment), fontSize = 22, unityFontStyleAndWeight = FontStyle.Bold, unityTextAlign = TextAnchor.MiddleCenter },
        });
        _detailCol.Add(cta);
    }

    // One foe demand: the attribute icon followed by a 10-pill bar filled to the demand value (no number).
    private VisualElement DemandPills(Attribute a, double demand)
    {
        int value = Mathf.Clamp(Mathf.RoundToInt((float)demand), 0, 10);
        Color fill = BardTheme.AxisColor(a);
        var empty = new Color(0f, 0f, 0f, 0.16f); // faint hollow on the light parchment
        var row = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center, marginTop = 7 } };
        row.Add(new Image
        {
            image = _art.AttributeIcon(a),
            pickingMode = PickingMode.Ignore,
            style = { width = 70, height = 70, marginRight = 12, flexShrink = 0 },
        });
        for (int i = 0; i < 10; i++)
        {
            row.Add(new VisualElement
            {
                style =
                {
                    width = 12, height = 18, marginRight = 3, flexShrink = 0,
                    backgroundColor = i < value ? fill : empty,
                    borderTopLeftRadius = 3, borderTopRightRadius = 3, borderBottomLeftRadius = 3, borderBottomRightRadius = 3,
                },
            });
        }

        return row;
    }

    // Strokes a rounded-rect border whose corners round INWARD (concave) — a CSS borderRadius only rounds
    // outward. Each corner is a quarter arc centered on the rectangle's own corner, so it scoops into the box.
    private static void DrawInwardBorder(MeshGenerationContext ctx, Color color, float lineWidth, float radius)
    {
        VisualElement e = ctx.visualElement;
        float w = e.contentRect.width, h = e.contentRect.height;
        if (w <= 0f || h <= 0f)
        {
            return;
        }

        float o = lineWidth / 2f;
        float r = Mathf.Min(radius, (Mathf.Min(w, h) / 2f) - o);
        Painter2D p = ctx.painter2D;
        p.lineWidth = lineWidth;
        p.strokeColor = color;
        p.lineJoin = LineJoin.Round;
        p.lineCap = LineCap.Round;
        p.BeginPath();
        p.MoveTo(new Vector2(o + r, o));
        p.LineTo(new Vector2(w - o - r, o));
        AddConcaveCorner(p, new Vector2(w - o, o), r, 180f, 90f);
        p.LineTo(new Vector2(w - o, h - o - r));
        AddConcaveCorner(p, new Vector2(w - o, h - o), r, 270f, 180f);
        p.LineTo(new Vector2(o + r, h - o));
        AddConcaveCorner(p, new Vector2(o, h - o), r, 0f, -90f);
        p.LineTo(new Vector2(o, o + r));
        AddConcaveCorner(p, new Vector2(o, o), r, 90f, 0f);
        p.ClosePath();
        p.Stroke();
    }

    // Emits the polyline of one concave corner: a quarter arc of the circle centered on the rect's own corner
    // (so it curves into the box), from startDeg to endDeg.
    private static void AddConcaveCorner(Painter2D p, Vector2 c, float r, float startDeg, float endDeg)
    {
        const int steps = 6;
        for (int i = 1; i <= steps; i++)
        {
            float t = Mathf.Lerp(startDeg, endDeg, i / (float)steps) * Mathf.Deg2Rad;
            p.LineTo(new Vector2(c.x + (r * Mathf.Cos(t)), c.y + (r * Mathf.Sin(t))));
        }
    }

    // YARG's real green confirm glyph (the shared MenuStandard sprite tinted green via NavigationIcons), so the
    // FIGHT plate matches the game's PLAY SONG button. Falls back to the drawn pip if the menu singleton or its
    // icon set is unavailable.
    private VisualElement GreenGlyph()
    {
        NavigationIcons icons = MenuData.NavigationIcons;
        Sprite green = icons?.GetIcon(MenuAction.Green);
        if (green == null)
        {
            return GreenPip();
        }

        // Size the element to the sprite's native aspect (148x108) so the horizontal fret bar — the "dash" —
        // is not squished away by a forced square.
        return new Image
        {
            sprite = green,
            tintColor = icons.GetColor(MenuAction.Green),
            pickingMode = PickingMode.Ignore,
            style = { width = 48, height = 35, marginRight = 12, flexShrink = 0 },
        };
    }

    // Drawn fallback for GreenGlyph — a small green disc echoing the controller button; used only when YARG's
    // real glyph can't be reached.
    private static VisualElement GreenPip() => new()
    {
        style =
        {
            width = 30, height = 30, marginRight = 12, flexShrink = 0,
            backgroundColor = new Color(0.29f, 0.78f, 0.30f),
            borderTopLeftRadius = 15, borderTopRightRadius = 15, borderBottomLeftRadius = 15, borderBottomRightRadius = 15,
            borderTopWidth = 2, borderBottomWidth = 2, borderLeftWidth = 2, borderRightWidth = 2,
            borderTopColor = new Color(0.12f, 0.35f, 0.13f), borderBottomColor = new Color(0.12f, 0.35f, 0.13f),
            borderLeftColor = new Color(0.12f, 0.35f, 0.13f), borderRightColor = new Color(0.12f, 0.35f, 0.13f),
        },
    };

    // A callout appears only where there is a true objective — an Elite, a class boss, or completion. During
    // the ordinary grind the board itself carries the intent, so no line is drawn.
    private void UpdateCallout()
    {
        string text = null;
        if (_view.IsComplete)
        {
            text = "Your legend is complete";
        }
        else if (_view.AtClassBoss)
        {
            text = $"The {BardTheme.ClassName(_view.Class)} boss guards the road to {NextClassName()}";
        }
        else if (_view.AtMiniBoss)
        {
            int bar = Mathf.RoundToInt((float)(QuestLadder.MiniBossBar(_view.Class) * 100.0));
            text = $"An Elite blocks the path — beat it at {bar}%";
        }

        _callout.text = text ?? string.Empty;
        _callout.style.display = text == null ? DisplayStyle.None : DisplayStyle.Flex;
    }

    // The next class up the ladder; the class-boss phase never fires on the top class, so this is valid
    // wherever UpdateCallout uses it.
    private string NextClassName()
    {
        var next = (PlayerClass)((int)_view.Class + 1);
        return BardTheme.ClassName(next);
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
