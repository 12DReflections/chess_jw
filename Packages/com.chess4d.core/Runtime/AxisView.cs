using System;

namespace Chess4D.Core
{
    /// <summary>
    /// Which axis each view slot shows, and with what sign. Slots 0..2 are the
    /// visible axes (screen X, Y, Z); slots 3..n-1 are hidden. Rotating the view
    /// swaps a visible slot with a hidden slot by a continuous rotation in the
    /// plane they span:
    ///
    ///   a' = a cos(phi) - b sin(phi)
    ///   b' = a sin(phi) + b cos(phi)
    ///
    /// about the board centre (side-1)/2. At phi = 90 degrees a' = -b and b' = a,
    /// so the incoming axis arrives reflected: cell value v appears at side-1-v.
    /// The endpoints phi = 0 and phi = 90 are computed as exact integer
    /// permutations, never through trigonometry (spec Stage 3).
    /// </summary>
    public sealed class AxisView
    {
        public const int VisibleSlots = 3;
        public const double QuarterTurnDegrees = 90.0;

        public readonly int Dimensions;
        public readonly int Side;
        private readonly int[] axisAtSlot;
        private readonly int[] signAtSlot;

        public int HiddenSlots { get { return Dimensions - VisibleSlots; } }
        public double Center { get { return (Side - 1) / 2.0; } }

        /// <summary>Identity view: slot i shows axis i with positive sign.</summary>
        public AxisView(int dimensions, int side)
        {
            if (dimensions < VisibleSlots + 1) throw new ArgumentOutOfRangeException(nameof(dimensions), "Need at least one hidden axis");
            Dimensions = dimensions;
            Side = side;
            axisAtSlot = new int[dimensions];
            signAtSlot = new int[dimensions];
            for (int i = 0; i < dimensions; i++) { axisAtSlot[i] = i; signAtSlot[i] = 1; }
        }

        public AxisView(AxisView other)
        {
            Dimensions = other.Dimensions;
            Side = other.Side;
            axisAtSlot = (int[])other.axisAtSlot.Clone();
            signAtSlot = (int[])other.signAtSlot.Clone();
        }

        public int AxisAtSlot(int slot) { return axisAtSlot[slot]; }
        public int SignAtSlot(int slot) { return signAtSlot[slot]; }

        public int SlotOfAxis(int axis)
        {
            for (int s = 0; s < Dimensions; s++) if (axisAtSlot[s] == axis) return s;
            throw new ArgumentOutOfRangeException(nameof(axis));
        }

        public bool IsVisible(int axis) { return SlotOfAxis(axis) < VisibleSlots; }

        /// <summary>The integer value shown in <paramref name="slot"/> for coordinate <paramref name="c"/>: the axis value, or side-1 minus it when the slot is reflected.</summary>
        public int ProjectExact(Coord c, int slot)
        {
            int v = c[axisAtSlot[slot]];
            return signAtSlot[slot] > 0 ? v : Side - 1 - v;
        }

        public void ProjectExact(Coord c, int[] slotValues)
        {
            for (int s = 0; s < Dimensions; s++) slotValues[s] = ProjectExact(c, s);
        }

        /// <summary>
        /// Projects <paramref name="c"/> while slot <paramref name="visibleSlot"/> rotates
        /// toward slot <paramref name="hiddenSlot"/> by <paramref name="phiDegrees"/>.
        /// Output values are in board units, 0..side-1 at the endpoints. Exact at
        /// exactly 0 and exactly 90; floating point in between.
        /// </summary>
        public void Project(Coord c, int visibleSlot, int hiddenSlot, double phiDegrees, double[] slotValues)
        {
            if (phiDegrees == 0.0)
            {
                for (int s = 0; s < Dimensions; s++) slotValues[s] = ProjectExact(c, s);
                return;
            }
            if (phiDegrees == QuarterTurnDegrees)
            {
                for (int s = 0; s < Dimensions; s++) slotValues[s] = ProjectExact(c, s);
                int a = slotValues.Length > 0 ? (int)slotValues[visibleSlot] : 0;
                int b = (int)slotValues[hiddenSlot];
                slotValues[visibleSlot] = Side - 1 - b; // a' = -b, re-centred
                slotValues[hiddenSlot] = a;             // b' = a
                return;
            }
            double ctr = Center;
            double phi = phiDegrees * Math.PI / 180.0;
            double cos = Math.Cos(phi), sin = Math.Sin(phi);
            for (int s = 0; s < Dimensions; s++) slotValues[s] = ProjectExact(c, s);
            double av = slotValues[visibleSlot] - ctr;
            double bv = slotValues[hiddenSlot] - ctr;
            slotValues[visibleSlot] = av * cos - bv * sin + ctr;
            slotValues[hiddenSlot] = av * sin + bv * cos + ctr;
        }

        /// <summary>The exact view after a full quarter turn: the visible slot now shows the hidden axis reflected, the hidden slot shows the outgoing axis unreflected.</summary>
        public AxisView AfterQuarterTurn(int visibleSlot, int hiddenSlot)
        {
            if (visibleSlot < 0 || visibleSlot >= VisibleSlots) throw new ArgumentOutOfRangeException(nameof(visibleSlot));
            if (hiddenSlot < VisibleSlots || hiddenSlot >= Dimensions) throw new ArgumentOutOfRangeException(nameof(hiddenSlot));
            var next = new AxisView(this);
            next.axisAtSlot[visibleSlot] = axisAtSlot[hiddenSlot];
            next.signAtSlot[visibleSlot] = -signAtSlot[hiddenSlot];
            next.axisAtSlot[hiddenSlot] = axisAtSlot[visibleSlot];
            next.signAtSlot[hiddenSlot] = signAtSlot[visibleSlot];
            return next;
        }

        /// <summary>True when the visible axis set equals <paramref name="axes"/>, in any order.</summary>
        public bool ShowsAxes(int[] axes)
        {
            if (axes.Length != VisibleSlots) return false;
            foreach (int a in axes) if (!IsVisible(a)) return false;
            return true;
        }

        public static readonly string[] AxisNames = { "x", "y", "z", "w", "v", "u" };

        public override string ToString()
        {
            var sb = new System.Text.StringBuilder();
            for (int s = 0; s < Dimensions; s++)
            {
                if (s == VisibleSlots) sb.Append("| ");
                if (signAtSlot[s] < 0) sb.Append('-');
                sb.Append(AxisNames[axisAtSlot[s]]).Append(' ');
            }
            return sb.ToString().TrimEnd();
        }
    }
}
