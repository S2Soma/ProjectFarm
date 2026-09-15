using System;
using System.Collections.Generic;
using UnityEngine;

namespace LQFarm
{
    /// <summary>A point on a pet's path, in an island's grid cells (<see cref="IslandView.GridPoint"/>, the
    /// field's own space, never mirrored). <see cref="hop"/>: the stretch that ARRIVES here is a jump over
    /// water, not a walk.</summary>
    public struct PetPoint
    {
        public Vector2 cell;
        public bool hop;
        public PetPoint(Vector2 cell, bool hop) { this.cell = cell; this.hop = hop; }
    }

    /// <summary>One stretch of a trip: a walk on one island, or the whole of one rope bridge.</summary>
    public class PetLeg
    {
        /// <summary>The island walked on; -1 on a bridge.</summary>
        public int island = -1;
        /// <summary>The bridge crossed (bridges are numbered by the island they lead to, as in
        /// ArchipelagoView.SyncBridges: bridge i joins island i-1 and island i); -1 on an island.</summary>
        public int bridge = -1;
        /// <summary>On a bridge: walking from island bridge-1 to island bridge.</summary>
        public bool forward;
        public List<PetPoint> points;
    }

    /// <summary>Something on an island the pet walks round: a prop's footprint (a circle, a = b) or the
    /// charged gap between two lightning rods (a capsule). Grid cells, already mirrored.</summary>
    public struct PetObstacle
    {
        public Vector2 a, b;
        public float radius;
        /// <summary>Kept clear round it for the pet's body: a prop's full <see cref="PetPaths.PetClear"/>; a
        /// plant's leaves may brush the pet.</summary>
        public float pad;
        public string name;

        public float Distance(Vector2 p)
        {
            var ab = b - a;
            float len2 = ab.sqrMagnitude;
            float t = len2 > 1e-6f ? Mathf.Clamp01(Vector2.Dot(p - a, ab) / len2) : 0f;
            return Vector2.Distance(p, a + ab * t);
        }
    }

    /// <summary>Where a pet may walk, and how it gets from one place to another — pure geometry, no game
    /// state (the Farm/ rule: nothing here reads the local player's state; which islands are open is passed in).
    ///
    /// Each island is a lattice of points every 1/8 of a grid cell over the island's square (<see cref="PetGrid"/>).
    /// Every point is priced from the island's own geometry, read from the tables IslandView and IslandLife
    /// build the island from — so moving a prop moves the pet's detour with it:
    ///
    ///   * blocked: outside the fence (<see cref="IslandView.FenceCells"/>, except through the two gate
    ///     mouths down to the bridge landings), the river and its spring on Đảo Nước, the footprint of
    ///     every prop, yard decoration and giant plant (<see cref="Obstacles"/>) — and the FRONT of the island
    ///     (<see cref="FrontBand"/>). The pet walks on its own canvas above the island, so anywhere the front
    ///     fence stands in front of it on screen — the front yard, the field's front edge — it is drawn over
    ///     the rails and reads as standing on the fence (the owner's "vượt rào"). Only the way in from each
    ///     gate stays open, since that is where the gates are. PetTest measures it against the fence.
    ///   * 1   the yard between the beds and the back fence
    ///   * 2.2 the furrows where two beds meet, and a bed's rim
    ///   * 2.4 the gate corners
    ///   * 4   across a bed
    ///
    /// Paths are A* over that lattice (8 neighbours, edge length measured on screen, not in cells), then
    /// pulled straight wherever a straight line costs no more than the lattice path it replaces. The river
    /// has exactly one crossing, <see cref="CrossingU"/>: a hop between the two banks.</summary>
    public static class PetPaths
    {
        // ---- clearances, in grid cells ----
        /// <summary>How close the pet's feet come to the fence line.</summary>
        public const float FenceClear = 0.2f;
        /// <summary>Added to every prop's footprint: half the pet's own width at its feet.</summary>
        public const float PetClear = 0.14f;
        /// <summary>From the water's edge.</summary>
        public const float RiverClear = 0.08f;
        /// <summary>Radius of the spring the river rises from (Tools/gen_islands.py, the river: 0.62).</summary>
        public const float SpringRadius = 0.62f;
        /// <summary>Where the pet hops the river on Đảo Nước: across the last column of beds before the waterfall.
        /// Not the grass strip by the falls — the pet is drawn above the island, and there it stood on the front
        /// fence's rails; here both banks are a clear step inside the fence on screen (PetTest measures it against
        /// the posts), and that column is the last the player buys, so it is lawn for most of the game.</summary>
        public const float CrossingU = 1.5f;
        /// <summary>Closed within this many cells of the front fence (see <see cref="PetPaths"/>): a post is 109 units
        /// tall and a cell back from the fence lifts the pet 119, so from here on the fence stays under its feet...</summary>
        public const float FrontBand = 1.0f;
        /// <summary>...except in the corner of each gate, past this many cells from the field's centre line.</summary>
        public const float GateCorner = 2.1f;

