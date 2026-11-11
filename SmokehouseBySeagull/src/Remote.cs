using System.Text.Json;

namespace SmokehouseBySeagull;

/// <summary>Coop, a friend in the host's world: what a button of the barrel's window asks the host to do.</summary>
public class Op
{
    public string Key = "";        // one per action: a repeat after a lost answer is not done twice
    public string Scene = "";
    public float X, Y, Z;
    public string Kind = "";       // mode, put, take, light, out, wood, pickup
    public bool Cold;              // mode
    public int Slot;               // take
    public string Raw = "";        // take: the piece the asker sees in that slot
    public float Kg;
    public Slot? Piece;            // put
    public float Hours;            // wood
}

/// <summary>The host's «yes»: the barrel as it is now and, for a piece taken out, the piece as the host had it.</summary>
public class Reply
{
    public string Kind = "";
    public BarrelState? Barrel;    // null: the barrel has no state left (taken, or never used)
    public Slot? Piece;
    public bool Done, Cold;
    public List<string>? Logs;     // wood handed back (taken out, or left in a barrel that is picked up)
}

public static partial class Barrels
{
    static readonly Dictionary<string, string> answers = new();
    static readonly Queue<string> answerOrder = new();

    /// <summary>At the host: a friend's action is checked against the barrel as it is now and done, or refused ("").
    /// Works on the state only: the host may be in another scene.</summary>
    public static string HostRequest(string json)
    {
        var op = JsonSerializer.Deserialize<Op>(json, Json);
        if (op == null) return "";
        if (op.Key.Length > 0 && answers.TryGetValue(op.Key, out var old)) return old;
        string answer = Decide(op);
        if (op.Key.Length > 0)
        {
            answers[op.Key] = answer;
            answerOrder.Enqueue(op.Key);
            while (answerOrder.Count > 50) answers.Remove(answerOrder.Dequeue());
        }
        Safe.Debug($"Host: {op.Kind} at {op.Scene} → {(answer.Length > 0 ? "done" : "refused")}");
        return answer;
    }

    static string Decide(Op op)
    {
        float now = Now;
        var pos = new Vector3(op.X, op.Y, op.Z);
        var b = Find(op.Scene, pos);
        var reply = new Reply { Kind = op.Kind };
        if (b == null)
        {
            // an empty cold barrel has no state: it can be taken, nothing can be taken out of it
            if (op.Kind == "pickup") return JsonSerializer.Serialize(reply, Json);
            if (op.Kind == "take" || op.Kind == "out") return "";
            b = new BarrelState { Scene = op.Scene, X = op.X, Y = op.Y, Z = op.Z, Last = now };
            State.Barrels.Add(b);
        }
        Step(b, now);
        switch (op.Kind)
        {
            case "mode":
                if (b.Cold != op.Cold)
                {
                    if (b.Started) return "";
                    b.Cold = op.Cold;
                }
                break;
            case "put":
            {
                int i = Array.FindIndex(b.Slots, s => s == null);
                if (i < 0 || op.Piece == null) return "";
                op.Piece.Hours = 0f;
                b.Slots[i] = op.Piece;
                break;
            }
            case "take":
            {
                if (op.Slot < 0 || op.Slot >= b.Slots.Length) return "";
                var s = b.Slots[op.Slot];
                if (s == null || s.Raw != op.Raw || Math.Abs(s.Kg - op.Kg) > .005f) return "";
                reply.Piece = s;
                reply.Done = b.Done(s);
                reply.Cold = b.Cold;
                b.Slots[op.Slot] = null;
                break;
            }
            case "light":
                if (b.Lit || b.Fuel <= 0f) return "";
                b.Last = now;
                b.Lit = true;
                break;
            case "out":
                b.Lit = false;
                break;
            case "wood":
                if (op.Hours <= 0f || op.Hours > 24f || b.Fuel + op.Hours > Tune.FuelMaxHours + .01f) return "";
                b.Fuel += op.Hours;
                (b.Logs ??= new()).Add(new Log { Name = op.Raw, Hours = op.Hours, Full = op.Hours });
                break;
            case "unwood":
                if (b.Lit) return "";
                reply.Logs = TakeLogs(b);
                if (reply.Logs.Count == 0) return "";
                break;
            case "pickup":
                if (!b.Idle) return "";
                reply.Logs = TakeLogs(b);
                State.Barrels.Remove(b);
                b.Fuel = 0f;
                b.Logs.Clear();
                if (Window.IsOpenOn(b)) Window.Close();
                Changed(b);
                return JsonSerializer.Serialize(reply, Json);
            default:
                return "";
        }
        Changed(b);
        reply.Barrel = b;
        return JsonSerializer.Serialize(reply, Json);
    }

