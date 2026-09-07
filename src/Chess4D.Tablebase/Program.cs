using System;
using System.Diagnostics;
using System.IO;
using Chess4D.Core;

namespace Chess4D.Tablebase
{
    /// <summary>
    /// Retrograde tablebase generator. Console only, never shipped in the game.
    ///   generate Q|R|B|N [--dims 4] [--side 8] [--out dir] [--threads n] [--no-checkpoint]
    ///   probe   Q|R|B|N --table file  wk wx bk w|b     (cells as digit tuples, e.g. 4033)
    /// </summary>
    public static class Program
    {
        public static int Main(string[] args)
        {
            if (args.Length == 0) { Console.WriteLine("usage: generate Q|R|B|N [--dims n] [--side s] [--out dir] [--threads n] [--no-checkpoint] [--verify wins draws]"); return 1; }
            string cmd = args[0];
            PieceType piece = ParsePiece(args.Length > 1 ? args[1] : "Q");
            int dims = 4, side = 8, threads = Environment.ProcessorCount, verifyWins = 40, verifyDraws = 40;
            string outDir = Path.Combine("Builds", "tablebase");
            bool checkpoint = true;
            for (int i = 2; i < args.Length; i++)
            {
                switch (args[i])
                {
                    case "--dims": dims = int.Parse(args[++i]); break;
                    case "--side": side = int.Parse(args[++i]); break;
                    case "--out": outDir = args[++i]; break;
                    case "--threads": threads = int.Parse(args[++i]); break;
                    case "--no-checkpoint": checkpoint = false; break;
                    case "--verify": verifyWins = int.Parse(args[++i]); verifyDraws = int.Parse(args[++i]); break;
                }
            }
            Directory.CreateDirectory(outDir);
            string name = "K" + Piece.ToChar(Piece.Make(piece, Color.White)) + "vK-" + dims + "d" + side;
            if (cmd == "list" || cmd == "verify")
            {
                var lg = new BoardGeometry(dims, side);
                var loaded = new Generator(lg, piece) { Log = Console.WriteLine, Threads = threads };
                loaded.LoadTable(Path.Combine(outDir, name + ".tb"));
                Console.Write(loaded.Summary());
                if (cmd == "list") { foreach (string line in loaded.ListDecided(200)) Console.WriteLine(line); return 0; }
                var vf = Verify.ConsistencySample(loaded, 5000, 11, Console.WriteLine);
                int depth = Math.Min(loaded.MaxWtmDistance + 1, 5);
                vf.AddRange(Verify.EngineSample(loaded, verifyWins, verifyDraws, depth, 12, Console.WriteLine));
                foreach (string f in vf) Console.WriteLine("FAIL " + f);
                return vf.Count == 0 ? 0 : 2;
            }
            if (cmd != "generate") { Console.WriteLine("unknown command " + cmd); return 1; }
            string logPath = Path.Combine(outDir, name + ".log");
            Action<string> log = s =>
            {
                string line = DateTime.Now.ToString("HH:mm:ss") + "  " + s;
                Console.WriteLine(line);
                File.AppendAllText(logPath, line + "\n");
            };

            var total = Stopwatch.StartNew();
            var g = new BoardGeometry(dims, side);
            var gen = new Generator(g, piece) { Log = log, Threads = threads };
            if (checkpoint) gen.CheckpointPath = Path.Combine(outDir, name + ".ckpt");
            log("table " + name + ": " + gen.Sym.EntryCount + " entries (" + (gen.Sym.EntryCount * 2 / 1048576) + " MB), " + threads + " threads");
            if (!gen.TryLoadCheckpoint()) gen.Initialise();
            gen.Solve();
            string tablePath = Path.Combine(outDir, name + ".tb");
            gen.WriteTable(tablePath);
            string summary = gen.Summary();
            File.WriteAllText(Path.Combine(outDir, name + ".txt"), summary);
            log("table written to " + tablePath + "\n" + summary);

            var failures = Verify.ConsistencySample(gen, 5000, 1, log);
            // The engine is a spot check on short mates only; it cannot prove a draw at 288-move branching, so keep its depth small.
            failures.AddRange(Verify.EngineSample(gen, verifyWins, verifyDraws, Math.Min(gen.MaxWtmDistance + 1, 5), 2, log));
            foreach (string f in failures) log("FAIL " + f);
            log("total time " + total.Elapsed.TotalMinutes.ToString("F1") + " min, verification failures " + failures.Count);
            if (checkpoint && File.Exists(gen.CheckpointPath)) File.Delete(gen.CheckpointPath);
            return failures.Count == 0 ? 0 : 2;
        }

        private static PieceType ParsePiece(string s)
        {
            switch (s.ToUpperInvariant())
            {
                case "Q": return PieceType.Queen;
                case "R": return PieceType.Rook;
                case "B": return PieceType.Bishop;
                case "N": return PieceType.Knight;
                default: throw new ArgumentException("piece must be Q, R, B or N");
            }
        }
    }
}