        /// <summary>In the front yard, where the pet would be drawn over the front fence — except the gate
        /// corners and the way in from each gate to the corner of the field (<paramref name="wayPad"/>: how far
        /// off the gate posts that way keeps).</summary>
        public static bool InFrontYard(Vector2 c, float band = FrontBand, float corner = GateCorner, float wayPad = 0.25f)
        {
            float F = IslandView.FenceCells;
            bool front = (c.y > F - band && c.x > -corner) || (c.x > F - band && c.y > -corner);
            if (!front) return false;
            float s = Mathf.Max(-c.x + c.y, c.x - c.y);
            return !(Mathf.Abs(c.x + c.y) <= IslandView.GateCells - wayPad && s >= 3.5f);
        }
        /// <summary>Kept round a giant plant: less than a prop's, its leaves may brush the pet — but more than the
        /// lattice's half-diagonal, so a pulled-straight path never cuts into the stem.</summary>
        public const float PlantClear = 0.09f;
        /// <summary>Plants at least this tall (Khổng Lồ's giant mushrooms and flowers) are walked round.</summary>
        public const float TallPlant = 120f;

        /// <summary>The bridge's landing point on an island, in cells: on the lawn outside the gate
        /// (IslandView.BridgeLandX along the field's horizontal axis). Left: (-L, L); right: (L, -L).</summary>
        public static float LandCell => IslandView.BridgeLandX / (2f * IslandView.StepX);
        public static Vector2 LeftLanding => new Vector2(-LandCell, LandCell);
        public static Vector2 RightLanding => new Vector2(LandCell, -LandCell);

        /// <summary>Screen length of a step in cells (the lattice is isometric: a step along u is 133 units,
        /// a step down the screen 119, a step across it 239).</summary>
        public static float WorldLength(Vector2 dCell) { return IslandView.GridPoint(dCell.x, dCell.y).magnitude; }

        /// <summary>Odd islands (except the river) draw their scenery mirrored left to right — the rule of
        /// IslandView.Mirrored, which needs a built view. Mirroring x in field space swaps u and v.</summary>
        public static bool Mirrored(int island)
        {
            return (island & 1) == 1 && IslandSys.Def(island).layout != IslandLayout.River;
        }

        /// <summary>A plant drawn <paramref name="width"/> wide stands on a ground ellipse about that wide;
        /// its radius in cells, a little inside the silhouette.</summary>
        public static float FootRadius(float width) { return width * 0.8f / (2f * IslandView.StepX * 1.41421f); }

