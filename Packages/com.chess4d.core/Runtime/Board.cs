using System;
using System.Collections.Generic;

namespace Chess4D.Core
{
    /// <summary>
    /// Mailbox board of size side^dimensions with a per-colour piece list.
    /// Make/unmake on a single instance; the board is never copied to search.
    /// All rules live here and in the geometry. No UnityEngine anywhere.
    /// </summary>
    public sealed class Board
    {
        public readonly BoardGeometry G;
        public readonly Zobrist Zobrist;
        public GameRules Rules = new GameRules();

        private readonly byte[] cells;
        private readonly int[][] pieceCells = new int[2][];
        private readonly int[] pieceCount = new int[2];
        private readonly int[] slotOfCell;
        private readonly int[] kingCell = { -1, -1 };

        public Color SideToMove { get; private set; }
        /// <summary>The cell a pawn just passed through on a double step, or -1. An enemy pawn may capture onto it.</summary>
        public int EnPassantCell { get; private set; }
        public int HalfmoveClock { get; private set; }
        /// <summary>Moves made since the last setup.</summary>
        public int Ply { get; private set; }
        public ulong Hash { get; private set; }

        private struct Undo
        {
            public Move Move;
            public byte MoverBefore;
            public byte Captured;
            public byte RookBefore;
            public int PrevEnPassant;
            public int PrevHalfmove;
            public ulong PrevHash;
        }

        private Undo[] undo = new Undo[128];
        private int undoCount;
        private ulong[] history = new ulong[256];
        private readonly MoveList scratch = new MoveList(512);

        public Board(BoardGeometry geometry)
        {
            G = geometry;
            Zobrist = new Zobrist(geometry.CellCount);
            cells = new byte[geometry.CellCount];
            slotOfCell = new int[geometry.CellCount];
            pieceCells[0] = new int[geometry.CellCount];
            pieceCells[1] = new int[geometry.CellCount];
            EnPassantCell = -1;
            ResetHistory();
        }

        public Board(int dimensions, int side) : this(new BoardGeometry(dimensions, side)) { }

        // ---------------------------------------------------------------- queries

        public byte GetPiece(int cell) { return cells[cell]; }
        public byte GetPiece(Coord c) { return cells[G.CellOf(c)]; }
        public int PieceCount(Color c) { return pieceCount[(int)c]; }
        public int KingCell(Color c) { return kingCell[(int)c]; }

        public void GetPieceCells(Color c, List<int> output)
        {
            output.Clear();
            int n = pieceCount[(int)c];
            int[] pcs = pieceCells[(int)c];
            for (int i = 0; i < n; i++) output.Add(pcs[i]);
        }

        public bool InCheck()
        {
            int k = kingCell[(int)SideToMove];
            return k >= 0 && IsAttacked(k, Piece.Opposite(SideToMove));
        }

        /// <summary>Castling rights derived from the moved bits of kings and their corner rooks. Bit (colour*2 + side), side 0 = high-x rook, 1 = low-x rook.</summary>
        public int CastlingRights()
        {
            int rights = 0;
            for (int c = 0; c < 2; c++)
            {
                int k = kingCell[c];
                if (k < 0) continue;
                byte kp = cells[k];
                if (Piece.HasMoved(kp)) continue;
                int kx = G.Coord(k, 0);
                for (int side = 0; side < 2; side++)
                {
                    int rookX = side == 0 ? G.Side - 1 : 0;
                    if (Math.Abs(rookX - kx) < 3) continue;
                    byte r = cells[G.WithCoord(k, 0, rookX)];
                    if (Piece.Is(r, PieceType.Rook, (Color)c) && !Piece.HasMoved(r)) rights |= 1 << (c * 2 + side);
                }
            }
            return rights;
        }

        // ---------------------------------------------------------------- editing

        public void Clear()
        {
            Array.Clear(cells, 0, cells.Length);
            pieceCount[0] = pieceCount[1] = 0;
            kingCell[0] = kingCell[1] = -1;
            SideToMove = Color.White;
            EnPassantCell = -1;
            HalfmoveClock = 0;
            RecomputeHash();
            ResetHistory();
        }

