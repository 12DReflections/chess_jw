namespace Chess4D.Core
{
    /// <summary>
    /// Zobrist keys: cellCount x 12 (six types, two colours) plus side to move,
    /// 16 castling-rights combinations, and one key per possible en passant cell.
    /// Generated from a fixed seed so every run is reproducible (spec Stage 1).
    /// The "has moved" bit is not hashed directly; its only hash-relevant effect,
    /// castling, enters through the castling-rights key.
    /// </summary>
    public sealed class Zobrist
    {
        public const ulong DefaultSeed = 0x4D43686573733444UL; // "MChess4D"

        private readonly ulong[] piece;
        private readonly ulong[] enPassant;
        private readonly ulong[] castling = new ulong[16];
        public readonly ulong Side;
        public readonly int CellCount;

        public Zobrist(int cellCount, ulong seed = DefaultSeed)
        {
            CellCount = cellCount;
            ulong state = seed;
            piece = new ulong[cellCount * 12];
            for (int i = 0; i < piece.Length; i++) piece[i] = Next(ref state);
            enPassant = new ulong[cellCount];
            for (int i = 0; i < enPassant.Length; i++) enPassant[i] = Next(ref state);
            for (int i = 1; i < 16; i++) castling[i] = Next(ref state); // rights == 0 hashes to 0
            Side = Next(ref state);
        }

        /// <summary>SplitMix64.</summary>
        private static ulong Next(ref ulong state)
        {
            ulong z = state += 0x9E3779B97F4A7C15UL;
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            return z ^ (z >> 31);
        }

        public ulong Piece(int cell, byte p) { return piece[cell * 12 + Core.Piece.Index12(p)]; }
        public ulong EnPassant(int cell) { return enPassant[cell]; }
        public ulong Castling(int rights) { return castling[rights]; }
    }
}