        /// <summary>Every obstacle on an island, read from what the island is built from: its props where they
        /// stand (<see cref="IslandView.PropsOn"/>: cell already mirrored, footprint radius from the decor
        /// catalogue), the worn yard decoration on Vườn Nhà, the charged gap between two lightning rods, and
        /// the giant plants (<see cref="IslandLife.PlantsFor"/>).</summary>
        public static List<PetObstacle> Obstacles(int island, string wornDecor)
        {
            var list = new List<PetObstacle>();
            void Add(string name, Vector2 c, float r, float pad) { list.Add(new PetObstacle { a = c, b = c, radius = r, pad = pad, name = name }); }

            Vector2? rod = null;
            foreach (var p in IslandView.PropsOn(island))
            {
                // a shop decoration stands in the yard only while it is worn (IslandView.RenderDecor)
                if (p.art.StartsWith("decor_") && p.art.Substring(6) != wornDecor) continue;
                string name = p.art.StartsWith("decor_") ? p.art.Substring(6) : p.art;
                Add(name, p.cell, p.radius, PetClear);
                // static arcs between the two lightning rods (IslandLife.AttachToProp): not a gap to walk through
                if (p.art.EndsWith("rod"))
                {
                    if (rod.HasValue) list.Add(new PetObstacle { a = rod.Value, b = p.cell, radius = p.radius, pad = PetClear, name = "arc" });
                    rod = p.cell;
                }
            }
            bool mirror = Mirrored(island);
            foreach (var pl in IslandLife.PlantsFor(island))
                if (pl.height >= TallPlant)
                    Add("plant", mirror ? new Vector2(pl.v, pl.u) : new Vector2(pl.u, pl.v), FootRadius(pl.height * 0.8f) * 0.85f, PlantClear);
            return list;
        }

        /// <summary>Whether a point is in the water of Đảo Nước, <paramref name="pad"/> cells either side
        /// (IslandLife.Meander / RiverBand / SpringU, the same shape gen_islands.py paints).</summary>
        public static bool InRiver(Vector2 c, float pad)
        {
            if (c.x > IslandLife.SpringU && Mathf.Abs(c.y - IslandLife.Meander(c.x)) < IslandLife.RiverBand + pad) return true;
            float du = c.x - IslandLife.SpringU, dv = c.y * 1.05f;
            return du * du + dv * dv < (SpringRadius + pad) * (SpringRadius + pad);
        }

        /// <summary>In one of the two gate mouths — the open corner between the gate posts, out to the
        /// bridge landing, kept off the posts and the grass edge.</summary>
        public static bool InGateMouth(Vector2 c, float postPad = 0.25f, float rimPad = 0.18f)
        {
            float F = IslandView.FenceCells, g = IslandView.GateCells;
            float t = c.x + c.y;
            if (Mathf.Abs(t) > g - postPad) return false;
            float s = Mathf.Max(-c.x + c.y, c.x - c.y);
            if (s < 2f * F - g - 0.3f || s > 2f * LandCell + 0.12f) return false;
            return IslandView.RimDistance(IslandView.GridPoint(c.x, c.y)) <= -rimPad;
        }

        // ============================================================
        // grids, cached per island
        // ============================================================
        static readonly Dictionary<int, PetGrid> Grids = new Dictionary<int, PetGrid>();
        static string _wornDecor;

        /// <summary>The yard decoration worn on Vườn Nhà (Cosmetics, slot Decor), or null. Only a worn one stands
        /// in the yard, so changing it rebuilds the home island's grid.</summary>
        public static string WornDecor
        {
            get { return _wornDecor; }
            set { if (value == _wornDecor) return; _wornDecor = value; Grids.Remove(0); }
        }
        static readonly Dictionary<int, PetBridgePath> Bridges = new Dictionary<int, PetBridgePath>();

        public static PetGrid For(int island)
        {
            if (!Grids.TryGetValue(island, out var g)) { g = new PetGrid(island); Grids[island] = g; }
            return g;
        }

        /// <summary>Drop the cached grids (a test that changes the tables, a debug overlay).</summary>
        public static void ClearCache() { Grids.Clear(); Bridges.Clear(); }

        /// <summary>The walking line along bridge <paramref name="to"/> (from island to-1 to island to).</summary>
        public static PetBridgePath Bridge(int to)
        {
            if (!Bridges.TryGetValue(to, out var b)) { b = new PetBridgePath(to); Bridges[to] = b; }
            return b;
        }

