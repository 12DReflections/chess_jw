namespace Chess4D.Core
{
    /// <summary>Optional draw rules. Both default to off; the tablebase work needs them off (spec section 2).</summary>
    public sealed class GameRules
    {
        public bool FiftyMoveRule = false;
        public bool ThreefoldRepetition = false;
    }

    public enum GameStatus
    {
        Ongoing,
        Checkmate,
        Stalemate,
        DrawFiftyMove,
        DrawRepetition,
    }
}
