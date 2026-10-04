using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Chess4D.Core;

namespace Chess4D.Tablebase
{
    /// <summary>
    /// Retrograde depth-to-mate solver for K+X vs K. Values are two bytes per
    /// canonical position: plies to mate (0 = checkmated, black to move), or one
    /// of the markers below. Fifty-move rule off. Black can never win, so a
    /// position is either a White win with a distance or a draw.
    ///
    /// Passes are parallel over index ranges. Within a pass every write to a
    /// value cell writes the same number, so races are benign; the black-move
    /// counters use atomic decrements and each (predecessor, successor) pair of
    /// canonical positions is counted exactly once on both sides.
    /// </summary>
    public sealed class Generator
    {
        public const ushort Unknown = 0xFFFF;
        public const ushort Illegal = 0xFFFE;
        public const ushort Stalemate = 0xFFFD;
        public const ushort MaxDistance = 0xFFF0;
        private const int Escape = 1 << 30;

        public readonly Symmetry Sym;
        public readonly ThreePiece Geo;
        public readonly BoardGeometry G;
        public ushort[] Values;
        private int[] counters; // black-to-move positions only: index >> 1
        public int Iteration { get; private set; }
        public string CheckpointPath;
        public Action<string> Log = s => Console.WriteLine(s);
        public int Threads = Environment.ProcessorCount;

        public long LegalWtm, LegalBtm, Mates, Stalemates, Wins, Losses, DeadSlots;
        public int MaxWtmDistance = -1, MaxBtmDistance = -1;

        public Generator(BoardGeometry g, PieceType whitePiece)
        {
            G = g;
            Sym = new Symmetry(g);
            Geo = new ThreePiece(g, whitePiece);
            Values = new ushort[Sym.EntryCount];
            Array.Fill(Values, Unknown);
            counters = new int[Sym.EntryCount / 2];
        }

        // ------------------------------------------------------------ init