        /// <summary>Bridge hops between two islands, or -1 when a locked island stands between them (a trip
        /// has to walk across every island on the way, and a bridge only exists to an open one).</summary>
        public static int Hops(int from, int to, Func<int, bool> unlocked)
        {
            if (from < 0 || to < 0 || !unlocked(from) || !unlocked(to)) return -1;
            int lo = Mathf.Min(from, to), hi = Mathf.Max(from, to);
            for (int i = lo; i <= hi; i++) if (!unlocked(i)) return -1;
            return hi - lo;
        }

        /// <summary>A whole trip: walk to the gate the next bridge lands at, cross it, walk through each
        /// island on the way from gate to gate, and on the last island from its gate to the goal. Null when
        /// the goal cannot be reached.</summary>
        public static List<PetLeg> Route(int fromIsland, Vector2 fromCell, int toIsland, Vector2 toCell, Func<int, bool> unlocked)
        {
            if (Hops(fromIsland, toIsland, unlocked) < 0) return null;
            var legs = new List<PetLeg>();
            int dir = Math.Sign(toIsland - fromIsland);
            int isl = fromIsland;
            var at = fromCell;
            while (isl != toIsland)
            {
                var exit = dir > 0 ? RightLanding : LeftLanding;
                var walk = For(isl).FindPath(at, exit);
                if (walk == null) return null;
                legs.Add(new PetLeg { island = isl, points = walk });
                legs.Add(new PetLeg { bridge = dir > 0 ? isl + 1 : isl, forward = dir > 0 });
                isl += dir;
                at = dir > 0 ? LeftLanding : RightLanding;
            }
            var last = For(isl).FindPath(at, toCell);
            if (last == null) return null;
            legs.Add(new PetLeg { island = isl, points = last });
            return legs;
        }

        /// <summary>Screen length of a trip.</summary>
        public static float Length(List<PetLeg> legs)
        {
            float len = 0f;
            if (legs == null) return 0f;
            foreach (var l in legs)
            {
                if (l.bridge >= 0) { len += Bridge(l.bridge).Length; continue; }
                for (int i = 1; i < l.points.Count; i++) len += WorldLength(l.points[i].cell - l.points[i - 1].cell);
            }
            return len;
        }
    }

    /// <summary>The walkable lattice of one island: see <see cref="PetPaths"/>.</summary>
    public sealed class PetGrid
    {
        public const float H = 0.125f;
        public const float Min = -3f;
        public const int N = 49;

        public const float Yard = 1f, Furrow = 3f, GateYard = 3.2f, Bed = 5f;
        /// <summary>A hop costs this much per unit of its length: taken when it is the way, never as a shortcut.</summary>
        public const float HopCost = 2f;
        /// <summary>Within this of a bed's edge is the furrow.</summary>
        const float FurrowBand = 0.09f;

        public readonly int island;
        public readonly IslandLayout layout;
        /// <summary>Per node: its price, or +inf where the pet may not stand.</summary>
        public readonly float[] cost = new float[N * N];
        /// <summary>Per node: connected piece (hops count as connections), -1 when blocked.</summary>
        public readonly int[] piece = new int[N * N];
        public readonly List<(int a, int b)> hops = new List<(int, int)>();
        public readonly List<PetObstacle> obstacles;
        readonly List<(Vector2 c, float half)> _beds = new List<(Vector2, float)>();

        public int LeftGateNode { get; private set; }
        public int RightGateNode { get; private set; }

        public static Vector2 CellOf(int node) { return new Vector2(Min + (node % N) * H, Min + (node / N) * H); }
        public static int NodeAt(int i, int j) { return (i < 0 || j < 0 || i >= N || j >= N) ? -1 : i + j * N; }
        public static int NearestNode(Vector2 c)
        {
            return NodeAt(Mathf.RoundToInt((c.x - Min) / H), Mathf.RoundToInt((c.y - Min) / H));
        }

        public bool Walkable(int node) { return node >= 0 && !float.IsInfinity(cost[node]); }

