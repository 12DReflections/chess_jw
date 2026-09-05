using System;
using System.Collections.Generic;
using System.Text;

namespace Chess4D.Core
{
    /// <summary>
    /// Plain-text position format, version 1. Tuples only, per the spec's rule
    /// that no notation is used before approval. One item per line:
    ///
    ///   chess4d-position 1
    ///   dimensions 4
    ///   side 8
    ///   to-move white
    ///   en-passant -            (or a cell tuple)
    ///   halfmove 0
    ///   W K (4,0,3,3)           (append "moved" for a piece that has moved)
    ///   B P (3,6,3,3) moved
    ///
    /// Lines starting with # are comments. Order of piece lines is irrelevant.
    /// </summary>
    public static class PositionText
    {
        public const string Header = "chess4d-position";

        public static string Save(Board board)
        {
            BoardGeometry g = board.G;
            var sb = new StringBuilder();
            sb.Append(Header).Append(" 1\n");
            sb.Append("dimensions ").Append(g.Dimensions).Append('\n');
            sb.Append("side ").Append(g.Side).Append('\n');
            sb.Append("to-move ").Append(board.SideToMove == Color.White ? "white" : "black").Append('\n');
            sb.Append("en-passant ").Append(board.EnPassantCell >= 0 ? g.CoordOf(board.EnPassantCell).ToString() : "-").Append('\n');
            sb.Append("halfmove ").Append(board.HalfmoveClock).Append('\n');
            var cells = new List<int>();
            for (int c = 0; c < 2; c++)
            {
                board.GetPieceCells((Color)c, cells);
                cells.Sort();
                foreach (int cell in cells)
                {
                    byte p = board.GetPiece(cell);
                    sb.Append(c == 0 ? "W " : "B ");
                    sb.Append(char.ToUpperInvariant(Piece.ToChar(Piece.Make(Piece.TypeOf(p), Color.White))));
                    sb.Append(' ').Append(g.CoordOf(cell).ToString());
                    if (Piece.HasMoved(p)) sb.Append(" moved");
                    sb.Append('\n');
                }
            }
            return sb.ToString();
        }

        /// <summary>Replaces the board contents. Throws FormatException on bad input; the board is left cleared in that case.</summary>
        public static void Load(Board board, string text)
        {
            BoardGeometry g = board.G;
            string[] lines = text.Replace("\r", "").Split('\n');
            if (lines.Length == 0 || !lines[0].Trim().StartsWith(Header)) throw new FormatException("Not a " + Header + " file");
            board.Clear();
            Color toMove = Color.White;
            int ep = -1;
            int halfmove = 0;
            foreach (string raw in lines)
            {
                string line = raw.Trim();
                if (line.Length == 0 || line.StartsWith("#") || line.StartsWith(Header)) continue;
                string[] parts = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                switch (parts[0])
                {
                    case "dimensions":
                        if (int.Parse(parts[1]) != g.Dimensions) throw new FormatException("Position has " + parts[1] + " dimensions, board has " + g.Dimensions);
                        break;
                    case "side":
                        if (int.Parse(parts[1]) != g.Side) throw new FormatException("Position has side " + parts[1] + ", board has " + g.Side);
                        break;
                    case "to-move":
                        toMove = parts[1] == "black" ? Color.Black : Color.White;
                        break;
                    case "en-passant":
                        if (parts[1] != "-")
                        {
                            if (!Coord.TryParse(parts[1], g.Dimensions, out Coord epc)) throw new FormatException("Bad en-passant cell " + parts[1]);
                            ep = g.CellOf(epc);
                        }
                        break;
                    case "halfmove":
                        halfmove = int.Parse(parts[1]);
                        break;
                    case "W":
                    case "B":
                        {
                            if (parts.Length < 3) throw new FormatException("Bad piece line: " + line);
                            Color color = parts[0] == "W" ? Color.White : Color.Black;
                            if (!Piece.TryFromChar(char.ToUpperInvariant(parts[1][0]), out byte pt)) throw new FormatException("Bad piece type in: " + line);
                            if (!Coord.TryParse(parts[2], g.Dimensions, out Coord c)) throw new FormatException("Bad coordinate in: " + line);
                            bool moved = parts.Length > 3 && parts[3] == "moved";
                            board.PlacePiece(g.CellOf(c), Piece.Make(Piece.TypeOf(pt), color, moved));
                            break;
                        }
                    default:
                        throw new FormatException("Unknown line: " + line);
                }
            }
            board.SetSideToMove(toMove);
            board.SetEnPassantCell(ep);
            board.SetHalfmoveClock(halfmove);
        }
    }
}