        /// <summary>Marks illegal positions, mates and stalemates, and counts each black-to-move position's distinct canonical successors.</summary>
        public void Initialise()
        {
            var sw = Stopwatch.StartNew();
            long legalW = 0, legalB = 0, mates = 0, stalemates = 0, dead = 0;
            int cells = G.CellCount;
            var opts = new ParallelOptions { MaxDegreeOfParallelism = Threads };
            Parallel.For(0L, Sym.PairCount, opts,
                () => new long[5],
                (pair, state, acc) =>
                {
                    int cls = Sym.ClassOfPair(pair);
                    int wk = Sym.DomainCells[cls];
                    int wx = Sym.RepCells[cls][pair - Sym.PairOffset[cls]];
                    var succ = new long[G.King.Length];
                    for (int bk = 0; bk < cells; bk++)
                    {
                        long wtm = ((pair * cells) + bk) * 2;
                        long btm = wtm + 1;
                        if (bk == wk || bk == wx || wk == wx) { Values[wtm] = Illegal; Values[btm] = Illegal; continue; }
                        // The black king's cell is not reduced under the stabiliser of (wk, wx); a slot that is not its own
                        // canonical index is a duplicate that no position ever maps to. Mark it illegal so it is never scanned.
                        if (Sym.Index(wk, wx, bk, 0) != wtm) { Values[wtm] = Illegal; Values[btm] = Illegal; acc[4]++; continue; }
                        bool bkAttacked = Geo.BlackKingAttacked(wk, wx, bk);
                        // White to move: illegal if Black's king could be captured.
                        if (bkAttacked) Values[wtm] = Illegal; else acc[0]++;
                        // Black to move: illegal if the kings touch (White's king would be capturable).
                        if (Geo.KingsAdjacent(wk, bk)) { Values[btm] = Illegal; continue; }
                        acc[1]++;
                        int count = 0;
                        bool escape = false;
                        foreach (var d in G.King)
                        {
                            int t = G.Step(bk, d);
                            if (t < 0 || t == wk) continue;
                            if (t == wx)
                            {
                                if (!Geo.Attacks(PieceType.King, wk, wx, -1)) { escape = true; break; } // captures the undefended piece: draw
                                continue;
                            }
                            if (Geo.BlackKingAttacked(wk, wx, t)) continue;
                            succ[count++] = Sym.Index(wk, wx, t, 0);
                        }
                        if (escape) { counters[btm >> 1] = Escape; continue; }
                        int distinct = Distinct(succ, count);
                        if (distinct == 0)
                        {
                            if (bkAttacked) { Values[btm] = 0; acc[2]++; } else { Values[btm] = Stalemate; acc[3]++; }
                            counters[btm >> 1] = Escape;
                        }
                        else counters[btm >> 1] = distinct;
                    }
                    return acc;
                },
                acc => { Interlocked.Add(ref legalW, acc[0]); Interlocked.Add(ref legalB, acc[1]); Interlocked.Add(ref mates, acc[2]); Interlocked.Add(ref stalemates, acc[3]); Interlocked.Add(ref dead, acc[4]); });
            LegalWtm = legalW; LegalBtm = legalB; Mates = mates; Stalemates = stalemates; DeadSlots = dead;
            Iteration = 0;
            Log("init: " + Sym.EntryCount + " entries, duplicate slots " + dead * 2 + ", legal WTM " + legalW + ", legal BTM " + legalB + ", checkmates " + mates + ", stalemates " + stalemates + " in " + sw.Elapsed.TotalSeconds.ToString("F1") + " s");
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

        /// <summary>Runs passes until two consecutive passes solve nothing. Checkpoints after every pass when a path is set.</summary>
        public void Solve()
        {
            int idle = 0;
            while (idle < 2)
            {
                long solved = Pass(Iteration);
                Iteration++;
                idle = solved == 0 ? idle + 1 : 0;
                if (CheckpointPath != null) SaveCheckpoint();
            }
            Finish();
        }

        /// <summary>One pass: every position with value == n generates its predecessors.</summary>
        public long Pass(int nValue)
        {
            var sw = Stopwatch.StartNew();
            ushort n = (ushort)nValue;
            long solved = 0;
            long total = Sym.EntryCount;
            long chunk = Math.Max(4096, total / (Threads * 64));
            var opts = new ParallelOptions { MaxDegreeOfParallelism = Threads };
            Parallel.For(0L, (total + chunk - 1) / chunk, opts,
                () => 0L,
                (ci, state, acc) =>
                {
                    long start = ci * chunk, end = Math.Min(total, start + chunk);
                    var preds = new long[512];
                    for (long i = start; i < end; i++)
                    {
                        if (Values[i] != n) continue;
                        Sym.Decode(i, out int wk, out int wx, out int bk, out int stm);
                        if (stm == 1) acc += WhitePredecessors(wk, wx, bk, (ushort)(n + 1));
                        else acc += BlackPredecessors(wk, wx, bk, (ushort)(n + 1), preds);
                    }
                    return acc;
                },
                acc => Interlocked.Add(ref solved, acc));
            Log("pass " + nValue + ": solved " + solved + " positions with distance " + (nValue + 1) + " in " + sw.Elapsed.TotalSeconds.ToString("F1") + " s");
            if (solved > 0)
            {
                if ((nValue + 1) % 2 == 1) MaxWtmDistance = nValue + 1; else MaxBtmDistance = nValue + 1;
                if (nValue + 1 > MaxDistance) throw new OverflowException("distance exceeds the two-byte range");
            }
            return solved;
        }

        /// <summary>The position (BTM, black loses in n-1) was reached by a White move: mark every legal White-to-move predecessor as a win in n.</summary>
        private long WhitePredecessors(int wk, int wx, int bk, ushort n)
        {
            long solved = 0;
            // The white king came from p.
            foreach (var d in G.King)
            {
                int p = G.Step(wk, d);
                if (p < 0 || p == wx || p == bk) continue;
                if (Geo.BlackKingAttacked(p, wx, bk)) continue; // Black would have been in check on White's move: illegal
                solved += MarkWin(p, wx, bk, n);
            }
            // The white piece came from p.
            switch (Geo.WhitePiece)
            {
                case PieceType.Knight:
                    foreach (var d in G.Knight)
                    {
                        int p = G.Step(wx, d);
                        if (p < 0 || p == wk || p == bk) continue;
                        if (Geo.BlackKingAttacked(wk, p, bk)) continue;
                        solved += MarkWin(wk, p, bk, n);
                    }
                    break;
                default:
                    {
                        Direction[] dirs = Geo.WhitePiece == PieceType.Rook ? G.Rook : Geo.WhitePiece == PieceType.Bishop ? G.Bishop : G.Queen;
                        foreach (var d in dirs)
                        {
                            int p = G.Step(wx, d);
                            while (p >= 0 && p != wk && p != bk)
                            {
                                if (!Geo.BlackKingAttacked(wk, p, bk)) solved += MarkWin(wk, p, bk, n);
                                p = G.Step(p, d);
                            }
                        }
                        break;
                    }
            }
            return solved;
        }

        private long MarkWin(int wk, int wx, int bk, ushort n)
        {
            long idx = Sym.Index(wk, wx, bk, 0);
            if (Values[idx] != Unknown) return 0;
            Values[idx] = n;
            return 1;
        }

        /// <summary>The position (WTM, White wins in n-1) was reached by a Black king move from p: decrement each distinct canonical predecessor; when a predecessor has no escape left it is lost in n.</summary>
        private long BlackPredecessors(int wk, int wx, int bk, ushort n, long[] preds)
        {
            int count = 0;
            foreach (var d in G.King)
            {
                int p = G.Step(bk, d);
                if (p < 0 || p == wk || p == wx) continue;
                if (Geo.KingsAdjacent(wk, p)) continue; // predecessor illegal
                preds[count++] = Sym.Index(wk, wx, p, 1);
            }
            if (count > 1) Array.Sort(preds, 0, count);
            long solved = 0;
            long prev = -1;
            for (int i = 0; i < count; i++)
            {
                long q = preds[i];
                if (q == prev) continue;
                prev = q;
                if (Values[q] != Unknown) continue;
                int left = Interlocked.Decrement(ref counters[q >> 1]);
                if (left == 0) { Values[q] = n; solved++; }
            }
            return solved;
        }

        private void Finish()
        {
            long wins = 0, losses = 0;
            for (long i = 0; i < Values.Length; i++)
            {
                ushort v = Values[i];
                if (v < Illegal - 2) { if ((i & 1) == 0) wins++; else losses++; }
            }
            Wins = wins; Losses = losses;
            Log("done after " + Iteration + " passes: WTM wins " + wins + " of " + LegalWtm + " legal, BTM losses " + losses + " of " + LegalBtm + " legal (" + Mates + " checkmates, " + Stalemates + " stalemates), longest WTM distance " + MaxWtmDistance + " plies, longest BTM distance " + MaxBtmDistance + " plies");
        }

        // ------------------------------------------------------------ queries

        public ushort Probe(int wk, int wx, int bk, int stm) { return Values[Sym.Index(wk, wx, bk, stm)]; }

        public static string Describe(ushort v)
        {
            if (v == Unknown) return "draw";
            if (v == Illegal) return "illegal";
            if (v == Stalemate) return "stalemate";
            return "mate in " + v + " plies";
        }

        // ------------------------------------------------------------ checkpoint and output

        public void SaveCheckpoint()
        {
            string tmp = CheckpointPath + ".tmp";
            using (var fs = new FileStream(tmp, FileMode.Create, FileAccess.Write, FileShare.None, 1 << 20))
            using (var bw = new BinaryWriter(fs))
            {
                bw.Write(Iteration);
                bw.Write(LegalWtm); bw.Write(LegalBtm); bw.Write(Mates); bw.Write(Stalemates); bw.Write(DeadSlots);
                bw.Write(MaxWtmDistance); bw.Write(MaxBtmDistance);
                bw.Write(Values.Length);
                fs.Write(MemoryMarshal.AsBytes(Values.AsSpan()));
                fs.Write(MemoryMarshal.AsBytes(counters.AsSpan()));
            }
            File.Move(tmp, CheckpointPath, true);
            Log("checkpoint written after pass " + (Iteration - 1));
        }

        public bool TryLoadCheckpoint()
        {
            if (CheckpointPath == null || !File.Exists(CheckpointPath)) return false;
            using (var fs = new FileStream(CheckpointPath, FileMode.Open, FileAccess.Read, FileShare.Read, 1 << 20))
            using (var br = new BinaryReader(fs))
            {
                Iteration = br.ReadInt32();
                LegalWtm = br.ReadInt64(); LegalBtm = br.ReadInt64(); Mates = br.ReadInt64(); Stalemates = br.ReadInt64(); DeadSlots = br.ReadInt64();
                MaxWtmDistance = br.ReadInt32(); MaxBtmDistance = br.ReadInt32();
                long len = br.ReadInt64();
                if (len != Values.Length) throw new InvalidDataException("checkpoint size mismatch");
                ReadFully(fs, MemoryMarshal.AsBytes(Values.AsSpan()));
                ReadFully(fs, MemoryMarshal.AsBytes(counters.AsSpan()));
            }
            Log("resumed from checkpoint at pass " + Iteration);
            return true;
        }

        private static void ReadFully(Stream s, Span<byte> buffer)
        {
            while (buffer.Length > 0)
            {
                int r = s.Read(buffer);
                if (r <= 0) throw new EndOfStreamException();
                buffer = buffer.Slice(r);
            }
        }

        /// <summary>Compact indexed binary: header then the value array. The index is the canonical index; Symmetry rebuilds the mapping deterministically.</summary>
        public void WriteTable(string path)
        {
            using (var fs = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None, 1 << 20))
            using (var bw = new BinaryWriter(fs))
            {
                bw.Write(new[] { 'C', '4', 'D', 'T', 'B', '1', ' ', ' ' });
                bw.Write(G.Dimensions); bw.Write(G.Side); bw.Write((int)Geo.WhitePiece);
                bw.Write(Sym.PairCount); bw.Write((long)Values.Length);
                bw.Write(MaxWtmDistance); bw.Write(MaxBtmDistance);
                fs.Write(MemoryMarshal.AsBytes(Values.AsSpan()));
            }
        }

