using System.Collections.Generic;
using Chess4D.Core;

namespace Chess4D.Engine
{
    /// <summary>
    /// AttackMapService: move generation only, no search. For every cell, how many
    /// pieces of each colour attack it. Recomputed once per turn for both sides;
    /// it is what makes threats along hidden axes visible in the UI. A colour's
    /// attack on a cell holding its own piece counts as a defence.
    /// </summary>
    public sealed class AttackMap
    {
        private readonly Board board;
        private readonly int[][] count = new int[2][];
        private readonly List<int> cells = new List<int>(300);
        public ulong ComputedForHash { get; private set; }

        public AttackMap(Board board)
        {
            this.board = board;
            count[0] = new int[board.G.CellCount];
            count[1] = new int[board.G.CellCount];
        }

        public Board Board { get { return board; } }

        /// <summary>Recomputes both colours. Cheap: every piece walks its attack pattern once.</summary>
        public void Compute()
        {
            System.Array.Clear(count[0], 0, count[0].Length);
            System.Array.Clear(count[1], 0, count[1].Length);
            for (int c = 0; c < 2; c++)
            {
                board.GetPieceCells((Color)c, cells);
                int[] table = count[c];
                foreach (int cell in cells) AddAttacks(cell, board.GetPiece(cell), table);
            }
            ComputedForHash = board.Hash;
        }

        /// <summary>Recompute only if the position changed since the last compute.</summary>
        public void Refresh() { if (ComputedForHash != board.Hash) Compute(); }

        private void AddAttacks(int from, byte p, int[] table)
        {
            BoardGeometry g = board.G;
            switch (Piece.TypeOf(p))
            {
                case PieceType.Pawn:
                    foreach (var d in g.PawnCapture[(int)Piece.ColorOf(p)]) { int t = g.Step(from, d); if (t >= 0) table[t]++; }
                    break;
                case PieceType.Knight:
                    foreach (var d in g.Knight) { int t = g.Step(from, d); if (t >= 0) table[t]++; }
                    break;
                case PieceType.King:
                    foreach (var d in g.King) { int t = g.Step(from, d); if (t >= 0) table[t]++; }
                    break;
                case PieceType.Bishop: Slide(from, g.Bishop, table); break;
                case PieceType.Rook: Slide(from, g.Rook, table); break;
                case PieceType.Queen: Slide(from, g.Queen, table); break;
            }
        }

        private void Slide(int from, Direction[] dirs, int[] table)
        {
            BoardGeometry g = board.G;
            foreach (var d in dirs)
            {
                int t = g.Step(from, d);
                while (t >= 0)
                {
                    table[t]++;
                    if (board.GetPiece(t) != 0) break;
                    t = g.Step(t, d);
                }
            }
        }

        /// <summary>Number of pieces of colour <paramref name="by"/> attacking (or defending) <paramref name="cell"/>.</summary>
        public int Attackers(int cell, Color by) { return count[(int)by][cell]; }

        public bool IsAttacked(int cell, Color by) { return count[(int)by][cell] > 0; }

        /// <summary>Is the piece on <paramref name="cell"/> attacked by the other colour?</summary>
        public bool IsPieceAttacked(int cell)
        {
            byte p = board.GetPiece(cell);
            return p != 0 && count[1 - (int)Piece.ColorOf(p)][cell] > 0;
        }

        public bool IsPieceDefended(int cell)
        {
            byte p = board.GetPiece(cell);
            return p != 0 && count[(int)Piece.ColorOf(p)][cell] > 0;
        }

        public bool InCheck(Color side)
        {
            int k = board.KingCell(side);
            return k >= 0 && count[1 - (int)side][k] > 0;
        }

        /// <summary>The attacking cells themselves, for a UI list.</summary>
        public void AttackerCells(int cell, Color by, List<int> output)
        {
            output.Clear();
            board.Attackers(cell, by, output);
        }
    }
}
