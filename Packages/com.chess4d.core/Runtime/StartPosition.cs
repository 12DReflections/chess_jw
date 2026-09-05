using System;
using System.Collections.Generic;

namespace Chess4D.Core
{
    /// <summary>
    /// Back rank plus a programmatically generated pawn shell (spec section 2).
    /// The shell is every on-board cell within Chebyshev distance 1 of a back-rank
    /// cell that is not itself a back-rank cell. At two dimensions this is the
    /// standard second rank; at four it is 136 pawns per side.
    /// </summary>
    public static class StartPosition
    {
        public static readonly PieceType[] BackRank =
        {
            PieceType.Rook, PieceType.Knight, PieceType.Bishop, PieceType.Queen,
            PieceType.King, PieceType.Bishop, PieceType.Knight, PieceType.Rook,
        };

        public static int BackRankY(BoardGeometry g, Color c) { return c == Color.White ? 0 : g.Side - 1; }

        /// <summary>Value of every axis beyond x and y for the back rank: the lower of the two central values, 3 on a side of 8.</summary>
        public static int CenterCoordinate(BoardGeometry g) { return g.Side / 2 - 1; }

        public static int[] BackRankCells(BoardGeometry g, Color c)
        {
            if (g.Side != BackRank.Length)
                throw new InvalidOperationException("The back rank pattern is defined for side " + BackRank.Length);
            int[] coords = new int[g.Dimensions];
            coords[BoardGeometry.AdvanceAxis] = BackRankY(g, c);
            for (int i = 2; i < g.Dimensions; i++) coords[i] = CenterCoordinate(g);
            int[] cells = new int[g.Side];
            for (int x = 0; x < g.Side; x++)
            {
                coords[0] = x;
                cells[x] = g.CellOf(coords);
            }
            return cells;
        }

        public static List<int> PawnShellCells(BoardGeometry g, Color c)
        {
            int[] back = BackRankCells(g, c);
            var isBack = new HashSet<int>(back);
            var shell = new List<int>();
            for (int cell = 0; cell < g.CellCount; cell++)
            {
                if (isBack.Contains(cell)) continue;
                for (int i = 0; i < back.Length; i++)
                {
                    if (g.ChebyshevDistance(cell, back[i]) <= 1) { shell.Add(cell); break; }
                }
            }
            return shell;
        }

        public static void Setup(Board board)
        {
            BoardGeometry g = board.G;
            board.Clear();
            for (int ci = 0; ci < 2; ci++)
            {
                Color c = (Color)ci;
                int[] back = BackRankCells(g, c);
                for (int x = 0; x < back.Length; x++) board.PlacePiece(back[x], Piece.Make(BackRank[x], c));
                foreach (int cell in PawnShellCells(g, c)) board.PlacePiece(cell, Piece.Make(PieceType.Pawn, c));
            }
            board.SetSideToMove(Color.White);
        }
    }
}