        /// <summary>Places a piece, replacing whatever was there. Editor use; resets move history.</summary>
        public void PlacePiece(int cell, byte piece)
        {
            if (cells[cell] != 0) RemovePiece(cell);
            if (piece == 0) return;
            cells[cell] = piece;
            AddToList(cell, Piece.ColorOf(piece));
            if (Piece.TypeOf(piece) == PieceType.King) kingCell[(int)Piece.ColorOf(piece)] = cell;
            RecomputeHash();
            ResetHistory();
        }

        public void PlacePiece(Coord c, PieceType type, Color color, bool moved = false)
        {
            PlacePiece(G.CellOf(c), Piece.Make(type, color, moved));
        }

        public void RemovePiece(int cell)
        {
            byte p = cells[cell];
            if (p == 0) return;
            Color c = Piece.ColorOf(p);
            RemoveFromList(cell, c);
            cells[cell] = 0;
            if (kingCell[(int)c] == cell) kingCell[(int)c] = FindKing(c);
            RecomputeHash();
            ResetHistory();
        }

        public void SetSideToMove(Color c)
        {
            SideToMove = c;
            RecomputeHash();
            ResetHistory();
        }

        public void SetEnPassantCell(int cell)
        {
            EnPassantCell = cell;
            RecomputeHash();
            ResetHistory();
        }

        public void SetHalfmoveClock(int value)
        {
            HalfmoveClock = value;
        }

        private int FindKing(Color c)
        {
            int n = pieceCount[(int)c];
            int[] pcs = pieceCells[(int)c];
            for (int i = 0; i < n; i++) if (Piece.TypeOf(cells[pcs[i]]) == PieceType.King) return pcs[i];
            return -1;
        }

        private void ResetHistory()
        {
            undoCount = 0;
            Ply = 0;
            history[0] = Hash;
        }

        /// <summary>Copies another board's position (cells, side to move, en passant, halfmove clock) into this one. History is reset. One copy per search is fine; copying per node is not.</summary>
        public void CopyFrom(Board other)
        {
            if (other.G.Dimensions != G.Dimensions || other.G.Side != G.Side) throw new ArgumentException("Geometry mismatch", nameof(other));
            Array.Clear(cells, 0, cells.Length);
            pieceCount[0] = pieceCount[1] = 0;
            kingCell[0] = kingCell[1] = -1;
            for (int cell = 0; cell < cells.Length; cell++)
            {
                byte p = other.cells[cell];
                if (p == 0) continue;
                cells[cell] = p;
                AddToList(cell, Piece.ColorOf(p));
                if (Piece.TypeOf(p) == PieceType.King) kingCell[(int)Piece.ColorOf(p)] = cell;
            }
            SideToMove = other.SideToMove;
            EnPassantCell = other.EnPassantCell;
            HalfmoveClock = other.HalfmoveClock;
            Hash = other.Hash;
            ResetHistory();
        }

        /// <summary>Full recomputation, for setup and for verifying the incremental hash in tests.</summary>
        public ulong ComputeHash()
        {
            ulong h = 0;
            for (int cell = 0; cell < cells.Length; cell++)
            {
                byte p = cells[cell];
                if (p != 0) h ^= Zobrist.Piece(cell, p);
            }
            if (SideToMove == Color.Black) h ^= Zobrist.Side;
            if (EnPassantCell >= 0) h ^= Zobrist.EnPassant(EnPassantCell);
            h ^= Zobrist.Castling(CastlingRights());
            return h;
        }

        private void RecomputeHash() { Hash = ComputeHash(); }

        // ---------------------------------------------------------------- piece list

        private void AddToList(int cell, Color c)
        {
            int s = pieceCount[(int)c]++;
            pieceCells[(int)c][s] = cell;
            slotOfCell[cell] = s;
        }

