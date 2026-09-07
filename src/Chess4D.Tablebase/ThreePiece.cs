using System;
using Chess4D.Core;

namespace Chess4D.Tablebase
{
    /// <summary>Fast geometry for positions with a white king, one white piece and a black king. No board object, no allocation.</summary>
    public sealed class ThreePiece
    {
        public readonly BoardGeometry G;
        public readonly PieceType WhitePiece;
        private readonly int n;

        public ThreePiece(BoardGeometry g, PieceType whitePiece)
        {
            G = g;
            WhitePiece = whitePiece;
            n = g.Dimensions;
            if (whitePiece == PieceType.Pawn || whitePiece == PieceType.King) throw new ArgumentException("Three-piece tables cover Q, R, B, N", nameof(whitePiece));
        }

        /// <summary>Does a piece of <paramref name="type"/> on <paramref name="from"/> attack <paramref name="to"/>, with at most one blocking cell?</summary>
        public bool Attacks(PieceType type, int from, int to, int blocker)
        {
            if (from == to) return false;
            int nonzero = 0, maxAbs = 0, axisA = -1, axisB = -1;
            for (int i = 0; i < n; i++)
            {
                int d = G.Coord(to, i) - G.Coord(from, i);
                if (d == 0) continue;
                int a = d < 0 ? -d : d;
                if (a > maxAbs) maxAbs = a;
                if (nonzero == 0) axisA = i; else if (nonzero == 1) axisB = i;
                nonzero++;
            }
            switch (type)
            {
                case PieceType.King:
                    return maxAbs == 1 && nonzero <= 2;
                case PieceType.Knight:
                    {
                        if (nonzero != 2 || maxAbs != 2) return false;
                        int da = Math.Abs(G.Coord(to, axisA) - G.Coord(from, axisA));
                        int db = Math.Abs(G.Coord(to, axisB) - G.Coord(from, axisB));
                        return (da == 2 && db == 1) || (da == 1 && db == 2);
                    }
                case PieceType.Rook:
                    return nonzero == 1 && Clear(from, to, maxAbs, blocker);
                case PieceType.Bishop:
                    return nonzero == 2 && Diagonal(from, to, axisA, axisB) && Clear(from, to, maxAbs, blocker);
                case PieceType.Queen:
                    if (nonzero == 1) return Clear(from, to, maxAbs, blocker);
                    return nonzero == 2 && Diagonal(from, to, axisA, axisB) && Clear(from, to, maxAbs, blocker);
            }
            return false;
        }

        private bool Diagonal(int from, int to, int a, int b)
        {
            return Math.Abs(G.Coord(to, a) - G.Coord(from, a)) == Math.Abs(G.Coord(to, b) - G.Coord(from, b));
        }

        private bool Clear(int from, int to, int steps, int blocker)
        {
            if (blocker < 0 || steps <= 1) return true;
            int delta = 0;
            for (int i = 0; i < n; i++)
            {
                int d = G.Coord(to, i) - G.Coord(from, i);
                delta += (d == 0 ? 0 : d > 0 ? 1 : -1) * G.Stride[i];
            }
            int cell = from;
            for (int k = 1; k < steps; k++)
            {
                cell += delta;
                if (cell == blocker) return false;
            }
            return true;
        }

        /// <summary>Is the black king on <paramref name="bk"/> attacked by White (king on wk, piece on wx)? The black king is never its own blocker.</summary>
        public bool BlackKingAttacked(int wk, int wx, int bk)
        {
            return Attacks(PieceType.King, wk, bk, -1) || Attacks(WhitePiece, wx, bk, wk);
        }

        public bool KingsAdjacent(int wk, int bk) { return Attacks(PieceType.King, wk, bk, -1); }
    }
}
