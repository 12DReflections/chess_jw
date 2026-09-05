using System;
using Chess4D.Core;
using NUnit.Framework;

namespace Chess4D.Core.Tests
{
    /// <summary>Stage 3: the rotation must be mathematically exact at the endpoints.</summary>
    public class AxisViewTests
    {
        private static readonly BoardGeometry G = new BoardGeometry(4, 8);

        [Test]
        public void PhiZeroIsTheIdentityOnEveryCell()
        {
            var view = new AxisView(4, 8);
            var outv = new double[4];
            for (int cell = 0; cell < G.CellCount; cell++)
            {
                Coord c = G.CoordOf(cell);
                view.Project(c, 0, 3, 0.0, outv);
                for (int s = 0; s < 4; s++) Assert.That(outv[s], Is.EqualTo((double)c[s]));
            }
        }

        [Test]
        public void QuarterTurnMapsEveryCellExactlyOntoItsReflectedSwappedCounterpart()
        {
            var view = new AxisView(4, 8);
            var outv = new double[4];
            var exact = new int[4];
            for (int visible = 0; visible < 3; visible++)
            {
                AxisView after = view.AfterQuarterTurn(visible, 3);
                for (int cell = 0; cell < G.CellCount; cell++)
                {
                    Coord c = G.CoordOf(cell);
                    view.Project(c, visible, 3, 90.0, outv);
                    after.ProjectExact(c, exact);
                    for (int s = 0; s < 4; s++)
                    {
                        Assert.That(outv[s], Is.EqualTo((double)exact[s]), "slot " + s + " cell " + c);
                        Assert.That(outv[s] == Math.Floor(outv[s]), Is.True, "not an integer");
                    }
                    // a' = -b: the visible slot shows the old hidden axis, reflected. b' = a: the hidden slot shows the old visible axis.
                    Assert.That(outv[visible], Is.EqualTo((double)(7 - c[3])));
                    Assert.That(outv[3], Is.EqualTo((double)c[visible]));
                }
            }
        }

        [Test]
        public void FloatPathApproachesTheIntegerEndpoints()
        {
            var view = new AxisView(4, 8);
            var near90 = new double[4];
            var at90 = new double[4];
            var near0 = new double[4];
            for (int cell = 0; cell < G.CellCount; cell += 7)
            {
                Coord c = G.CoordOf(cell);
                view.Project(c, 1, 3, 89.999, near90);
                view.Project(c, 1, 3, 90.0, at90);
                view.Project(c, 1, 3, 0.001, near0);
                for (int s = 0; s < 4; s++)
                {
                    Assert.That(near90[s], Is.EqualTo(at90[s]).Within(1e-3));
                    Assert.That(near0[s], Is.EqualTo((double)c[s]).Within(1e-3));
                }
            }
        }

        [Test]
        public void MidRotationIsARotationNotAnInterpolation()
        {
            // At 45 degrees a point at distance r from the centre in the a-b plane stays at distance r.
            var view = new AxisView(4, 8);
            var outv = new double[4];
            Coord c = new Coord(4, 7, 2, 0, 5);
            view.Project(c, 0, 3, 45.0, outv);
            double ctr = view.Center;
            double before = Math.Sqrt(Math.Pow(7 - ctr, 2) + Math.Pow(5 - ctr, 2));
            double after = Math.Sqrt(Math.Pow(outv[0] - ctr, 2) + Math.Pow(outv[3] - ctr, 2));
            Assert.That(after, Is.EqualTo(before).Within(1e-9));
            Assert.That(outv[1], Is.EqualTo(2.0));
            Assert.That(outv[2], Is.EqualTo(0.0));
        }

        [Test]
        public void FourQuarterTurnsInTheSamePlaneReturnToTheStart()
        {
            var view = new AxisView(4, 8);
            var v = view;
            for (int i = 0; i < 4; i++) v = v.AfterQuarterTurn(2, 3);
            Assert.That(v.ToString(), Is.EqualTo(view.ToString()));
            var two = view.AfterQuarterTurn(2, 3).AfterQuarterTurn(2, 3);
            Assert.That(two.AxisAtSlot(2), Is.EqualTo(2));
            Assert.That(two.SignAtSlot(2), Is.EqualTo(-1), "half turn reflects the axis in place");
            Assert.That(two.SignAtSlot(3), Is.EqualTo(-1));
        }

        [Test]
        public void EveryStateIsASignedPermutationAndAllFourPerspectivesAreReachable()
        {
            var view = new AxisView(4, 8);
            var seen = new System.Collections.Generic.HashSet<string>();
            var rng = new Random(3);
            var v = view;
            for (int i = 0; i < 500; i++)
            {
                v = v.AfterQuarterTurn(rng.Next(3), 3);
                var axes = new System.Collections.Generic.HashSet<int>();
                for (int s = 0; s < 4; s++)
                {
                    Assert.That(axes.Add(v.AxisAtSlot(s)), Is.True, "axis repeated");
                    Assert.That(Math.Abs(v.SignAtSlot(s)), Is.EqualTo(1));
                }
                string key = v.IsVisible(0) + "," + v.IsVisible(1) + "," + v.IsVisible(2) + "," + v.IsVisible(3);
                seen.Add(key);
            }
            Assert.That(seen.Count, Is.EqualTo(4), "C(4,3) perspectives");
        }

        [Test]
        public void SixDimensionsHasThreeHiddenSlots()
        {
            var view = new AxisView(6, 8);
            Assert.That(view.HiddenSlots, Is.EqualTo(3));
            var after = view.AfterQuarterTurn(0, 5);
            Assert.That(after.AxisAtSlot(0), Is.EqualTo(5));
            Assert.That(after.AxisAtSlot(5), Is.EqualTo(0));
            Assert.That(after.ToString(), Is.EqualTo("-u y z | w v x"));
        }
    }
}
