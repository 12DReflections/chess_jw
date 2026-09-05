using Chess4D.Core;
using NUnit.Framework;

namespace Chess4D.Core.Tests
{
    public class RulesTests
    {
        private static Board Load(string fen)
        {
            var board = new Board(2, 8);
            Fen.Load(board, fen);
            return board;
        }

        private static MoveList Legal(Board b)
        {
            var list = new MoveList();
            b.GenerateLegal(list);
            return list;
        }

        private static int CountWhere(MoveList list, System.Func<Move, bool> pred)
        {
            int n = 0;
            for (int i = 0; i < list.Count; i++) if (pred(list[i])) n++;
            return n;
        }

        [Test]
        public void CastlingAppearsInTheMoveList()
        {
            var b = Load("r3k2r/8/8/8/8/8/8/R3K2R w KQkq - 0 1");
            var list = Legal(b);
            Assert.That(CountWhere(list, m => m.IsCastle), Is.EqualTo(2));
            var g = b.G;
            Assert.That(list.Contains(new Move(g.CellOf(4, 0), g.CellOf(6, 0), PieceType.None, MoveFlags.Castle, g.CellOf(7, 0), g.CellOf(5, 0))), Is.True, "kingside");
            Assert.That(list.Contains(new Move(g.CellOf(4, 0), g.CellOf(2, 0), PieceType.None, MoveFlags.Castle, g.CellOf(0, 0), g.CellOf(3, 0))), Is.True, "queenside");
        }

        [Test]
        public void CastlingIsRefusedThroughOrOutOfCheck()
        {
            Assert.That(CountWhere(Legal(Load("4k3/8/8/8/8/8/5r2/R3K2R w KQ - 0 1")), m => m.IsCastle), Is.EqualTo(1), "f1 attacked: only queenside");
            Assert.That(CountWhere(Legal(Load("4k3/8/8/8/8/8/4r3/R3K2R w KQ - 0 1")), m => m.IsCastle), Is.EqualTo(0), "in check: none");
            Assert.That(CountWhere(Legal(Load("4k3/8/8/8/8/8/8/RN2K2R w KQ - 0 1")), m => m.IsCastle), Is.EqualTo(1), "b1 occupied: only kingside");
        }

        [Test]
        public void CastlingMovesTheRookAndUnmakeRestoresIt()
        {
            var b = Load("r3k2r/8/8/8/8/8/8/R3K2R w KQkq - 0 1");
            var g = b.G;
            var m = new Move(g.CellOf(4, 0), g.CellOf(6, 0), PieceType.None, MoveFlags.Castle, g.CellOf(7, 0), g.CellOf(5, 0));
            ulong before = b.Hash;
            b.Make(m);
            Assert.That(Piece.Is(b.GetPiece(g.CellOf(6, 0)), PieceType.King, Color.White));
            Assert.That(Piece.Is(b.GetPiece(g.CellOf(5, 0)), PieceType.Rook, Color.White));
            Assert.That(b.GetPiece(g.CellOf(7, 0)), Is.EqualTo(0));
            Assert.That(b.CastlingRights() & 3, Is.EqualTo(0), "white rights gone");
            b.Unmake();
            Assert.That(b.Hash, Is.EqualTo(before));
            Assert.That(Fen.Save(b), Is.EqualTo("r3k2r/8/8/8/8/8/8/R3K2R w KQkq - 0 1"));
        }

        [Test]
        public void EnPassantAppearsAfterADoubleStepAndOnlyThen()
        {
            var b = Load("4k3/8/8/8/3p4/8/4P3/4K3 w - - 0 1");
            var g = b.G;
            b.Make(new Move(g.CellOf(4, 1), g.CellOf(4, 3), PieceType.None, MoveFlags.DoubleStep, g.CellOf(4, 2)));
            Assert.That(b.EnPassantCell, Is.EqualTo(g.CellOf(4, 2)));
            var list = Legal(b);
            Assert.That(CountWhere(list, m => m.IsEnPassant), Is.EqualTo(1));
            var ep = new Move(g.CellOf(3, 3), g.CellOf(4, 2), PieceType.None, MoveFlags.Capture | MoveFlags.EnPassant, g.CellOf(4, 3));
            Assert.That(list.Contains(ep), Is.True);
            b.Make(ep);
            Assert.That(b.GetPiece(g.CellOf(4, 3)), Is.EqualTo(0), "captured pawn removed");
            Assert.That(b.PieceCount(Color.White), Is.EqualTo(1));
            b.Unmake();
            Assert.That(b.PieceCount(Color.White), Is.EqualTo(2));
            // A quiet move by Black instead: the right lapses.
            b.Make(new Move(g.CellOf(4, 7), g.CellOf(3, 7)));
            b.Make(new Move(g.CellOf(4, 0), g.CellOf(3, 0)));
            Assert.That(CountWhere(Legal(b), m => m.IsEnPassant), Is.EqualTo(0));
        }

