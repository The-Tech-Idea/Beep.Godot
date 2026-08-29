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

        [Export] public ColourAxis ColourCarries { get => _colourCarries; set { if (_colourCarries == value) return; _colourCarries = value; RefreshVisualAndRedraw(); } }
        private ColourAxis _colourCarries = ColourAxis.Branch;

        /// <summary>Palette role per branch. Branch identity is read before anything else on the
        /// screen, so it comes from the theme rather than from literals.</summary>
        public UiSurface.Role[] BranchRoles =
        {
            UiSurface.Role.Info, UiSurface.Role.Success,
            UiSurface.Role.Warning, UiSurface.Role.Accent2,
        };

        [Export]
        public int[] BranchRoleOrdinals
        {
            get
            {
                var roles = new int[BranchRoles.Length];
                for (int i = 0; i < BranchRoles.Length; i++)
                    roles[i] = (int)BranchRoles[i];
                return roles;
            }
            set => SetBranchRoleOrdinals(value);
        }

        public readonly List<Node> Nodes = new();

        [Export]
        public int[] NodeColumns
        {
            get
            {
                var columns = new int[Nodes.Count];
                for (int i = 0; i < Nodes.Count; i++)
                    columns[i] = Nodes[i].Column;
                return columns;
            }
            set => SetNodeColumns(value);
        }

        [Export]
        public int[] NodeTiers
        {
            get
            {
                var tiers = new int[Nodes.Count];
                for (int i = 0; i < Nodes.Count; i++)
                    tiers[i] = Nodes[i].Tier;
                return tiers;
            }
            set => SetNodeTiers(value);
        }

        [Export]
        public int[] NodeBranches
        {
            get
            {
                var branches = new int[Nodes.Count];
                for (int i = 0; i < Nodes.Count; i++)
                    branches[i] = Nodes[i].Branch;
                return branches;
            }
            set => SetNodeBranches(value);
        }

        [Export]
        public int[] NodeStates
        {
            get
            {
                var states = new int[Nodes.Count];
                for (int i = 0; i < Nodes.Count; i++)
                    states[i] = (int)Nodes[i].State;
                return states;
            }
            set => SetNodeStates(value);
        }

        [Export]
        public Texture2D[] NodeIcons
        {
            get
            {
                var icons = new Texture2D[Nodes.Count];
                for (int i = 0; i < Nodes.Count; i++)
                    icons[i] = Nodes[i].Icon!;
                return icons;
            }
            set => SetNodeIcons(value);
        }

        [Export]
        public int[] NodeCosts
        {
            get
            {
                var costs = new int[Nodes.Count];
                for (int i = 0; i < Nodes.Count; i++)
                    costs[i] = Nodes[i].Cost;
                return costs;
            }
            set => SetNodeCosts(value);
        }

        [Export]
        public string[] NodeParentIndices
        {
            get
            {
                var parents = new string[Nodes.Count];
                for (int i = 0; i < Nodes.Count; i++)
                    parents[i] = string.Join(",", Nodes[i].Parents);
                return parents;
            }
            set => SetNodeParentIndices(value);
        }

        public void SetNodes(IEnumerable<Node>? nodes, bool expandBounds = true)
        {
            List<Node> next = NormalizeNodes(nodes);
            if (SameNodes(Nodes, next))
                return;
            Nodes.Clear();
            Nodes.AddRange(next);
            RefreshNodes(expandBounds);
        }

        public void SetNodeColumns(int[]? columns)
        {
            int count = columns?.Length ?? 0;
            bool changed = Nodes.Count != count;
            while (Nodes.Count > count)
                Nodes.RemoveAt(Nodes.Count - 1);
            for (int i = 0; i < count; i++)
            {
                EnsureNode(i);
                int next = Mathf.Max(0, columns![i]);
                if (Nodes[i].Column == next) continue;
                Nodes[i].Column = next;
                changed = true;
            }
            if (!changed) return;
            RefreshNodes();
        }

        public void SetNodeTiers(int[]? tiers)
        {
            if (tiers == null)
            {
                bool changed = false;
                for (int i = 0; i < Nodes.Count; i++)
                {
                    if (Nodes[i].Tier == 0) continue;
                    Nodes[i].Tier = 0;
                    changed = true;
                }
                if (!changed) return;
                RefreshNodes();
                return;
            }

            bool updated = false;
            for (int i = 0; i < tiers.Length; i++)
            {
                EnsureNode(i);
                int next = Mathf.Max(0, tiers[i]);
                if (Nodes[i].Tier == next) continue;
                Nodes[i].Tier = next;
                updated = true;
            }
            for (int i = tiers.Length; i < Nodes.Count; i++)
            {
                if (Nodes[i].Tier == 0) continue;
                Nodes[i].Tier = 0;
                updated = true;
            }
            if (!updated) return;
            RefreshNodes();
        }

        public void SetNodeBranches(int[]? branches)
        {
            if (branches == null)
            {
                bool changed = false;
                for (int i = 0; i < Nodes.Count; i++)
                {
                    if (Nodes[i].Branch == 0) continue;
                    Nodes[i].Branch = 0;
                    changed = true;
                }
                if (!changed) return;
                RefreshNodes();
                return;
            }

            bool updated = false;
            for (int i = 0; i < branches.Length; i++)
            {
                EnsureNode(i);
                int next = Mathf.Max(0, branches[i]);
                if (Nodes[i].Branch == next) continue;
                Nodes[i].Branch = next;
                updated = true;
            }
            for (int i = branches.Length; i < Nodes.Count; i++)
            {
                if (Nodes[i].Branch == 0) continue;
                Nodes[i].Branch = 0;
                updated = true;
            }
            if (!updated) return;
            RefreshNodes();
        }

        public void SetNodeStates(int[]? states)
        {
            if (states == null)
            {
                bool changed = false;
                for (int i = 0; i < Nodes.Count; i++)
                {
                    if (Nodes[i].State == NodeState.Locked) continue;
                    Nodes[i].State = NodeState.Locked;
                    changed = true;
                }
                if (!changed) return;
                RefreshNodes();
                return;
            }

            bool updated = false;
            for (int i = 0; i < states.Length; i++)
            {
                EnsureNode(i);
                NodeState next = StateFromOrdinal(states[i]);
                if (Nodes[i].State == next) continue;
                Nodes[i].State = next;
                updated = true;
            }
            for (int i = states.Length; i < Nodes.Count; i++)
            {
                if (Nodes[i].State == NodeState.Locked) continue;
                Nodes[i].State = NodeState.Locked;
                updated = true;
            }
            if (!updated) return;
            RefreshNodes();
        }

        public void SetNodeIcons(Texture2D[]? icons)
        {
            if (icons == null)
            {
                bool changed = false;
                for (int i = 0; i < Nodes.Count; i++)
                {
                    if (Nodes[i].Icon == null) continue;
                    Nodes[i].Icon = null;
                    changed = true;
                }
                if (!changed) return;
                RefreshNodes();
                return;
            }

            bool updated = false;
            for (int i = 0; i < icons.Length; i++)
            {
                EnsureNode(i);
                if (Nodes[i].Icon == icons[i]) continue;
                Nodes[i].Icon = icons[i];
                updated = true;
            }
            for (int i = icons.Length; i < Nodes.Count; i++)
            {
                if (Nodes[i].Icon == null) continue;
                Nodes[i].Icon = null;
                updated = true;
            }
            if (!updated) return;
            RefreshNodes();
        }

        public void SetNodeCosts(int[]? costs)
        {
            if (costs == null)
            {
                bool changed = false;
                for (int i = 0; i < Nodes.Count; i++)
                {
                    if (Nodes[i].Cost == 0) continue;
                    Nodes[i].Cost = 0;
                    changed = true;
                }
                if (!changed) return;
                RefreshNodes();
                return;
            }

            bool updated = false;
            for (int i = 0; i < costs.Length; i++)
            {
                EnsureNode(i);
                int next = Mathf.Max(0, costs[i]);
                if (Nodes[i].Cost == next) continue;
                Nodes[i].Cost = next;
                updated = true;
            }
            for (int i = costs.Length; i < Nodes.Count; i++)
            {
                if (Nodes[i].Cost == 0) continue;
                Nodes[i].Cost = 0;
                updated = true;
            }
            if (!updated) return;
            RefreshNodes();
        }

        public void SetNodeParentIndices(string[]? parents)
        {
            if (parents == null)
            {
                bool changed = false;
                foreach (Node node in Nodes)
                {
                    if (node.Parents.Count == 0) continue;
                    node.Parents.Clear();
                    changed = true;
                }
                if (!changed) return;
                RefreshNodes();
                return;
            }

            bool updated = false;
            for (int i = 0; i < parents.Length; i++)
            {
                EnsureNode(i);
                List<int> next = ParseParentList(parents[i], Nodes.Count);
                if (SameParents(Nodes[i].Parents, next)) continue;
                Nodes[i].Parents.Clear();
                Nodes[i].Parents.AddRange(next);
                updated = true;
            }
            for (int i = parents.Length; i < Nodes.Count; i++)
            {
                if (Nodes[i].Parents.Count == 0) continue;
                Nodes[i].Parents.Clear();
                updated = true;
            }
            if (!updated) return;
            RefreshNodes();
        }

        public void SetBranchRoleOrdinals(int[]? roles)
        {
            if (roles == null || roles.Length == 0)
            {
                if (BranchRoles.Length == 0) return;
                BranchRoles = System.Array.Empty<UiSurface.Role>();
                RefreshVisualAndRedraw();
                return;
            }

            UiSurface.Role[] next = new UiSurface.Role[roles.Length];
            for (int i = 0; i < roles.Length; i++)
                next[i] = RoleFromOrdinal(roles[i]);
            if (SameRoles(BranchRoles, next)) return;
            BranchRoles = next;
            RefreshVisualAndRedraw();
        }

        public Node AddNode(int column, int tier, int branch = 0,
                            NodeState state = NodeState.Locked, int cost = 0,
                            Texture2D? icon = null, IEnumerable<int>? parents = null,
                            bool expandBounds = true)
        {
            var node = new Node
            {
                Column = Mathf.Max(0, column),
                Tier = Mathf.Max(0, tier),
                Branch = branch,
                State = state,
                Cost = Mathf.Max(0, cost),
                Icon = icon,
            };
            if (parents != null)
            {
                foreach (int parent in parents)
                    if (parent >= 0 && parent < Nodes.Count)
                        node.Parents.Add(parent);
            }
            Nodes.Add(node);
            RefreshNodes(expandBounds);
            return node;
        }

        public bool RemoveNode(int index, bool expandBounds = true)
        {
            if (index < 0 || index >= Nodes.Count)
                return false;

            Nodes.RemoveAt(index);
            RemapParentReferencesAfterRemove(index);
            if (index <= _sel)
                _sel = Mathf.Max(-1, _sel - 1);
            if (_hover == index)
                _hover = -1;
            else if (index < _hover)
                _hover--;
            RefreshNodes(expandBounds);
            return true;
        }

        public void ClearNodes()
        {
            if (Nodes.Count == 0 && _sel < 0 && _hover < 0)
                return;

            Nodes.Clear();
            _sel = -1;
            _hover = -1;
            RefreshNodes(expandBounds: false);
        }

        public void RefreshNodes(bool expandBounds = true)
        {
            NormalizeParentReferences();
            if (expandBounds)
                ExpandBoundsToNodes();
            if (_sel >= Nodes.Count)
                _sel = Nodes.Count - 1;
            if (_hover >= Nodes.Count)
                _hover = -1;
            if (IsInsideTree())
                KitChrome.RefreshAutoMinimumSize(this, _GetMinimumSize());
            UpdateMinimumSize();
            QueueRedraw();
        }

        private void RemapParentReferencesAfterRemove(int removed)
        {
            foreach (Node node in Nodes)
            {
                for (int i = node.Parents.Count - 1; i >= 0; i--)
                {
                    int parent = node.Parents[i];
                    if (parent == removed)
                        node.Parents.RemoveAt(i);
                    else if (parent > removed)
                        node.Parents[i] = parent - 1;
                }
            }
        }

        private void NormalizeParentReferences()
        {
            int count = Nodes.Count;
            foreach (Node node in Nodes)
            {
                for (int i = node.Parents.Count - 1; i >= 0; i--)
                {
                    int parent = node.Parents[i];
                    if (parent < 0 || parent >= count)
                        node.Parents.RemoveAt(i);
                }
            }
        }

        private void RefreshVisualAndRedraw()
        {
            QueueRedraw();
        }

        private void RefreshMinimumAndRedraw()
        {
            if (IsInsideTree())
                KitChrome.RefreshAutoMinimumSize(this, _GetMinimumSize());
            UpdateMinimumSize();
            QueueRedraw();
        }

        private void EnsureNode(int index)
        {
            while (Nodes.Count <= index)
                Nodes.Add(new Node());
        }

        private static List<Node> NormalizeNodes(IEnumerable<Node>? nodes)
        {
            var next = new List<Node>();
            var parentSources = new List<List<int>>();
            if (nodes == null)
                return next;

            foreach (Node? node in nodes)
            {
                next.Add(new Node
                {
                    Column = Mathf.Max(0, node?.Column ?? 0),
                    Tier = Mathf.Max(0, node?.Tier ?? 0),
                    Branch = Mathf.Max(0, node?.Branch ?? 0),
                    State = StateFromOrdinal((int)(node?.State ?? NodeState.Locked)),
                    Icon = node?.Icon,
                    Cost = Mathf.Max(0, node?.Cost ?? 0),
                });

                var parents = new List<int>();
                if (node != null)
                    parents.AddRange(node.Parents);
                parentSources.Add(parents);
            }

            for (int i = 0; i < next.Count; i++)
            {
                foreach (int parent in parentSources[i])
                    if (parent >= 0 && parent < next.Count)
                        next[i].Parents.Add(parent);
            }
            return next;
        }

        private static bool SameNodes(IReadOnlyList<Node> left, IReadOnlyList<Node> right)
        {
            if (left.Count != right.Count) return false;
            for (int i = 0; i < left.Count; i++)
            {
                if (Mathf.Max(0, left[i].Column) != right[i].Column) return false;
                if (Mathf.Max(0, left[i].Tier) != right[i].Tier) return false;
                if (Mathf.Max(0, left[i].Branch) != right[i].Branch) return false;
                if (StateFromOrdinal((int)left[i].State) != right[i].State) return false;
                if (!ReferenceEquals(left[i].Icon, right[i].Icon)) return false;
                if (Mathf.Max(0, left[i].Cost) != right[i].Cost) return false;
                if (!SameParents(left[i].Parents, right[i].Parents)) return false;
            }
            return true;
        }

        private static bool SameParents(IReadOnlyList<int> left, IReadOnlyList<int> right)
        {
            if (left.Count != right.Count) return false;
            for (int i = 0; i < left.Count; i++)
                if (left[i] != right[i]) return false;
            return true;
        }

        private static bool SameRoles(IReadOnlyList<UiSurface.Role> left, IReadOnlyList<UiSurface.Role> right)
        {
            if (left.Count != right.Count) return false;
            for (int i = 0; i < left.Count; i++)
                if (left[i] != right[i]) return false;
            return true;
        }

        private static NodeState StateFromOrdinal(int value)
            => (NodeState)Mathf.Clamp(value, (int)NodeState.Locked, (int)NodeState.Owned);

        private static UiSurface.Role RoleFromOrdinal(int value)
            => (UiSurface.Role)Mathf.Clamp(value, (int)UiSurface.Role.Neutral, (int)UiSurface.Role.Info);

        private static List<int> ParseParentList(string? text, int nodeCount)
        {
            var parents = new List<int>();
            if (string.IsNullOrWhiteSpace(text))
                return parents;
            string[] parts = text.Split(',');
            foreach (string part in parts)
            {
                string token = part.Trim();
                if (token.Length == 0 || !int.TryParse(token, out int parent))
                    continue;
                if (parent >= 0 && parent < nodeCount)
                    parents.Add(parent);
            }
            return parents;
        }

        [Export(PropertyHint.Range, "1,10,1")]
        public int Columns
        {
            get => _cols;
            set
            {
                int next = Mathf.Max(1, value);
                if (_cols == next) return;
                _cols = next;
                RefreshMinimumAndRedraw();
            }
        }
        private int _cols = 4;

        [Export(PropertyHint.Range, "1,10,1")]
        public int Tiers
        {
            get => _tiers;
            set
            {
                int next = Mathf.Max(1, value);
                if (_tiers == next) return;
                _tiers = next;
                RefreshMinimumAndRedraw();
            }
        }
        private int _tiers = 3;

        [Export] public int Selected
        {
            get => _sel;
            set
            {
                int next = Nodes.Count == 0 ? -1 : Mathf.Clamp(value, -1, Nodes.Count - 1);
                if (_sel == next) return;
                _sel = next;
                RefreshVisualAndRedraw();
            }
        }
        private int _sel = -1;
        private int _hover = -1;

        [Export] public bool CycleStateOnClick { get => _cycleStateOnClick; set { if (_cycleStateOnClick == value) return; _cycleStateOnClick = value; } }
        private bool _cycleStateOnClick = true;
        private bool _eventsHooked;

        [Signal] public delegate void NodeActivatedEventHandler(int index);

        public override void _Ready()
        {
            base._Ready();
            ApplyInputDefaults(MouseFilterEnum.Stop, FocusModeEnum.All);
            if (!_eventsHooked)
            {
                MouseExited += ClearHover;
                _eventsHooked = true;
            }
            KitChrome.SetAutoMinimumSize(this, _GetMinimumSize());
        }

        public override void _Notification(int what)
        {
            base._Notification(what);
            if (KitChrome.ShouldClearPointerState(this, what))
                ClearHover();
        }

        private float Pitch() => Mathf.Min(Size.X / _cols, Size.Y / _tiers);

        public override Vector2 _GetMinimumSize()
        {
            int fs = UiSurface.FontSize(this);
            float pitch = fs * 3.6f;
            return new Vector2(pitch * _cols, pitch * _tiers);
        }

        private void ExpandBoundsToNodes()
        {
            int maxColumn = _cols;
            int maxTier = _tiers;
            foreach (Node node in Nodes)
            {
                maxColumn = Mathf.Max(maxColumn, node.Column + 1);
                maxTier = Mathf.Max(maxTier, node.Tier + 1);
            }
            _cols = Mathf.Clamp(maxColumn, 1, 10);
            _tiers = Mathf.Clamp(maxTier, 1, 10);
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
            if (@event is InputEventKey key)
            {
                Vector2I dir = KitChrome.DirectionFromKey(key);
                if (dir != Vector2I.Zero)
                {
                    MoveSelection(dir);
                    AcceptEvent();
                    return;
                }
                if (KitChrome.IsConfirmKey(key) && _sel >= 0)
                {
                    ActivateNode(_sel);
                    AcceptEvent();
                    return;
                }
            }

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
                GrabFocus();
                ActivateNode(hit);
                AcceptEvent();
            }
        }

        private void ActivateNode(int index)
        {
            if (index < 0 || index >= Nodes.Count) return;
            Selected = index;
            if (CycleStateOnClick)
                Nodes[index].State = Nodes[index].State switch
                {
                    NodeState.Locked => NodeState.Available,
                    NodeState.Available => NodeState.Owned,
                    _ => NodeState.Locked,
                };
            EmitSignal(SignalName.NodeActivated, index);
            QueueRedraw();
        }

        private void MoveSelection(Vector2I dir)
        {
            if (Nodes.Count == 0) return;
            if (dir.X <= -9999) { Selected = 0; return; }
            if (dir.X >= 9999) { Selected = Nodes.Count - 1; return; }

            int current = Mathf.Clamp(_sel < 0 ? 0 : _sel, 0, Nodes.Count - 1);
            Node origin = Nodes[current];
            int targetColumn = origin.Column + dir.X;
            int targetTier = origin.Tier + dir.Y;
            for (int i = 0; i < Nodes.Count; i++)
            {
                Node candidate = Nodes[i];
                if (candidate.Column == targetColumn && candidate.Tier == targetTier)
                {
                    Selected = i;
                    return;
                }
            }
        }

        private int HitNode(Vector2 p)
        {
            for (int i = 0; i < Nodes.Count; i++)
                if (NodeRect(Nodes[i]).HasPoint(p)) return i;
            return -1;
        }

        private void ClearHover()
        {
            if (_hover < 0) return;
            _hover = -1;
            QueueRedraw();
        }

        private KitShape NodeShape => Geo.Register == KitRegister.Pixel ? KitShape.Stepped : KitShape.Round;

        public override void _Draw()
        {
            if (Size.X <= 8 || Size.Y <= 8) return;
            if (Nodes.Count == 0)
            {
                KitChrome.DrawEmptyPreview(this, KitChrome.GenreOf(this), new Rect2(Vector2.Zero, Size),
                                           ActiveShape, "Nodes");
                DrawAttachments();
                return;
            }

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
                    txt = KitChrome.EllipsizeText(font, txt, small, r.Size.X * 0.50f);
                    if (string.IsNullOrEmpty(txt)) continue;
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

            KitChrome.DrawFocusRing(this, KitChrome.GenreOf(this), new Rect2(Vector2.Zero, Size), ActiveShape, 0.8f);
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