        private void RemoveFromList(int cell, Color c)
        {
            int[] pcs = pieceCells[(int)c];
            int s = slotOfCell[cell];
            int last = --pieceCount[(int)c];
            int lastCell = pcs[last];
            pcs[s] = lastCell;
            slotOfCell[lastCell] = s;
        }

        private void MoveInList(int from, int to, Color c)
        {
            int s = slotOfCell[from];
            pieceCells[(int)c][s] = to;
            slotOfCell[to] = s;
        }

        // ---------------------------------------------------------------- make / unmake

        public void Make(in Move m)
        {
            if (undoCount == undo.Length) Array.Resize(ref undo, undo.Length * 2);
            ref Undo u = ref undo[undoCount++];

            Color us = SideToMove;
            Color them = Piece.Opposite(us);
            byte mover = cells[m.From];

            u.Move = m;
            u.MoverBefore = mover;
            u.Captured = 0;
            u.RookBefore = 0;
            u.PrevEnPassant = EnPassantCell;
            u.PrevHalfmove = HalfmoveClock;
            u.PrevHash = Hash;

            int castlingBefore = CastlingRights();

            int capturedCell = m.IsEnPassant ? m.Aux1 : (m.IsCapture ? m.To : -1);
            if (capturedCell >= 0)
            {
                byte cap = cells[capturedCell];
                u.Captured = cap;
                RemoveFromList(capturedCell, them);
                cells[capturedCell] = 0;
                Hash ^= Zobrist.Piece(capturedCell, cap);
            }

            cells[m.From] = 0;
            Hash ^= Zobrist.Piece(m.From, mover);
            byte placed = m.IsPromotion ? Piece.Make(m.Promotion, us, true) : Piece.WithMoved(mover);
            cells[m.To] = placed;
            Hash ^= Zobrist.Piece(m.To, placed);
            MoveInList(m.From, m.To, us);
            if (Piece.TypeOf(mover) == PieceType.King) kingCell[(int)us] = m.To;

            if (m.IsCastle)
            {
                byte rook = cells[m.Aux1];
                u.RookBefore = rook;
                cells[m.Aux1] = 0;
                Hash ^= Zobrist.Piece(m.Aux1, rook);
                byte movedRook = Piece.WithMoved(rook);
                cells[m.Aux2] = movedRook;
                Hash ^= Zobrist.Piece(m.Aux2, movedRook);
                MoveInList(m.Aux1, m.Aux2, us);
            }

            if (EnPassantCell >= 0) Hash ^= Zobrist.EnPassant(EnPassantCell);
            EnPassantCell = m.IsDoubleStep ? m.Aux1 : -1;
            if (EnPassantCell >= 0) Hash ^= Zobrist.EnPassant(EnPassantCell);

            int castlingAfter = CastlingRights();
            if (castlingAfter != castlingBefore) Hash ^= Zobrist.Castling(castlingBefore) ^ Zobrist.Castling(castlingAfter);

            HalfmoveClock = (Piece.TypeOf(mover) == PieceType.Pawn || capturedCell >= 0) ? 0 : HalfmoveClock + 1;

            SideToMove = them;
            Hash ^= Zobrist.Side;

            Ply++;
            if (Ply == history.Length) Array.Resize(ref history, history.Length * 2);
            history[Ply] = Hash;
        }

        public void Unmake()
        {
            if (undoCount == 0) throw new InvalidOperationException("Nothing to unmake");
            ref Undo u = ref undo[--undoCount];
            Move m = u.Move;
            Color them = SideToMove;
            Color us = Piece.Opposite(them);

            if (m.IsCastle)
            {
                cells[m.Aux2] = 0;
                cells[m.Aux1] = u.RookBefore;
                MoveInList(m.Aux2, m.Aux1, us);
            }

            cells[m.To] = 0;
            cells[m.From] = u.MoverBefore;
            MoveInList(m.To, m.From, us);
            if (Piece.TypeOf(u.MoverBefore) == PieceType.King) kingCell[(int)us] = m.From;

            if (u.Captured != 0)
            {
                int capturedCell = m.IsEnPassant ? m.Aux1 : m.To;
                cells[capturedCell] = u.Captured;
                AddToList(capturedCell, them);
            }

            EnPassantCell = u.PrevEnPassant;
            HalfmoveClock = u.PrevHalfmove;
            Hash = u.PrevHash;
            SideToMove = us;
            Ply--;
        }

