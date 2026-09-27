using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Numerics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Chess4D.Core;

namespace Chess4D.Tablebase
{
    /// <summary>
    /// Exact solver for K + pieces vs K that stores only the decided positions,
    /// for boards where a dense table does not fit but almost nothing is won.
    ///
    /// 1. Every checkmate is enumerated: the black king's cell runs over the
    ///    fundamental domain, and White's cells are searched with blocker-free
    ///    cover masks as a bound, then confirmed with the full Core rules.
    /// 2. The won set is closed by retrograde steps: White un-moves from a lost
    ///    position give won positions; black un-moves from a won position give
    ///    candidates, and a candidate is lost when every legal black move lands
    ///    in the won set (a capture is looked up in the solver for the smaller
    ///    material, solved first). Lost positions whose only moves are such
    ///    captures are seeded from the smaller solver's won set.
    /// 3. Lost positions are processed in order of distance, so distances are
    ///    exact. Everything not in the two sets is a draw.
    ///
    /// Legality, check and move generation all go through <see cref="Board"/>,
    /// so this shares no rules code with the dense generators it is checked against.
    /// </summary>
    public sealed class SparseSolver
    {
        public readonly BoardGeometry G;
        public readonly PieceType[] Pieces;
        public readonly Dictionary<ulong, int> Won = new Dictionary<ulong, int>();   // white to move, plies to mate
        public readonly Dictionary<ulong, int> Lost = new Dictionary<ulong, int>();  // black to move, plies to mate
        public long Mates;
        public int MaxWtmDistance = -1, MaxBtmDistance = -1;
        public Action<string> Log = s => { };
        public static long WonLimitDefault = 20_000_000;
        public long WonLimit = WonLimitDefault;

        private readonly Symmetry sym;
        private readonly ThreePiece geo;
        private readonly SparseSolver[] sub;
        private readonly int k, cellBits;
        private readonly Board board;
        private readonly MoveList moves = new MoveList(1024);

        private static readonly Dictionary<string, SparseSolver> cache = new Dictionary<string, SparseSolver>();

        public static SparseSolver For(BoardGeometry g, PieceType[] pieces, Action<string> log)
        {
            var sorted = (PieceType[])pieces.Clone();
            Array.Sort(sorted, (x, y) => ((int)y).CompareTo((int)x));
            string key = g.Dimensions + ":" + g.Side + ":" + g.DiagonalAxes + ":" + g.KingAxes + ":" + Material(sorted);
            if (cache.TryGetValue(key, out var s)) return s;
            s = new SparseSolver(g, sorted, log);
            cache[key] = s;
            s.Solve();
            return s;
        }

        private static string Material(PieceType[] pieces)
        {
            var sb = new StringBuilder("K");
            foreach (var t in pieces) sb.Append(Piece.ToChar(Piece.Make(t, Color.White)));
            return sb.ToString();
        }

        public string Name { get { return Material(Pieces) + "vK-" + G.Dimensions + "d" + G.Side + Program.Variant(G); } }

        private SparseSolver(BoardGeometry g, PieceType[] sortedPieces, Action<string> log)
        {
            G = g; Pieces = sortedPieces; k = sortedPieces.Length;
            if (log != null) Log = log;
            cellBits = 1;
            while ((1 << cellBits) < g.CellCount) cellBits++;
            if ((k + 2) * cellBits > 64) throw new ArgumentException("too many pieces for a 64-bit position key on this board");
            sym = new Symmetry(g);
            geo = new ThreePiece(g, PieceType.Queen);
            board = new Board(g);
            sub = new SparseSolver[k];
            if (k > 1)
            {
                for (int i = 0; i < k; i++)
                {
                    var rest = new List<PieceType>(Pieces);
                    rest.RemoveAt(i);
                    sub[i] = For(g, rest.ToArray(), log);
                }
            }
        }

        // ------------------------------------------------------------ keys

