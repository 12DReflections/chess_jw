using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Chess4D.Core;

namespace Chess4D.Tablebase
{
    /// <summary>
    /// Retrograde depth-to-mate solver for K+A+B vs K, the four-piece extension of
    /// <see cref="Generator"/> with the same value markers and pass structure.
    ///
    /// Index: white king in the fundamental domain, piece A an orbit
    /// representative under the king's stabiliser, piece B and the black king
    /// unreduced; slots that are not their own canonical index are marked
    /// illegal. Storage is one array per (king, A) pair so nothing exceeds the
    /// array length limit.
    ///
    /// The black king may capture an undefended piece, which leaves a
    /// three-piece position with White to move. Those values come from the
    /// three-piece tables of the same geometry, solved first: a drawn one is an
    /// escape, a won one sets a floor on the loss distance (packed above the
    /// successor count in the counter).
    /// </summary>
    public sealed class FourPieceGenerator
    {
        private const int Escape = (1 << 30) | 0x80; // the low byte never reaches zero within 32 decrements

        public readonly BoardGeometry G;
        public readonly Symmetry Sym;
        public readonly ThreePiece Geo;
        public readonly PieceType A, B;
        public readonly Generator SubA, SubB; // K+A vs K (B was captured), K+B vs K (A was captured)
        public readonly ushort[][] Values;
        private readonly int[][] counters;
        private readonly int cells;
        private readonly int slotsPerPair;
        private int maxAssigned;

        public Action<string> Log = s => Console.WriteLine(s);
        public int Threads = Environment.ProcessorCount;
        public int Iteration { get; private set; }
        public long LegalWtm, LegalBtm, Mates, Stalemates, Wins, Losses, DeadSlots, CaptureEscapes;
        public int MaxWtmDistance = -1, MaxBtmDistance = -1;

        public long EntryCount { get { return Sym.PairCount * slotsPerPair; } }

        public FourPieceGenerator(BoardGeometry g, PieceType a, PieceType b, Action<string> log = null)
        {
            if (log != null) Log = log;
            G = g; A = a; B = b;
            cells = g.CellCount;
            Sym = new Symmetry(g);
            Geo = new ThreePiece(g, a);
            slotsPerPair = checked(cells * cells * 2);
            SubA = SolveSub(a);
            SubB = a == b ? SubA : SolveSub(b);
            Values = new ushort[Sym.PairCount][];
            counters = new int[Sym.PairCount][];
            for (long p = 0; p < Sym.PairCount; p++)
            {
                Values[p] = new ushort[slotsPerPair];
                Array.Fill(Values[p], Generator.Unknown);
                counters[p] = new int[slotsPerPair / 2];
            }
        }

        private Generator SolveSub(PieceType t)
        {
            var sub = new Generator(G, t) { Log = s => { }, Threads = Threads };
            sub.Initialise();
            sub.Solve();
            Log("sub-table K+" + Piece.ToChar(Piece.Make(t, Color.White)) + " vs K: WTM wins " + sub.Wins + " of " + sub.LegalWtm + ", checkmates " + sub.Mates + ", longest " + sub.MaxWtmDistance + " plies");
            return sub;
        }

        // ------------------------------------------------------------ geometry and index

        private bool Attacked(int wk, int wa, int wb, int target)
        {
            return Geo.Attacks(PieceType.King, wk, target, -1)
                || (Geo.Attacks(A, wa, target, wk) && Geo.Attacks(A, wa, target, wb))
                || (Geo.Attacks(B, wb, target, wk) && Geo.Attacks(B, wb, target, wa));
        }

        public void Index(int wk, int wa, int wb, int bk, int stm, out long pair, out int slot)
        {
            int c = Sym.ClassOf(wk);
            int ba = int.MaxValue, bb = int.MaxValue, bkk = int.MaxValue;
            foreach (int t in Sym.Coset(wk))
            {
                int a = Sym.Apply(t, wa);
                if (a > ba) continue;
                int b = Sym.Apply(t, wb);
                if (a == ba && b > bb) continue;
                int k = Sym.Apply(t, bk);
                if (a < ba || b < bb || k < bkk) { ba = a; bb = b; bkk = k; }
            }
            pair = Sym.PairOffset[c] + Sym.RepId(c, ba);
            slot = ((bb * cells) + bkk) * 2 + stm;
        }

        public ushort Probe(int wk, int wa, int wb, int bk, int stm)
        {
            Index(wk, wa, wb, bk, stm, out long pair, out int slot);
            return Values[pair][slot];
        }

