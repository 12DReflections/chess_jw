using System;
using System.Collections.Generic;
using Chess4D.Core;
using Chess4D.Engine;
using NUnit.Framework;

namespace Chess4D.Core.Tests
{
    public class EngineTests
    {
        [Test]
        public void AttackMapAgreesWithTheBoardRayWalkOnRandomPositions()
        {
            var b = new Board(4, 8);
            StartPosition.Setup(b);
            var map = new AttackMap(b);
            var list = new MoveList(1024);
            var rng = new Random(11);
            int checkedCells = 0;
            for (int ply = 0; ply < 60; ply++)
            {
                map.Compute();
                for (int i = 0; i < 400; i++)
                {
                    int cell = rng.Next(b.G.CellCount);
                    Assert.That(map.IsAttacked(cell, Color.White), Is.EqualTo(b.IsAttacked(cell, Color.White)), "white attacks " + b.G.CoordOf(cell) + " at ply " + ply);
                    Assert.That(map.IsAttacked(cell, Color.Black), Is.EqualTo(b.IsAttacked(cell, Color.Black)), "black attacks " + b.G.CoordOf(cell) + " at ply " + ply);
                    var attackers = new List<int>();
                    b.Attackers(cell, Color.White, attackers);
                    Assert.That(map.Attackers(cell, Color.White), Is.EqualTo(attackers.Count));
                    checkedCells++;
                }
                Assert.That(map.InCheck(b.SideToMove), Is.EqualTo(b.InCheck()));
                b.GenerateLegal(list);
                if (list.Count == 0) break;
                b.Make(list[rng.Next(list.Count)]);
            }
            Assert.That(checkedCells, Is.GreaterThan(10000));
        }

        [Test]
        public void AttackMapSeesThreatsAlongHiddenAxes()
        {
            var b = new Board(4, 8);
            b.Clear();
            var g = b.G;
            b.PlacePiece(g.CellOf(4, 0, 3, 3), Piece.Make(PieceType.King, Color.White, true));
            b.PlacePiece(g.CellOf(4, 7, 3, 3), Piece.Make(PieceType.King, Color.Black, true));
            b.PlacePiece(g.CellOf(2, 2, 3, 0), Piece.Make(PieceType.Rook, Color.Black, true)); // same x,y,z, different w
            b.PlacePiece(g.CellOf(2, 2, 3, 5), Piece.Make(PieceType.Knight, Color.White, true));
            b.SetSideToMove(Color.White);
            var map = new AttackMap(b);
            map.Compute();
            Assert.That(map.IsPieceAttacked(g.CellOf(2, 2, 3, 5)), Is.True, "rook attacks along w");
            Assert.That(map.IsPieceDefended(g.CellOf(2, 2, 3, 5)), Is.False);
            Assert.That(map.Attackers(g.CellOf(2, 2, 3, 3), Color.Black), Is.EqualTo(1));
        }

        [Test]
        public void SearchFindsMateInOne()
        {
            var b = new Board(4, 8);
            b.Clear();
            var g = b.G;
            b.PlacePiece(g.CellOf(0, 0, 0, 0), Piece.Make(PieceType.King, Color.Black, true));
            for (int x = 0; x < 2; x++) for (int y = 0; y < 2; y++) for (int z = 0; z < 2; z++) for (int w = 0; w < 2; w++)
                if (x + y + z + w > 0) b.PlacePiece(g.CellOf(x, y, z, w), Piece.Make(PieceType.Pawn, Color.Black, true));
            b.PlacePiece(g.CellOf(7, 7, 7, 7), Piece.Make(PieceType.King, Color.White, true));
            b.PlacePiece(g.CellOf(4, 2, 0, 0), Piece.Make(PieceType.Knight, Color.White, true));
            b.SetSideToMove(Color.White);
            var engine = new SearchEngine();
            SearchResult r = engine.Search(b, new SearchLimits { MaxDepth = 3, TimeMs = 5000 });
            Assert.That(r.HasMove, Is.True);
            Assert.That(r.BestMove.To, Is.EqualTo(g.CellOf(2, 1, 0, 0)), "mate in one: " + r.Line);
            Assert.That(Evaluation.IsMateScore(r.Score), Is.True, "score " + r.Score);
            Assert.That(b.Hash, Is.EqualTo(b.ComputeHash()), "board restored");
            Assert.That(b.Ply, Is.EqualTo(0));
        }