        public PetGrid(int island)
        {
            this.island = island;
            layout = IslandSys.Def(island).layout;
            obstacles = PetPaths.Obstacles(island, island == 0 ? PetPaths.WornDecor : null);
            for (int s = 0; s < IslandSys.PlotsPerIsland; s++)
                if (IslandSys.KindOf(island, s) != PlotKind.None)
                    _beds.Add((IslandSys.SlotCell(island, s), IslandSys.SizeOf(island, s) * 0.5f));

            for (int n = 0; n < N * N; n++) cost[n] = CostAt(CellOf(n));
            if (layout == IslandLayout.River) AddCrossing();
            LeftGateNode = Snap(PetPaths.LeftLanding);
            RightGateNode = Snap(PetPaths.RightLanding);
            FindPieces();
            // A pocket the gates cannot reach (a corner shut in by a prop and the fence) is not somewhere the
            // pet can be: dropping it keeps every walkable point one walk from every other.
            int main = MainPiece;
            for (int n = 0; n < N * N; n++)
                if (Walkable(n) && piece[n] != main) { cost[n] = float.PositiveInfinity; piece[n] = -1; Pruned++; }
        }

        /// <summary>Walkable points dropped because the gates cannot reach them (IslandTest-style sanity: few).</summary>
        public int Pruned { get; private set; }

        /// <summary>The price of standing at a point, from the island's geometry alone.</summary>
        public float CostAt(Vector2 c)
        {
            float F = IslandView.FenceCells;
            bool inside = Mathf.Max(Mathf.Abs(c.x), Mathf.Abs(c.y)) <= F - PetPaths.FenceClear;
            bool mouth = PetPaths.InGateMouth(c);
            if (!inside && !mouth) return float.PositiveInfinity;
            if (!mouth && PetPaths.InFrontYard(c)) return float.PositiveInfinity;
            if (layout == IslandLayout.River && PetPaths.InRiver(c, PetPaths.RiverClear)) return float.PositiveInfinity;
            foreach (var o in obstacles)
                if (o.Distance(c) < o.radius + o.pad) return float.PositiveInfinity;

            float edge = float.NegativeInfinity;
            foreach (var b in _beds)
                edge = Mathf.Max(edge, b.half - Mathf.Max(Mathf.Abs(c.x - b.c.x), Mathf.Abs(c.y - b.c.y)));
            if (edge > FurrowBand) return Bed;
            if (edge >= -1e-3f) return Furrow;
            return c.x > F - PetPaths.FrontBand || c.y > F - PetPaths.FrontBand ? GateYard : Yard;
        }

        public bool IsBed(Vector2 c)
        {
            foreach (var b in _beds)
                if (Mathf.Max(Mathf.Abs(c.x - b.c.x), Mathf.Abs(c.y - b.c.y)) < b.half - FurrowBand) return true;
            return false;
        }

        /// <summary>Đảo Nước: link the last dry point on each bank of the river's column at <see cref="PetPaths.CrossingU"/>.</summary>
        void AddCrossing()
        {
            int i = Mathf.RoundToInt((PetPaths.CrossingU - Min) / H);
            int mid = Mathf.RoundToInt((IslandLife.Meander(PetPaths.CrossingU) - Min) / H);
            int below = -1, above = -1;
            for (int j = mid; j >= 0; j--) if (Walkable(NodeAt(i, j))) { below = NodeAt(i, j); break; }
            for (int j = mid; j < N; j++) if (Walkable(NodeAt(i, j))) { above = NodeAt(i, j); break; }
            if (below >= 0 && above >= 0) hops.Add((below, above));
        }

        /// <summary>The walkable node nearest a point (within 3/4 of a cell), or -1.</summary>
        public int Snap(Vector2 c)
        {
            int ci = Mathf.RoundToInt((c.x - Min) / H), cj = Mathf.RoundToInt((c.y - Min) / H);
            int best = -1; float bestD = float.MaxValue;
            for (int r = 0; r <= 6 && best < 0; r++)
                for (int dj = -r; dj <= r; dj++)
                    for (int di = -r; di <= r; di++)
                    {
                        if (Mathf.Max(Mathf.Abs(di), Mathf.Abs(dj)) != r) continue;
                        int n = NodeAt(ci + di, cj + dj);
                        if (!Walkable(n)) continue;
                        float d = (CellOf(n) - c).sqrMagnitude;
                        if (d < bestD) { bestD = d; best = n; }
                    }
            return best;
        }

