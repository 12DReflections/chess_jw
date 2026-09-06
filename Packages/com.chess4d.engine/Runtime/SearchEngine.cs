using System;
using System.Collections.Generic;
using System.Diagnostics;
using Chess4D.Core;

namespace Chess4D.Engine
{
    public sealed class SearchLimits
    {
        public int MaxDepth = 4;
        public int TimeMs = 1000;
        public long MaxNodes = long.MaxValue;
    }

    public struct SearchResult
    {
        public bool HasMove;
        public Move BestMove;
        public int Score;
        public int Depth;
        public long Nodes;
        public double Seconds;
        public string Line;
    }

    /// <summary>
    /// Single-threaded alpha-beta: iterative deepening, transposition table keyed by
    /// the Core Zobrist hash, MVV-LVA capture ordering then killers then history,
    /// quiescence on captures, material-plus-mobility evaluation. Make/unmake on
    /// the board it is given; it never copies the board per node. Not multithreaded,
    /// by rule (WebGL). Expect a branching factor in the hundreds and a practical
    /// depth of 2 to 4 with 288 pieces.
    /// </summary>
    public sealed class SearchEngine
    {
        private struct TtEntry
        {
            public ulong Key;
            public int Score;
            public Move Move;
            public short Depth;
            public byte Flag; // 0 empty, 1 exact, 2 lower, 3 upper
        }

        private const int MaxPly = 64;
        private const int TtBits = 18;
        private readonly TtEntry[] tt = new TtEntry[1 << TtBits];
        private readonly Move[,] killers = new Move[MaxPly, 2];
        private readonly int[,] history = new int[24, 4096 * 4];
        private readonly MoveList[] lists = new MoveList[MaxPly + 8];
        private readonly int[][] scores = new int[MaxPly + 8][];
        private readonly MoveList evalUs = new MoveList(1024), evalThem = new MoveList(1024);
        private readonly List<int> cellScratch = new List<int>(300);
        private Board board;
        private SearchLimits limits;
        private Stopwatch clock;
        private long nodes;
        private bool aborted;

        public SearchEngine()
        {
            for (int i = 0; i < lists.Length; i++) { lists[i] = new MoveList(1024); scores[i] = new int[1024]; }
        }

        public void ClearMemory()
        {
            Array.Clear(tt, 0, tt.Length);
            Array.Clear(killers, 0, killers.Length);
            Array.Clear(history, 0, history.Length);
        }

        /// <summary>Searches the position on <paramref name="position"/>; the board is restored before returning.</summary>
        public SearchResult Search(Board position, SearchLimits searchLimits)
        {
            board = position;
            limits = searchLimits;
            clock = Stopwatch.StartNew();
            nodes = 0;
            aborted = false;
            Array.Clear(killers, 0, killers.Length);
            var result = new SearchResult();

            MoveList root = lists[0];
            board.GenerateLegal(root);
            if (root.Count == 0) { result.Seconds = clock.Elapsed.TotalSeconds; return result; }
            result.HasMove = true;
            result.BestMove = root[0];

            for (int depth = 1; depth <= limits.MaxDepth; depth++)
            {
                int score = SearchRoot(depth, out Move best);
                if (aborted && depth > 1) break;
                result.BestMove = best;
                result.Score = score;
                result.Depth = depth;
                result.Line = Notation.Describe(board, best);
                if (Evaluation.IsMateScore(score)) break;
                if (clock.ElapsedMilliseconds * 2 > limits.TimeMs) break; // the next iteration would not finish
            }
            result.Nodes = nodes;
            result.Seconds = clock.Elapsed.TotalSeconds;
            return result;
        }

        private bool TimeUp()
        {
            if ((nodes & 1023) == 0 && (clock.ElapsedMilliseconds >= limits.TimeMs || nodes >= limits.MaxNodes)) aborted = true;
            return aborted;
        }

        private int SearchRoot(int depth, out Move best)
        {
            MoveList moves = lists[0];
            int[] sc = scores[0];
            Move ttMove = Probe(out _, out _, out _, 0) ? tt[Index(board.Hash)].Move : default;
            Order(moves, sc, ttMove, 0);
            best = moves[0];
            int alpha = -Evaluation.Infinity, beta = Evaluation.Infinity;
            Color us = board.SideToMove;
            for (int i = 0; i < moves.Count; i++)
            {
                PickBest(moves, sc, i);
                Move m = moves[i];
                board.Make(m);
                int score = -AlphaBeta(depth - 1, -beta, -alpha, 1);
                board.Unmake();
                if (aborted) return alpha;
                if (score > alpha)
                {
                    alpha = score;
                    best = m;
                    Store(board.Hash, depth, score, 1, m);
                }
            }
            return alpha;
        }

