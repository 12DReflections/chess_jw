using System;
using System.Collections.Generic;

namespace Chess4D.Core
{
    /// <summary>A step direction in Z^n plus its precomputed cell-index delta.</summary>
    public sealed class Direction
    {
        public readonly int[] Vec;
        public readonly int Delta;

        internal Direction(int[] vec, int delta) { Vec = vec; Delta = delta; }

        public override string ToString() { return "(" + string.Join(",", Vec) + ")"; }
    }

    /// <summary>
    /// Everything that depends only on the dimension count and side length:
    /// cell indexing, coordinate lookup, and the direction sets for every piece.
    /// Nothing here is hardcoded to 2 or 4 dimensions (spec section 5).
    /// </summary>
    public sealed class BoardGeometry
    {
        /// <summary>Axis 1 is y, the axis of advance. White moves +y, Black moves -y.</summary>
        public const int AdvanceAxis = 1;

        public readonly int Dimensions;
        public readonly int Side;
        public readonly int CellCount;
        /// <summary>Stride[i] = Side^i. Cell index = sum of coord[i] * Stride[i].</summary>
        public readonly int[] Stride;
        private readonly byte[] coordTable;

        public readonly Direction[] Rook;
        public readonly Direction[] Bishop;
        /// <summary>Rook directions first, then Bishop directions. Index below Rook.Length means orthogonal.</summary>
        public readonly Direction[] Queen;
        /// <summary>Same set as Queen; the King takes one step.</summary>
        public readonly Direction[] King;
        public readonly Direction[] Knight;
        /// <summary>Indexed by colour.</summary>
        public readonly Direction[] PawnForward;
        /// <summary>Indexed by colour: forward plus +/-1 on exactly one non-advance axis.</summary>
        public readonly Direction[][] PawnCapture;
        /// <summary>Negated capture directions per colour, for "which pawns attack this cell" queries.</summary>
        public readonly Direction[][] PawnCaptureReverse;

        public BoardGeometry(int dimensions, int side)
        {
            if (dimensions < 2 || dimensions > CoreInfo.MaxDimensions)
                throw new ArgumentOutOfRangeException(nameof(dimensions), "Dimensions must be between 2 and " + CoreInfo.MaxDimensions);
            if (side < 4 || side > 255)
                throw new ArgumentOutOfRangeException(nameof(side), "Side must be between 4 and 255");

            Dimensions = dimensions;
            Side = side;
            Stride = new int[dimensions];
            long count = 1;
            for (int i = 0; i < dimensions; i++)
            {
                Stride[i] = checked((int)count);
                count *= side;
            }
            CellCount = checked((int)count);

            coordTable = new byte[CellCount * dimensions];
            for (int cell = 0; cell < CellCount; cell++)
            {
                int rem = cell;
                for (int i = 0; i < dimensions; i++)
                {
                    coordTable[cell * dimensions + i] = (byte)(rem % side);
                    rem /= side;
                }
            }

            Rook = BuildRook();
            Bishop = BuildBishop();
            Queen = new Direction[Rook.Length + Bishop.Length];
            Array.Copy(Rook, 0, Queen, 0, Rook.Length);
            Array.Copy(Bishop, 0, Queen, Rook.Length, Bishop.Length);
            King = Queen;
            Knight = BuildKnight();

            PawnForward = new Direction[2];
            PawnCapture = new Direction[2][];
            PawnCaptureReverse = new Direction[2][];
            for (int c = 0; c < 2; c++)
            {
                int fwd = c == (int)Color.White ? 1 : -1;
                int[] f = new int[dimensions];
                f[AdvanceAxis] = fwd;
                PawnForward[c] = Make(f);

                var caps = new List<Direction>();
                var rev = new List<Direction>();
                for (int a = 0; a < dimensions; a++)
                {
                    if (a == AdvanceAxis) continue;
                    for (int sign = -1; sign <= 1; sign += 2)
                    {
                        int[] v = new int[dimensions];
                        v[AdvanceAxis] = fwd;
                        v[a] = sign;
                        caps.Add(Make(v));
                        int[] r = new int[dimensions];
                        r[AdvanceAxis] = -fwd;
                        r[a] = -sign;
                        rev.Add(Make(r));
                    }
                }
                PawnCapture[c] = caps.ToArray();
                PawnCaptureReverse[c] = rev.ToArray();
            }
        }

