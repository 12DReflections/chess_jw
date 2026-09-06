using Chess4D.Core;

namespace Chess4D.Engine
{
    /// <summary>
    /// Static evaluation from the side to move's point of view, in centipawns.
    /// Material plus mobility. PROVISIONAL: the piece values are the standard 2D
    /// values as a placeholder; true 4D values are unknown (spec Stage 5).
    /// </summary>
    public static class Evaluation
    {
        // PROVISIONAL 2D values, indexed by PieceType. Nobody knows the 4D values yet.
        public static readonly int[] PieceValue = { 0, 100, 320, 330, 500, 900, 0 };
        public const int MobilityWeight = 2;
        public const int MateScore = 100000;
        public const int Infinity = 1000000;

        public static bool IsMateScore(int score) { return score > MateScore - 1000 || score < -MateScore + 1000; }

        public static int Material(Board board, Color side)
        {
            int sum = 0;
            var cells = new System.Collections.Generic.List<int>(300);
            board.GetPieceCells(side, cells);
            foreach (int cell in cells) sum += PieceValue[(int)Piece.TypeOf(board.GetPiece(cell))];
            return sum;
        }

        /// <summary>Material difference plus a small mobility term, for the side to move.</summary>
        public static int Evaluate(Board board, MoveList scratchUs, MoveList scratchThem, System.Collections.Generic.List<int> cellScratch)
        {
            Color us = board.SideToMove;
            Color them = Piece.Opposite(us);
            int score = 0;
            board.GetPieceCells(us, cellScratch);
            foreach (int cell in cellScratch) score += PieceValue[(int)Piece.TypeOf(board.GetPiece(cell))];
            board.GetPieceCells(them, cellScratch);
            foreach (int cell in cellScratch) score -= PieceValue[(int)Piece.TypeOf(board.GetPiece(cell))];
            board.GeneratePseudoLegalFor(us, scratchUs);
            board.GeneratePseudoLegalFor(them, scratchThem);
            score += MobilityWeight * (scratchUs.Count - scratchThem.Count);
            return score;
        }
    }
}
