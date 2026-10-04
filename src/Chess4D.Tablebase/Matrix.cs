using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;
using Chess4D.Core;

namespace Chess4D.Tablebase
{
    /// <summary>
    /// The ruleset-by-material result table as a reproducible dataset: reads the ruleset registry
    /// (docs/rulesets.json), solves each (ruleset, material) cell with whichever solver fits, and
    /// appends one CSV row per cell with the counts, the longest mate, the verification outcome and
    /// the command that regenerates it. Cells already present in the CSV are skipped, so the run
    /// can be resumed.
    /// </summary>
    public static class Matrix
    {
        public sealed class Ruleset
        {
            public string name { get; set; }
            public string tuple { get; set; }
            public int dims { get; set; }
            public int side { get; set; }
            public int diag { get; set; } = 2;
            public int king { get; set; }
            public bool boardKing { get; set; }
            public bool pairDiagonals { get; set; }
            public string source { get; set; }
            public bool skipPairs { get; set; }
            public string note { get; set; }
            public BoardGeometry Geometry() { return new BoardGeometry(dims, side, diag, king, boardKing, pairDiagonals); }
            public string Flags()
            {
                var sb = new StringBuilder("--dims " + dims + " --side " + side + " --diag " + diag);
                if (king != 0) sb.Append(" --king ").Append(king);
                if (boardKing) sb.Append(" --boardking");
                if (pairDiagonals) sb.Append(" --pairdiag");
                return sb.ToString();
            }
        }

        public const string Header = "ruleset,tuple,material,dims,side,board,solver,legal_wtm,won_wtm,won_pct,checkmates,longest_wtm_plies,status,verification,command";

        public static int Run(string registryPath, string csvPath, string[] materials, long sparseCap, Action<string> log)
        {
            var doc = JsonSerializer.Deserialize<JsonElement>(File.ReadAllText(registryPath));
            var rulesets = JsonSerializer.Deserialize<List<Ruleset>>(doc.GetProperty("rulesets").GetRawText());
            var done = new HashSet<string>();
            if (File.Exists(csvPath))
                foreach (string line in File.ReadAllLines(csvPath))
                {
                    List<string> f = ParseCsvLine(line);
                    if (f.Count > 2 && f[0] != "ruleset") done.Add(f[0] + "|" + f[2]);
                }
            else File.WriteAllText(csvPath, Header + "\n");
            int failures = 0;
            foreach (var rs in rulesets)
                foreach (string mat in materials)
                {
                    if (done.Contains(rs.name + "|" + mat)) continue;
                    if (rs.skipPairs && mat.Length >= 2) { string skip = Csv(rs, mat, "none", "", "", "", "", "", "not computed on this board (see the registry note)", "", ""); File.AppendAllText(csvPath, skip + "\n"); continue; }
                    string row;
                    try { row = Cell(rs, mat, sparseCap, log); }
                    catch (Exception e) { row = Csv(rs, mat, "none", "", "", "", "", "", "error: " + e.GetType().Name, "", ""); failures++; }
                    File.AppendAllText(csvPath, row + "\n");
                    log(row);
                }
            return failures;
        }

