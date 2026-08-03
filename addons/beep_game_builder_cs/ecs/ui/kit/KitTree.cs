using Godot;
using System.Collections.Generic;

namespace Beep.ECS.UI.Kit
{
    /// <summary>
    /// A skill / upgrade / research tree: nodes on a tier grid, joined by connector lines.
    ///
    /// Measured from Example_Art/skilltree.png and skilltree1.png:
    ///  - node <b>~50px square</b> on a 430px-wide screen, <b>7-14px gutters</b> — roughly 12% of
    ///    the tile, so the grid is derived from node size rather than set independently.
    ///  - connectors are <b>thin ORTHOGONAL lines running at right angles</b>, drawn
    ///    <b>BEHIND</b> the nodes (skilltree1 states this explicitly).
    ///  - a <b>locked node is a DARK SILHOUETTE</b> — "art rendered near-black, no colour, no
    ///    number". Not a dimmed version of the owned node: the art is present but drained.
    ///  - an <b>owned node is full-colour</b>, S=0.66-1.00.
    ///
    /// The governing rule, stated twice in skilltree1.md and taken verbatim:
    /// <b>"Spend colour on branch identity OR on node state, not both."</b> Doing both produces a
    /// tree where neither reading survives, so <see cref="ColourCarries"/> is an either/or and
    /// deliberately not two independent toggles.
    ///
    /// skilltree.md also notes the branch-colour scheme is "greyscale-hostile but colour-blind-
    /// survivable if the branches are also positional" — which they are here, since a branch owns
    /// a column. That is why branch colour is allowed to be the only cue.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class KitTree : KitControl
    {
        public enum NodeState { Locked, Available, Owned }

        /// <summary>Which axis the palette is spent on. Never both — see the class remarks.</summary>
        public enum ColourAxis { Branch, State }

        public sealed class Node
        {
            public int Column;
            public int Tier;
            /// <summary>Index into <see cref="BranchRoles"/>.</summary>
            public int Branch;
            public NodeState State = NodeState.Locked;
            public Texture2D? Icon;
            /// <summary>Cost badge at the corner. 0 hides it.</summary>
            public int Cost;
            /// <summary>Indices of nodes this one connects up to.</summary>
            public readonly List<int> Parents = new();
        }

        [Export] public ColourAxis ColourCarries { get; set; } = ColourAxis.Branch;

        /// <summary>Palette role per branch. Branch identity is read before anything else on the
        /// screen, so it comes from the theme rather than from literals.</summary>
        public UiSurface.Role[] BranchRoles =
        {
            UiSurface.Role.Info, UiSurface.Role.Success,
            UiSurface.Role.Warning, UiSurface.Role.Accent2,
        };

        public readonly List<Node> Nodes = new();

        [Export(PropertyHint.Range, "1,10,1")] public int Columns { get => _cols; set { _cols = Mathf.Max(1, value); QueueRedraw(); } }
        private int _cols = 4;

        [Export(PropertyHint.Range, "1,10,1")] public int Tiers { get => _tiers; set { _tiers = Mathf.Max(1, value); QueueRedraw(); } }
        private int _tiers = 3;

        [Export] public int Selected { get => _sel; set { _sel = value; QueueRedraw(); } }
        private int _sel = -1;
        private int _hover = -1;

        [Signal] public delegate void NodeActivatedEventHandler(int index);

        public override void _Ready()
        {
            base._Ready();
            MouseFilter = MouseFilterEnum.Stop;
            if (Nodes.Count == 0)
                SeedDemoNodes();
            if (CustomMinimumSize == Vector2.Zero)
            {
                int fs = UiSurface.FontSize(this);
                float pitch = fs * 3.6f;
                CustomMinimumSize = new Vector2(pitch * _cols, pitch * _tiers);
            }
        }

        private float Pitch() => Mathf.Min(Size.X / _cols, Size.Y / _tiers);

        private void SeedDemoNodes()
        {
            _cols = Mathf.Max(_cols, 4);
            _tiers = Mathf.Max(_tiers, 3);

            Nodes.Add(new Node { Column = 1, Tier = 0, Branch = 0, State = NodeState.Owned, Cost = 1 });
            Nodes.Add(new Node { Column = 0, Tier = 1, Branch = 0, State = NodeState.Owned, Cost = 1, Parents = { 0 } });
            Nodes.Add(new Node { Column = 1, Tier = 1, Branch = 1, State = NodeState.Available, Cost = 2, Parents = { 0 } });
            Nodes.Add(new Node { Column = 2, Tier = 1, Branch = 2, State = NodeState.Locked, Parents = { 0 } });
            Nodes.Add(new Node { Column = 0, Tier = 2, Branch = 0, State = NodeState.Available, Cost = 3, Parents = { 1 } });
            Nodes.Add(new Node { Column = 1, Tier = 2, Branch = 1, State = NodeState.Locked, Parents = { 2 } });
            Nodes.Add(new Node { Column = 2, Tier = 2, Branch = 2, State = NodeState.Locked, Parents = { 3 } });
            Nodes.Add(new Node { Column = 3, Tier = 2, Branch = 3, State = NodeState.Locked, Parents = { 3 } });
        }

        /// <summary>Node box. The gutter is ~12% of the tile, per the measured 7-14px on ~50px.</summary>
        private Rect2 NodeRect(Node n)
        {
            float pitch = Pitch();
            float side = pitch * 0.78f;
            float pad = (pitch - side) * 0.5f;
            return new Rect2(n.Column * pitch + pad, n.Tier * pitch + pad, side, side);
        }

        private Color BranchColor(Node n)
            => BranchRoles.Length == 0
                ? UiSurface.Semantic(this, UiSurface.Role.Accent)
                : UiSurface.Semantic(this, BranchRoles[Mathf.PosMod(n.Branch, BranchRoles.Length)]);

        private Color StateColor(NodeState s) => s switch
        {
            NodeState.Owned => UiSurface.Semantic(this, UiSurface.Role.Success),
            NodeState.Available => UiSurface.Semantic(this, UiSurface.Role.Info),
            _ => UiSurface.Semantic(this, UiSurface.Role.Neutral),
        };

        /// <summary>The colour this node is entitled to spend, on whichever single axis the tree
        /// has chosen. A locked node spends none — it is a silhouette.</summary>
        private Color CueColor(Node n)
        {
            if (n.State == NodeState.Locked) return default;
            return ColourCarries == ColourAxis.Branch ? BranchColor(n) : StateColor(n.State);
        }

        public override void _GuiInput(InputEvent @event)
        {
            if (@event is InputEventMouseMotion mm)
            {
                int next = HitNode(mm.Position);
                if (next != _hover)
                {
                    _hover = next;
                    QueueRedraw();
                }
                return;
            }

            if (@event is not InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } mb)
                return;
            int hit = HitNode(mb.Position);
            if (hit >= 0)
            {
                Selected = hit;
                EmitSignal(SignalName.NodeActivated, hit);
                AcceptEvent();
            }
        }

