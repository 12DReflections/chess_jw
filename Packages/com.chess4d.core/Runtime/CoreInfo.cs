namespace Chess4D.Core
{
    /// <summary>
    /// Build-time facts about the core library. This file exists so Stage 0 has
    /// something to compile in both Unity and .NET; Stage 1 adds the real types.
    /// </summary>
    public static class CoreInfo
    {
        public const string Version = "0.1.0";

        /// <summary>Maximum axis count the Coord struct will carry. Stage 1 fixes this at 6.</summary>
        public const int MaxDimensions = 6;
    }
}
