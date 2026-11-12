namespace JellyfishByEMPEROR;

/// <summary>What the coop mod (CoopByEMPEROR, its JellyfishLink) uses by reflection, with no hard link between the dlls, as the other EMPEROR mods.
/// Jars and the aquarium are ordinary items, so coop already shares where they stand. Ours: the jellyfish in a fresh hole (seen by everyone,
/// taken by the first) and what swims in each aquarium. Messages are text: "hole|…", "holeend|…", "tank|{json}".</summary>
public static class CoopApi
{
    public const int Version = 1;

    /// <summary>Set by the coop mod while in a shared world; null otherwise.</summary>
    public static Action<string>? Send;

    /// <summary>Set by the coop mod only for a friend in the host's world: the question goes to the host, its number comes back (0: not sent).</summary>
    public static Func<string, int>? Request;

    public static void Receive(string msg)
    {
        try
        {
            int i = msg.IndexOf('|');
            string kind = i < 0 ? msg : msg.Substring(0, i), rest = i < 0 ? "" : msg.Substring(i + 1);
            switch (kind)
            {
                case "hole": Catch.Remote(rest); break;
                case "holeend": Catch.RemoteEnd(rest); break;
                case "holekeep": Catch.RemoteKeep(rest); break;
                case "tank": Tanks.Apply(rest); break;
            }
        }
        catch (Exception e) { Safe.Log("Coop receive", e); }
    }

    public static string Snapshot()
    {
        try { return Tanks.Snapshot(); }
        catch (Exception e) { Safe.Log("Coop snapshot", e); return ""; }
    }

    public static void ApplySnapshot(string all)
    {
        try { if (!string.IsNullOrEmpty(all)) Tanks.ApplySnapshot(all); }
        catch (Exception e) { Safe.Log("Coop snapshot in", e); }
    }

    /// <summary>At the host: a friend's question, checked and done here, told to everyone through Send. "" = no.</summary>
    public static string HostRequest(string op)
    {
        try { return Tanks.HostRequest(op) ?? ""; }
        catch (Exception e) { Safe.Log("Coop host request", e); return ""; }
    }

    /// <summary>At the friend: the host's answer to the question of that number.</summary>
    public static void Answer(int id, string answer)
    {
        try { Remote.Answer(id, answer); }
        catch (Exception e) { Safe.Log("Coop answer", e); }
    }
}