        private static string Cell(Ruleset rs, string mat, long sparseCap, Action<string> log)
        {
            var g = rs.Geometry();
            PieceType[] pieces = SafeRegion.ParseMaterial(mat);
            string board = string.Join("x", System.Linq.Enumerable.Repeat(rs.side.ToString(), rs.dims));
            if (pieces.Length == 1)
            {
                var sym = new Symmetry(g);
                if (sym.EntryCount < int.MaxValue)
                {
                    var gen = new Generator(g, pieces[0]) { Log = s => { } };
                    gen.Initialise(); gen.Solve();
                    var f = Verify.ConsistencySample(gen, 5000, 1);
                    string status = gen.Wins == 0 ? "no checkmate exists" : gen.Wins == gen.LegalWtm ? "forced win" : "not forced";
                    return Csv(rs, mat, "dense3", gen.LegalWtm.ToString(), gen.Wins.ToString(), Pct(gen.Wins, gen.LegalWtm), gen.Mates.ToString(), gen.MaxWtmDistance.ToString(), status, "consistency 5000 samples, " + f.Count + " failures", "generate " + mat + " " + rs.Flags());
                }
            }
            else if (pieces.Length == 2)
            {
                var sym = new Symmetry(g);
                long entries = sym.PairCount * (long)g.CellCount * g.CellCount * 2;
                if (entries <= 7_000_000_000L)
                {
                    var gen = new FourPieceGenerator(g, pieces[0], pieces[1], s => { });
                    gen.Initialise(); gen.Solve();
                    var f = gen.ConsistencySample(5000, 1);
                    string status = gen.Wins == 0 ? "no checkmate exists" : gen.Wins == gen.LegalWtm ? "forced win" : "not forced";
                    return Csv(rs, mat, "dense4", gen.LegalWtm.ToString(), gen.Wins.ToString(), Pct(gen.Wins, gen.LegalWtm), gen.Mates.ToString(), gen.MaxWtmDistance.ToString(), status, "consistency 5000 samples, " + f.Count + " failures", "generate4 " + mat + " " + rs.Flags());
                }
            }
            // Implied cells: if K+Q alone is a forced win under this ruleset, every pair containing a Queen is a win too; say so rather than spend hours hitting the cap.
            if (pieces.Length == 2 && (pieces[0] == PieceType.Queen || pieces[1] == PieceType.Queen))
            {
                var sym1 = new Symmetry(g);
                if (sym1.EntryCount < int.MaxValue)
                {
                    var kq = new Generator(g, PieceType.Queen) { Log = s => { } };
                    kq.Initialise(); kq.Solve();
                    if (kq.Wins == kq.LegalWtm && kq.Wins > 0)
                        return Csv(rs, mat, "implied", "", "", "", "", "", "forced win (implied: K+Q alone is a forced win under this ruleset; not separately tabulated)", "", "generate Q " + rs.Flags());
                }
            }
            // Sparse: exact when little is won; reports a cap otherwise. On large boards a pair that is winning would take hours to reach the cap, so cap early.
            SparseSolver.WonLimitDefault = pieces.Length >= 2 && g.CellCount >= 4096 ? Math.Min(sparseCap, 300_000) : sparseCap;
            try
            {
                var sp = SparseSolver.For(g, pieces, s => { });
                var f = sp.VerifyOnePly();
                string status = sp.Won.Count == 0 ? "no checkmate exists" : "not forced (sparse exact)";
                return Csv(rs, mat, "sparse", "", sp.Won.Count + " classes", "", sp.Mates.ToString(), sp.MaxWtmDistance.ToString(), status, "one-ply check of every stored position, " + f.Count + " failures", "sparse " + mat + " " + rs.Flags());
            }
            catch (InvalidOperationException)
            {
                return Csv(rs, mat, "sparse", "", "> " + SparseSolver.WonLimitDefault + " classes", "", "", "", "won set exceeds the sparse cap (likely win; dense table needed)", "", "sparse " + mat + " " + rs.Flags() + " --cap " + SparseSolver.WonLimitDefault);
            }
        }

        private static List<string> ParseCsvLine(string line)
        {
            var fields = new List<string>();
            var sb = new StringBuilder();
            bool quoted = false;
            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];
                if (quoted)
                {
                    if (c == '"' && i + 1 < line.Length && line[i + 1] == '"') { sb.Append('"'); i++; }
                    else if (c == '"') quoted = false;
                    else sb.Append(c);
                }
                else if (c == '"') quoted = true;
                else if (c == ',') { fields.Add(sb.ToString()); sb.Clear(); }
                else sb.Append(c);
            }
            fields.Add(sb.ToString());
            return fields;
        }

        private static string Pct(long a, long b) { return b > 0 ? (100.0 * a / b).ToString("F3") : ""; }

        private static string Csv(Ruleset rs, string mat, string solver, string legal, string won, string pct, string mates, string longest, string status, string verification, string command)
        {
            string board = string.Join("x", System.Linq.Enumerable.Repeat(rs.side.ToString(), rs.dims));
            string[] f = { rs.name, rs.tuple, mat, rs.dims.ToString(), rs.side.ToString(), board, solver, legal, won, pct, mates, longest, status, verification, command };
            for (int i = 0; i < f.Length; i++) if (f[i].Contains(',') || f[i].Contains('"')) f[i] = "\"" + f[i].Replace("\"", "\"\"") + "\"";
            return string.Join(",", f);
        }
    }
}