        /// <summary>Reads a table written by WriteTable into a generator built for the same geometry and piece.</summary>
        public void LoadTable(string path)
        {
            using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 1 << 20))
            using (var br = new BinaryReader(fs))
            {
                br.ReadChars(8);
                int dims = br.ReadInt32(), side = br.ReadInt32(), piece = br.ReadInt32();
                if (dims != G.Dimensions || side != G.Side || piece != (int)Geo.WhitePiece) throw new InvalidDataException("table does not match this generator");
                long pairs = br.ReadInt64(), len = br.ReadInt64();
                if (pairs != Sym.PairCount || len != Values.Length) throw new InvalidDataException("table size mismatch");
                MaxWtmDistance = br.ReadInt32(); MaxBtmDistance = br.ReadInt32();
                ReadFully(fs, MemoryMarshal.AsBytes(Values.AsSpan()));
            }
            long legalW = 0, legalB = 0, mates = 0, stalemates = 0, wins = 0, losses = 0;
            for (long i = 0; i < Values.Length; i++)
            {
                ushort v = Values[i];
                if (v == Illegal) continue;
                if ((i & 1) == 0) { legalW++; if (v < Stalemate) wins++; }
                else { legalB++; if (v == Stalemate) stalemates++; else if (v < Stalemate) { losses++; if (v == 0) mates++; } }
            }
            LegalWtm = legalW; LegalBtm = legalB; Mates = mates; Stalemates = stalemates; Wins = wins; Losses = losses;
        }

        /// <summary>Every won or lost position with its distance, for small result sets.</summary>
        public IEnumerable<string> ListDecided(int limit)
        {
            int shown = 0;
            for (long i = 0; i < Values.Length && shown < limit; i++)
            {
                ushort v = Values[i];
                if (v >= Stalemate) continue;
                Sym.Decode(i, out int wk, out int wx, out int bk, out int stm);
                shown++;
                yield return (stm == 0 ? "WTM " : "BTM ") + Describe(v) + "  K" + G.CoordOf(wk).ToCompact() + " " + Piece.ToChar(Piece.Make(Geo.WhitePiece, Color.White)) + G.CoordOf(wx).ToCompact() + " k" + G.CoordOf(bk).ToCompact();
            }
        }

        public string Summary()
        {
            var sb = new System.Text.StringBuilder();
            sb.Append("K+").Append(Piece.ToChar(Piece.Make(Geo.WhitePiece, Color.White))).Append(" vs K, ")
              .Append(G.Dimensions).Append(" dimensions, side ").Append(G.Side).Append(RuleText(G)).Append('\n');
            sb.Append("symmetry group order ").Append(Sym.TransformCount).Append(", king classes ").Append(Sym.DomainCells.Length)
              .Append(", canonical (king, piece) pairs ").Append(Sym.PairCount).Append(", table entries ").Append(Values.Length)
              .Append(" of which ").Append(DeadSlots * 2).Append(" are duplicate slots (unreduced black king under the pair stabiliser)\n");
            sb.Append("legal positions: white to move ").Append(LegalWtm).Append(", black to move ").Append(LegalBtm).Append('\n');
            sb.Append("checkmates ").Append(Mates).Append(", stalemates ").Append(Stalemates).Append('\n');
            sb.Append("white-to-move wins ").Append(Wins).Append(" (").Append(LegalWtm > 0 ? (100.0 * Wins / LegalWtm).ToString("F2") : "0").Append("%), black-to-move losses ").Append(Losses).Append('\n');
            sb.Append("longest forced mate: white to move ").Append(MaxWtmDistance).Append(" plies");
            if (MaxWtmDistance > 0) sb.Append(" (").Append((MaxWtmDistance + 1) / 2).Append(" moves)");
            sb.Append(", black to move ").Append(MaxBtmDistance).Append(" plies\n");
            sb.Append("passes ").Append(Iteration).Append('\n');
            var hist = new SortedDictionary<int, long>();
            for (long i = 0; i < Values.Length; i += 2) { ushort v = Values[i]; if (v < Stalemate) { hist.TryGetValue(v, out long c); hist[v] = c + 1; } }
            sb.Append("white-to-move distance histogram (plies: positions):");
            foreach (var kv in hist) sb.Append(' ').Append(kv.Key).Append(':').Append(kv.Value);
            sb.Append('\n');
            return sb.ToString();
        }

        internal static string RuleText(BoardGeometry g)
        {
            if (g.PairDiagonals) return ", rule variant: Hyperchess pair diagonals (diagonals only within the x-y and z-w pairs; King 16 moves, Queen 16 directions)";
            if (g.BoardKing) return ", rule variant: diagonals up to " + g.DiagonalAxes + " axes, board-king (2D king on the x-y board, straight steps across z and w)";
            return g.DiagonalAxes == 2 && g.KingAxes == 2 ? "" : ", rule variant: diagonals up to " + g.DiagonalAxes + " axes, king up to " + g.KingAxes + " axes";
        }
    }
}