    /// <summary>At the asker: the barrel as the host answered it.</summary>
    internal static void Take(Reply r, Op op)
    {
        if (r.Barrel != null) { Apply(Encode(r.Barrel)); return; }
        var b = Find(op.Scene, new Vector3(op.X, op.Y, op.Z));
        if (b != null) State.Barrels.Remove(b);
    }
}

public static partial class Window
{
    class Pending
    {
        public Op Op = new();
        public string Json = "";
        public GearItem? Gear;     // what leaves the pack on «yes» (raw piece, log, fire starter) or the barrel to lift
        public int Tries;
        public float SentAt, Born;
        public bool Finished;
    }

    static readonly Dictionary<int, Pending> asked = new();
    static Pending? waiting;
    static int opCount;
    static readonly System.Random rnd = new();

    /// <summary>A friend in the host's world: buttons ask the host and change nothing here until the answer.</summary>
    static bool Remote => CoopApi.Request != null;
    static bool Waiting => waiting != null;

    static void Ask(BarrelState b, Op op, GearItem? gear)
    {
        if (waiting != null) return;
        op.Key = $"{++opCount}-{rnd.Next():x}-{rnd.Next():x}";
        op.Scene = b.Scene; op.X = b.X; op.Y = b.Y; op.Z = b.Z;
        var p = new Pending { Op = op, Json = JsonSerializer.Serialize(op, Barrels.Json), Gear = gear, Born = Time.unscaledTime };
        if (!Post(p)) Barrels.Say("MSG_NO_HOST");
    }

    static bool Post(Pending p)
    {
        int id = 0;
        try { id = CoopApi.Request?.Invoke(p.Json) ?? 0; }
        catch (Exception e) { Safe.Log("Coop request", e); }
        p.Tries++;
        p.SentAt = Time.unscaledTime;
        if (id == 0) { if (waiting == p) waiting = null; return false; }
        asked[id] = p;
        waiting = p;
        Safe.Debug($"Asked the host: {p.Op.Kind}, request {id}, try {p.Tries}");
        return true;
    }

    static void RemoteTick()
    {
        var p = waiting;
        if (p != null)
        {
            if (CoopApi.Request == null) waiting = null;
            else if (Time.unscaledTime - p.SentAt > 5f)
            {
                // the same key again: the host answers a repeat without doing the action twice
                if (p.Tries >= 3 || !Post(p))
                {
                    waiting = null;
                    Barrels.Say("MSG_NO_HOST");
                }
            }
        }
        if (asked.Count > 0 && Time.frameCount % 600 == 0)
            foreach (var id in asked.Where(kv => Time.unscaledTime - kv.Value.Born > 180f).Select(kv => kv.Key).ToList()) asked.Remove(id);
    }

    /// <summary>The host's answer. A late «yes» to a request already given up is still carried out, once.</summary>
    public static void Answer(int id, string answer)
    {
        if (!asked.TryGetValue(id, out var p)) return;
        asked.Remove(id);
        if (p.Finished) return;
        p.Finished = true;
        if (waiting == p) waiting = null;
        nextScan = 0f;
        if (string.IsNullOrEmpty(answer)) { Barrels.Say("MSG_OTHER"); return; }
        var r = JsonSerializer.Deserialize<Reply>(answer, Barrels.Json);
        if (r == null) return;
        var op = p.Op;
        GearItem? g = null;
        try { if (p.Gear != null && p.Gear.gameObject != null) g = p.Gear; } catch { }
        Safe.Debug($"Host said yes: {op.Kind}");
        switch (op.Kind)
        {
            case "put":
                if (g != null) GameManager.GetInventoryComponent().DestroyGear(g.gameObject);
                if (op.Piece != null && op.Piece.Salted) TakeSalt();
                break;
            case "take":
                if (r.Piece != null && !Give(r.Piece, r.Done, r.Cold)) MelonLogger.Error("Smokehouse: the piece the host gave out could not be created");
                break;
            case "light":
                if (g != null) Strike(g);
                break;
            case "wood":
                if (g != null) TakeOne(g);
                break;
            case "unwood":
            case "pickup":
                GiveLogs(r.Logs);
                break;
        }
        Barrels.Take(r, op);
        if (op.Kind == "pickup")
        {
            bool onGround = false;
            try { onGround = g != null && !g.m_InPlayerInventory && g.gameObject.activeInHierarchy; } catch { }
            if (item != null && g != null && item.Pointer == g.Pointer) Close();
            if (onGround) Lift(g);
        }
    }
}