        /// <summary>Canonical key: the least packed (white king, pieces, black king) over the whole symmetry group, identical pieces sorted.</summary>
        public ulong Key(int wk, int[] w, int bk, int[] scratch)
        {
            ulong best = ulong.MaxValue;
            for (int t = 0; t < sym.TransformCount; t++)
            {
                for (int i = 0; i < k; i++) scratch[i] = sym.Apply(t, w[i]);
                for (int i = 1; i < k; i++) // pieces are grouped by type; sort cells within a group
                    for (int j = i; j > 0 && Pieces[j] == Pieces[j - 1] && scratch[j] < scratch[j - 1]; j--) { int x = scratch[j]; scratch[j] = scratch[j - 1]; scratch[j - 1] = x; }
                ulong key = (ulong)sym.Apply(t, wk);
                for (int i = 0; i < k; i++) key = (key << cellBits) | (uint)scratch[i];
                key = (key << cellBits) | (uint)sym.Apply(t, bk);
                if (key < best) best = key;
            }
            return best;
        }

        public void Decode(ulong key, out int wk, int[] w, out int bk)
        {
            ulong mask = (1UL << cellBits) - 1;
            bk = (int)(key & mask); key >>= cellBits;
            for (int i = k - 1; i >= 0; i--) { w[i] = (int)(key & mask); key >>= cellBits; }
            wk = (int)(key & mask);
        }

        public int ProbeWtm(int wk, int[] w, int bk) { return Won.TryGetValue(Key(wk, w, bk, new int[k]), out int v) ? v : -1; }
        public int ProbeBtm(int wk, int[] w, int bk) { return Lost.TryGetValue(Key(wk, w, bk, new int[k]), out int v) ? v : -1; }

        // ------------------------------------------------------------ rules through the Core board

        private static void Setup(Board b, PieceType[] pieces, int wk, int[] w, int bk, Color stm)
        {
            b.Clear();
            b.PlacePiece(wk, Piece.Make(PieceType.King, Color.White, true));
            for (int i = 0; i < pieces.Length; i++) b.PlacePiece(w[i], Piece.Make(pieces[i], Color.White, true));
            b.PlacePiece(bk, Piece.Make(PieceType.King, Color.Black, true));
            b.SetSideToMove(stm);
        }

        private bool Occupied(int cell, int wk, int[] w, int bk)
        {
            if (cell == wk || cell == bk) return true;
            for (int i = 0; i < k; i++) if (w[i] == cell) return true;
            return false;
        }

        // ------------------------------------------------------------ step 1: every checkmate

        private List<ulong> EnumerateMates()
        {
            var found = new ConcurrentDictionary<ulong, byte>();
            int cells = G.CellCount;
            var types = new PieceType[k + 1];
            types[0] = PieceType.King;
            Array.Copy(Pieces, 0, types, 1, k);
            var boards = new ThreadLocal<Board>(() => new Board(G));
            var lists = new ThreadLocal<MoveList>(() => new MoveList(1024));

            foreach (int bk in sym.DomainCells)
            {
                var flights = new List<int>();
                foreach (var d in G.King) { int t = G.Step(bk, d); if (t >= 0) flights.Add(t); }
                int selfBit = flights.Count;
                ulong target = (1UL << (selfBit + 1)) - 1;
                // Blocker-free cover of each flight cell (attack or occupation) and of the king's own cell, per piece type and cell.
                var mask = new ulong[k + 1][];
                var maxPop = new int[k + 2];
                for (int lvl = 0; lvl <= k; lvl++)
                {
                    mask[lvl] = new ulong[cells];
                    if (lvl > 0 && types[lvl] == types[lvl - 1]) { mask[lvl] = mask[lvl - 1]; continue; }
                    for (int p = 0; p < cells; p++)
                    {
                        if (p == bk) continue;
                        ulong m = 0;
                        for (int i = 0; i < flights.Count; i++) if (flights[i] == p || geo.Attacks(types[lvl], p, flights[i], -1)) m |= 1UL << i;
                        if (lvl > 0 && geo.Attacks(types[lvl], p, bk, -1)) m |= 1UL << selfBit;
                        mask[lvl][p] = m;
                    }
                }
                var suffix = new int[k + 2];
                for (int lvl = k; lvl >= 0; lvl--)
                {
                    int best = 0;
                    foreach (ulong m in mask[lvl]) best = Math.Max(best, BitOperations.PopCount(m));
                    suffix[lvl] = suffix[lvl + 1] + best;
                }
                if (BitOperations.PopCount(target) > suffix[0]) continue; // this king cell can never be mated by this material

                Parallel.For(0, cells, wk =>
                {
                    if (wk == bk || geo.KingsAdjacent(wk, bk)) return;
                    ulong rem = target & ~mask[0][wk];
                    if (BitOperations.PopCount(rem) > suffix[1]) return;
                    var w = new int[k];
                    var scratch = new int[k];
                    Place(1, rem, wk, w, bk, mask, suffix, boards.Value, lists.Value, scratch, found);
                });
            }
            return new List<ulong>(found.Keys);
        }

