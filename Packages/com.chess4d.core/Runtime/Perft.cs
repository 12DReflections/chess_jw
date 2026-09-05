using System.Collections.Generic;

namespace Chess4D.Core
{
    /// <summary>Leaf-node counting over legal moves, the Stage 1 and Stage 2 oracle.</summary>
    public static class Perft
    {
        public static long Count(Board board, int depth)
        {
            if (depth <= 0) return 1;
            var lists = new MoveList[depth + 1];
            for (int i = 0; i <= depth; i++) lists[i] = new MoveList(512);
            return Count(board, depth, lists);
        }

        private static long Count(Board board, int depth, MoveList[] lists)
        {
            MoveList list = lists[depth];
            board.GenerateLegal(list);
            if (depth == 1) return list.Count;
            long total = 0;
            for (int i = 0; i < list.Count; i++)
            {
                board.Make(list[i]);
                total += Count(board, depth - 1, lists);
                board.Unmake();
            }
            return total;
        }

        /// <summary>Per-root-move counts, for locating a discrepancy against a reference engine.</summary>
        public static List<KeyValuePair<Move, long>> Divide(Board board, int depth)
        {
            var result = new List<KeyValuePair<Move, long>>();
            var root = new MoveList(512);
            board.GenerateLegal(root);
            for (int i = 0; i < root.Count; i++)
            {
                board.Make(root[i]);
                long n = depth <= 1 ? 1 : Count(board, depth - 1);
                board.Unmake();
                result.Add(new KeyValuePair<Move, long>(root[i], n));
            }
            return result;
        }
    }
}
