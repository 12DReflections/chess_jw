using System;
using System.Collections.Generic;
using Chess4D.Core;

namespace Chess4D.Tablebase
{
    /// <summary>
    /// The hyperoctahedral group B_n acting on the board: every axis permutation
    /// combined with every set of axis reflections, n! * 2^n elements (384 at n=4,
    /// 8 at n=2). Pawnless positions are invariant under all of it. Canonical
    /// form: the white king is mapped into the fundamental domain (coordinates
    /// non-decreasing and at most (side-1)/2), then the remaining freedom (the
    /// stabiliser of that cell) is used to minimise the white piece's cell, then
    /// the black king's cell, lexicographically.
    /// </summary>
    public sealed class Symmetry
    {
        public readonly BoardGeometry G;
        public readonly int TransformCount;
        private readonly int[][] map;            // transform -> cell -> cell
        public readonly int[] DomainCells;       // class -> cell
        private readonly int[] classOfCell;      // cell -> class of its canonical image
        private readonly int[][] cosets;         // cell -> transforms mapping it into the domain
        public readonly int[][] RepCells;        // class -> id -> white piece cell (orbit minimum under the stabiliser)
        private readonly int[][] repId;          // class -> cell -> id or -1
        public readonly long[] PairOffset;       // class -> first pair index
        public readonly long PairCount;

        public int CellCount { get { return G.CellCount; } }
        public long EntryCount { get { return PairCount * G.CellCount * 2; } }

        public Symmetry(BoardGeometry g)
        {
            G = g;
            int n = g.Dimensions, side = g.Side, cells = g.CellCount;

            // All transforms.
            var perms = new List<int[]>();
            Permute(new int[n], new bool[n], 0, perms);
            // A board-King distinguishes the x-y board from the other axes: keep only permutations that map {0,1} onto itself.
            if (g.BoardKing) perms.RemoveAll(perm => (perm[0] > 1) || (perm[1] > 1));
            // Pair diagonals distinguish the partition {{0,1},{2,3}}: axes may swap within a pair, and the pairs may swap.
            else if (g.PairDiagonals) perms.RemoveAll(perm => (perm[0] / 2) != (perm[1] / 2));
            TransformCount = perms.Count << n;
            map = new int[TransformCount][];
            int t = 0;
            int[] coords = new int[n];
            foreach (int[] perm in perms)
            {
                for (int mask = 0; mask < (1 << n); mask++)
                {
                    int[] m = new int[cells];
                    for (int cell = 0; cell < cells; cell++)
                    {
                        for (int i = 0; i < n; i++)
                        {
                            int v = g.Coord(cell, perm[i]);
                            coords[i] = (mask & (1 << i)) != 0 ? side - 1 - v : v;
                        }
                        m[cell] = g.CellOf(coords);
                    }
                    map[t++] = m;
                }
            }

            // Fundamental domain and classes.
            var domain = new List<int>();
            classOfCell = new int[cells];
            for (int cell = 0; cell < cells; cell++) if (InDomain(cell)) { domain.Add(cell); }
            DomainCells = domain.ToArray();
            var classOfDomainCell = new Dictionary<int, int>();
            for (int c = 0; c < DomainCells.Length; c++) classOfDomainCell[DomainCells[c]] = c;

            cosets = new int[cells][];
            var tmp = new List<int>();
            for (int cell = 0; cell < cells; cell++)
            {
                tmp.Clear();
                int cls = -1;
                for (int k = 0; k < TransformCount; k++)
                {
                    int img = map[k][cell];
                    if (classOfDomainCell.TryGetValue(img, out int c))
                    {
                        if (cls >= 0 && cls != c) throw new InvalidOperationException("Domain is not fundamental");
                        cls = c;
                        tmp.Add(k);
                    }
                }
                classOfCell[cell] = cls;
                cosets[cell] = tmp.ToArray();
            }

            // White-piece orbit representatives under each class stabiliser.
            RepCells = new int[DomainCells.Length][];
            repId = new int[DomainCells.Length][];
            PairOffset = new long[DomainCells.Length + 1];
            for (int c = 0; c < DomainCells.Length; c++)
            {
                int k0 = DomainCells[c];
                int[] stab = cosets[k0]; // transforms fixing the domain cell are exactly its coset members
                var reps = new List<int>();
                repId[c] = new int[cells];
                for (int v = 0; v < cells; v++)
                {
                    int min = v;
                    foreach (int s in stab) { int img = map[s][v]; if (img < min) min = img; }
                    repId[c][v] = min == v ? reps.Count : -1;
                    if (min == v) reps.Add(v);
                }
                RepCells[c] = reps.ToArray();
                PairOffset[c + 1] = PairOffset[c] + reps.Count;
            }
            PairCount = PairOffset[DomainCells.Length];
        }