        private void Place(int lvl, ulong rem, int wk, int[] w, int bk, ulong[][] mask, int[] suffix, Board b, MoveList ml, int[] scratch, ConcurrentDictionary<ulong, byte> found)
        {
            if (lvl > k)
            {
                if (rem != 0) return;
                Setup(b, Pieces, wk, w, bk, Color.Black);
                if (!b.InCheck()) return;
                b.GenerateLegal(ml);
                if (ml.Count == 0) found.TryAdd(Key(wk, w, bk, scratch), 0);
                return;
            }
            int cells = G.CellCount;
            ulong[] m = mask[lvl];
            for (int p = 0; p < cells; p++)
            {
                if (p == wk || p == bk) continue;
                bool clash = false;
                for (int i = 0; i < lvl - 1; i++) if (w[i] == p) { clash = true; break; }
                if (clash) continue;
                ulong r = rem & ~m[p];
                if (BitOperations.PopCount(r) > suffix[lvl + 1]) continue;
                w[lvl - 1] = p;
                Place(lvl + 1, r, wk, w, bk, mask, suffix, b, ml, scratch, found);
            }
        }

        // ------------------------------------------------------------ steps 2 and 3: retrograde closure in distance order

        private void Solve()
        {
            var sw = Stopwatch.StartNew();
            var buckets = new List<List<ulong>>();
            void AddLost(ulong key, int v)
            {
                Lost[key] = v;
                while (buckets.Count <= v) buckets.Add(new List<ulong>());
                buckets[v].Add(key);
            }

            var mates = EnumerateMates();
            Mates = mates.Count;
            foreach (ulong m in mates) AddLost(m, 0);
            Log(Name + ": " + Mates + " checkmate classes found in " + sw.Elapsed.TotalSeconds.ToString("F1") + " s");

            var w = new int[k];
            var pw = new int[k];
            var scratch = new int[k];

            // Lost positions whose every move is a capture into a won smaller position have no won successor of this material to be found from.
            for (int i = 0; i < k; i++)
            {
                if (sub[i] == null) continue;
                if (i > 0 && Pieces[i] == Pieces[i - 1]) continue;
                var sw2 = new int[k - 1];
                foreach (ulong skey in sub[i].Won.Keys)
                {
                    sub[i].Decode(skey, out int swk, sw2, out int sbk);
                    for (int j = 0, o = 0; j < k; j++) w[j] = j == i ? sbk : sw2[o++];
                    foreach (var d in G.King)
                    {
                        int p = G.Step(sbk, d);
                        if (p < 0 || Occupied(p, swk, w, -1) || geo.KingsAdjacent(swk, p)) continue;
                        ulong ckey = Key(swk, w, p, scratch);
                        if (Lost.ContainsKey(ckey)) continue;
                        int v = EvaluateLost(swk, w, p, scratch);
                        if (v >= 0) AddLost(ckey, v);
                    }
                }
            }

            for (int n = 0; n < buckets.Count; n++)
            {
                var layer = buckets[n];
                for (int li = 0; li < layer.Count; li++)
                {
                    Decode(layer[li], out int wk, w, out int bk);
                    // White un-moves: the king, then each piece.
                    for (int mover = -1; mover < k; mover++)
                    {
                        PieceType type = mover < 0 ? PieceType.King : Pieces[mover];
                        int from = mover < 0 ? wk : w[mover];
                        bool slides = type == PieceType.Queen || type == PieceType.Rook || type == PieceType.Bishop;
                        Direction[] dirs = type == PieceType.King ? G.King : type == PieceType.Knight ? G.Knight : type == PieceType.Rook ? G.Rook : type == PieceType.Bishop ? G.Bishop : G.Queen;
                        foreach (var d in dirs)
                        {
                            int p = G.Step(from, d);
                            while (p >= 0 && !Occupied(p, wk, w, bk))
                            {
                                Array.Copy(w, pw, k);
                                int pwk = wk;
                                if (mover < 0) pwk = p; else pw[mover] = p;
                                Setup(board, Pieces, pwk, pw, bk, Color.White);
                                if (!board.IsAttacked(bk, Color.White))
                                {
                                    ulong wkey = Key(pwk, pw, bk, scratch);
                                    if (!Won.ContainsKey(wkey))
                                    {
                                        Won[wkey] = n + 1;
                                        if (Won.Count > WonLimit)
                                        {
                                            Log(Name + ": cap reached at distance " + (n + 1) + " plies; partial won set by the black king's distance from the nearest edge: " + LevelHistogram());
                                            throw new InvalidOperationException("won set exceeds " + WonLimit + " positions; this material is not a sparse case");
                                        }
                                        NewWon(pwk, pw, bk, scratch, AddLost);
                                    }
                                }
                                if (!slides) break;
                                p = G.Step(p, d);
                            }
                        }
                    }
                }
                if (layer.Count > 0) Log(Name + ": distance " + n + ": " + layer.Count + " lost classes, won so far " + Won.Count + " (" + sw.Elapsed.TotalSeconds.ToString("F0") + " s)");
            }
            foreach (var kv in Won) if (kv.Value > MaxWtmDistance) MaxWtmDistance = kv.Value;
            foreach (var kv in Lost) if (kv.Value > MaxBtmDistance) MaxBtmDistance = kv.Value;
            Log(Name + ": solved in " + sw.Elapsed.TotalSeconds.ToString("F1") + " s, won classes " + Won.Count + ", lost classes " + Lost.Count + ", longest " + MaxWtmDistance + " plies");
        }

