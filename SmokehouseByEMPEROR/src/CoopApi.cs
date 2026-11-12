namespace SmokehouseByEMPEROR;

/// <summary>What the coop mod (CoopByEMPEROR) uses by reflection, with no hard link between the dlls (as the other EMPEROR mods).
/// The barrel itself is an ordinary item, so the coop mod already shares where it stands. What is inside, the fire and the wood are ours:
/// Send is called with one barrel's state (JSON text) after every change a player makes here; the coop mod delivers it to the other players and calls Receive there.
/// Snapshot gives all barrels of the world for a player who joins; ApplySnapshot takes it on their side.
/// Timers run on each machine by its own game clock, so no clock is sent: a received barrel goes on from the receiver's «now».</summary>
public static class CoopApi
{
    public const int Version = 3;

    /// <summary>Set by the coop mod while a session runs; null otherwise.</summary>
    public static Action<string>? Send;

    /// <summary>Set by the coop mod while in a shared world: the world's common clock in hours (the same for every player, not running ahead
    /// for one who slept or worked alone). Null otherwise: the player's own game clock is used.</summary>
    public static Func<float>? Hours;

    public static void Receive(string barrel)
    {
        try { Barrels.Apply(barrel); }
        catch (Exception e) { Safe.Log("Coop receive", e); }
    }

    public static string Snapshot()
    {
        try { return Barrels.Snapshot(); }
        catch (Exception e) { Safe.Log("Coop snapshot", e); return ""; }
    }

    public static void ApplySnapshot(string all)
    {
        try { if (!string.IsNullOrEmpty(all)) Barrels.ApplySnapshot(all); }
        catch (Exception e) { Safe.Log("Coop snapshot in", e); }
    }

    /// <summary>Set by the coop mod only for a friend in the host's world (null at the host and outside coop). While it is set, the buttons of
    /// the barrel's window change nothing here: they send the action and wait for the host's answer. Returns the request's number, 0 = not sent.</summary>
    public static Func<string, int>? Request;

    /// <summary>Called at the host with a friend's action: checked, done, told to everyone through Send. "" = refused.</summary>
    public static string HostRequest(string op)
    {
        try { return Barrels.HostRequest(op) ?? ""; }
        catch (Exception e) { Safe.Log("Coop host request", e); return ""; }
    }

    /// <summary>Called at the friend with the host's answer to the request of that number.</summary>
    public static void Answer(int id, string answer)
    {
        try { Window.Answer(id, answer); }
        catch (Exception e) { Safe.Log("Coop answer", e); }
    }
}