        private Direction Make(int[] vec)
        {
            int delta = 0;
            for (int i = 0; i < Dimensions; i++) delta += vec[i] * Stride[i];
            return new Direction(vec, delta);
        }

        private Direction[] BuildRook()
        {
            var list = new List<Direction>();
            for (int a = 0; a < Dimensions; a++)
                for (int sign = -1; sign <= 1; sign += 2)
                {
                    int[] v = new int[Dimensions];
                    v[a] = sign;
                    list.Add(Make(v));
                }
            return list.ToArray();
        }

        private Direction[] BuildBishop()
        {
            var list = new List<Direction>();
            for (int a = 0; a < Dimensions; a++)
                for (int b = a + 1; b < Dimensions; b++)
                    for (int sa = -1; sa <= 1; sa += 2)
                        for (int sb = -1; sb <= 1; sb += 2)
                        {
                            int[] v = new int[Dimensions];
                            v[a] = sa;
                            v[b] = sb;
                            list.Add(Make(v));
                        }
            return list.ToArray();
        }

        private Direction[] BuildKnight()
        {
            var list = new List<Direction>();
            for (int a = 0; a < Dimensions; a++)
                for (int b = 0; b < Dimensions; b++)
                {
                    if (a == b) continue;
                    for (int sa = -1; sa <= 1; sa += 2)
                        for (int sb = -1; sb <= 1; sb += 2)
                        {
                            int[] v = new int[Dimensions];
                            v[a] = 2 * sa;
                            v[b] = sb;
                            list.Add(Make(v));
                        }
                }
            return list.ToArray();
        }

        public bool Contains(int cell) { return (uint)cell < (uint)CellCount; }

        public int Coord(int cell, int axis) { return coordTable[cell * Dimensions + axis]; }

        /// <summary>The cell one step from <paramref name="cell"/> along <paramref name="d"/>, or -1 if that leaves the board.</summary>
        public int Step(int cell, Direction d)
        {
            int baseIndex = cell * Dimensions;
            int[] v = d.Vec;
            for (int i = 0; i < Dimensions; i++)
            {
                int value = coordTable[baseIndex + i] + v[i];
                if ((uint)value >= (uint)Side) return -1;
            }
            return cell + d.Delta;
        }

        /// <summary>Same cell with one axis replaced. No bounds check on <paramref name="value"/>.</summary>
        public int WithCoord(int cell, int axis, int value)
        {
            return cell + (value - Coord(cell, axis)) * Stride[axis];
        }

        public int CellOf(Coord c)
        {
            if (c.Dimensions != Dimensions) throw new ArgumentException("Coord dimension mismatch", nameof(c));
            int cell = 0;
            for (int i = 0; i < Dimensions; i++)
            {
                int v = c[i];
                if ((uint)v >= (uint)Side) throw new ArgumentOutOfRangeException(nameof(c), "Coordinate off board: " + c);
                cell += v * Stride[i];
            }
            return cell;
        }

        public int CellOf(params int[] coords)
        {
            if (coords.Length != Dimensions) throw new ArgumentException("Expected " + Dimensions + " coordinates", nameof(coords));
            int cell = 0;
            for (int i = 0; i < Dimensions; i++)
            {
                if ((uint)coords[i] >= (uint)Side) throw new ArgumentOutOfRangeException(nameof(coords));
                cell += coords[i] * Stride[i];
            }
            return cell;
        }

        public Coord CoordOf(int cell)
        {
            int b = cell * Dimensions;
            return new Coord(Dimensions,
                coordTable[b],
                coordTable[b + 1],
                Dimensions > 2 ? coordTable[b + 2] : 0,
                Dimensions > 3 ? coordTable[b + 3] : 0,
                Dimensions > 4 ? coordTable[b + 4] : 0,
                Dimensions > 5 ? coordTable[b + 5] : 0);
        }

        public int ChebyshevDistance(int a, int b)
        {
            int best = 0;
            for (int i = 0; i < Dimensions; i++)
            {
                int d = Math.Abs(Coord(a, i) - Coord(b, i));
                if (d > best) best = d;
            }
            return best;
        }
    }
}