        /// <summary>A position has just become won: every black-to-move position one non-capturing king move before it is a candidate.</summary>
        private void NewWon(int wk, int[] w, int bk, int[] scratch, Action<ulong, int> addLost)
        {
            var cw = (int[])w.Clone(); // Setup below reuses the shared board; keep our own cells
            foreach (var d in G.King)
            {
                int p = G.Step(bk, d);
                if (p < 0 || Occupied(p, wk, cw, -1) || geo.KingsAdjacent(wk, p)) continue;
                ulong ckey = Key(wk, cw, p, scratch);
                if (Lost.ContainsKey(ckey)) continue;
                int v = EvaluateLost(wk, cw, p, scratch);
                if (v >= 0) addLost(ckey, v);
            }
        }

        /// <summary>Plies to mate if every legal black move leads to a won position (at least one move), else -1.</summary>
        private int EvaluateLost(int wk, int[] w, int bk, int[] scratch)
        {
            Setup(board, Pieces, wk, w, bk, Color.Black);
            board.GenerateLegal(moves);
            if (moves.Count == 0) return -1;
            int worst = -1;
            for (int i = 0; i < moves.Count; i++)
            {
                int to = moves[i].To;
                int captured = -1;
                for (int j = 0; j < k; j++) if (w[j] == to) captured = j;
                int v;
                if (captured < 0) { if (!Won.TryGetValue(Key(wk, w, to, scratch), out v)) return -1; }
                else
                {
                    if (sub[captured] == null) return -1; // bare kings
                    var rest = new int[k - 1];
                    for (int j = 0, o = 0; j < k; j++) if (j != captured) rest[o++] = w[j];
                    v = sub[captured].ProbeWtm(wk, rest, to);
                    if (v < 0) return -1;
                }
                if (v > worst) worst = v;
            }
            return worst + 1;
        }

