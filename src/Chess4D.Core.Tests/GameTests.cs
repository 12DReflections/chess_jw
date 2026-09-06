using Chess4D.Core;
using NUnit.Framework;

namespace Chess4D.Core.Tests
{
    public class GameTests
    {
        private static Game NewGame(int dims = 4)
        {
            var b = new Board(dims, 8);
            StartPosition.Setup(b);
            return new Game(b);
        }

        [Test]
        public void MovesUndoAndRedoWithHistoryText()
        {
            var game = NewGame();
            var g = game.Board.G;
            ulong start = game.Board.Hash;
            Assert.That(game.TryMove(new Move(g.CellOf(2, 1, 3, 3), g.CellOf(2, 3, 3, 3), PieceType.None, MoveFlags.DoubleStep, g.CellOf(2, 2, 3, 3))), Is.True);
            Assert.That(game.TryMove(new Move(g.CellOf(3, 6, 3, 3), g.CellOf(3, 4, 3, 3), PieceType.None, MoveFlags.DoubleStep, g.CellOf(3, 5, 3, 3))), Is.True);
            Assert.That(game.TryMove(new Move(g.CellOf(3, 0, 3, 3), g.CellOf(0, 3, 3, 3))), Is.True);
            Assert.That(game.HistoryText[0], Is.EqualTo("2133-2333"));
            Assert.That(game.HistoryText[2], Is.EqualTo("Q3033-0333+"));
            Assert.That(game.HistoryLine(2), Is.EqualTo("2. W Q3033-0333+"));
            Assert.That(game.Board.InCheck(), Is.True);
            ulong afterCheck = game.Board.Hash;

            Assert.That(game.Undo(), Is.True);
            Assert.That(game.Undo(), Is.True);
            Assert.That(game.Undo(), Is.True);
            Assert.That(game.Undo(), Is.False);
            Assert.That(game.Board.Hash, Is.EqualTo(start));
            Assert.That(game.History.Count, Is.EqualTo(0));
            Assert.That(game.RedoCount, Is.EqualTo(3));
            Assert.That(game.Redo() && game.Redo() && game.Redo(), Is.True);
            Assert.That(game.Board.Hash, Is.EqualTo(afterCheck));
            Assert.That(game.HistoryText[2], Is.EqualTo("Q3033-0333+"));

            // A new move after undo discards the redo branch.
            game.Undo();
            Assert.That(game.TryMove(new Move(g.CellOf(1, 0, 3, 3), g.CellOf(0, 2, 3, 3))), Is.True);
            Assert.That(game.RedoCount, Is.EqualTo(0));
        }

        [Test]
        public void IllegalMovesAreRefused()
        {
            var game = NewGame();
            var g = game.Board.G;
            Assert.That(game.TryMove(new Move(g.CellOf(4, 0, 3, 3), g.CellOf(4, 1, 3, 3))), Is.False, "king blocked by own pawn");
            Assert.That(game.TryMove(new Move(g.CellOf(2, 1, 3, 3), g.CellOf(2, 3, 3, 3))), Is.False, "double step without its flag is not the same move");
            Assert.That(game.History.Count, Is.EqualTo(0));
        }

        [Test]
        public void TypedMovesParseAgainstTheLegalList()
        {
            var game = NewGame();
            Assert.That(Notation.TryParseMove(game.Board, game.Legal, "(2,1,3,3) (2,3,3,3)", out Move m), Is.True);
            Assert.That(m.IsDoubleStep, Is.True);
            Assert.That(Notation.TryParseMove(game.Board, game.Legal, "(2,1,3,3)-(2,2,3,3)", out m), Is.True);
            Assert.That(m.IsDoubleStep, Is.False);
            Assert.That(Notation.TryParseMove(game.Board, game.Legal, "(4,0,3,3)-(4,1,3,3)", out m), Is.False);
            Assert.That(Notation.TryParseMove(game.Board, game.Legal, "nonsense", out m), Is.False);
            // Compact form, with and without the piece letter, separator, and suffixes.
            Assert.That(Notation.TryParseMove(game.Board, game.Legal, "2133 2333", out m), Is.True);
            Assert.That(m.IsDoubleStep, Is.True);
            Assert.That(Notation.TryParseMove(game.Board, game.Legal, "N1033-0233", out m), Is.True);
            Assert.That(Notation.TryParseMove(game.Board, game.Legal, "N1033-0233+", out m), Is.True);
            Assert.That(Notation.TryParseMove(game.Board, game.Legal, "2133-(2,2,3,3)", out m), Is.True);
            Assert.That(Notation.TryParseMove(game.Board, game.Legal, "213-2333", out m), Is.False, "three digits is not a cell");
            Assert.That(Notation.TryParseMove(game.Board, game.Legal, "2133-23331", out m), Is.False, "trailing junk");
            Assert.That(Notation.DescribeLong(game.Board, game.Legal[0]), Does.Match(@"^[KQRBN]?\(\d,\d,\d,\d\)[-x]\(\d,\d,\d,\d\)"));
        }

