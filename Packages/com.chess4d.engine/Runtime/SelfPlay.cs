using System;
using System.Collections.Generic;
using Chess4D.Core;

namespace Chess4D.Engine
{
    /// <summary>Engine-versus-engine games with an independent replay board that must agree with the played board after every move.</summary>
    public sealed class SelfPlayReport
    {
        public int Games;
        public int Checkmates, Stalemates, PlyCapReached;
        public long TotalPlies, TotalNodes;
        public double TotalSeconds;
        public int MaxDepthSeen;
        public readonly List<string> Failures = new List<string>();
        public bool Passed { get { return Failures.Count == 0; } }
        public override string ToString()
        {
            return "games " + Games + ", checkmates " + Checkmates + ", stalemates " + Stalemates + ", ply cap " + PlyCapReached
                + ", plies " + TotalPlies + ", nodes " + TotalNodes + ", seconds " + TotalSeconds.ToString("F1")
                + ", max depth " + MaxDepthSeen + ", failures " + Failures.Count;
        }
    }

    public static class SelfPlay
    {
        public static SelfPlayReport Run(int games, int timeMsPerMove, int maxDepth, int maxPlies, int seed, Action<string> log = null)
        {
            var report = new SelfPlayReport();
            var board = new Board(4, 8);
            var replay = new Board(4, 8);
            var engine = new SearchEngine();
            var limits = new SearchLimits { TimeMs = timeMsPerMove, MaxDepth = maxDepth };
            var legal = new MoveList(1024);
            var rng = new Random(seed);
            for (int gi = 0; gi < games; gi++)
            {
                StartPosition.Setup(board);
                replay.CopyFrom(board);
                engine.ClearMemory();
                var game = new Game(board);
                // A couple of random opening plies so games differ.
                int randomPlies = rng.Next(0, 4);
                for (int i = 0; i < randomPlies; i++)
                {
                    board.GenerateLegal(legal);
                    Move r = legal[rng.Next(legal.Count)];
                    game.TryMove(r);
                    replay.Make(r);
                }
                int ply = randomPlies;
                string end = "cap";
                while (ply < maxPlies)
                {
                    GameStatus status = game.Status;
                    if (status == GameStatus.Checkmate) { report.Checkmates++; end = "checkmate"; break; }
                    if (status == GameStatus.Stalemate) { report.Stalemates++; end = "stalemate"; break; }
                    SearchResult r = engine.Search(board, limits);
                    report.TotalNodes += r.Nodes;
                    report.TotalSeconds += r.Seconds;
                    if (r.Depth > report.MaxDepthSeen) report.MaxDepthSeen = r.Depth;
                    if (!r.HasMove) { report.Failures.Add("game " + gi + " ply " + ply + ": engine returned no move but status was " + status); break; }
                    board.GenerateLegal(legal);
                    if (!legal.Contains(r.BestMove)) { report.Failures.Add("game " + gi + " ply " + ply + ": illegal engine move " + r.BestMove.ToString(board.G)); break; }
                    ulong before = board.Hash;
                    if (!game.TryMove(r.BestMove)) { report.Failures.Add("game " + gi + " ply " + ply + ": game refused " + r.BestMove.ToString(board.G)); break; }
                    replay.Make(r.BestMove);
                    if (replay.Hash != board.Hash || board.Hash != board.ComputeHash() || PositionText.Save(replay) != PositionText.Save(board))
                    {
                        report.Failures.Add("game " + gi + " ply " + ply + ": state desync after " + r.BestMove.ToString(board.G) + " (hash before " + before + ")");
                        break;
                    }
                    ply++;
                }
                if (end == "cap") report.PlyCapReached++;
                report.Games++;
                report.TotalPlies += ply;
                log?.Invoke("game " + gi + ": " + end + " after " + ply + " plies, material W " + Evaluation.Material(board, Color.White) + " B " + Evaluation.Material(board, Color.Black));
            }
            return report;
        }
    }
}
