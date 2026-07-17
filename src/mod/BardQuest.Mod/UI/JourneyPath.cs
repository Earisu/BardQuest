using BardQuest.Domain.Progression;

using UnityEngine;
using UnityEngine.UIElements;

namespace BardQuest.Mod.UI;

// The class ladder as a serpentine path of six medallion nodes with a connector line behind them: cleared
// nodes carry a green ring and a check badge, the current node renders larger with a pulsing gold glow and
// subrank pips, locked nodes are dimmed. Browsing (MoveSelection) is independent of node state — the Hub
// decides what selecting a cleared/locked node means; this component only tracks and reports the index.
public sealed class JourneyPath
{
    private const int NodeCount = 6;
    private const float PulsePeriodSeconds = 1.6f;

    // Serpentine anchors as percent of the component box, matching the mockup.
    private static readonly Vector2[] Anchors =
    [
        new(8f, 58f), new(25f, 32f), new(42f, 60f), new(59f, 32f), new(76f, 60f), new(92f, 38f),
    ];

    private readonly BardQuestArt _art;
    private readonly VisualElement[] _nodeWraps = new VisualElement[NodeCount];

    private IReadOnlyList<ClassNode> _nodes = [];
    private VisualElement? _currentGlow;
    private IVisualElementScheduledItem? _pulse;
    private int _currentIndex;

    public VisualElement Root { get; }

    public int Selected { get; private set; }

    public ClassNode SelectedNode => _nodes[Selected];

    public event System.Action? SelectionChanged;

    public JourneyPath(BardQuestArt art)
    {
        _art = art;
        Root = new VisualElement
        {
            style = { flexShrink = 0, height = 240, width = Length.Percent(100), position = Position.Relative },
        };
        Root.generateVisualContent += OnGenerateVisualContent;
    }

    public void Build(PlayerClass current, int currentSubrank)
    {
        _pulse?.Pause();
        _pulse = null;
        _currentGlow = null;
        Root.Clear();

        _nodes = ClassLadder.NodesFor(current);
        _currentIndex = 0;
        for (int i = 0; i < _nodes.Count; i++)
        {
            if (_nodes[i].State == ClassNodeState.Current)
            {
                _currentIndex = i;
            }
        }

        Selected = _currentIndex;

        for (int i = 0; i < NodeCount; i++)
        {
            VisualElement wrap = BuildNode(_nodes[i], i, currentSubrank);
            _nodeWraps[i] = wrap;
            Root.Add(wrap);
        }

        if (_currentGlow != null)
        {
            _pulse = Root.schedule.Execute(TickPulse).Every(16);
        }

        ApplySelectionVisuals();
        Root.MarkDirtyRepaint();
    }

    public void MoveSelection(int delta)
    {
        int next = Mathf.Clamp(Selected + delta, 0, NodeCount - 1);
        if (next == Selected)
        {
            return;
        }

        Selected = next;
        ApplySelectionVisuals();
        SelectionChanged?.Invoke();
    }

    private VisualElement BuildNode(ClassNode node, int index, int currentSubrank)
    {
        bool isCurrent = node.State == ClassNodeState.Current;
        float discSize = isCurrent ? 96f : 64f;
        Vector2 anchor = Anchors[index];

        var wrap = new VisualElement
        {
            style =
            {
                position = Position.Absolute,
                left = Length.Percent(anchor.x), top = Length.Percent(anchor.y),
                // Horizontally centre the disc on the anchor; vertically pin the disc's CENTRE (not the whole
                // wrap, which also holds the label/pips below) so the connector line meets the medallion middle.
                translate = new Translate(Length.Percent(-50), -discSize / 2f),
                alignItems = Align.Center,
            },
        };

        if (isCurrent)
        {
            const float glowSize = 160f;
            _currentGlow = new Image
            {
                image = _art.Glow(),
                pickingMode = PickingMode.Ignore,
                style =
                {
                    position = Position.Absolute,
                    width = glowSize, height = glowSize,
                    left = (discSize - glowSize) / 2f, top = (discSize - glowSize) / 2f,
                },
            };
            wrap.Add(_currentGlow);
        }

        var discWrap = new VisualElement
        {
            style = { position = Position.Relative, width = discSize, height = discSize },
        };

        switch (node.State)
        {
            case ClassNodeState.Cleared:
                discWrap.Add(BuildMedallion(node.Class, discSize));
                discWrap.Add(BuildRing(discSize, (Color)BardTheme.Glowmoss));
                discWrap.Add(BuildCheckBadge(discSize));
                break;
            case ClassNodeState.Current:
                discWrap.Add(BuildMedallion(node.Class, discSize));
                discWrap.Add(BuildRing(discSize, (Color)BardTheme.Gilt));
                break;
            default: // Locked: grey disc with a drawn padlock, no medallion.
                discWrap.Add(BuildLockedFace(discSize));
                break;
        }

        wrap.Add(discWrap);

        var label = new Label(BardTheme.ClassName(node.Class))
        {
            style =
            {
                color = (Color)(node.State == ClassNodeState.Locked ? BardTheme.OldWood : BardTheme.Parchment),
                fontSize = 14, marginTop = 6, unityTextAlign = TextAnchor.MiddleCenter,
                whiteSpace = WhiteSpace.NoWrap,
            },
        };
        BardFont.ApplyDisplay(label);
        wrap.Add(label);

        if (isCurrent)
        {
            wrap.Add(BuildPips(currentSubrank));
        }

        return wrap;
    }

