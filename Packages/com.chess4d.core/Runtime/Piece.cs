namespace Chess4D.Core
{
    public enum Color : byte { White = 0, Black = 1 }

    public enum PieceType : byte { None = 0, Pawn = 1, Knight = 2, Bishop = 3, Rook = 4, Queen = 5, King = 6 }

    /// <summary>
    /// A piece is one byte: bits 0-2 type, bit 3 colour, bit 4 "has moved".
    /// The moved bit drives pawn double-steps and castling rights (spec section 2).
    /// </summary>
    public static class Piece
    {
        public const byte None = 0;
        private const int MovedBit = 16;

        public static byte Make(PieceType type, Color color, bool moved = false)
        {
            return (byte)((int)type | ((int)color << 3) | (moved ? MovedBit : 0));
        }

        public static PieceType TypeOf(byte p) { return (PieceType)(p & 7); }
        public static Color ColorOf(byte p) { return (Color)((p >> 3) & 1); }
        public static bool HasMoved(byte p) { return (p & MovedBit) != 0; }
        public static byte WithMoved(byte p) { return (byte)(p | MovedBit); }
        public static byte WithoutMoved(byte p) { return (byte)(p & ~MovedBit); }
        public static bool IsEmpty(byte p) { return p == 0; }
        public static bool Is(byte p, PieceType type, Color color) { return p != 0 && (p & 15) == ((int)type | ((int)color << 3)); }
        public static Color Opposite(Color c) { return (Color)(1 - (int)c); }

        /// <summary>0..11: six types for White then six for Black. Used as the Zobrist piece index.</summary>
        public static int Index12(byte p) { return ((p & 7) - 1) + 6 * ((p >> 3) & 1); }

        public static char ToChar(byte p)
        {
            if (p == 0) return '.';
            char c;
            switch (TypeOf(p))
            {
                case PieceType.Pawn: c = 'p'; break;
                case PieceType.Knight: c = 'n'; break;
                case PieceType.Bishop: c = 'b'; break;
                case PieceType.Rook: c = 'r'; break;
                case PieceType.Queen: c = 'q'; break;
                case PieceType.King: c = 'k'; break;
                default: c = '?'; break;
            }
            return ColorOf(p) == Color.White ? char.ToUpperInvariant(c) : c;
        }

        public static bool TryFromChar(char ch, out byte piece)
        {
            Color color = char.IsUpper(ch) ? Color.White : Color.Black;
            PieceType type;
            switch (char.ToLowerInvariant(ch))
            {
                case 'p': type = PieceType.Pawn; break;
                case 'n': type = PieceType.Knight; break;
                case 'b': type = PieceType.Bishop; break;
                case 'r': type = PieceType.Rook; break;
                case 'q': type = PieceType.Queen; break;
                case 'k': type = PieceType.King; break;
                default: piece = 0; return false;
            }
            piece = Make(type, color);
            return true;
        }
    }
}