        private void DecodePair(long pair, out int wk, out int wa)
        {
            int c = Sym.ClassOfPair(pair);
            wk = Sym.DomainCells[c];
            wa = Sym.RepCells[c][pair - Sym.PairOffset[c]];
        }

        // ------------------------------------------------------------ init

        public void Initialise()
        {
            var sw = Stopwatch.StartNew();
            long legalW = 0, legalB = 0, mates = 0, stalemates = 0, dead = 0, capEsc = 0;
            var opts = new ParallelOptions { MaxDegreeOfParallelism = Threads };
            Parallel.For(0L, Sym.PairCount, opts,
                () => new long[6],
                (pair, state, acc) =>
                {
                    DecodePair(pair, out int wk, out int wa);
                    ushort[] vals = Values[pair];
                    int[] ctr = counters[pair];
                    var succ = new long[64];
                    for (int wb = 0; wb < cells; wb++)
                    {
                        for (int bk = 0; bk < cells; bk++)
                        {
                            int wtm = ((wb * cells) + bk) * 2, btm = wtm + 1;
                            if (wa == wk || wb == wk || wb == wa || bk == wk || bk == wa || bk == wb) { vals[wtm] = Generator.Illegal; vals[btm] = Generator.Illegal; continue; }
                            Index(wk, wa, wb, bk, 0, out long cp, out int cs);
                            if (cp != pair || cs != wtm) { vals[wtm] = Generator.Illegal; vals[btm] = Generator.Illegal; acc[4]++; continue; }
                            bool inCheck = Attacked(wk, wa, wb, bk);
                            if (inCheck) vals[wtm] = Generator.Illegal; else acc[0]++;
                            if (Geo.KingsAdjacent(wk, bk)) { vals[btm] = Generator.Illegal; continue; }
                            acc[1]++;
                            int count = 0, floor = 0;
                            bool escape = false;
                            foreach (var d in G.King)
                            {
                                int t = G.Step(bk, d);
                                if (t < 0 || t == wk) continue;
                                if (t == wa || t == wb)
                                {
                                    // Capture. The line through the cell the king leaves is open, so only the white king can block the defender.
                                    bool defended = t == wa
                                        ? Geo.KingsAdjacent(wk, wa) || Geo.Attacks(B, wb, wa, wk)
                                        : Geo.KingsAdjacent(wk, wb) || Geo.Attacks(A, wa, wb, wk);
                                    if (defended) continue;
                                    ushort sv = t == wa ? SubB.Probe(wk, wb, t, 0) : SubA.Probe(wk, wa, t, 0);
                                    if (sv == Generator.Illegal) throw new InvalidOperationException("capture leads to an illegal three-piece position");
                                    if (sv >= Generator.Stalemate) { escape = true; acc[5]++; break; }
                                    if (sv + 1 > floor) floor = sv + 1;
                                    continue;
                                }
                                if (Attacked(wk, wa, wb, t)) continue;
                                Index(wk, wa, wb, t, 0, out long sp, out int ss);
                                succ[count++] = sp * slotsPerPair + ss;
                            }
                            if (escape) { ctr[btm >> 1] = Escape; continue; }
                            int distinct = Distinct(succ, count);
                            if (distinct == 0)
                            {
                                ctr[btm >> 1] = Escape;
                                if (floor > 0) { vals[btm] = (ushort)floor; RaiseMax(floor); }
                                else if (inCheck) { vals[btm] = 0; acc[2]++; }
                                else { vals[btm] = Generator.Stalemate; acc[3]++; }
                            }
                            else ctr[btm >> 1] = distinct | (floor << 8);
                        }
                    }
                    return acc;
                },
                acc =>
                {
                    Interlocked.Add(ref legalW, acc[0]); Interlocked.Add(ref legalB, acc[1]); Interlocked.Add(ref mates, acc[2]);
                    Interlocked.Add(ref stalemates, acc[3]); Interlocked.Add(ref dead, acc[4]); Interlocked.Add(ref capEsc, acc[5]);
                });
            LegalWtm = legalW; LegalBtm = legalB; Mates = mates; Stalemates = stalemates; DeadSlots = dead; CaptureEscapes = capEsc;
            Iteration = 0;
            Log("init: " + EntryCount + " entries, legal WTM " + legalW + ", legal BTM " + legalB + ", checkmates " + mates + ", stalemates " + stalemates + ", BTM positions saved by a capture " + capEsc + " in " + sw.Elapsed.TotalSeconds.ToString("F1") + " s");
        }

