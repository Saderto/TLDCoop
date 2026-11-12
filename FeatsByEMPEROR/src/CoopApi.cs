using System;

namespace FeatsByEMPEROR
{
    /// <summary>
    /// For CoopByEMPEROR (found by reflection, no hard link). Animals of a scene run in one player's game; while an animal there
    /// "thinks" about another player, CoopByEMPEROR sets <see cref="BorrowedWolfFleeBonus"/> to return that player's bonus
    /// (sent in its AnimalSense message from <see cref="LocalWolfFleeBonus"/>), null when the animal thinks about us.
    /// </summary>
    public static class CoopApi
    {
        public static int LocalWolfFleeBonus() => FeatObjects.On(Defs.WolfHunter) ? Defs.WolfFleeBonus : 0;

        public static Func<int?> BorrowedWolfFleeBonus;

        internal static int WolfFleeBonusNow()
        {
            try { var b = BorrowedWolfFleeBonus?.Invoke(); if (b.HasValue) return b.Value; } catch { }
            return LocalWolfFleeBonus();
        }
    }
}