        [Test]
        public void PromotionViaTypedMoveDefaultsToQueenOrTakesTheSuffix()
        {
            var b = new Board(4, 8);
            b.Clear();
            var g = b.G;
            b.PlacePiece(g.CellOf(4, 0, 3, 3), Piece.Make(PieceType.King, Color.White, true));
            b.PlacePiece(g.CellOf(4, 7, 0, 0), Piece.Make(PieceType.King, Color.Black, true));
            b.PlacePiece(g.CellOf(0, 6, 3, 3), Piece.Make(PieceType.Pawn, Color.White, true));
            b.SetSideToMove(Color.White);
            var game = new Game(b);
            Assert.That(Notation.TryParseMove(b, game.Legal, "(0,6,3,3)-(0,7,3,3)", out Move q), Is.True);
            Assert.That(q.Promotion, Is.EqualTo(PieceType.Queen));
            Assert.That(Notation.TryParseMove(b, game.Legal, "0633-0733=N", out Move n), Is.True);
            Assert.That(n.Promotion, Is.EqualTo(PieceType.Knight));
            Assert.That(game.TryMove(n), Is.True);
            Assert.That(game.HistoryText[0], Is.EqualTo("0633-0733=N"));
        }

        /// <summary>A hand-constructed 4D checkmate: the black king in the corner is smothered by its own pawns on all 15 neighbours, and a knight arrives to give check it cannot capture or block.</summary>
        [Test]
        public void SmotheredKnightMateAtFourDimensions()
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
            var game = new Game(b);
            Assert.That(game.Status, Is.EqualTo(GameStatus.Ongoing));
            Assert.That(game.TryMove(new Move(g.CellOf(4, 2, 0, 0), g.CellOf(2, 1, 0, 0))), Is.True);
            Assert.That(game.Status, Is.EqualTo(GameStatus.Checkmate));
            Assert.That(game.IsOver, Is.True);
            Assert.That(game.HistoryText[0], Is.EqualTo("N4200-2100#"));
            Assert.That(game.TryMove(new Move(g.CellOf(0, 0, 0, 0), g.CellOf(0, 0, 0, 1))), Is.False, "no moves after mate");
            game.Undo();
            Assert.That(game.Status, Is.EqualTo(GameStatus.Ongoing));
        }

        [Test]
        public void PositionTextRoundTripsTheStartAndAnEditedPosition()
        {
            var b = new Board(4, 8);
            StartPosition.Setup(b);
            string text = PositionText.Save(b);
            var c = new Board(4, 8);
            PositionText.Load(c, text);
            Assert.That(c.Hash, Is.EqualTo(b.Hash));
            Assert.That(c.PieceCount(Color.White), Is.EqualTo(144));
            Assert.That(PositionText.Save(c), Is.EqualTo(text));

            var g = b.G;
            b.Clear();
            b.PlacePiece(g.CellOf(4, 0, 3, 3), Piece.Make(PieceType.King, Color.White));
            b.PlacePiece(g.CellOf(3, 0, 3, 3), Piece.Make(PieceType.Queen, Color.White, true));
            b.PlacePiece(g.CellOf(4, 7, 3, 3), Piece.Make(PieceType.King, Color.Black));
            b.PlacePiece(g.CellOf(2, 3, 1, 5), Piece.Make(PieceType.Pawn, Color.Black, true));
            b.SetSideToMove(Color.Black);
            b.SetEnPassantCell(g.CellOf(2, 2, 1, 5));
            b.SetHalfmoveClock(7);
            text = PositionText.Save(b);
            Assert.That(text, Does.Contain("to-move black"));
            Assert.That(text, Does.Contain("W Q (3,0,3,3) moved"));
            Assert.That(text, Does.Contain("en-passant (2,2,1,5)"));
            PositionText.Load(c, text);
            Assert.That(c.Hash, Is.EqualTo(b.Hash));
            Assert.That(c.HalfmoveClock, Is.EqualTo(7));
            Assert.That(Piece.HasMoved(c.GetPiece(g.CellOf(3, 0, 3, 3))), Is.True);
            Assert.That(Piece.HasMoved(c.GetPiece(g.CellOf(4, 0, 3, 3))), Is.False);
        }

        [Test]
        public void PositionTextRejectsBadInput()
        {
            var b = new Board(4, 8);
            Assert.Throws<System.FormatException>(() => PositionText.Load(b, "hello"));
            Assert.Throws<System.FormatException>(() => PositionText.Load(b, "chess4d-position 1\ndimensions 2\n"));
            Assert.Throws<System.FormatException>(() => PositionText.Load(b, "chess4d-position 1\nW X (0,0,0,0)\n"));
        }

        [Test]
        public void KingAndQueenVersusKingCanBeSetUpAndPlayed()
        {
            var b = new Board(4, 8);
            b.Clear();
            var g = b.G;
            b.PlacePiece(g.CellOf(4, 0, 3, 3), Piece.Make(PieceType.King, Color.White, true));
            b.PlacePiece(g.CellOf(3, 0, 3, 3), Piece.Make(PieceType.Queen, Color.White, true));
            b.PlacePiece(g.CellOf(4, 7, 3, 3), Piece.Make(PieceType.King, Color.Black, true));
            b.SetSideToMove(Color.White);
            var game = new Game(b);
            Assert.That(game.Legal.Count, Is.GreaterThan(32));
            var rng = new System.Random(1);
            for (int i = 0; i < 40 && !game.IsOver; i++)
            {
                MoveList legal = game.Legal;
                Assert.That(game.TryMove(legal[rng.Next(legal.Count)]), Is.True);
            }
            Assert.That(game.History.Count, Is.GreaterThan(0));
            Assert.That(game.Board.PieceCount(Color.White) + game.Board.PieceCount(Color.Black), Is.GreaterThanOrEqualTo(2));
        }
    }
}