        // ---------------------------------------------------------------- attacks

        /// <summary>Is <paramref name="cell"/> attacked by any piece of colour <paramref name="by"/>? Real ray walk in every direction; nothing is approximated.</summary>
        public bool IsAttacked(int cell, Color by)
        {
            Direction[] q = G.Queen;
            int rookCount = G.Rook.Length;
            bool[] kingDir = G.QueenIsKing;
            for (int i = 0; i < q.Length; i++)
            {
                Direction d = q[i];
                int c = G.Step(cell, d);
                bool first = true;
                while (c >= 0)
                {
                    byte p = cells[c];
                    if (p != 0)
                    {
                        if (Piece.ColorOf(p) == by)
                        {
                            PieceType t = Piece.TypeOf(p);
                            if (t == PieceType.Queen) return true;
                            if (i < rookCount ? t == PieceType.Rook : t == PieceType.Bishop) return true;
                            if (first && kingDir[i] && t == PieceType.King) return true;
                        }
                        break;
                    }
                    first = false;
                    c = G.Step(c, d);
                }
            }

            Direction[] kn = G.Knight;
            for (int i = 0; i < kn.Length; i++)
            {
                int c = G.Step(cell, kn[i]);
                if (c >= 0 && Piece.Is(cells[c], PieceType.Knight, by)) return true;
            }

            Direction[] pr = G.PawnCaptureReverse[(int)by];
            for (int i = 0; i < pr.Length; i++)
            {
                int c = G.Step(cell, pr[i]);
                if (c >= 0 && Piece.Is(cells[c], PieceType.Pawn, by)) return true;
            }
            return false;
        }

        /// <summary>Appends every cell holding a piece of colour <paramref name="by"/> that attacks <paramref name="cell"/>. For the UI's threat display.</summary>
        public void Attackers(int cell, Color by, List<int> output)
        {
            Direction[] q = G.Queen;
            int rookCount = G.Rook.Length;
            bool[] kingDir = G.QueenIsKing;
            for (int i = 0; i < q.Length; i++)
            {
                Direction d = q[i];
                int c = G.Step(cell, d);
                bool first = true;
                while (c >= 0)
                {
                    byte p = cells[c];
                    if (p != 0)
                    {
                        if (Piece.ColorOf(p) == by)
                        {
                            PieceType t = Piece.TypeOf(p);
                            if (t == PieceType.Queen || (i < rookCount ? t == PieceType.Rook : t == PieceType.Bishop) || (first && kingDir[i] && t == PieceType.King))
                                output.Add(c);
                        }
                        break;
                    }
                    first = false;
                    c = G.Step(c, d);
                }
            }
            Direction[] kn = G.Knight;
            for (int i = 0; i < kn.Length; i++)
            {
                int c = G.Step(cell, kn[i]);
                if (c >= 0 && Piece.Is(cells[c], PieceType.Knight, by)) output.Add(c);
            }
            Direction[] pr = G.PawnCaptureReverse[(int)by];
            for (int i = 0; i < pr.Length; i++)
            {
                int c = G.Step(cell, pr[i]);
                if (c >= 0 && Piece.Is(cells[c], PieceType.Pawn, by)) output.Add(c);
            }
        }

        // ---------------------------------------------------------------- move generation

        /// <summary>All pseudo-legal moves for the side to move. Castling already respects the attack conditions; king safety after the move is not checked here.</summary>
        public void GeneratePseudoLegal(MoveList list) { GeneratePseudoLegalFor(SideToMove, list); }