    private Image BuildMedallion(PlayerClass cls, float discSize)
    {
        float r = discSize / 2f;
        return new Image
        {
            image = _art.ClassMedallion(cls),
            style =
            {
                width = discSize, height = discSize, overflow = Overflow.Hidden,
                borderTopLeftRadius = r, borderTopRightRadius = r,
                borderBottomLeftRadius = r, borderBottomRightRadius = r,
            },
        };
    }

    // The rank ring sits slightly inside the disc box so it hugs the medallion emblem rather than floating a
    // gap outside it.
    private static VisualElement BuildRing(float discSize, Color color)
    {
        float inset = discSize * 0.035f;
        float d = discSize - (inset * 2f);
        float r = d / 2f;
        return new VisualElement
        {
            pickingMode = PickingMode.Ignore,
            style =
            {
                position = Position.Absolute,
                left = inset, top = inset, width = d, height = d,
                borderTopLeftRadius = r, borderTopRightRadius = r,
                borderBottomLeftRadius = r, borderBottomRightRadius = r,
                borderTopWidth = 3, borderBottomWidth = 3, borderLeftWidth = 3, borderRightWidth = 3,
                borderTopColor = color, borderBottomColor = color, borderLeftColor = color, borderRightColor = color,
            },
        };
    }

    private static VisualElement BuildLockedFace(float discSize)
    {
        float r = discSize / 2f;
        var face = new VisualElement
        {
            style =
            {
                width = discSize, height = discSize,
                alignItems = Align.Center, justifyContent = Justify.Center,
                backgroundColor = new Color(0.34f, 0.34f, 0.36f, 1f),
                borderTopLeftRadius = r, borderTopRightRadius = r,
                borderBottomLeftRadius = r, borderBottomRightRadius = r,
                borderTopWidth = 2, borderBottomWidth = 2, borderLeftWidth = 2, borderRightWidth = 2,
                borderTopColor = (Color)BardTheme.Nightwood, borderBottomColor = (Color)BardTheme.Nightwood,
                borderLeftColor = (Color)BardTheme.Nightwood, borderRightColor = (Color)BardTheme.Nightwood,
            },
        };
        face.Add(BuildLockGlyph(discSize));
        return face;
    }

    // A padlock drawn from primitives (the 🔒 glyph tofus on the fallback font): an open ∩ shackle (top + side
    // borders only) overlapping a filled body block.
    private static VisualElement BuildLockGlyph(float discSize)
    {
        float s = discSize / 64f;
        var ink = new Color(0.14f, 0.14f, 0.16f, 1f);
        var glyph = new VisualElement
        {
            pickingMode = PickingMode.Ignore,
            style = { alignItems = Align.Center, justifyContent = Justify.Center },
        };
        glyph.Add(new VisualElement
        {
            style =
            {
                width = 13f * s, height = 10f * s, marginBottom = -2f * s,
                borderTopWidth = 3f * s, borderLeftWidth = 3f * s, borderRightWidth = 3f * s,
                borderTopLeftRadius = 7f * s, borderTopRightRadius = 7f * s,
                borderTopColor = ink, borderLeftColor = ink, borderRightColor = ink,
            },
        });
        glyph.Add(new VisualElement
        {
            style =
            {
                width = 20f * s, height = 15f * s, backgroundColor = ink,
                borderTopLeftRadius = 3f * s, borderTopRightRadius = 3f * s,
                borderBottomLeftRadius = 3f * s, borderBottomRightRadius = 3f * s,
            },
        });
        return glyph;
    }