        private int HitNode(Vector2 p)
        {
            for (int i = 0; i < Nodes.Count; i++)
                if (NodeRect(Nodes[i]).HasPoint(p)) return i;
            return -1;
        }

        private KitShape NodeShape => Geo.Register == KitRegister.Pixel ? KitShape.Stepped : KitShape.Round;

        public override void _Draw()
        {
            if (Size.X <= 8 || Size.Y <= 8 || Nodes.Count == 0) return;

            var g = Geo;
            Color face = FaceColor();
            Color ink = InkColor();
            var font = KitFont();
            int fs = UiSurface.FontSize(this);
            float pitch = Pitch();

            float lane = Mathf.Max(1f, pitch * 0.025f);
            for (int c = 0; c < _cols; c++)
            {
                float x = pitch * (c + 0.5f);
                DrawLine(new Vector2(x, pitch * 0.18f), new Vector2(x, Size.Y - pitch * 0.18f),
                         new Color(face.R * 0.65f, face.G * 0.65f, face.B * 0.70f, 0.28f), lane);
            }

            // Connectors first, so they run behind the nodes.
            float lw = Mathf.Max(2f, pitch * 0.055f);
            foreach (var n in Nodes)
            {
                Rect2 nr = NodeRect(n);
                var childTop = new Vector2(nr.Position.X + nr.Size.X * 0.5f, nr.Position.Y);
                foreach (int pi in n.Parents)
                {
                    if (pi < 0 || pi >= Nodes.Count) continue;
                    Rect2 pr = NodeRect(Nodes[pi]);
                    var parentBottom = new Vector2(pr.Position.X + pr.Size.X * 0.5f, pr.End.Y);

                    // A connector is lit only when the link is actually earned; an unearned one
                    // stays neutral so the eye follows the owned path.
                    bool lit = n.State != NodeState.Locked && Nodes[pi].State == NodeState.Owned;
                    Color line = lit ? CueColor(n) : new Color(face.R * 0.5f, face.G * 0.5f, face.B * 0.55f);
                    if (lit && line == default) line = ink;

                    // ORTHOGONAL: down from the parent, across, then down into the child. Never a
                    // diagonal — the reference runs every link at right angles.
                    float midY = (parentBottom.Y + childTop.Y) * 0.5f;
                    DrawLine(parentBottom, new Vector2(parentBottom.X, midY), line, lw);
                    DrawLine(new Vector2(parentBottom.X, midY), new Vector2(childTop.X, midY), line, lw);
                    DrawLine(new Vector2(childTop.X, midY), childTop, line, lw);
                }
            }

            // Nodes.
            for (int i = 0; i < Nodes.Count; i++)
            {
                Node n = Nodes[i];
                Rect2 r = NodeRect(n);
                if (r.Size.X < 3f) continue;

                Color cue = CueColor(n);
                Color plate;
                if (n.State == NodeState.Locked)
                {
                    // Dark silhouette: near-black, no colour. Not a faded owned node.
                    plate = new Color(face.R * 0.22f, face.G * 0.22f, face.B * 0.25f, 1f);
                }
                else
                {
                    float k = n.State == NodeState.Owned ? 0.62f : 0.30f;
                    plate = new Color(Mathf.Lerp(face.R, cue.R, k),
                                      Mathf.Lerp(face.G, cue.G, k),
                                      Mathf.Lerp(face.B, cue.B, k), 1f);
                }

                DrawShape(r, NodeShape, plate, ink, Mathf.Max(1f, g.Rim * 0.7f * (fs / 14f)));

                if (n.State == NodeState.Available)
                {
                    Color ring = cue == default ? UiSurface.Semantic(this, UiSurface.Role.Info) : cue;
                    var poly = KitChrome.Poly(NodeShape, r.Grow(r.Size.X * 0.08f), Geo);
                    KitSelect.Draw(this, Geo.SelectFor(WidgetClass), poly, r.Grow(r.Size.X * 0.08f),
                                   ring, Mathf.Max(1.5f, pitch * 0.035f));
                }

                if (n.Icon != null)
                {
                    Color mod = n.State == NodeState.Locked
                        ? new Color(0.10f, 0.10f, 0.12f, 1f)     // silhouette
                        : Colors.White;
                    DrawTextureRect(n.Icon, r.Grow(-r.Size.X * 0.20f), false, mod);
                }
                else
                {
                    DrawNodeGlyph(r, n, cue == default ? ink : cue, face);
                }

                // Cost badge at the corner — and never on a locked node, which shows "no number".
                if (n.Cost > 0 && n.State != NodeState.Locked && font != null)
                {
                    string txt = n.Cost.ToString();
                    int small = UiSurface.FitRole(this, UiSurface.TextRole.Small,
                                                  new Vector2(r.Size.X * 0.50f, r.Size.Y * 0.34f),
                                                  txt, font, min: 8);
                    Vector2 m = font.GetStringSize(txt, HorizontalAlignment.Left, -1, small);
                    float bw = Mathf.Max(m.X + small * 0.7f, small * 1.4f), bh = small * 1.2f;
                    var b = new Rect2(r.End.X - bw * 0.55f, r.Position.Y - bh * 0.35f, bw, bh);
                    DrawShape(b, KitShape.Pill, UiSurface.Semantic(this, UiSurface.Role.Warning), ink, 1.5f);
                    DrawText(font, new Vector2(b.Position.X + (b.Size.X - m.X) * 0.5f, b.Position.Y + (b.Size.Y + m.Y * 0.6f) * 0.5f),
                               txt, small, new Color(0.10f, 0.09f, 0.08f));
                }

                DrawStatePip(r, n, cue == default ? ink : cue, face);

                // The theme's declared cues, not a hardcoded cream ring.
                if (i == _hover && i != _sel)
                    KitSelect.Draw(this, Geo.SelectFor(WidgetClass),
                                   KitChrome.Poly(NodeShape, r, Geo), r,
                                   UiSurface.Semantic(this, UiSurface.Role.Info),
                                   Mathf.Max(1.5f, 2f * (fs / 14f)));

                if (i == _sel)
                    KitSelect.Draw(this, Geo.SelectFor(WidgetClass),
                                   KitChrome.Poly(NodeShape, r, Geo), r,
                                   UiSurface.Semantic(this, UiSurface.Role.Accent),
                                   Mathf.Max(2f, 3f * (fs / 14f)));
            }

            DrawAttachments();
        }

