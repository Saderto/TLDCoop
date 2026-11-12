using System.Text.Json;

namespace SmokehouseByEMPEROR;

/// <summary>A sausage being made at the table: the meat, the gut and the salt are already in it; Done is how much of the work is over.
/// 11-12 owner: making takes time as crafting does, and what was not finished is finished later from the same place.</summary>
public class Job
{
    public string Kind = "";
    public float Done, Kg, Cal, Cond;
    /// <summary>The unfinished sausage was put in the pack as an item (0.4.19): the work goes on only with it at hand.</summary>
    public bool Item;
}

public static partial class Barrels
{
    /// <summary>This player's unfinished sausages in this world, one per kind. Kept beside the world's file, never sent to the other players.</summary>
    public static List<Job> Jobs = new();

    static string JobsFile(string key) => FileOf(key).Replace(".json", ".jobs.json");

    public static void LoadJobs()
    {
        Jobs = new();
        try
        {
            string key = WorldKey();
            if (key.Length == 0) return;
            var path = JobsFile(key);
            if (File.Exists(path)) Jobs = JsonSerializer.Deserialize<List<Job>>(File.ReadAllText(path), Json) ?? new();
            Safe.Info($"Unfinished sausages: {Jobs.Count}");
            Crafting.Apply();
        }
        catch (Exception e) { Safe.Log("Unfinished sausages unreadable", e); }
    }

    public static void SaveJobs()
    {
        try
        {
            string key = WorldKey();
            if (key.Length == 0) return;
            Directory.CreateDirectory(Folder);
            var path = JobsFile(key);
            if (Jobs.Count == 0) { if (File.Exists(path)) File.Delete(path); }
            else File.WriteAllText(path, JsonSerializer.Serialize(Jobs, Json));
        }
        catch (Exception e) { Safe.Log("Saving unfinished sausages", e); }
    }
}