        static readonly int[] Di = { 1, -1, 0, 0, 1, 1, -1, -1 };
        static readonly int[] Dj = { 0, 0, 1, -1, 1, -1, 1, -1 };

        /// <summary>Neighbours of a node with the cost of stepping there. A diagonal step needs both of the
        /// straight steps beside it open, so a path never squeezes between two blocked points.</summary>
        void Neighbours(int n, List<(int node, float w, bool hop)> outp)
        {
            outp.Clear();
            int i = n % N, j = n / N;
            var c = CellOf(n);
            for (int k = 0; k < 8; k++)
            {
                int m = NodeAt(i + Di[k], j + Dj[k]);
                if (!Walkable(m)) continue;
                if (k >= 4 && (!Walkable(NodeAt(i + Di[k], j)) || !Walkable(NodeAt(i, j + Dj[k])))) continue;
                float len = PetPaths.WorldLength(new Vector2(Di[k], Dj[k]) * H);
                outp.Add((m, len * 0.5f * (cost[n] + cost[m]), false));
            }
            foreach (var h in hops)
            {
                int m = h.a == n ? h.b : h.b == n ? h.a : -1;
                if (m >= 0) outp.Add((m, PetPaths.WorldLength(CellOf(m) - c) * HopCost, true));
            }
        }

        void FindPieces()
        {
            for (int n = 0; n < piece.Length; n++) piece[n] = -1;
            var queue = new Queue<int>();
            var nb = new List<(int, float, bool)>();
            int id = 0;
            for (int n = 0; n < piece.Length; n++)
            {
                if (!Walkable(n) || piece[n] >= 0) continue;
                piece[n] = id;
                queue.Enqueue(n);
                while (queue.Count > 0)
                {
                    int x = queue.Dequeue();
                    Neighbours(x, nb);
                    foreach (var (m, _, _) in nb)
                        if (piece[m] < 0) { piece[m] = id; queue.Enqueue(m); }
                }
                id++;
            }
            Pieces = id;
        }

        /// <summary>Separate walkable pieces before the pockets were dropped.</summary>
        public int Pieces { get; private set; }

        /// <summary>The piece the bridges land in — where the pet lives.</summary>
        public int MainPiece => LeftGateNode >= 0 ? piece[LeftGateNode] : RightGateNode >= 0 ? piece[RightGateNode] : -1;

        // ============================================================
        // A*
        // ============================================================
        readonly float[] _g = new float[N * N];
        readonly int[] _from = new int[N * N];
        readonly bool[] _viaHop = new bool[N * N];
        readonly int[] _stamp = new int[N * N];
        int _search;

        /// <summary>A path from one point to another on this island (both snapped to the lattice, the exact
        /// ends kept when they are walkable), pulled straight. Null when they are not connected.</summary>
        public List<PetPoint> FindPath(Vector2 from, Vector2 to)
        {
            int s = Snap(from), goal = Snap(to);
            if (s < 0 || goal < 0 || piece[s] != piece[goal]) return null;

            _search++;
            var open = new MinHeap();
            var nb = new List<(int node, float w, bool hop)>();
            var goalCell = CellOf(goal);
            _stamp[s] = _search; _g[s] = 0f; _from[s] = -1; _viaHop[s] = false;
            open.Push(s, PetPaths.WorldLength(goalCell - CellOf(s)));
            var closed = new HashSet<int>();
            while (open.Count > 0)
            {
                int n = open.Pop();
                if (n == goal) break;
                if (!closed.Add(n)) continue;
                Neighbours(n, nb);
                foreach (var (m, w, hop) in nb)
                {
                    float g = _g[n] + w;
                    if (_stamp[m] == _search && g >= _g[m]) continue;
                    _stamp[m] = _search; _g[m] = g; _from[m] = n; _viaHop[m] = hop;
                    open.Push(m, g + PetPaths.WorldLength(goalCell - CellOf(m)) * Yard);
                }
            }
            if (_stamp[goal] != _search) return null;

            var nodes = new List<PetPoint>();
            for (int n = goal; n >= 0; n = _from[n]) nodes.Add(new PetPoint(CellOf(n), _viaHop[n]));
            nodes.Reverse();

            // the exact ends, when the pet may stand there and step straight on from the lattice
            if ((from - nodes[0].cell).sqrMagnitude > 1e-6f && !float.IsInfinity(CostAt(from))
                && !float.IsInfinity(SegmentCost(from, nodes[0].cell)))
                nodes.Insert(0, new PetPoint(from, false));
            var end = nodes[nodes.Count - 1].cell;
            if ((to - end).sqrMagnitude > 1e-6f && !float.IsInfinity(CostAt(to)) && !float.IsInfinity(SegmentCost(end, to)))
                nodes.Add(new PetPoint(to, false));
            return Smooth(nodes);
        }