        [Test]
        public void SearchPrefersWinningAQueen()
        {
            var b = new Board(4, 8);
            b.Clear();
            var g = b.G;
            b.PlacePiece(g.CellOf(4, 0, 3, 3), Piece.Make(PieceType.King, Color.White, true));
            b.PlacePiece(g.CellOf(4, 7, 3, 3), Piece.Make(PieceType.King, Color.Black, true));
            b.PlacePiece(g.CellOf(0, 3, 3, 3), Piece.Make(PieceType.Rook, Color.White, true));
            b.PlacePiece(g.CellOf(6, 3, 3, 3), Piece.Make(PieceType.Queen, Color.Black, true)); // undefended, on the rook's line
            b.SetSideToMove(Color.White);
            var engine = new SearchEngine();
            SearchResult r = engine.Search(b, new SearchLimits { MaxDepth = 2, TimeMs = 5000 });
            Assert.That(r.BestMove.To, Is.EqualTo(g.CellOf(6, 3, 3, 3)), r.Line);
            Assert.That(r.BestMove.IsCapture, Is.True);
        }

        [Test]
        public void SearchFromTheStartingPositionReturnsALegalMoveWithinTime()
        {
            var b = new Board(4, 8);
            StartPosition.Setup(b);
            var engine = new SearchEngine();
            var sw = System.Diagnostics.Stopwatch.StartNew();
            SearchResult r = engine.Search(b, new SearchLimits { MaxDepth = 6, TimeMs = 800 });
            sw.Stop();
            TestContext.Out.WriteLine("start position: depth " + r.Depth + ", " + r.Nodes + " nodes, " + r.Seconds.ToString("F2") + " s, " + r.Line + ", score " + r.Score);
            var legal = new MoveList(1024);
            b.GenerateLegal(legal);
            Assert.That(legal.Contains(r.BestMove), Is.True);
            Assert.That(sw.ElapsedMilliseconds, Is.LessThan(2500), "time limit respected");
            Assert.That(r.Depth, Is.GreaterThanOrEqualTo(2));
        }

        /// <summary>Stage 5 gate: 10,000 random legal moves across random positions, every make/unmake must restore the position and hash exactly.</summary>
        [Test]
        public void FuzzMakeUnmakeOverTenThousandRandomMoves()
        {
            var b = new Board(4, 8);
            StartPosition.Setup(b);
            var list = new MoveList(1024);
            var rng = new Random(2026);
            int checkedMoves = 0, positions = 0;
            while (checkedMoves < 10000)
            {
                b.GenerateLegal(list);
                if (list.Count == 0 || b.Ply > 120)
                {
                    StartPosition.Setup(b);
                    for (int i = 0; i < rng.Next(0, 40); i++)
                    {
                        b.GenerateLegal(list);
                        if (list.Count == 0) break;
                        b.Make(list[rng.Next(list.Count)]);
                    }
                    b.GenerateLegal(list);
                    if (list.Count == 0) continue;
                }
                positions++;
                string before = PositionText.Save(b);
                ulong hash = b.Hash;
                int ply = b.Ply;
                int toCheck = Math.Min(list.Count, 25);
                for (int i = 0; i < toCheck; i++)
                {
                    Move m = list[rng.Next(list.Count)];
                    b.Make(m);
                    Assert.That(b.Hash, Is.EqualTo(b.ComputeHash()), "incremental hash after " + m.ToString(b.G));
                    b.Unmake();
                    Assert.That(b.Hash, Is.EqualTo(hash), "hash restored after " + m.ToString(b.G));
                    Assert.That(b.Ply, Is.EqualTo(ply));
                    Assert.That(PositionText.Save(b), Is.EqualTo(before), "position restored after " + m.ToString(b.G));
                    checkedMoves++;
                }
                b.Make(list[rng.Next(list.Count)]);
            }
            TestContext.Out.WriteLine("fuzz: " + checkedMoves + " make/unmake round trips over " + positions + " positions");
        }

        [Test]
        public void ShortSelfPlaySmoke()
        {
            SelfPlayReport report = SelfPlay.Run(games: 2, timeMsPerMove: 60, maxDepth: 3, maxPlies: 12, seed: 5, log: s => TestContext.Out.WriteLine(s));
            TestContext.Out.WriteLine(report.ToString());
            Assert.That(report.Failures, Is.Empty);
            Assert.That(report.Games, Is.EqualTo(2));
        }

        [Test, Explicit("Stage 5 gate: long self-play run, results recorded in PROGRESS.md")]
        public void SelfPlayGate()
        {
            SelfPlayReport report = SelfPlay.Run(games: 20, timeMsPerMove: 150, maxDepth: 4, maxPlies: 200, seed: 1, log: s => TestContext.Out.WriteLine(s));
            TestContext.Out.WriteLine(report.ToString());
            foreach (string f in report.Failures) TestContext.Out.WriteLine("FAIL " + f);
            Assert.That(report.Failures, Is.Empty);
        }
    }
}
