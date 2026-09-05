using System;

namespace Chess4D.Core
{
    /// <summary>
    /// Forsyth-Edwards Notation for the two-dimensional configuration only.
    /// Exists so published perft positions can be loaded as test fixtures. File a..h
    /// maps to x 0..7 and rank 1..8 maps to y 0..7; White advances +y.
    /// </summary>
    public static class Fen
    {
        public const string StartPosition = "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1";

        public static void Load(Board board, string fen)
        {
            BoardGeometry g = board.G;
            if (g.Dimensions != 2 || g.Side != 8) throw new InvalidOperationException("FEN is defined for the 8x8 two-dimensional board only");
            string[] parts = fen.Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2) throw new FormatException("FEN needs at least placement and side to move");

            board.Clear();
            string[] ranks = parts[0].Split('/');
            if (ranks.Length != 8) throw new FormatException("FEN placement needs 8 ranks");
            for (int r = 0; r < 8; r++)
            {
                int y = 7 - r;
                int x = 0;
                foreach (char ch in ranks[r])
                {
                    if (char.IsDigit(ch)) { x += ch - '0'; continue; }
                    if (!Piece.TryFromChar(ch, out byte p)) throw new FormatException("Bad piece char " + ch);
                    bool moved = true;
                    if (Piece.TypeOf(p) == PieceType.Pawn) moved = y != (Piece.ColorOf(p) == Color.White ? 1 : 6);
                    if (Piece.TypeOf(p) == PieceType.King || Piece.TypeOf(p) == PieceType.Rook) moved = false; // fixed below from castling rights
                    board.PlacePiece(g.CellOf(x, y), moved ? Piece.WithMoved(p) : p);
                    x++;
                }
            }

            string castling = parts.Length > 2 ? parts[2] : "-";
            MarkCastling(board, Color.White, castling.Contains("K"), castling.Contains("Q"));
            MarkCastling(board, Color.Black, castling.Contains("k"), castling.Contains("q"));

            board.SetSideToMove(parts[1] == "w" ? Color.White : Color.Black);

            string ep = parts.Length > 3 ? parts[3] : "-";
            if (ep != "-" && ep.Length == 2)
                board.SetEnPassantCell(g.CellOf(ep[0] - 'a', ep[1] - '1'));

            if (parts.Length > 4 && int.TryParse(parts[4], out int half)) board.SetHalfmoveClock(half);
        }

        /// <summary>Castling rights become moved bits: a king with no rights is marked moved; a rook without its right is marked moved.</summary>
        private static void MarkCastling(Board board, Color c, bool kingSide, bool queenSide)
        {
            BoardGeometry g = board.G;
            int y = c == Color.White ? 0 : 7;
            int k = g.CellOf(4, y);
            byte king = board.GetPiece(k);
            bool kingHome = Piece.Is(king, PieceType.King, c);
            if (!kingHome || (!kingSide && !queenSide))
            {
                foreach (int cell in new[] { k, g.CellOf(0, y), g.CellOf(7, y) })
                {
                    byte p = board.GetPiece(cell);
                    if (p != 0 && Piece.ColorOf(p) == c && (Piece.TypeOf(p) == PieceType.King || Piece.TypeOf(p) == PieceType.Rook))
                        board.PlacePiece(cell, Piece.WithMoved(p));
                }
                return;
            }
            if (!kingSide) MarkMoved(board, g.CellOf(7, y));
            if (!queenSide) MarkMoved(board, g.CellOf(0, y));
        }

        private static void MarkMoved(Board board, int cell)
        {
            byte p = board.GetPiece(cell);
            if (p != 0) board.PlacePiece(cell, Piece.WithMoved(p));
        }

        public static string Save(Board board)
        {
            BoardGeometry g = board.G;
            var sb = new System.Text.StringBuilder();
            for (int y = 7; y >= 0; y--)
            {
                int empty = 0;
                for (int x = 0; x < 8; x++)
                {
                    byte p = board.GetPiece(g.CellOf(x, y));
                    if (p == 0) { empty++; continue; }
                    if (empty > 0) { sb.Append(empty); empty = 0; }
                    sb.Append(Piece.ToChar(p));
                }
                if (empty > 0) sb.Append(empty);
                if (y > 0) sb.Append('/');
            }
            sb.Append(board.SideToMove == Color.White ? " w " : " b ");
            int rights = board.CastlingRights();
            string cs = "";
            if ((rights & 1) != 0) cs += "K";
            if ((rights & 2) != 0) cs += "Q";
            if ((rights & 4) != 0) cs += "k";
            if ((rights & 8) != 0) cs += "q";
            sb.Append(cs.Length == 0 ? "-" : cs);
            sb.Append(' ');
            if (board.EnPassantCell >= 0)
                sb.Append((char)('a' + g.Coord(board.EnPassantCell, 0))).Append((char)('1' + g.Coord(board.EnPassantCell, 1)));
            else sb.Append('-');
            sb.Append(' ').Append(board.HalfmoveClock).Append(' ').Append(board.Ply / 2 + 1);
            return sb.ToString();
        }
    }
}
