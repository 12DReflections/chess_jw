using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using Chess4D.Core;

namespace Chess4D.Tablebase
{
    /// <summary>
    /// A one-ply drawing certificate for K + material vs K that needs no table.
    ///
    /// A set S of cells is safe when, for every cell b in S and EVERY placement of
    /// the white pieces (white king not adjacent to b, since it is Black's move),
    /// the black king on b has a move to a cell of S that is neither occupied nor
    /// attacked. Attacks are computed with no blockers at all, which is a superset
    /// of the real attacks (and covers lines through the cell the king leaves), and
    /// occupied cells count as covered even when the capture would be legal, so
    /// every approximation favours White. If S is not empty, a black king inside S
    /// with Black to move always has a legal move that stays inside S: it is never
    /// mated or stalemated, so White cannot force mate from any such position.
    ///
    /// The greatest safe set is found by erosion from the whole board. The rule is
    /// invariant under the board's symmetry group, so S is a union of king classes
    /// and only one cell per class is examined. An empty result proves nothing:
    /// White's pieces are allowed to teleport between moves here.
    /// </summary>
    public static class SafeRegion
    {
        public sealed class Result
        {
            public bool[] ClassSafe;
            public int SafeCells, Iterations;
            public int CentreMoves, CentreMaxCovered;
            public string Report;
        }

        public static Result Compute(BoardGeometry g, PieceType[] material, Action<string> log = null)
        {
            var sym = new Symmetry(g);
            var geo = new ThreePiece(g, PieceType.Queen);
            int classes = sym.DomainCells.Length;
            var types = new PieceType[material.Length + 1];
            types[0] = PieceType.King;
            Array.Copy(material, 0, types, 1, material.Length);

            // Per class: neighbour cells of the domain cell, and the raw cover masks of each piece type over them.
            var neighbours = new int[classes][];
            var masks = new uint[classes][][];
            Parallel.For(0, classes, c =>
            {
                int b = sym.DomainCells[c];
                var nb = new List<int>();
                foreach (var d in g.King) { int t = g.Step(b, d); if (t >= 0) nb.Add(t); }
                neighbours[c] = nb.ToArray();
                masks[c] = new uint[types.Length][];
                for (int k = 0; k < types.Length; k++)
                {
                    var set = new HashSet<uint>();
                    for (int p = 0; p < g.CellCount; p++)
                    {
                        if (p == b) continue;
                        if (k == 0 && geo.Attacks(PieceType.King, p, b, -1)) continue; // kings never touch
                        uint m = 0;
                        for (int i = 0; i < nb.Count; i++)
                            if (nb[i] == p || geo.Attacks(types[k], p, nb[i], -1)) m |= 1u << i;
                        if (m != 0) set.Add(m);
                    }
                    masks[c][k] = new uint[set.Count];
                    set.CopyTo(masks[c][k]);
                }
            });

            var safe = new bool[classes];
            Array.Fill(safe, true);
            int iterations = 0;
            while (true)
            {
                var next = (bool[])safe.Clone();
                Parallel.For(0, classes, c =>
                {
                    if (!safe[c]) return;
                    uint target = 0;
                    for (int i = 0; i < neighbours[c].Length; i++) if (safe[sym.ClassOf(neighbours[c][i])]) target |= 1u << i;
                    if (target == 0 || Coverable(masks[c], target)) next[c] = false;
                });
                iterations++;
                bool changed = false;
                for (int c = 0; c < classes; c++) if (next[c] != safe[c]) changed = true;
                safe = next;
                if (log != null) log("erosion pass " + iterations + ": " + CountCells(sym, g, safe) + " cells remain");
                if (!changed) break;
            }

            var r = new Result { ClassSafe = safe, Iterations = iterations, SafeCells = CountCells(sym, g, safe) };
            int centre = classes - 1; // the domain cell with the largest coordinates
            for (int c = 0; c < classes; c++) if (neighbours[c].Length > neighbours[centre].Length) centre = c;
            r.CentreMoves = neighbours[centre].Length;
            uint all = r.CentreMoves == 32 ? uint.MaxValue : (1u << r.CentreMoves) - 1;
            r.CentreMaxCovered = MaxCover(masks[centre], all);

            var sb = new StringBuilder();
            sb.Append("K");
            foreach (var t in material) sb.Append('+').Append(Piece.ToChar(Piece.Make(t, Color.White)));
            sb.Append(" vs K, ").Append(g.Dimensions).Append(" dimensions, side ").Append(g.Side).Append('\n');
            sb.Append("most White can cover of a central king's ").Append(r.CentreMoves).Append(" moves: ").Append(r.CentreMaxCovered).Append('\n');
            sb.Append("safe region after ").Append(iterations).Append(" erosion passes: ").Append(r.SafeCells).Append(" of ").Append(g.CellCount).Append(" cells\n");
            if (r.SafeCells > 0)
            {
                sb.Append("safe king classes (domain cell: moves that stay inside):");
                for (int c = 0; c < classes; c++)
                {
                    if (!safe[c]) continue;
                    int inside = 0;
                    foreach (int t in neighbours[c]) if (safe[sym.ClassOf(t)]) inside++;
                    sb.Append(' ').Append(g.CoordOf(sym.DomainCells[c]).ToCompact()).Append(':').Append(inside);
                }
                sb.Append('\n').Append("RESULT: White cannot force mate against a black king inside the safe region.\n");
            }
            else sb.Append("RESULT: inconclusive. The one-ply certificate fails; this does not show a forced mate.\n");
            r.Report = sb.ToString();
            return r;
        }