        private void RaiseMax(int v)
        {
            int cur;
            while (v > (cur = Volatile.Read(ref maxAssigned)) && Interlocked.CompareExchange(ref maxAssigned, v, cur) != cur) { }
        }

        private static int Distinct(long[] a, int count)
        {
            if (count <= 1) return count;
            Array.Sort(a, 0, count);
            int d = 1;
            for (int i = 1; i < count; i++) if (a[i] != a[i - 1]) d++;
            return d;
        }

        // ------------------------------------------------------------ retrograde passes

        public void Solve()
        {
            int idle = 0;
            while (idle < 2 || Iteration <= maxAssigned)
            {
                long solved = Pass(Iteration);
                Iteration++;
                idle = solved == 0 ? idle + 1 : 0;
            }
            Finish();
        }

        private long Pass(int nValue)
        {
            var sw = Stopwatch.StartNew();
            ushort n = (ushort)nValue;
            long solved = 0;
            var opts = new ParallelOptions { MaxDegreeOfParallelism = Threads };
            Parallel.For(0L, Sym.PairCount, opts,
                () => 0L,
                (pair, state, acc) =>
                {
                    ushort[] vals = Values[pair];
                    long[] preds = null;
                    int wk = -1, wa = -1;
                    for (int slot = 0; slot < vals.Length; slot++)
                    {
                        if (vals[slot] != n) continue;
                        if (preds == null) { preds = new long[64]; DecodePair(pair, out wk, out wa); }
                        int stm = slot & 1, rest = slot >> 1;
                        int bk = rest % cells, wb = rest / cells;
                        if (stm == 1) acc += WhitePredecessors(wk, wa, wb, bk, (ushort)(n + 1));
                        else acc += BlackPredecessors(wk, wa, wb, bk, n + 1, preds);
                    }
                    return acc;
                },
                acc => Interlocked.Add(ref solved, acc));
            if (solved > 0 || nValue % 10 == 0) Log("pass " + nValue + ": solved " + solved + " positions with distance " + (nValue + 1) + " in " + sw.Elapsed.TotalSeconds.ToString("F1") + " s");
            if (nValue + 1 > Generator.MaxDistance) throw new OverflowException("distance exceeds the two-byte range");
            return solved;
        }

        /// <summary>The position is lost for Black in n-1 and was reached by a White move: every legal White-to-move predecessor wins in n.</summary>
        private long WhitePredecessors(int wk, int wa, int wb, int bk, ushort n)
        {
            long solved = 0;
            foreach (var d in G.King)
            {
                int p = G.Step(wk, d);
                if (p < 0 || p == wa || p == wb || p == bk) continue;
                if (!Attacked(p, wa, wb, bk)) solved += MarkWin(p, wa, wb, bk, n);
            }
            solved += PiecePredecessors(A, wa, true, wk, wa, wb, bk, n);
            solved += PiecePredecessors(B, wb, false, wk, wa, wb, bk, n);
            return solved;
        }

        private long PiecePredecessors(PieceType type, int from, bool isA, int wk, int wa, int wb, int bk, ushort n)
        {
            long solved = 0;
            if (type == PieceType.Knight)
            {
                foreach (var d in G.Knight)
                {
                    int p = G.Step(from, d);
                    if (p < 0 || p == wk || p == wa || p == wb || p == bk) continue;
                    int na = isA ? p : wa, nb = isA ? wb : p;
                    if (!Attacked(wk, na, nb, bk)) solved += MarkWin(wk, na, nb, bk, n);
                }
                return solved;
            }
            Direction[] dirs = type == PieceType.Rook ? G.Rook : type == PieceType.Bishop ? G.Bishop : G.Queen;
            foreach (var d in dirs)
            {
                int p = G.Step(from, d);
                while (p >= 0 && p != wk && p != wa && p != wb && p != bk)
                {
                    int na = isA ? p : wa, nb = isA ? wb : p;
                    if (!Attacked(wk, na, nb, bk)) solved += MarkWin(wk, na, nb, bk, n);
                    p = G.Step(p, d);
                }
            }
            return solved;
        }

        private long MarkWin(int wk, int wa, int wb, int bk, ushort n)
        {
            Index(wk, wa, wb, bk, 0, out long pair, out int slot);
            ushort[] vals = Values[pair];
            if (vals[slot] != Generator.Unknown) return 0;
            vals[slot] = n;
            return 1;
        }

