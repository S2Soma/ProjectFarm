using System;

namespace LQFarm
{
    /// <summary>Where a plot is, absolutely. Missions, notifications, social actions and FX all
    /// address plots through this rather than a bare index, so that none of them need changing
    /// when a second island — or a second player's farm — appears.</summary>
    [Serializable]
    public struct PlotRef : IEquatable<PlotRef>
    {
        public string owner;   // "" = local player
        public int island;
        public int slot;       // 0..15

        public PlotRef(string owner, int island, int slot)
        { this.owner = owner; this.island = island; this.slot = slot; }

        /// <summary>A plot on the local player's first island — the only shape that exists
        /// before the multi-island step lands.</summary>
        public static PlotRef Home(int slot) { return new PlotRef("", 0, slot); }

        public bool Equals(PlotRef o) { return slot == o.slot && island == o.island && owner == o.owner; }
        public override bool Equals(object o) { return o is PlotRef r && Equals(r); }
        public override int GetHashCode() { return ((owner ?? "").GetHashCode() * 397 ^ island) * 397 ^ slot; }
        public override string ToString() { return (string.IsNullOrEmpty(owner) ? "me" : owner) + "/" + island + "/" + slot; }
    }

    /// <summary>What the acting player is allowed to do on the farm they are looking at.</summary>
    [Flags]
    public enum FarmPerm
    {
        None    = 0,
        View    = 1,
        Water   = 1 << 1,
        Harvest = 1 << 2,
        Plant   = 1 << 3,
        Unlock  = 1 << 4,
        Steal   = 1 << 5,
        Trade   = 1 << 6,
        All     = View | Water | Harvest | Plant | Unlock | Steal | Trade,
    }

    /// <summary>Who owns the farm, who is acting on it, and what that is allowed to do.
    ///
    /// The split between <see cref="owner"/> and <see cref="actor"/> is the whole point: when a
    /// friend waters your crop, the time reduction lands on the owner's plot, the energy lands
    /// on the owner, and the mission credit lands on the actor. One struct keeps those three
    /// facts from being conflated, which is the mistake that makes social features expensive to
    /// retrofit.</summary>
    public readonly struct FarmContext
    {
        public readonly PlayerState owner;
        public readonly PlayerState actor;
        public readonly FarmPerm perm;

        public FarmContext(PlayerState owner, PlayerState actor, FarmPerm perm)
        { this.owner = owner; this.actor = actor; this.perm = perm; }

        public bool Can(FarmPerm p) { return (perm & p) == p; }

        /// <summary>True while the actor is standing on their own farm — the only case that
        /// exists today, and the case every "you" string in the UI assumes.</summary>
        public bool IsOwn => ReferenceEquals(owner, actor);

        /// <summary>The local player acting on their own farm.</summary>
        public static FarmContext Own => new FarmContext(GS.Local, GS.Local, FarmPerm.All);

        /// <summary>The local player standing in a friend's farm. Deliberately cannot plant,
        /// harvest or buy land — only the things the online design says a visitor may do.</summary>
        public static FarmContext Visiting(PlayerState host)
        {
            return new FarmContext(host, GS.Local, FarmPerm.View | FarmPerm.Water | FarmPerm.Steal);
        }
    }
}
