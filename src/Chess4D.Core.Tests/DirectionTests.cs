using Chess4D.Core;
using NUnit.Framework;

namespace Chess4D.Core.Tests
{
    public class DirectionTests
    {
        [TestCase(2, 4, 4, 8, 8, 8)]
        [TestCase(4, 8, 24, 32, 32, 48)]
        [TestCase(6, 12, 60, 72, 72, 120)]
        public void DirectionCountsFollowTheDimension(int dims, int rook, int bishop, int queen, int king, int knight)
        {
            var g = new BoardGeometry(dims, 8);
            Assert.That(g.Rook.Length, Is.EqualTo(rook), "rook");
            Assert.That(g.Bishop.Length, Is.EqualTo(bishop), "bishop");
            Assert.That(g.Queen.Length, Is.EqualTo(queen), "queen");
            Assert.That(g.King.Length, Is.EqualTo(king), "king");
            Assert.That(g.Knight.Length, Is.EqualTo(knight), "knight");
        }

        [TestCase(2, 2)]
        [TestCase(4, 6)]
        public void PawnCaptureCountIsTwoPerNonAdvanceAxis(int dims, int expected)
        {
            var g = new BoardGeometry(dims, 8);
            Assert.That(g.PawnCapture[0].Length, Is.EqualTo(expected));
            Assert.That(g.PawnCapture[1].Length, Is.EqualTo(expected));
            foreach (var d in g.PawnCapture[(int)Color.White]) Assert.That(d.Vec[BoardGeometry.AdvanceAxis], Is.EqualTo(1));
            foreach (var d in g.PawnCapture[(int)Color.Black]) Assert.That(d.Vec[BoardGeometry.AdvanceAxis], Is.EqualTo(-1));
        }

        [Test]
        public void DirectionsAreDistinctAndDeltasMatchVectors()
        {
            var g = new BoardGeometry(4, 8);
            foreach (var set in new[] { g.Queen, g.Knight, g.PawnCapture[0], g.PawnCapture[1] })
            {
                var seen = new System.Collections.Generic.HashSet<string>();
                foreach (var d in set)
                {
                    Assert.That(seen.Add(d.ToString()), Is.True, "duplicate " + d);
                    int delta = 0;
                    for (int i = 0; i < g.Dimensions; i++) delta += d.Vec[i] * g.Stride[i];
                    Assert.That(d.Delta, Is.EqualTo(delta));
                }
            }
        }

        [Test]
        public void StepRespectsEveryAxisBoundary()
        {
            var g = new BoardGeometry(4, 8);
            int corner = g.CellOf(0, 0, 0, 0);
            int onBoard = 0;
            foreach (var d in g.Queen) if (g.Step(corner, d) >= 0) onBoard++;
            Assert.That(onBoard, Is.EqualTo(4 + 6), "from the origin only the all-positive rook (4) and bishop (6) directions stay on board");
            int centre = g.CellOf(3, 3, 3, 3);
            foreach (var d in g.Knight) Assert.That(g.Step(centre, d), Is.GreaterThanOrEqualTo(0));
        }

        [Test]
        public void CoordRoundTrips()
        {
            var g = new BoardGeometry(4, 8);
            for (int cell = 0; cell < g.CellCount; cell += 97)
            {
                Coord c = g.CoordOf(cell);
                Assert.That(g.CellOf(c), Is.EqualTo(cell));
                Assert.That(Coord.TryParse(c.ToString(), 4, out Coord parsed), Is.True);
                Assert.That(parsed, Is.EqualTo(c));
            }
        }
    }
}