        /// <summary>Cost of walking a straight line, sampled on the lattice every half step; +inf if it crosses
        /// anywhere the pet may not stand.</summary>
        public float SegmentCost(Vector2 a, Vector2 b)
        {
            var d = b - a;
            int steps = Mathf.Max(1, Mathf.CeilToInt(Mathf.Max(Mathf.Abs(d.x), Mathf.Abs(d.y)) / (H * 0.5f)));
            float len = PetPaths.WorldLength(d) / steps, total = 0f;
            for (int k = 0; k < steps; k++)
            {
                var p = a + d * ((k + 0.5f) / steps);
                int n = NearestNode(p);
                if (n < 0) return float.PositiveInfinity;
                float c = cost[n];
                if (float.IsInfinity(c)) return c;
                total += c * len;
            }
            return total;
        }

        /// <summary>String-pulling: from each kept point, jump to the farthest later point whose straight line
        /// costs no more than the path it replaces. A hop is never cut into.</summary>
        List<PetPoint> Smooth(List<PetPoint> pts)
        {
            if (pts.Count <= 2) return pts;
            var prefix = new float[pts.Count];
            for (int i = 1; i < pts.Count; i++)
                prefix[i] = prefix[i - 1] + (pts[i].hop ? 0f : Mathf.Min(1e6f, SegmentCost(pts[i - 1].cell, pts[i].cell)));

            var outp = new List<PetPoint> { pts[0] };
            int at = 0;
            while (at < pts.Count - 1)
            {
                int best = at + 1;
                if (!pts[at + 1].hop)
                    for (int k = at + 2; k < pts.Count && k <= at + 56; k++)
                    {
                        if (pts[k].hop) break;
                        float straight = SegmentCost(pts[at].cell, pts[k].cell);
                        if (straight <= prefix[k] - prefix[at] + 0.5f) best = k;
                    }
                outp.Add(pts[best]);
                at = best;
            }
            return outp;
        }

        // ============================================================
        // places the pet goes
        // ============================================================
        /// <summary>Where the pet stands to work a plot: the middle of one of the bed's four edges, the front
        /// ones first (standing in front of its own crop is what reads right with the pet drawn above the
        /// island), then whichever is nearest. Null for a slot that is not land.</summary>
        public Vector2? WorkSpot(int slot, Vector2 near)
        {
            if (IslandSys.KindOf(island, slot) == PlotKind.None) return null;
            var c = IslandSys.SlotCell(island, slot);
            float half = IslandSys.SizeOf(island, slot) * 0.5f;
            int main = MainPiece;
            Vector2? best = null;
            float bestScore = float.MaxValue;
            var edges = new[] { new Vector2(0, half), new Vector2(half, 0), new Vector2(-half, 0), new Vector2(0, -half) };
            for (int e = 0; e < edges.Length; e++)
            {
                var spot = c + edges[e];
                int n = NearestNode(spot);
                if (!Walkable(n) || (main >= 0 && piece[n] != main)) continue;
                float score = PetPaths.WorldLength(spot - near) + (e >= 2 ? 160f : 0f);
                if (score < bestScore) { bestScore = score; best = spot; }
            }
            return best;
        }

