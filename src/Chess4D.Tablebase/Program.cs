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
            if (cmd == "safe") return Safe(args);
            if (cmd == "generate4") return Generate4(args);
            if (cmd == "sparse") return Sparse(args);
            PieceType piece = ParsePiece(args.Length > 1 ? args[1] : "Q");
            int dims = 4, side = 8, diag = 2, king = 0, threads = Environment.ProcessorCount, verifyWins = 40, verifyDraws = 40;
            bool boardKing = false;
            string outDir = Path.Combine("Builds", "tablebase");
            bool checkpoint = true;
            for (int i = 2; i < args.Length; i++)
            {
                switch (args[i])
                {
                    case "--dims": dims = int.Parse(args[++i]); break;
                    case "--side": side = int.Parse(args[++i]); break;
                    case "--diag": diag = int.Parse(args[++i]); break;
                    case "--king": king = int.Parse(args[++i]); break;
                    case "--boardking": boardKing = true; break;
                    case "--out": outDir = args[++i]; break;
                    case "--threads": threads = int.Parse(args[++i]); break;
                    case "--no-checkpoint": checkpoint = false; break;
                    case "--verify": verifyWins = int.Parse(args[++i]); verifyDraws = int.Parse(args[++i]); break;
                }
            }
            Directory.CreateDirectory(outDir);
            var geometry = new BoardGeometry(dims, side, diag, king, boardKing);
            string name = "K" + Piece.ToChar(Piece.Make(piece, Color.White)) + "vK-" + dims + "d" + side + Variant(geometry);
            if (cmd == "list" || cmd == "verify")
            {
                var lg = geometry;
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
            var g = geometry;
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

        /// <summary>generate4 QR [--dims 4] [--side 4] [--out dir] [--threads n]: K+A+B vs K, held in memory, summary written to the output directory.</summary>
        private static int Generate4(string[] args)
        {
            if (args.Length < 2 || args[1].Length != 2) { Console.WriteLine("usage: generate4 <two letters from QRBN> [--dims n] [--side s] [--out dir] [--threads n]"); return 1; }
            int dims = 4, side = 4, diag = 2, king = 0, threads = Environment.ProcessorCount;
            bool boardKing = false;
            string outDir = Path.Combine("Builds", "tablebase");
            for (int i = 2; i < args.Length; i++)
            {
                switch (args[i])
                {
                    case "--dims": dims = int.Parse(args[++i]); break;
                    case "--side": side = int.Parse(args[++i]); break;
                    case "--diag": diag = int.Parse(args[++i]); break;
                    case "--king": king = int.Parse(args[++i]); break;
                    case "--boardking": boardKing = true; break;
                    case "--out": outDir = args[++i]; break;
                    case "--threads": threads = int.Parse(args[++i]); break;
                }
            }
            Directory.CreateDirectory(outDir);
            var total = Stopwatch.StartNew();
            PieceType[] m = SafeRegion.ParseMaterial(args[1]);
            Action<string> log = s => Console.WriteLine(DateTime.Now.ToString("HH:mm:ss") + "  " + s);
            var gen = new FourPieceGenerator(new BoardGeometry(dims, side, diag, king, boardKing), m[0], m[1], log) { Threads = threads };
            log("table " + gen.Name + ": " + gen.EntryCount + " entries");
            gen.Initialise();
            gen.Solve();
            string summary = gen.Summary();
            Console.Write(summary);
            var failures = gen.ConsistencySample(5000, 1);
            foreach (string f in failures) log("FAIL " + f);
            summary += "one-ply consistency against the full rules: 5000 sampled positions, " + failures.Count + " failures\n";
            File.WriteAllText(Path.Combine(outDir, gen.Name + ".txt"), summary);
            log("total time " + total.Elapsed.TotalMinutes.ToString("F1") + " min, verification failures " + failures.Count);
            return failures.Count == 0 ? 0 : 2;
        }

        /// <summary>sparse QR [--dims 4] [--side 8] [--out dir]: exact solve that stores only decided positions; for boards where almost nothing is won.</summary>
        private static int Sparse(string[] args)
        {
            if (args.Length < 2) { Console.WriteLine("usage: sparse <material, letters from QRBN> [--dims n] [--side s] [--out dir]"); return 1; }
            int dims = 4, side = 8, diag = 2, king = 0;
            bool boardKing = false;
            long cap = 20_000_000;
            string outDir = null;
            for (int i = 2; i < args.Length; i++)
            {
                switch (args[i])
                {
                    case "--dims": dims = int.Parse(args[++i]); break;
                    case "--side": side = int.Parse(args[++i]); break;
                    case "--diag": diag = int.Parse(args[++i]); break;
                    case "--king": king = int.Parse(args[++i]); break;
                    case "--boardking": boardKing = true; break;
                    case "--cap": cap = long.Parse(args[++i]); break;
                    case "--out": outDir = args[++i]; break;
                }
            }
            Action<string> log = s => Console.WriteLine(DateTime.Now.ToString("HH:mm:ss") + "  " + s);
            SparseSolver.WonLimitDefault = cap;
            var solver = SparseSolver.For(new BoardGeometry(dims, side, diag, king, boardKing), SafeRegion.ParseMaterial(args[1]), log);
            var sb = new System.Text.StringBuilder(solver.Summary());
            foreach (string line in solver.Deepest(12)) sb.Append(line).Append('\n');
            var failures = solver.VerifyOnePly();
            foreach (string f in failures) sb.Append("FAIL ").Append(f).Append('\n');
            sb.Append("one-ply verification of every stored position against the full rules: ").Append(failures.Count).Append(" failures\n");
            Console.Write(sb.ToString());
            if (outDir != null)
            {
                Directory.CreateDirectory(outDir);
                File.WriteAllText(Path.Combine(outDir, "sparse-" + solver.Name + ".txt"), sb.ToString());
            }
            return failures.Count == 0 ? 0 : 2;
        }

        /// <summary>safe QR [--dims 4] [--side 8] [--out dir]: the table-free one-ply drawing certificate for K + material vs K.</summary>
        private static int Safe(string[] args)
        {
            if (args.Length < 2) { Console.WriteLine("usage: safe <material, letters from QRBN> [--dims n] [--side s] [--out dir]"); return 1; }
            int dims = 4, side = 8, diag = 2, king = 0;
            bool boardKing = false;
            string outDir = null;
            for (int i = 2; i < args.Length; i++)
            {
                switch (args[i])
                {
                    case "--dims": dims = int.Parse(args[++i]); break;
                    case "--side": side = int.Parse(args[++i]); break;
                    case "--diag": diag = int.Parse(args[++i]); break;
                    case "--king": king = int.Parse(args[++i]); break;
                    case "--boardking": boardKing = true; break;
                    case "--out": outDir = args[++i]; break;
                }
            }
            var sw = Stopwatch.StartNew();
            var sg = new BoardGeometry(dims, side, diag, king, boardKing);
            var result = SafeRegion.Compute(sg, SafeRegion.ParseMaterial(args[1]), Console.WriteLine);
            Console.Write(result.Report);
            Console.WriteLine("time " + sw.Elapsed.TotalSeconds.ToString("F1") + " s");
            if (outDir != null)
            {
                Directory.CreateDirectory(outDir);
                File.WriteAllText(Path.Combine(outDir, "safe-K" + args[1].ToUpperInvariant() + "vK-" + dims + "d" + side + Variant(sg) + ".txt"), result.Report);
            }
            return 0;
        }

        /// <summary>File-name suffix for a rule variant; empty for the settled rules.</summary>
        public static string Variant(BoardGeometry g)
        {
            if (g.BoardKing) return "-diag" + g.DiagonalAxes + "boardking";
            if (g.DiagonalAxes == 2 && g.KingAxes == 2) return "";
            return "-diag" + g.DiagonalAxes + "king" + g.KingAxes;
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
