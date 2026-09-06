using System;
using System.Text;

namespace Chess4D.Core
{
    /// <summary>
    /// A lattice coordinate with up to six axes and an active dimension count.
    /// Value type, no allocation. Axis 0 is x, axis 1 is y (the axis of advance),
    /// then z, w, v, u. Text form is the plain tuple "(x,y,z,w)" required by the
    /// spec until a notation is approved in Stage 4.
    /// </summary>
    public readonly struct Coord : IEquatable<Coord>
    {
        public readonly byte Dimensions;
        private readonly byte a0, a1, a2, a3, a4, a5;

        public Coord(int dimensions, int x, int y, int z = 0, int w = 0, int v = 0, int u = 0)
        {
            if (dimensions < 2 || dimensions > CoreInfo.MaxDimensions)
                throw new ArgumentOutOfRangeException(nameof(dimensions));
            Dimensions = (byte)dimensions;
            a0 = (byte)x; a1 = (byte)y; a2 = (byte)z; a3 = (byte)w; a4 = (byte)v; a5 = (byte)u;
        }

        public int this[int axis]
        {
            get
            {
                switch (axis)
                {
                    case 0: return a0;
                    case 1: return a1;
                    case 2: return a2;
                    case 3: return a3;
                    case 4: return a4;
                    case 5: return a5;
                    default: throw new ArgumentOutOfRangeException(nameof(axis));
                }
            }
        }

        public Coord With(int axis, int value)
        {
            switch (axis)
            {
                case 0: return new Coord(Dimensions, value, a1, a2, a3, a4, a5);
                case 1: return new Coord(Dimensions, a0, value, a2, a3, a4, a5);
                case 2: return new Coord(Dimensions, a0, a1, value, a3, a4, a5);
                case 3: return new Coord(Dimensions, a0, a1, a2, value, a4, a5);
                case 4: return new Coord(Dimensions, a0, a1, a2, a3, value, a5);
                case 5: return new Coord(Dimensions, a0, a1, a2, a3, a4, value);
                default: throw new ArgumentOutOfRangeException(nameof(axis));
            }
        }

        public bool Equals(Coord o)
        {
            return Dimensions == o.Dimensions && a0 == o.a0 && a1 == o.a1 && a2 == o.a2 && a3 == o.a3 && a4 == o.a4 && a5 == o.a5;
        }

        public override bool Equals(object obj) { return obj is Coord c && Equals(c); }

        public override int GetHashCode()
        {
            return Dimensions | (a0 << 4) | (a1 << 8) | (a2 << 12) | (a3 << 16) | (a4 << 20) | (a5 << 24);
        }

        public static bool operator ==(Coord a, Coord b) { return a.Equals(b); }
        public static bool operator !=(Coord a, Coord b) { return !a.Equals(b); }

        public override string ToString()
        {
            var sb = new StringBuilder(16);
            sb.Append('(');
            for (int i = 0; i < Dimensions; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append(this[i]);
            }
            sb.Append(')');
            return sb.ToString();
        }

        /// <summary>Digits only, one per axis in axis order: (3,0,3,3) is "3033". Requires side at most 10.</summary>
        public string ToCompact()
        {
            var sb = new StringBuilder(Dimensions);
            for (int i = 0; i < Dimensions; i++) sb.Append((char)('0' + this[i]));
            return sb.ToString();
        }

        /// <summary>Parses the compact digit form: exactly <paramref name="dimensions"/> digits.</summary>
        public static bool TryParseCompact(string text, int dimensions, out Coord coord)
        {
            coord = default;
            if (text == null) return false;
            string s = text.Trim();
            if (s.Length != dimensions) return false;
            int[] v = new int[6];
            for (int i = 0; i < dimensions; i++)
            {
                if (!char.IsDigit(s[i])) return false;
                v[i] = s[i] - '0';
            }
            coord = new Coord(dimensions, v[0], v[1], v[2], v[3], v[4], v[5]);
            return true;
        }

        /// <summary>Parses "(x,y,z,w)" with exactly <paramref name="dimensions"/> entries (parentheses optional), or the compact digit form.</summary>
        public static bool TryParse(string text, int dimensions, out Coord coord)
        {
            coord = default;
            if (string.IsNullOrEmpty(text)) return false;
            if (text.Trim().IndexOf(',') < 0) return TryParseCompact(text, dimensions, out coord);
            string s = text.Trim().TrimStart('(').TrimEnd(')');
            string[] parts = s.Split(',');
            if (parts.Length != dimensions) return false;
            int[] v = new int[6];
            for (int i = 0; i < parts.Length; i++)
            {
                if (!int.TryParse(parts[i].Trim(), out v[i]) || v[i] < 0 || v[i] > 255) return false;
            }
            coord = new Coord(dimensions, v[0], v[1], v[2], v[3], v[4], v[5]);
            return true;
        }
    }
}
