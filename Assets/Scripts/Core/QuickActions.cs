namespace LQFarm
{
    /// <summary>The three bulk verbs on the bottom bar, and the level each one unlocks at.
    ///
    /// Staggered on purpose. A new player learns the farm one plot at a time — tapping a ripe
    /// crop, choosing a seed for an empty bed, catching a watering window — and a "do everything"
    /// button on day one skips exactly the lessons that make the systems legible. Each verb
    /// arrives once its plot-by-plot version has been done enough times to be tedious, which is
    /// the moment a shortcut feels like a reward instead of a mystery.
    ///
    /// Harvest first (level 3): it is the most repeated action and has no decision in it.
    /// Plant next (5): it opens the seed sheet, so it needs the player to know what seeds are.
    /// Water last (7): windows open and close on a timer, and the player has to have seen that
    /// rhythm before a button that ignores it means anything.</summary>
    public static class QuickActions
    {
        public const int HarvestLevel = 3;
        public const int PlantLevel = 5;
        public const int WaterLevel = 7;

        public static bool HarvestUnlocked(PlayerState s) { return s.lv >= HarvestLevel; }
        public static bool PlantUnlocked(PlayerState s)   { return s.lv >= PlantLevel; }
        public static bool WaterUnlocked(PlayerState s)   { return s.lv >= WaterLevel; }

        /// <summary>Name of the verb that unlocks exactly at this level, or null.</summary>
        public static string UnlockedAt(int level)
        {
            if (level == HarvestLevel) return "Thu hoạch nhanh";
            if (level == PlantLevel) return "Gieo nhanh";
            if (level == WaterLevel) return "Tưới nhanh";
            return null;
        }
    }
}