        /// <summary>The position is won for White in n-1 and was reached by a black king move: each distinct canonical predecessor loses one escape.</summary>
        private long BlackPredecessors(int wk, int wa, int wb, int bk, int n, long[] preds)
        {
            int count = 0;
            foreach (var d in G.King)
            {
                int p = G.Step(bk, d);
                if (p < 0 || p == wk || p == wa || p == wb) continue;
                if (Geo.KingsAdjacent(wk, p)) continue;
                Index(wk, wa, wb, p, 1, out long pp, out int ps);
                preds[count++] = pp * slotsPerPair + ps;
            }
            if (count > 1) Array.Sort(preds, 0, count);
            long solved = 0, prev = -1;
            for (int i = 0; i < count; i++)
            {
                long q = preds[i];
                if (q == prev) continue;
                prev = q;
                long pair = q / slotsPerPair;
                int slot = (int)(q % slotsPerPair);
                if (Values[pair][slot] != Generator.Unknown) continue;
                int left = Interlocked.Decrement(ref counters[pair][slot >> 1]);
                if ((left & 0xFF) != 0 || (left & (1 << 30)) != 0) continue;
                int v = Math.Max(n, left >> 8);
                Values[pair][slot] = (ushort)v;
                if (v > n) RaiseMax(v);
                solved++;
            }
            return solved;
        }

        private void Finish()
        {
            long wins = 0, losses = 0;
            int maxW = -1, maxB = -1;
            foreach (ushort[] vals in Values)
            {
                for (int s = 0; s < vals.Length; s++)
                {
                    ushort v = vals[s];
                    if (v >= Generator.Stalemate) continue;
                    if ((s & 1) == 0) { wins++; if (v > maxW) maxW = v; } else { losses++; if (v > maxB) maxB = v; }
                }
            }
            Wins = wins; Losses = losses; MaxWtmDistance = maxW; MaxBtmDistance = maxB;
        }

        // ------------------------------------------------------------ report

        private int Centrality(int cell)
        {
            int m = int.MaxValue;
            for (int i = 0; i < G.Dimensions; i++) { int v = G.Coord(cell, i); m = Math.Min(m, Math.Min(v, G.Side - 1 - v)); }
            return m;
        }

        public string Name { get { return "K" + Piece.ToChar(Piece.Make(A, Color.White)) + Piece.ToChar(Piece.Make(B, Color.White)) + "vK-" + G.Dimensions + "d" + G.Side; } }

        public string Summary()
        {
            var sb = new StringBuilder();
            sb.Append("K+").Append(Piece.ToChar(Piece.Make(A, Color.White))).Append('+').Append(Piece.ToChar(Piece.Make(B, Color.White)))
              .Append(" vs K, ").Append(G.Dimensions).Append(" dimensions, side ").Append(G.Side).Append('\n');
            sb.Append("table entries ").Append(EntryCount).Append(", duplicate slots ").Append(DeadSlots * 2).Append('\n');
            sb.Append("legal positions: white to move ").Append(LegalWtm).Append(", black to move ").Append(LegalBtm).Append('\n');
            sb.Append("checkmates ").Append(Mates).Append(", stalemates ").Append(Stalemates).Append(", black-to-move positions drawn at once by capturing a piece ").Append(CaptureEscapes).Append('\n');
            sb.Append("white-to-move wins ").Append(Wins).Append(" (").Append(Percent(Wins, LegalWtm)).Append("%), black-to-move losses ").Append(Losses).Append(" (").Append(Percent(Losses, LegalBtm)).Append("%)\n");
            sb.Append("longest forced mate: white to move ").Append(MaxWtmDistance).Append(" plies, black to move ").Append(MaxBtmDistance).Append(" plies; passes ").Append(Iteration).Append('\n');

            int levels = (G.Side + 1) / 2;
            var legal = new long[levels]; var won = new long[levels];
            var hist = new SortedDictionary<int, long>();
            for (long pair = 0; pair < Sym.PairCount; pair++)
            {
                ushort[] vals = Values[pair];
                for (int s = 0; s < vals.Length; s += 2)
                {
                    ushort v = vals[s];
                    if (v == Generator.Illegal) continue;
                    int lvl = Centrality((s >> 1) % cells);
                    legal[lvl]++;
                    if (v < Generator.Stalemate) { won[lvl]++; hist.TryGetValue(v, out long c); hist[v] = c + 1; }
                }
            }
            sb.Append("white-to-move wins by the black king's distance from the nearest edge (canonical positions):");
            for (int l = 0; l < levels; l++) sb.Append("  ").Append(l).Append(": ").Append(won[l]).Append('/').Append(legal[l]).Append(" (").Append(Percent(won[l], legal[l])).Append("%)");
            sb.Append('\n');
            sb.Append("white-to-move distance histogram (plies: positions):");
            foreach (var kv in hist) sb.Append(' ').Append(kv.Key).Append(':').Append(kv.Value);
            sb.Append('\n');
            return sb.ToString();
        }