        private void DrawNodeGlyph(Rect2 r, Node n, Color cue, Color face)
        {
            Vector2 c = r.Position + r.Size * 0.5f;
            float w = Mathf.Max(2f, r.Size.X * 0.07f);
            if (n.State == NodeState.Locked)
            {
                Color lockInk = new Color(0.86f, 0.86f, 0.88f, 0.50f);
                DrawArc(c + new Vector2(0, -r.Size.Y * 0.05f), r.Size.X * 0.15f,
                        Mathf.Pi, Mathf.Tau, 12, lockInk, w);
                DrawRect(new Rect2(c.X - r.Size.X * 0.17f, c.Y, r.Size.X * 0.34f, r.Size.Y * 0.22f),
                         lockInk);
                return;
            }

            Color glyph = n.State == NodeState.Owned
                ? new Color(face.R * 0.12f, face.G * 0.12f, face.B * 0.12f, 0.95f)
                : new Color(cue.R, cue.G, cue.B, 0.92f);
            DrawArc(c, r.Size.X * 0.23f, 0f, Mathf.Tau, 24, glyph, w);
            DrawLine(c - new Vector2(r.Size.X * 0.17f, 0), c + new Vector2(r.Size.X * 0.17f, 0), glyph, w);
            DrawLine(c - new Vector2(0, r.Size.X * 0.17f), c + new Vector2(0, r.Size.X * 0.17f), glyph, w);
        }