        /// <summary>Pseudo-legal moves for either colour, for mobility evaluation. En passant is only offered to the side to move.</summary>
        public void GeneratePseudoLegalFor(Color us, MoveList list)
        {
            list.Clear();
            Color them = Piece.Opposite(us);
            int n = pieceCount[(int)us];
            int[] pcs = pieceCells[(int)us];
            for (int k = 0; k < n; k++)
            {
                int from = pcs[k];
                byte p = cells[from];
                switch (Piece.TypeOf(p))
                {
                    case PieceType.Pawn: GenPawn(from, p, us, them, list); break;
                    case PieceType.Knight: GenJumps(from, G.Knight, them, list); break;
                    case PieceType.Bishop: GenSlides(from, G.Bishop, them, list); break;
                    case PieceType.Rook: GenSlides(from, G.Rook, them, list); break;
                    case PieceType.Queen: GenSlides(from, G.Queen, them, list); break;
                    case PieceType.King:
                        GenJumps(from, G.King, them, list);
                        GenCastling(from, p, us, them, list);
                        break;
                }
            }
        }

        /// <summary>All legal moves: pseudo-legal, then make, attack test on own king, unmake.</summary>
        public void GenerateLegal(MoveList list)
        {
            GeneratePseudoLegal(scratch);
            list.Clear();
            Color us = SideToMove;
            Color them = Piece.Opposite(us);
            for (int i = 0; i < scratch.Count; i++)
            {
                Move m = scratch[i];
                Make(m);
                int k = kingCell[(int)us];
                bool legal = k < 0 || !IsAttacked(k, them);
                Unmake();
                if (legal) list.Add(m);
            }
        }

        public bool IsLegal(in Move m)
        {
            GeneratePseudoLegal(scratch);
            if (!scratch.Contains(m)) return false;
            Color us = SideToMove;
            Make(m);
            int k = kingCell[(int)us];
            bool legal = k < 0 || !IsAttacked(k, Piece.Opposite(us));
            Unmake();
            return legal;
        }

        private void GenJumps(int from, Direction[] dirs, Color them, MoveList list)
        {
            for (int i = 0; i < dirs.Length; i++)
            {
                int to = G.Step(from, dirs[i]);
                if (to < 0) continue;
                byte q = cells[to];
                if (q == 0) list.Add(new Move(from, to));
                else if (Piece.ColorOf(q) == them) list.Add(new Move(from, to, PieceType.None, MoveFlags.Capture));
            }
        }

        private void GenSlides(int from, Direction[] dirs, Color them, MoveList list)
        {
            for (int i = 0; i < dirs.Length; i++)
            {
                Direction d = dirs[i];
                int to = G.Step(from, d);
                while (to >= 0)
                {
                    byte q = cells[to];
                    if (q == 0) list.Add(new Move(from, to));
                    else
                    {
                        if (Piece.ColorOf(q) == them) list.Add(new Move(from, to, PieceType.None, MoveFlags.Capture));
                        break;
                    }
                    to = G.Step(to, d);
                }
            }
        }

        private void GenPawn(int from, byte p, Color us, Color them, MoveList list)
        {
            Direction fwd = G.PawnForward[(int)us];
            int one = G.Step(from, fwd);
            if (one >= 0 && cells[one] == 0)
            {
                AddPawnMove(from, one, us, MoveFlags.None, list);
                if (!Piece.HasMoved(p))
                {
                    int two = G.Step(one, fwd);
                    if (two >= 0 && cells[two] == 0)
                        list.Add(new Move(from, two, PieceType.None, MoveFlags.DoubleStep, one));
                }
            }
            Direction[] caps = G.PawnCapture[(int)us];
            for (int i = 0; i < caps.Length; i++)
            {
                int to = G.Step(from, caps[i]);
                if (to < 0) continue;
                byte q = cells[to];
                if (q != 0)
                {
                    if (Piece.ColorOf(q) == them) AddPawnMove(from, to, us, MoveFlags.Capture, list);
                }
                else if (to == EnPassantCell && us == SideToMove)
                {
                    int capturedCell = G.Step(to, G.PawnForward[(int)them]);
                    if (capturedCell >= 0 && Piece.Is(cells[capturedCell], PieceType.Pawn, them))
                        list.Add(new Move(from, to, PieceType.None, MoveFlags.Capture | MoveFlags.EnPassant, capturedCell));
                }
            }
        }