    // A check badge drawn from primitives (no ✓ glyph): a small green plate holding a checkmark made the
    // same way AppHeader/CreateQuestScreen draw chevrons — a single element's two adjacent borders, rotated.
    private static VisualElement BuildCheckBadge(float discSize)
    {
        var badge = new VisualElement
        {
            pickingMode = PickingMode.Ignore,
            style =
            {
                position = Position.Absolute, right = -2, top = -2, width = 22, height = 22,
                backgroundColor = (Color)BardTheme.Glowmoss,
                borderTopLeftRadius = 11, borderTopRightRadius = 11, borderBottomLeftRadius = 11, borderBottomRightRadius = 11,
                borderTopWidth = 2, borderBottomWidth = 2, borderLeftWidth = 2, borderRightWidth = 2,
                borderTopColor = (Color)BardTheme.Nightwood, borderBottomColor = (Color)BardTheme.Nightwood,
                borderLeftColor = (Color)BardTheme.Nightwood, borderRightColor = (Color)BardTheme.Nightwood,
                alignItems = Align.Center, justifyContent = Justify.Center,
            },
        };
        badge.Add(new VisualElement
        {
            pickingMode = PickingMode.Ignore,
            style =
            {
                width = 10, height = 6, marginTop = -2,
                borderBottomWidth = 2, borderRightWidth = 2,
                borderBottomColor = Color.white, borderRightColor = Color.white,
                rotate = new Rotate(new Angle(45f, AngleUnit.Degree)),
            },
        });
        return badge;
    }

    // currentSubrank is 0-based (ClassDerivation.SubranksPerClass = 3, Roman(0)="I"); pip i lights when
    // i <= currentSubrank, so subrank 0 shows one lit pip ("I") and subrank 2 shows all three ("III").
    private static VisualElement BuildPips(int currentSubrank)
    {
        var row = new VisualElement
        {
            pickingMode = PickingMode.Ignore,
            style = { flexDirection = FlexDirection.Row, marginTop = 4, justifyContent = Justify.Center },
        };
        for (int i = 0; i < ClassDerivation.SubranksPerClass; i++)
        {
            bool lit = i <= currentSubrank;
            row.Add(new VisualElement
            {
                style =
                {
                    width = 8, height = 8, marginLeft = 2, marginRight = 2,
                    backgroundColor = lit ? (Color)BardTheme.Gilt : (Color)BardTheme.OldWood,
                    borderTopLeftRadius = 4, borderTopRightRadius = 4, borderBottomLeftRadius = 4, borderBottomRightRadius = 4,
                },
            });
        }

        return row;
    }

    private void ApplySelectionVisuals()
    {
        for (int i = 0; i < NodeCount; i++)
        {
            bool selected = i == Selected;
            _nodeWraps[i].style.scale = new Scale(Vector2.one * (selected ? 1.06f : 1f));
        }
    }

    private void TickPulse()
    {
        if (_currentGlow == null)
        {
            return;
        }

        float u = Mathf.Repeat(Time.realtimeSinceStartup / PulsePeriodSeconds, 1f);
        float k = 0.5f - (0.5f * Mathf.Cos(u * 2f * Mathf.PI)); // 0..1, gentle cosine ease
        _currentGlow.style.opacity = Mathf.Lerp(0.55f, 1.0f, k);
        _currentGlow.style.scale = new Scale(Vector2.one * Mathf.Lerp(1.0f, 1.08f, k));
    }

    // Connector polyline behind the nodes: segments up to the current node read as travelled (green), the
    // rest as not-yet-reached (dim wood). Anchors are percent-of-box, converted to pixels via contentRect.
    private void OnGenerateVisualContent(MeshGenerationContext ctx)
    {
        float w = Root.contentRect.width, h = Root.contentRect.height;
        if (w <= 0 || h <= 0 || _nodes.Count == 0)
        {
            return;
        }

        Painter2D p = ctx.painter2D;
        p.lineWidth = 4f;
        for (int i = 0; i < NodeCount - 1; i++)
        {
            var a = new Vector2(w * Anchors[i].x / 100f, h * Anchors[i].y / 100f);
            var b = new Vector2(w * Anchors[i + 1].x / 100f, h * Anchors[i + 1].y / 100f);

            // Segments among cleared nodes read as travelled (solid green); segments touching a not-yet-reached
            // (locked) node read as a dashed dim-wood trail — the current node is at _currentIndex, everything
            // beyond it is locked, so segment i touches a locked node once i >= _currentIndex.
            bool travelled = i < _currentIndex;
            p.strokeColor = travelled ? (Color)BardTheme.Glowmoss : (Color)BardTheme.OldWood;
            if (travelled)
            {
                StrokeLine(p, a, b);
            }
            else
            {
                StrokeDashed(p, a, b);
            }
        }
    }

    private static void StrokeLine(Painter2D p, Vector2 a, Vector2 b)
    {
        p.BeginPath();
        p.MoveTo(a);
        p.LineTo(b);
        p.Stroke();
    }

    private static void StrokeDashed(Painter2D p, Vector2 a, Vector2 b)
    {
        const float dash = 9f, gap = 7f;
        float total = Vector2.Distance(a, b);
        if (total <= 0f)
        {
            return;
        }

        Vector2 dir = (b - a) / total;
        for (float pos = 0f; pos < total; pos += dash + gap)
        {
            float end = Mathf.Min(pos + dash, total);
            p.BeginPath();
            p.MoveTo(a + (dir * pos));
            p.LineTo(a + (dir * end));
            p.Stroke();
        }
    }
}
