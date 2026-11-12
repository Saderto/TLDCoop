namespace JellyfishByEMPEROR;

/// <summary>A friend in the host's world asks the host before changing anything shared (taking the jellyfish from the hole, pouring into an
/// aquarium, taking out of it): one question at a time, asked again with the same key after 5 s (the host does a repeat only once), given up after 3.
/// A late «yes» to a question already given up is still carried out: the host has done its part.</summary>
internal static class Remote
{
    class Pending
    {
        public string Op = "";
        public Action<string> Done = null!;
        public float SentAt, Born;
        public int Tries;
        public bool Finished;
    }

    static readonly Dictionary<int, Pending> asked = new();
    static Pending? waiting;
    static int count;
    static readonly System.Random rnd = new();

    public static bool On => CoopApi.Request != null;
    public static bool Waiting => waiting != null;

    public static string NewKey() => $"{++count}-{rnd.Next():x}-{rnd.Next():x}";

    public static bool Ask(string op, Action<string> done)
    {
        if (waiting != null) return false;
        var p = new Pending { Op = op, Done = done, Born = Time.unscaledTime };
        if (Post(p)) return true;
        Catch.Say("MSG_NO_HOST");
        return false;
    }

    static bool Post(Pending p)
    {
        int id = 0;
        try { id = CoopApi.Request?.Invoke(p.Op) ?? 0; }
        catch (Exception e) { Safe.Log("Coop request", e); }
        p.Tries++;
        p.SentAt = Time.unscaledTime;
        if (id == 0) { if (waiting == p) waiting = null; return false; }
        asked[id] = p;
        waiting = p;
        Safe.Debug($"Asked the host: {p.Op.Split('|')[0]}, request {id}, try {p.Tries}");
        return true;
    }

    public static void Tick()
    {
        var p = waiting;
        if (p != null)
        {
            if (CoopApi.Request == null) waiting = null;
            else if (Time.unscaledTime - p.SentAt > 5f && (p.Tries >= 3 || !Post(p)))
            {
                waiting = null;
                Catch.Say("MSG_NO_HOST");
            }
        }
        if (asked.Count > 0 && Time.frameCount % 600 == 0)
            foreach (var id in asked.Where(kv => Time.unscaledTime - kv.Value.Born > 180f).Select(kv => kv.Key).ToList()) asked.Remove(id);
    }

    public static void Answer(int id, string answer)
    {
        if (!asked.TryGetValue(id, out var p)) return;
        foreach (var k in asked.Where(kv => kv.Value == p).Select(kv => kv.Key).ToList()) asked.Remove(k);
        if (p.Finished) return;
        p.Finished = true;
        if (waiting == p) waiting = null;
        Safe.Debug($"Host answered {p.Op.Split('|')[0]}: {(answer.Length > 0 ? answer : "no")}");
        try { p.Done(answer ?? ""); }
        catch (Exception e) { Safe.Log("Coop answer", e); }
    }
}