        private int AlphaBeta(int depth, int alpha, int beta, int ply)
        {
            nodes++;
            if (TimeUp()) return 0;
            if (ply >= MaxPly - 1) return Evaluate();
            if (depth <= 0) return Quiescence(alpha, beta, ply);

            int alphaOrig = alpha;
            if (Probe(out int ttScore, out int ttDepth, out int ttFlag, ply) && ttDepth >= depth)
            {
                if (ttFlag == 1) return ttScore;
                if (ttFlag == 2 && ttScore > alpha) alpha = ttScore;
                else if (ttFlag == 3 && ttScore < beta) beta = ttScore;
                if (alpha >= beta) return ttScore;
            }
            Move ttMove = tt[Index(board.Hash)].Key == board.Hash ? tt[Index(board.Hash)].Move : default;

            MoveList moves = lists[ply];
            int[] sc = scores[ply];
            board.GeneratePseudoLegal(moves);
            Order(moves, sc, ttMove, ply);

            Color us = board.SideToMove;
            Color them = Piece.Opposite(us);
            int legalCount = 0;
            int best = -Evaluation.Infinity;
            Move bestMove = default;
            for (int i = 0; i < moves.Count; i++)
            {
                PickBest(moves, sc, i);
                Move m = moves[i];
                board.Make(m);
                int k = board.KingCell(us);
                if (k >= 0 && board.IsAttacked(k, them)) { board.Unmake(); continue; }
                legalCount++;
                int score = -AlphaBeta(depth - 1, -beta, -alpha, ply + 1);
                board.Unmake();
                if (aborted) return 0;
                if (score > best)
                {
                    best = score;
                    bestMove = m;
                    if (score > alpha)
                    {
                        alpha = score;
                        if (alpha >= beta)
                        {
                            if (!m.IsCapture)
                            {
                                if (!killers[ply, 0].Equals(m)) { killers[ply, 1] = killers[ply, 0]; killers[ply, 0] = m; }
                                history[HistoryIndex(board.GetPiece(m.From)), m.To] += depth * depth;
                            }
                            break;
                        }
                    }
                }
            }
            if (legalCount == 0)
            {
                int k = board.KingCell(us);
                bool inCheck = k >= 0 && board.IsAttacked(k, them);
                return inCheck ? -Evaluation.MateScore + ply : 0;
            }
            byte flag = best <= alphaOrig ? (byte)3 : best >= beta ? (byte)2 : (byte)1;
            Store(board.Hash, depth, best, flag, bestMove);
            return best;
        }

        private int Quiescence(int alpha, int beta, int ply)
        {
            nodes++;
            if (TimeUp()) return 0;
            int stand = Evaluate();
            if (stand >= beta) return stand;
            if (stand > alpha) alpha = stand;
            if (ply >= MaxPly - 1) return stand;

            MoveList moves = lists[ply];
            int[] sc = scores[ply];
            board.GeneratePseudoLegal(moves);
            // captures only
            int n = 0;
            for (int i = 0; i < moves.Count; i++)
            {
                Move m = moves[i];
                if (!m.IsCapture && !m.IsPromotion) continue;
                if (n != i) { }
                sc[n] = CaptureScore(m);
                if (n != i) moves.Swap(i, n);
                n++;
            }
            moves.Count = n;
            Color us = board.SideToMove;
            Color them = Piece.Opposite(us);
            for (int i = 0; i < moves.Count; i++)
            {
                PickBest(moves, sc, i);
                Move m = moves[i];
                board.Make(m);
                int k = board.KingCell(us);
                if (k >= 0 && board.IsAttacked(k, them)) { board.Unmake(); continue; }
                int score = -Quiescence(-beta, -alpha, ply + 1);
                board.Unmake();
                if (aborted) return 0;
                if (score >= beta) return score;
                if (score > alpha) alpha = score;
            }
            return alpha;
        }

        private int Evaluate() { return Evaluation.Evaluate(board, evalUs, evalThem, cellScratch); }

        // ------------------------------------------------------------ ordering

        private int CaptureScore(in Move m)
        {
            int victim = m.IsEnPassant ? Evaluation.PieceValue[(int)PieceType.Pawn] : Evaluation.PieceValue[(int)Piece.TypeOf(board.GetPiece(m.To))];
            int attacker = Evaluation.PieceValue[(int)Piece.TypeOf(board.GetPiece(m.From))];
            int s = 1000000 + victim * 10 - attacker / 10;
            if (m.IsPromotion) s += Evaluation.PieceValue[(int)m.Promotion];
            return s;
        }

        private void Order(MoveList moves, int[] sc, in Move ttMove, int ply)
        {
            for (int i = 0; i < moves.Count; i++)
            {
                Move m = moves[i];
                int s;
                if (m.Equals(ttMove)) s = int.MaxValue;
                else if (m.IsCapture) s = CaptureScore(m);
                else if (m.IsPromotion) s = 900000 + Evaluation.PieceValue[(int)m.Promotion];
                else if (m.Equals(killers[ply, 0])) s = 800000;
                else if (m.Equals(killers[ply, 1])) s = 799000;
                else s = history[HistoryIndex(board.GetPiece(m.From)), m.To];
                sc[i] = s;
            }
        }

        private static void PickBest(MoveList moves, int[] sc, int i)
        {
            int best = i;
            for (int j = i + 1; j < moves.Count; j++) if (sc[j] > sc[best]) best = j;
            if (best != i)
            {
                moves.Swap(i, best);
                int t = sc[i]; sc[i] = sc[best]; sc[best] = t;
            }
        }

        private static int HistoryIndex(byte piece) { return (piece & 15); }

        // ------------------------------------------------------------ transposition table

        private static int Index(ulong key) { return (int)(key & ((1UL << TtBits) - 1)); }

        private void Store(ulong key, int depth, int score, byte flag, in Move move)
        {
            ref TtEntry e = ref tt[Index(key)];
            if (e.Key == key && e.Depth > depth && e.Flag != 0) return;
            e.Key = key; e.Depth = (short)depth; e.Score = score; e.Flag = flag; e.Move = move;
        }

        private bool Probe(out int score, out int depth, out int flag, int ply)
        {
            ref TtEntry e = ref tt[Index(board.Hash)];
            score = e.Score; depth = e.Depth; flag = e.Flag;
            return e.Flag != 0 && e.Key == board.Hash;
        }
    }
}