        private static int CountCells(Symmetry sym, BoardGeometry g, bool[] safe)
        {
            int n = 0;
            for (int cell = 0; cell < g.CellCount; cell++) if (safe[sym.ClassOf(cell)]) n++;
            return n;
        }

        /// <summary>Masks restricted to the target, duplicates and dominated masks removed, largest first.</summary>
        private static uint[][] Reduce(uint[][] raw, uint target)
        {
            var lists = new uint[raw.Length][];
            for (int k = 0; k < raw.Length; k++)
            {
                var set = new HashSet<uint>();
                foreach (uint m in raw[k]) if ((m & target) != 0) set.Add(m & target);
                var arr = new uint[set.Count];
                set.CopyTo(arr);
                Array.Sort(arr, (x, y) => BitOperations.PopCount(y).CompareTo(BitOperations.PopCount(x)));
                var keep = new List<uint>();
                foreach (uint m in arr)
                {
                    bool dominated = false;
                    foreach (uint q in keep) if ((m & ~q) == 0) { dominated = true; break; }
                    if (!dominated) keep.Add(m);
                }
                lists[k] = keep.ToArray();
            }
            return lists;
        }

        /// <summary>Can one mask per piece cover every bit of the target?</summary>
        private static bool Coverable(uint[][] raw, uint target)
        {
            var lists = Reduce(raw, target);
            Array.Sort(lists, (x, y) => MaxPop(y).CompareTo(MaxPop(x)));
            var suffix = new int[lists.Length + 1];
            for (int k = lists.Length - 1; k >= 0; k--) suffix[k] = suffix[k + 1] + MaxPop(lists[k]);
            return Cover(lists, suffix, 0, target);
        }

        private static bool Cover(uint[][] lists, int[] suffix, int level, uint remaining)
        {
            if (remaining == 0) return true;
            if (level == lists.Length || BitOperations.PopCount(remaining) > suffix[level]) return false;
            foreach (uint m in lists[level])
                if ((m & remaining) != 0 && Cover(lists, suffix, level + 1, remaining & ~m)) return true;
            return Cover(lists, suffix, level + 1, remaining); // this piece contributes nothing
        }

        private static int MaxPop(uint[] list) { return list.Length == 0 ? 0 : BitOperations.PopCount(list[0]); }

        /// <summary>The largest number of target bits one mask per piece can cover.</summary>
        private static int MaxCover(uint[][] raw, uint target)
        {
            var lists = Reduce(raw, target);
            Array.Sort(lists, (x, y) => MaxPop(y).CompareTo(MaxPop(x)));
            var suffix = new int[lists.Length + 1];
            for (int k = lists.Length - 1; k >= 0; k--) suffix[k] = suffix[k + 1] + MaxPop(lists[k]);
            int best = 0;
            Best(lists, suffix, 0, 0, target, ref best);
            return best;
        }

        private static void Best(uint[][] lists, int[] suffix, int level, uint covered, uint target, ref int best)
        {
            int have = BitOperations.PopCount(covered);
            if (have > best) best = have;
            if (level == lists.Length || have + suffix[level] <= best) return;
            foreach (uint m in lists[level]) Best(lists, suffix, level + 1, covered | m, target, ref best);
        }

        public static PieceType[] ParseMaterial(string s)
        {
            var list = new List<PieceType>();
            foreach (char ch in s.ToUpperInvariant())
            {
                switch (ch)
                {
                    case 'Q': list.Add(PieceType.Queen); break;
                    case 'R': list.Add(PieceType.Rook); break;
                    case 'B': list.Add(PieceType.Bishop); break;
                    case 'N': list.Add(PieceType.Knight); break;
                    default: throw new ArgumentException("material must be letters from Q, R, B, N");
                }
            }
            return list.ToArray();
        }
    }
}