        private static string Percent(long a, long b) { return b > 0 ? (100.0 * a / b).ToString("F2") : "0"; }

        // ------------------------------------------------------------ verification

        /// <summary>One-ply consistency of sampled positions against the full Core rules, including captures into the three-piece tables.</summary>
        public List<string> ConsistencySample(int samples, int seed)
        {
            var failures = new List<string>();
            var rng = new Random(seed);
            var legal = new MoveList(512);
            var whiteCells = new List<int>();
            int checkedCount = 0;
            for (int s = 0; s < samples * 50 && checkedCount < samples; s++)
            {
                long pair = rng.NextInt64(Sym.PairCount);
                int slot = rng.Next(slotsPerPair);
                ushort v = Values[pair][slot];
                if (v == Generator.Illegal) continue;
                DecodePair(pair, out int wk, out int wa);
                int stm = slot & 1, bk = (slot >> 1) % cells, wb = (slot >> 1) / cells;
                var b = new Board(G);
                b.Clear();
                b.PlacePiece(wk, Piece.Make(PieceType.King, Color.White, true));
                b.PlacePiece(wa, Piece.Make(A, Color.White, true));
                b.PlacePiece(wb, Piece.Make(B, Color.White, true));
                b.PlacePiece(bk, Piece.Make(PieceType.King, Color.Black, true));
                b.SetSideToMove(stm == 0 ? Color.White : Color.Black);
                b.GenerateLegal(legal);
                checkedCount++;
                string where = " at " + Name + " K" + G.CoordOf(wk).ToCompact() + " a" + G.CoordOf(wa).ToCompact() + " b" + G.CoordOf(wb).ToCompact() + " k" + G.CoordOf(bk).ToCompact() + (stm == 0 ? " w" : " b");
                if (v == Generator.Stalemate) { if (legal.Count != 0 || b.InCheck()) failures.Add("stalemate mark but " + legal.Count + " moves" + where); continue; }
                int best = int.MaxValue, worst = -1;
                bool escape = false;
                for (int i = 0; i < legal.Count; i++)
                {
                    b.Make(legal[i]);
                    ushort sv;
                    int nbk = b.KingCell(Color.Black), nwk = b.KingCell(Color.White);
                    if (b.PieceCount(Color.White) == 3)
                    {
                        int na = -1, nb = -1;
                        whiteCells.Clear();
                        b.GetPieceCells(Color.White, whiteCells);
                        // With two pieces of one type the two orderings are mirror slots with equal values, so either assignment is right.
                        foreach (int c in whiteCells)
                        {
                            if (c == nwk) continue;
                            if (na < 0 && Piece.TypeOf(b.GetPiece(c)) == A) na = c; else nb = c;
                        }
                        sv = Probe(nwk, na, nb, nbk, 1 - stm);
                    }
                    else
                    {
                        whiteCells.Clear();
                        b.GetPieceCells(Color.White, whiteCells);
                        int nx = whiteCells[0] == nwk ? whiteCells[1] : whiteCells[0];
                        sv = (Piece.TypeOf(b.GetPiece(nx)) == A ? SubA : SubB).Probe(nwk, nx, nbk, 0);
                    }
                    b.Unmake();
                    if (sv == Generator.Illegal) { failures.Add("successor marked illegal" + where); continue; }
                    bool win = sv < Generator.Stalemate;
                    if (stm == 0) { if (win && sv < best) best = sv; }
                    else { if (!win) escape = true; else if (sv > worst) worst = sv; }
                }
                if (stm == 0)
                {
                    if (v == Generator.Unknown) { if (best != int.MaxValue) failures.Add("WTM draw but a winning move exists" + where); }
                    else if (best != v - 1) failures.Add("WTM value " + v + " but best successor " + best + where);
                }
                else
                {
                    if (v == Generator.Unknown) { if (!escape && legal.Count > 0) failures.Add("BTM draw but every move loses" + where); }
                    else if (v == 0) { if (legal.Count != 0 || !b.InCheck()) failures.Add("mate mark but not mated" + where); }
                    else if (escape) failures.Add("BTM loss " + v + " but an escape exists" + where);
                    else if (worst != v - 1) failures.Add("BTM value " + v + " but worst successor " + worst + where);
                }
            }
            Log("consistency: checked " + checkedCount + " sampled positions against the full rules, " + failures.Count + " failures");
            return failures;
        }
    }
}
