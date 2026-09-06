using System;

namespace Chess4D.Core
{
    [Flags]
    public enum MoveFlags : byte
    {
        None = 0,
        Capture = 1,
        DoubleStep = 2,
        EnPassant = 4,
        Castle = 8,
    }

    /// <summary>
    /// One move. Aux1/Aux2 meaning depends on the flag:
    /// DoubleStep: Aux1 = the passed-through cell (becomes the en passant target).
    /// EnPassant: Aux1 = the cell of the captured pawn.
    /// Castle: Aux1 = rook from, Aux2 = rook to.
    /// </summary>
    public readonly struct Move : IEquatable<Move>
    {
        public readonly int From;
        public readonly int To;
        public readonly int Aux1;
        public readonly int Aux2;
        public readonly PieceType Promotion;
        public readonly MoveFlags Flags;

        public Move(int from, int to, PieceType promotion = PieceType.None, MoveFlags flags = MoveFlags.None, int aux1 = -1, int aux2 = -1)
        {
            From = from; To = to; Promotion = promotion; Flags = flags; Aux1 = aux1; Aux2 = aux2;
        }

        public bool IsCapture { get { return (Flags & MoveFlags.Capture) != 0; } }
        public bool IsDoubleStep { get { return (Flags & MoveFlags.DoubleStep) != 0; } }
        public bool IsEnPassant { get { return (Flags & MoveFlags.EnPassant) != 0; } }
        public bool IsCastle { get { return (Flags & MoveFlags.Castle) != 0; } }
        public bool IsPromotion { get { return Promotion != PieceType.None; } }

        public bool Equals(Move o)
        {
            return From == o.From && To == o.To && Aux1 == o.Aux1 && Aux2 == o.Aux2 && Promotion == o.Promotion && Flags == o.Flags;
        }

        public override bool Equals(object obj) { return obj is Move m && Equals(m); }
        public override int GetHashCode() { return From * 31 + To * 17 + (int)Promotion * 7 + (int)Flags; }

        public string ToString(BoardGeometry g)
        {
            string s = g.CoordOf(From).ToString() + (IsCapture ? "x" : "-") + g.CoordOf(To).ToString();
            if (IsPromotion) s += "=" + char.ToUpperInvariant(Piece.ToChar(Piece.Make(Promotion, Color.White)));
            if (IsEnPassant) s += " e.p.";
            if (IsCastle) s += " castle";
            return s;
        }

        public override string ToString() { return From + "->" + To + (IsPromotion ? "=" + Promotion : "") + (Flags != 0 ? " " + Flags : ""); }
    }

    /// <summary>Growable move buffer without per-call allocation.</summary>
    public sealed class MoveList
    {
        private Move[] items;
        public int Count;

        public MoveList(int capacity = 256) { items = new Move[capacity]; }

        public Move this[int index] { get { return items[index]; } }

        public void Add(in Move m)
        {
            if (Count == items.Length) Array.Resize(ref items, items.Length * 2);
            items[Count++] = m;
        }

        public void Clear() { Count = 0; }

        public void Swap(int i, int j)
        {
            Move t = items[i]; items[i] = items[j]; items[j] = t;
        }

        public bool Contains(in Move m)
        {
            for (int i = 0; i < Count; i++) if (items[i].Equals(m)) return true;
            return false;
        }
    }
}
