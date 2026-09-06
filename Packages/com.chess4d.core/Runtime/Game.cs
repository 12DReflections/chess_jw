using System;
using System.Collections.Generic;

namespace Chess4D.Core
{
    /// <summary>
    /// A game in progress: the board plus move history with undo and redo.
    /// View operations (rotate, page, orbit, isolate) never touch this class;
    /// only moves enter the history (spec Stage 4).
    /// </summary>
    public sealed class Game
    {
        public readonly Board Board;
        private readonly List<Move> history = new List<Move>();
        private readonly List<string> historyText = new List<string>();
        private readonly List<Move> redo = new List<Move>();
        private readonly MoveList legal = new MoveList(1024);
        private bool legalDirty = true;

        public Game(Board board) { Board = board; }

        public IReadOnlyList<Move> History { get { return history; } }
        public IReadOnlyList<string> HistoryText { get { return historyText; } }
        public int RedoCount { get { return redo.Count; } }
        public bool CanUndo { get { return history.Count > 0; } }
        public bool CanRedo { get { return redo.Count > 0; } }

        /// <summary>Legal moves for the side to move, cached until the position changes.</summary>
        public MoveList Legal
        {
            get
            {
                if (legalDirty) { Board.GenerateLegal(legal); legalDirty = false; }
                return legal;
            }
        }

        public GameStatus Status { get { return Board.GetStatus(Legal.Count); } }
        public bool IsOver { get { return Status != GameStatus.Ongoing; } }

        /// <summary>Call after editing the board directly (setup mode, load): history is discarded.</summary>
        public void ResetHistory()
        {
            history.Clear();
            historyText.Clear();
            redo.Clear();
            legalDirty = true;
        }

        public void LegalFrom(int cell, MoveList output)
        {
            output.Clear();
            MoveList all = Legal;
            for (int i = 0; i < all.Count; i++) if (all[i].From == cell) output.Add(all[i]);
        }

        /// <summary>Makes the move if it is legal in the current position.</summary>
        public bool TryMove(in Move m)
        {
            if (IsOver || !Legal.Contains(m)) return false;
            Apply(m);
            redo.Clear();
            return true;
        }

        private void Apply(in Move m)
        {
            string text = Notation.Describe(Board, m);
            Board.Make(m);
            legalDirty = true;
            GameStatus status = Status;
            if (status == GameStatus.Checkmate) text += "#";
            else if (Board.InCheck()) text += "+";
            history.Add(m);
            historyText.Add(text);
        }

        public bool Undo()
        {
            if (history.Count == 0) return false;
            Move m = history[history.Count - 1];
            history.RemoveAt(history.Count - 1);
            historyText.RemoveAt(historyText.Count - 1);
            Board.Unmake();
            legalDirty = true;
            redo.Add(m);
            return true;
        }

        public bool Redo()
        {
            if (redo.Count == 0) return false;
            Move m = redo[redo.Count - 1];
            redo.RemoveAt(redo.Count - 1);
            Apply(m);
            return true;
        }

        /// <summary>"12. W Q(3,0,3,3)-(0,3,3,3)+" style lines, newest last.</summary>
        public string HistoryLine(int index)
        {
            int number = index / 2 + 1;
            string side = index % 2 == 0 ? "W" : "B";
            return number + ". " + side + " " + historyText[index];
        }
    }

    /// <summary>
    /// Move notation (docs/NOTATION.md). Compact form for history and typed
    /// input: piece letter, from cell as digits, "-" or "x", to cell as digits,
    /// "=X" for promotion, " e.p." for en passant, "O-O" / "O-O-O" for castling
    /// toward +x / -x, with "+" and "#" appended by Game. The long form uses
    /// tuples in place of digit runs and is what save files use.
    /// </summary>
    public static class Notation
    {
        public static string Describe(Board board, in Move m) { return Describe(board, m, true); }

        public static string DescribeLong(Board board, in Move m) { return Describe(board, m, false); }

        private static string Describe(Board board, in Move m, bool compact)
        {
            BoardGeometry g = board.G;
            byte p = board.GetPiece(m.From);
            if (m.IsCastle) return g.Coord(m.To, 0) > g.Coord(m.From, 0) ? "O-O" : "O-O-O";
            var sb = new System.Text.StringBuilder();
            PieceType t = Piece.TypeOf(p);
            if (t != PieceType.Pawn) sb.Append(char.ToUpperInvariant(Piece.ToChar(Piece.Make(t, Color.White))));
            Coord from = g.CoordOf(m.From), to = g.CoordOf(m.To);
            sb.Append(compact ? from.ToCompact() : from.ToString());
            sb.Append(m.IsCapture ? "x" : "-");
            sb.Append(compact ? to.ToCompact() : to.ToString());
            if (m.IsPromotion) sb.Append('=').Append(char.ToUpperInvariant(Piece.ToChar(Piece.Make(m.Promotion, Color.White))));
            if (m.IsEnPassant) sb.Append(" e.p.");
            return sb.ToString();
        }

        /// <summary>
        /// Parses a typed move in compact or long form ("3033 0333", "Q3033-0333", "(3,0,3,3)x(0,3,3,3)",
        /// with optional "=X", trailing "+" or "#" and " e.p."), resolving flags against the legal move list.
        /// </summary>
        public static bool TryParseMove(Board board, MoveList legal, string text, out Move move)
        {
            move = default;
            if (string.IsNullOrWhiteSpace(text)) return false;
            string s = text.Trim().Replace("e.p.", "").TrimEnd('+', '#', ' ');
            PieceType promotion = PieceType.None;
            int eq = s.LastIndexOf('=');
            if (eq >= 0 && eq == s.Length - 2)
            {
                if (!Piece.TryFromChar(char.ToUpperInvariant(s[eq + 1]), out byte pp)) return false;
                promotion = Piece.TypeOf(pp);
                s = s.Substring(0, eq).TrimEnd();
            }
            if (s.Length > 1 && "KQRBN".IndexOf(s[0]) >= 0 && (char.IsDigit(s[1]) || s[1] == '(')) s = s.Substring(1);

            int dims = board.G.Dimensions;
            if (!TakeCell(ref s, dims, out Coord from)) return false;
            s = s.TrimStart(' ', '-', 'x', 'X');
            if (!TakeCell(ref s, dims, out Coord to)) return false;
            if (s.Trim().Length != 0) return false;

            int f, t;
            try { f = board.G.CellOf(from); t = board.G.CellOf(to); } catch (ArgumentException) { return false; }
            for (int i = 0; i < legal.Count; i++)
            {
                Move m = legal[i];
                if (m.From != f || m.To != t) continue;
                if (m.IsPromotion && m.Promotion != (promotion == PieceType.None ? PieceType.Queen : promotion)) continue;
                move = m;
                return true;
            }
            return false;
        }

        /// <summary>Consumes one cell from the front of <paramref name="s"/>: a parenthesised tuple or a run of exactly dims digits.</summary>
        private static bool TakeCell(ref string s, int dims, out Coord c)
        {
            c = default;
            s = s.TrimStart();
            if (s.Length == 0) return false;
            if (s[0] == '(')
            {
                int close = s.IndexOf(')');
                if (close < 0) return false;
                string tuple = s.Substring(0, close + 1);
                s = s.Substring(close + 1);
                return Coord.TryParse(tuple, dims, out c);
            }
            if (s.Length < dims) return false;
            string digits = s.Substring(0, dims);
            s = s.Substring(dims);
            return Coord.TryParseCompact(digits, dims, out c);
        }
    }
}