        [Test]
        public void PromotionOffersFourPiecesAndCapturePromotions()
        {
            var b = Load("1n2k3/P7/8/8/8/8/8/4K3 w - - 0 1");
            var list = Legal(b);
            Assert.That(CountWhere(list, m => m.IsPromotion && !m.IsCapture), Is.EqualTo(4));
            Assert.That(CountWhere(list, m => m.IsPromotion && m.IsCapture), Is.EqualTo(4));
            var g = b.G;
            b.Make(new Move(g.CellOf(0, 6), g.CellOf(0, 7), PieceType.Knight));
            Assert.That(Piece.Is(b.GetPiece(g.CellOf(0, 7)), PieceType.Knight, Color.White));
            b.Unmake();
            Assert.That(Piece.Is(b.GetPiece(g.CellOf(0, 6)), PieceType.Pawn, Color.White));
        }

        [Test]
        public void PawnDoubleStepNeedsBothCellsEmptyAndAnUnmovedPawn()
        {
            var b = Load("4k3/8/8/8/4n3/8/4P3/4K3 w - - 0 1");
            Assert.That(CountWhere(Legal(b), m => m.IsDoubleStep), Is.EqualTo(0), "landing cell occupied");
            b = Load("4k3/8/8/8/8/4n3/4P3/4K3 w - - 0 1");
            Assert.That(CountWhere(Legal(b), m => m.From == b.G.CellOf(4, 1)), Is.EqualTo(0), "blocked in front");
            b = Load("4k3/8/8/8/8/4P3/8/4K3 w - - 0 1");
            Assert.That(CountWhere(Legal(b), m => m.IsDoubleStep), Is.EqualTo(0), "pawn off its start rank has moved");
        }

        [Test]
        public void CheckCheckmateAndStalemateAreDetected()
        {
            var b = Load("rnb1kbnr/pppp1ppp/8/4p3/6Pq/5P2/PPPPP2P/RNBQKBNR w KQkq - 1 3"); // fool's mate
            Assert.That(b.InCheck(), Is.True);
            Assert.That(b.GetStatus(), Is.EqualTo(GameStatus.Checkmate));

            b = Load("7k/5Q2/6K1/8/8/8/8/8 b - - 0 1");
            Assert.That(b.InCheck(), Is.False);
            Assert.That(b.GetStatus(), Is.EqualTo(GameStatus.Stalemate));

            b = Load("4k3/8/8/8/8/8/8/4K3 w - - 0 1");
            Assert.That(b.GetStatus(), Is.EqualTo(GameStatus.Ongoing));
        }

        [Test]
        public void PinnedPieceCannotExposeTheKing()
        {
            var b = Load("4k3/8/8/8/8/8/4R3/r3K3 w - - 0 1"); // rook e2 pinned? no: a1 rook attacks along rank 1, king e1. e2 rook is free. Use a real pin:
            b = Load("4k3/8/8/8/8/8/4R3/4K2r w - - 0 1"); // h1 rook attacks e1 along rank; e2 rook is not on that line -> free; king in check
            Assert.That(b.InCheck(), Is.True);
            b = Load("4k3/4r3/8/8/8/8/4R3/4K3 w - - 0 1"); // e2 rook pinned on the e file
            var list = Legal(b);
            Assert.That(CountWhere(list, m => m.From == b.G.CellOf(4, 1) && b.G.Coord(m.To, 0) != 4), Is.EqualTo(0), "pinned rook may not leave the file");
            Assert.That(CountWhere(list, m => m.From == b.G.CellOf(4, 1)), Is.EqualTo(5), "but may slide along it, including the capture");
        }

        [Test]
        public void DrawFlagsDefaultOffAndWorkWhenOn()
        {
            var b = Load("4k3/8/8/8/8/8/8/4K3 w - - 100 1");
            Assert.That(b.GetStatus(), Is.EqualTo(GameStatus.Ongoing));
            b.Rules.FiftyMoveRule = true;
            Assert.That(b.GetStatus(), Is.EqualTo(GameStatus.DrawFiftyMove));

            b = Load("4k3/8/8/8/8/8/8/4K3 w - - 0 1");
            b.Rules.ThreefoldRepetition = true;
            var g = b.G;
            for (int i = 0; i < 2; i++)
            {
                b.Make(new Move(g.CellOf(4, 0), g.CellOf(3, 0)));
                b.Make(new Move(g.CellOf(4, 7), g.CellOf(3, 7)));
                b.Make(new Move(g.CellOf(3, 0), g.CellOf(4, 0)));
                b.Make(new Move(g.CellOf(3, 7), g.CellOf(4, 7)));
            }
            Assert.That(b.RepetitionCount(), Is.EqualTo(3));
            Assert.That(b.GetStatus(), Is.EqualTo(GameStatus.DrawRepetition));
        }

        [Test]
        public void CoreReferencesNoUnityAssembly()
        {
            foreach (var a in typeof(Board).Assembly.GetReferencedAssemblies())
                Assert.That(a.Name, Does.Not.StartWith("UnityEngine"));
        }
    }
}
