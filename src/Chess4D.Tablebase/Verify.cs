using System;
using System.Collections.Generic;
using Chess4D.Core;
using Chess4D.Engine;

namespace Chess4D.Tablebase
{
    /// <summary>Spot checks: one-ply consistency of the table against itself, and agreement with the engine's own search on short mates and on draws.</summary>
    public static class Verify
    {
        public static Board MakeBoard(Generator gen, int wk, int wx, int bk, int stm)
        {
            var b = new Board(gen.G);
            b.Clear();
            b.PlacePiece(wk, Piece.Make(PieceType.King, Color.White, true));
            b.PlacePiece(wx, Piece.Make(gen.Geo.WhitePiece, Color.White, true));
            b.PlacePiece(bk, Piece.Make(PieceType.King, Color.Black, true));
            b.SetSideToMove(stm == 0 ? Color.White : Color.Black);
            return b;
        }

        /// <summary>For sampled legal positions: a WTM win in n has a move to a BTM loss in n-1 and none to a smaller one; a BTM loss in n has all moves to WTM wins at most n-1 with one at exactly n-1; a draw has no winning move (WTM) or an escaping move (BTM). Uses the full Core rules, not the fast geometry.</summary>
        public static List<string> ConsistencySample(Generator gen, int samples, int seed, Action<string> log = null)
        {
            var failures = new List<string>();
            var rng = new Random(seed);
            var legal = new MoveList(512);
            int checkedCount = 0;
            for (int s = 0; s < samples * 20 && checkedCount < samples; s++)
            {
                long idx = (long)(rng.NextDouble() * gen.Values.Length);
                ushort v = gen.Values[idx];
                if (v == Generator.Illegal) continue;
                gen.Sym.Decode(idx, out int wk, out int wx, out int bk, out int stm);
                Board b = MakeBoard(gen, wk, wx, bk, stm);
                b.GenerateLegal(legal);
                checkedCount++;
                if (v == Generator.Stalemate)
                {
                    if (legal.Count != 0 || b.InCheck()) failures.Add("stalemate mark but " + legal.Count + " moves at " + idx);
                    continue;
                }
                int best = int.MaxValue, worst = -1;
                bool escape = false;
                for (int i = 0; i < legal.Count; i++)
                {
                    Move m = legal[i];
                    b.Make(m);
                    ushort sv;
                    if (b.PieceCount(Color.White) < 2) { sv = Generator.Unknown; escape = true; }
                    else
                    {
                        List<int> cells = new List<int>();
                        b.GetPieceCells(Color.White, cells);
                        int nwk = b.KingCell(Color.White);
                        int nwx = cells[0] == nwk ? cells[1] : cells[0];
                        sv = gen.Probe(nwk, nwx, b.KingCell(Color.Black), 1 - stm);
                    }
                    b.Unmake();
                    if (sv == Generator.Illegal) { failures.Add("successor marked illegal at " + idx + " via " + m.ToString(b.G)); continue; }
                    bool win = sv < Generator.Stalemate;
                    if (stm == 0) { if (win && sv < best) best = sv; }
                    else { if (!win) escape = true; else if (sv > worst) worst = sv; }
                }
                if (stm == 0)
                {
                    if (v == Generator.Unknown) { if (best != int.MaxValue) failures.Add("WTM draw but winning move exists at " + idx); }
                    else if (legal.Count == 0) failures.Add("WTM win with no legal moves at " + idx);
                    else if (best != v - 1) failures.Add("WTM value " + v + " but best successor " + best + " at " + idx);
                }
                else
                {
                    if (v == Generator.Unknown) { if (!escape && legal.Count > 0) failures.Add("BTM draw but every move loses at " + idx); }
                    else if (v == 0) { if (legal.Count != 0 || !b.InCheck()) failures.Add("mate mark but not mated at " + idx); }
                    else if (escape) failures.Add("BTM loss " + v + " but an escape exists at " + idx);
                    else if (worst != v - 1) failures.Add("BTM value " + v + " but worst successor " + worst + " at " + idx);
                }
            }
            log?.Invoke("consistency: checked " + checkedCount + " sampled positions, " + failures.Count + " failures");
            return failures;
        }

        /// <summary>The engine must find the same mate distance on sampled short wins, and no mate on sampled draws within its depth.</summary>
        public static List<string> EngineSample(Generator gen, int wins, int draws, int maxDistance, int seed, Action<string> log = null)
        {
            var failures = new List<string>();
            var rng = new Random(seed);
            var engine = new SearchEngine();
            int checkedWins = 0, checkedDraws = 0;
            for (int s = 0; s < 200000 && (checkedWins < wins || checkedDraws < draws); s++)
            {
                long idx = (long)(rng.NextDouble() * gen.Values.Length) & ~1L; // white to move
                ushort v = gen.Values[idx];
                if (v == Generator.Illegal || v == Generator.Stalemate) continue;
                bool isWin = v < Generator.Stalemate;
                if (isWin && (v > maxDistance || checkedWins >= wins)) continue;
                if (!isWin && checkedDraws >= draws) continue;
                gen.Sym.Decode(idx, out int wk, out int wx, out int bk, out int stm);
                Board b = MakeBoard(gen, wk, wx, bk, stm);
                engine.ClearMemory();
                SearchResult r = engine.Search(b, new SearchLimits { MaxDepth = maxDistance + 1, TimeMs = 60000 });
                if (isWin)
                {
                    checkedWins++;
                    int expected = Evaluation.MateScore - v;
                    if (r.Score != expected) failures.Add("engine score " + r.Score + " but table says mate in " + v + " plies at " + idx + " (" + r.Line + ")");
                }
                else
                {
                    checkedDraws++;
                    if (Evaluation.IsMateScore(r.Score) && r.Score > 0) failures.Add("engine found a mate in a table draw at " + idx + " (" + r.Line + ", score " + r.Score + ")");
                }
            }
            log?.Invoke("engine spot check: " + checkedWins + " wins and " + checkedDraws + " draws sampled, " + failures.Count + " failures");
            return failures;
        }
    }
}
