using Chess4D.Core;
using NUnit.Framework;

namespace Chess4D.Core.Tests
{
    public class ZobristTests
    {
        private static void Walk(Board b, int depth, ref long nodes)
        {
            Assert.That(b.Hash, Is.EqualTo(b.ComputeHash()), "incremental hash drifted at ply " + b.Ply);
            if (depth == 0) return;
            var list = new MoveList();
            b.GenerateLegal(list);
            for (int i = 0; i < list.Count; i++)
            {
                ulong before = b.Hash;
                string fenBefore = Fen.Save(b);
                b.Make(list[i]);
                nodes++;
                Walk(b, depth - 1, ref nodes);
                b.Unmake();
                Assert.That(b.Hash, Is.EqualTo(before), "unmake did not restore the hash for " + list[i].ToString(b.G));
                Assert.That(Fen.Save(b), Is.EqualTo(fenBefore), "unmake did not restore the position for " + list[i].ToString(b.G));
            }
        }

        [Test]
        public void IncrementalHashMatchesFullRecomputeThroughPerft3()
        {
            var b = new Board(2, 8);
            StartPosition.Setup(b);
            long nodes = 0;
            Walk(b, 3, ref nodes);
            Assert.That(nodes, Is.EqualTo(20 + 400 + 8902));
        }

        [Test]
        public void IncrementalHashMatchesOnKiwipeteThroughDepth2()
        {
            var b = new Board(2, 8);
            Fen.Load(b, "r3k2r/p1ppqpb1/bn2pnp1/3PN3/1p2P3/2N2Q1p/PPPBBPPP/R3K2R w KQkq - 0 1");
            long nodes = 0;
            Walk(b, 2, ref nodes);
            Assert.That(nodes, Is.EqualTo(48 + 2039));
        }

        [Test]
        public void KeysAreReproducibleAndDistinct()
        {
            var a = new Zobrist(64);
            var b = new Zobrist(64);
            Assert.That(a.Side, Is.EqualTo(b.Side));
            Assert.That(a.Piece(10, Piece.Make(PieceType.Queen, Color.Black)), Is.EqualTo(b.Piece(10, Piece.Make(PieceType.Queen, Color.Black))));
            Assert.That(a.Piece(10, Piece.Make(PieceType.Queen, Color.Black)), Is.Not.EqualTo(a.Piece(10, Piece.Make(PieceType.Queen, Color.White))));
            Assert.That(a.Castling(0), Is.EqualTo(0UL));
        }
    }
}