        // ------------------------------------------------------------ verification

        /// <summary>
        /// Exhaustive one-ply check of every stored position with the full Core rules: a win in n has a move to a loss in n-1 and
        /// none to a smaller one; a loss in n has only moves to wins, the largest n-1; a loss in 0 is checkmate. Together with the
        /// closure (every predecessor of a decided position was examined) this is what makes "everything else is a draw" hold.
        /// </summary>
        public List<string> VerifyOnePly()
        {
            var failures = new ConcurrentBag<string>();
            var wonKeys = new List<ulong>(Won.Keys);
            var lostKeys = new List<ulong>(Lost.Keys);
            var boards = new ThreadLocal<Board>(() => new Board(G));
            var lists = new ThreadLocal<MoveList>(() => new MoveList(1024));
            Parallel.For(0, wonKeys.Count, idx =>
            {
                var w = new int[k]; var nw = new int[k]; var scratch = new int[k];
                Board b = boards.Value; MoveList ml = lists.Value;
                Decode(wonKeys[idx], out int wk, w, out int bk);
                Setup(b, Pieces, wk, w, bk, Color.White);
                if (b.IsAttacked(bk, Color.White)) { failures.Add("won position is illegal: " + wonKeys[idx]); return; }
                b.GenerateLegal(ml);
                int best = int.MaxValue;
                for (int i = 0; i < ml.Count; i++)
                {
                    Array.Copy(w, nw, k);
                    int nwk = wk;
                    if (ml[i].From == wk) nwk = ml[i].To; else for (int j = 0; j < k; j++) if (w[j] == ml[i].From) nw[j] = ml[i].To;
                    if (Lost.TryGetValue(Key(nwk, nw, bk, scratch), out int v) && v < best) best = v;
                }
                if (best != Won[wonKeys[idx]] - 1) failures.Add("win in " + Won[wonKeys[idx]] + " but best successor is " + best + ": " + wonKeys[idx]);
            });
            Parallel.For(0, lostKeys.Count, idx =>
            {
                var w = new int[k]; var scratch = new int[k];
                Board b = boards.Value; MoveList ml = lists.Value;
                Decode(lostKeys[idx], out int wk, w, out int bk);
                int expected = Lost[lostKeys[idx]];
                Setup(b, Pieces, wk, w, bk, Color.Black);
                b.GenerateLegal(ml);
                if (expected == 0) { if (ml.Count != 0 || !b.InCheck()) failures.Add("marked mate but is not: " + lostKeys[idx]); return; }
                int worst = -1;
                for (int i = 0; i < ml.Count; i++)
                {
                    int to = ml[i].To, captured = -1, v;
                    for (int j = 0; j < k; j++) if (w[j] == to) captured = j;
                    if (captured < 0) { if (!Won.TryGetValue(Key(wk, w, to, scratch), out v)) v = -1; }
                    else
                    {
                        var rest = new int[k - 1];
                        for (int j = 0, o = 0; j < k; j++) if (j != captured) rest[o++] = w[j];
                        v = sub[captured] == null ? -1 : sub[captured].ProbeWtm(wk, rest, to);
                    }
                    if (v < 0) { failures.Add("loss with an escape: " + lostKeys[idx]); return; }
                    if (v > worst) worst = v;
                }
                if (ml.Count == 0 || worst != expected - 1) failures.Add("loss in " + expected + " but worst successor is " + worst + ": " + lostKeys[idx]);
            });
            Log(Name + ": one-ply verification of all " + wonKeys.Count + " wins and " + lostKeys.Count + " losses against the full rules, " + failures.Count + " failures");
            return new List<string>(failures);
        }