        /// <summary>A spot in the back yard (never a bed, never a gate) reachable from the gates, between
        /// <paramref name="minDist"/> and <paramref name="maxDist"/> screen units from <paramref name="near"/>
        /// when there is one.</summary>
        public Vector2 YardSpot(Func<float> rand, Vector2 near, float minDist, float maxDist)
        {
            int main = MainPiece;
            Vector2 fallback = CellOf(main >= 0 ? (LeftGateNode >= 0 ? LeftGateNode : RightGateNode) : NearestNode(Vector2.zero));
            for (int tries = 0; tries < 200; tries++)
            {
                int n = Mathf.Clamp((int)(rand() * N * N), 0, N * N - 1);
                if (!Walkable(n) || piece[n] != main) continue;
                if (cost[n] != Yard) continue;
                var cell = CellOf(n);
                // not in a gate mouth: that is where the bridges come in, not where a pet sits down
                if (PetPaths.InGateMouth(cell, 0f, 0f)) continue;
                float d = PetPaths.WorldLength(cell - near);
                if (tries < 150 && (d < minDist || d > maxDist)) continue;
                return cell;
            }
            return fallback;
        }

        /// <summary>A binary heap of (node, priority).</summary>
        sealed class MinHeap
        {
            readonly List<(int n, float p)> _h = new List<(int, float)>();
            public int Count => _h.Count;
            public void Push(int n, float p)
            {
                _h.Add((n, p));
                int i = _h.Count - 1;
                while (i > 0)
                {
                    int up = (i - 1) / 2;
                    if (_h[up].p <= _h[i].p) break;
                    (_h[up], _h[i]) = (_h[i], _h[up]);
                    i = up;
                }
            }
            public int Pop()
            {
                int top = _h[0].n;
                int last = _h.Count - 1;
                _h[0] = _h[last];
                _h.RemoveAt(last);
                int i = 0;
                while (true)
                {
                    int l = i * 2 + 1, r = l + 1, m = i;
                    if (l < _h.Count && _h[l].p < _h[m].p) m = l;
                    if (r < _h.Count && _h[r].p < _h[m].p) m = r;
                    if (m == i) break;
                    (_h[m], _h[i]) = (_h[i], _h[m]);
                    i = m;
                }
                return top;
            }
        }
    }

    /// <summary>The line a pet walks along one rope bridge: the deck's own centreline
    /// (<see cref="ArchipelagoView.BridgeCentre"/>, the curve the planks are laid on, sag and all), sampled
    /// by arc length so the pet keeps an even pace over the dip. Archipelago units, which are the pet layer's.</summary>
    public sealed class PetBridgePath
    {
        public readonly int to;
        readonly Vector2[] _pts;
        readonly float[] _arc;
        const int Samples = 97;

        public PetBridgePath(int to)
        {
            this.to = to;
            _pts = new Vector2[Samples];
            _arc = new float[Samples];
            for (int i = 0; i < Samples; i++)
            {
                _pts[i] = ArchipelagoView.BridgeCentre(to, i / (float)(Samples - 1));
                if (i > 0) _arc[i] = _arc[i - 1] + Vector2.Distance(_pts[i - 1], _pts[i]);
            }
        }

        public float Length => _arc[Samples - 1];
        public Vector2 Start => _pts[0];
        public Vector2 End => _pts[Samples - 1];

        /// <summary>The point <paramref name="s"/> units along the deck from the (to-1) end.</summary>
        public Vector2 At(float s)
        {
            if (s <= 0f) return _pts[0];
            if (s >= Length) return _pts[Samples - 1];
            int lo = 0, hi = Samples - 1;
            while (hi - lo > 1) { int mid = (lo + hi) / 2; if (_arc[mid] <= s) lo = mid; else hi = mid; }
            float t = Mathf.InverseLerp(_arc[lo], _arc[hi], s);
            return Vector2.Lerp(_pts[lo], _pts[hi], t);
        }
    }
}