        private static void Permute(int[] cur, bool[] used, int pos, List<int[]> output)
        {
            if (pos == cur.Length) { output.Add((int[])cur.Clone()); return; }
            for (int i = 0; i < cur.Length; i++)
            {
                if (used[i]) continue;
                used[i] = true; cur[pos] = i;
                Permute(cur, used, pos + 1, output);
                used[i] = false;
            }
        }

        private bool InDomain(int cell)
        {
            int half = (G.Side - 1) / 2; // odd sides keep the centre coordinate, which reflection fixes
            int prev = -1;
            for (int i = 0; i < G.Dimensions; i++)
            {
                int v = G.Coord(cell, i);
                if (v > half) return false;
                if ((G.BoardKing || G.PairDiagonals) && i == 2) prev = -1; // axes are only interchangeable within {x,y} and within the rest
                if (v < prev) return false;
                prev = v;
            }
            if (G.PairDiagonals)
            {
                // The two pairs may also be swapped: order them lexicographically.
                int a0 = G.Coord(cell, 0), a1 = G.Coord(cell, 1), b0 = G.Coord(cell, 2), b1 = G.Coord(cell, 3);
                if (a0 > b0 || (a0 == b0 && a1 > b1)) return false;
            }
            return true;
        }

        public int Apply(int transform, int cell) { return map[transform][cell]; }
        public int ClassOf(int cell) { return classOfCell[cell]; }
        public int RepId(int cls, int cell) { return repId[cls][cell]; }
        public int[] Coset(int cell) { return cosets[cell]; }

        /// <summary>Canonical table index of a position. Side to move is the low bit.</summary>
        public long Index(int wk, int wx, int bk, int stm)
        {
            int c = classOfCell[wk];
            int bestWx = int.MaxValue, bestBk = int.MaxValue;
            foreach (int t in cosets[wk])
            {
                int a = map[t][wx];
                if (a > bestWx) continue;
                int b = map[t][bk];
                if (a < bestWx || b < bestBk) { bestWx = a; bestBk = b; }
            }
            int id = repId[c][bestWx];
            if (id < 0) throw new InvalidOperationException("canonical white piece cell is not a representative");
            return (((PairOffset[c] + id) * G.CellCount) + bestBk) * 2 + stm;
        }

        public long IndexOfCanonical(int cls, int id, int bk, int stm) { return (((PairOffset[cls] + id) * G.CellCount) + bk) * 2 + stm; }

        public void Decode(long index, out int wk, out int wx, out int bk, out int stm)
        {
            stm = (int)(index & 1);
            long rest = index >> 1;
            bk = (int)(rest % G.CellCount);
            long pair = rest / G.CellCount;
            int c = ClassOfPair(pair);
            wk = DomainCells[c];
            wx = RepCells[c][pair - PairOffset[c]];
        }

        public int ClassOfPair(long pair)
        {
            int lo = 0, hi = DomainCells.Length - 1;
            while (lo < hi)
            {
                int mid = (lo + hi + 1) >> 1;
                if (PairOffset[mid] <= pair) lo = mid; else hi = mid - 1;
            }
            return lo;
        }
    }
}