        private string LevelHistogram()
        {
            var byLevel = new long[(G.Side + 1) / 2];
            var w = new int[k];
            foreach (var kv in Won) { Decode(kv.Key, out int wk, w, out int bk); byLevel[Centrality(bk)]++; }
            var sb = new StringBuilder();
            for (int l = 0; l < byLevel.Length; l++) sb.Append(l).Append(':').Append(byLevel[l]).Append(' ');
            return sb.ToString().TrimEnd();
        }

        // ------------------------------------------------------------ report

        private int Centrality(int cell)
        {
            int m = int.MaxValue;
            for (int i = 0; i < G.Dimensions; i++) { int v = G.Coord(cell, i); m = Math.Min(m, Math.Min(v, G.Side - 1 - v)); }
            return m;
        }

        public string Summary()
        {
            var sb = new StringBuilder();
            sb.Append(Material(Pieces)).Append(" vs K, ").Append(G.Dimensions).Append(" dimensions, side ").Append(G.Side).Append(RuleText(G)).Append(" (sparse exact solve; counts are positions up to symmetry)\n");
            sb.Append("checkmates ").Append(Mates).Append(", white-to-move wins ").Append(Won.Count).Append(", black-to-move losses ").Append(Lost.Count).Append(", every other legal position is a draw\n");
            sb.Append("longest forced mate: white to move ").Append(MaxWtmDistance).Append(" plies, black to move ").Append(MaxBtmDistance).Append(" plies\n");
            var byLevel = new long[(G.Side + 1) / 2];
            var hist = new SortedDictionary<int, long>();
            var w = new int[k];
            foreach (var kv in Won)
            {
                Decode(kv.Key, out int wk, w, out int bk);
                byLevel[Centrality(bk)]++;
                hist.TryGetValue(kv.Value, out long c); hist[kv.Value] = c + 1;
            }
            sb.Append("white-to-move wins by the black king's distance from the nearest edge:");
            for (int l = 0; l < byLevel.Length; l++) sb.Append("  ").Append(l).Append(": ").Append(byLevel[l]);
            sb.Append('\n').Append("white-to-move distance histogram (plies: classes):");
            foreach (var kv in hist) sb.Append(' ').Append(kv.Key).Append(':').Append(kv.Value);
            sb.Append('\n');
            return sb.ToString();
        }

        /// <summary>The deepest wins, written out so they can be set up on the board.</summary>
        public IEnumerable<string> Deepest(int limit)
        {
            var w = new int[k];
            int shown = 0;
            foreach (var kv in Won)
            {
                if (kv.Value != MaxWtmDistance) continue;
                if (shown++ >= limit) yield break;
                Decode(kv.Key, out int wk, w, out int bk);
                var sb = new StringBuilder("WTM mate in " + kv.Value + " plies  K" + G.CoordOf(wk).ToCompact());
                for (int i = 0; i < k; i++) sb.Append(' ').Append(Piece.ToChar(Piece.Make(Pieces[i], Color.White))).Append(G.CoordOf(w[i]).ToCompact());
                sb.Append(" k").Append(G.CoordOf(bk).ToCompact());
                yield return sb.ToString();
            }
        }

        internal static string RuleText(BoardGeometry g)
        {
            if (g.BoardKing) return ", rule variant: diagonals up to " + g.DiagonalAxes + " axes, board-king (2D king on the x-y board, straight steps across z and w)";
            return g.DiagonalAxes == 2 && g.KingAxes == 2 ? "" : ", rule variant: diagonals up to " + g.DiagonalAxes + " axes, king up to " + g.KingAxes + " axes";
        }
    }
}