        private void AddPawnMove(int from, int to, Color us, MoveFlags flags, MoveList list)
        {
            int promotionY = us == Color.White ? G.Side - 1 : 0;
            if (G.Coord(to, BoardGeometry.AdvanceAxis) == promotionY)
            {
                list.Add(new Move(from, to, PieceType.Queen, flags));
                list.Add(new Move(from, to, PieceType.Rook, flags));
                list.Add(new Move(from, to, PieceType.Bishop, flags));
                list.Add(new Move(from, to, PieceType.Knight, flags));
            }
            else list.Add(new Move(from, to, PieceType.None, flags));
        }

        /// <summary>Castling along the x axis at the king's own y,z,w. Spec section 2: rook to the cell the king crosses, all conditions as in standard chess.</summary>
        private void GenCastling(int from, byte king, Color us, Color them, MoveList list)
        {
            if (Piece.HasMoved(king)) return;
            int kx = G.Coord(from, 0);
            int s0 = G.Stride[0];
            for (int side = 1; side >= -1; side -= 2)
            {
                int rookX = side > 0 ? G.Side - 1 : 0;
                if (Math.Abs(rookX - kx) < 3) continue;
                int rookCell = from + (rookX - kx) * s0;
                byte r = cells[rookCell];
                if (!Piece.Is(r, PieceType.Rook, us) || Piece.HasMoved(r)) continue;

                bool clear = true;
                for (int x = kx + side; x != rookX; x += side)
                {
                    if (cells[from + (x - kx) * s0] != 0) { clear = false; break; }
                }
                if (!clear) continue;

                int pass = from + side * s0;
                int land = from + 2 * side * s0;
                if (IsAttacked(from, them) || IsAttacked(pass, them) || IsAttacked(land, them)) continue;

                list.Add(new Move(from, land, PieceType.None, MoveFlags.Castle, rookCell, pass));
            }
        }

        // ---------------------------------------------------------------- game end

        public int RepetitionCount()
        {
            int count = 0;
            for (int i = 0; i <= Ply; i++) if (history[i] == Hash) count++;
            return count;
        }

        public GameStatus GetStatus()
        {
            var legal = new MoveList(64);
            GenerateLegal(legal);
            return GetStatus(legal.Count);
        }

        /// <summary>Status given a precomputed legal move count for the side to move.</summary>
        public GameStatus GetStatus(int legalMoveCount)
        {
            if (legalMoveCount == 0) return InCheck() ? GameStatus.Checkmate : GameStatus.Stalemate;
            if (Rules.FiftyMoveRule && HalfmoveClock >= 100) return GameStatus.DrawFiftyMove;
            if (Rules.ThreefoldRepetition && RepetitionCount() >= 3) return GameStatus.DrawRepetition;
            return GameStatus.Ongoing;
        }

        // ---------------------------------------------------------------- debug

        /// <summary>Text dump of the x-y plane at the given values of the remaining axes, y descending like a chess diagram.</summary>
        public string DumpPlane(params int[] otherAxes)
        {
            var sb = new System.Text.StringBuilder();
            int[] coords = new int[G.Dimensions];
            for (int i = 2; i < G.Dimensions; i++) coords[i] = i - 2 < otherAxes.Length ? otherAxes[i - 2] : 0;
            for (int y = G.Side - 1; y >= 0; y--)
            {
                for (int x = 0; x < G.Side; x++)
                {
                    coords[0] = x; coords[1] = y;
                    sb.Append(Piece.ToChar(cells[G.CellOf(coords)]));
                    sb.Append(' ');
                }
                sb.Append('\n');
            }
            return sb.ToString();
        }
    }
}