        private void DrawStatePip(Rect2 r, Node n, Color cue, Color face)
        {
            Vector2 c = r.Position + new Vector2(r.Size.X * 0.18f, r.Size.Y * 0.18f);
            float rr = r.Size.X * 0.10f;
            if (n.State == NodeState.Owned)
            {
                Color ok = UiSurface.Semantic(this, UiSurface.Role.Success);
                DrawCircle(c, rr, ok);
                DrawLine(c + new Vector2(-rr * 0.48f, -rr * 0.04f), c + new Vector2(-rr * 0.12f, rr * 0.35f),
                         face, Mathf.Max(1.2f, rr * 0.25f));
                DrawLine(c + new Vector2(-rr * 0.12f, rr * 0.35f), c + new Vector2(rr * 0.55f, -rr * 0.45f),
                         face, Mathf.Max(1.2f, rr * 0.25f));
            }
            else if (n.State == NodeState.Available)
            {
                DrawCircle(c, rr, cue);
                DrawLine(c - new Vector2(rr * 0.45f, 0), c + new Vector2(rr * 0.45f, 0), face, Mathf.Max(1.2f, rr * 0.22f));
                DrawLine(c - new Vector2(0, rr * 0.45f), c + new Vector2(0, rr * 0.45f), face, Mathf.Max(1.2f, rr * 0.22f));
            }
        }
    }
}
