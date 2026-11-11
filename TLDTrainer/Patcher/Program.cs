using Mono.Cecil;
using Mono.Cecil.Cil;

// Переписывает старые моды под игру 2.58: вызовы убранных из игры функций
// заменяются на вызовы из TLDCompat258.dll.
// Аргументы: <TLDCompat258.dll> <папка со сборками игры и зависимостями>... -- <мод.dll> <выходная папка> [патч Harmony, который отключить]...
var compatPath = args[0];
int sep = Array.IndexOf(args, "--");
var searchDirs = args.Skip(1).Take(sep - 1).ToArray();
var input = args[sep + 1];
var outDir = args[sep + 2];
var disablePatches = args.Skip(sep + 3).ToHashSet();

var resolver = new DefaultAssemblyResolver();
foreach (var d in searchDirs) resolver.AddSearchDirectory(d);
resolver.AddSearchDirectory(Path.GetDirectoryName(Path.GetFullPath(compatPath)));
resolver.AddSearchDirectory(Path.GetDirectoryName(Path.GetFullPath(input)));

var compat = AssemblyDefinition.ReadAssembly(compatPath, new ReaderParameters { AssemblyResolver = resolver });
var replacements = new Dictionary<string, MethodDefinition>
{
    ["Il2Cpp.GearItem Il2Cpp.GearItem::LoadGearItemPrefab(System.String)"] =
        compat.MainModule.GetType("TLDCompat.Gear").Methods.Single(m => m.Name == "LoadGearItemPrefab"),
};

var asm = AssemblyDefinition.ReadAssembly(input, new ReaderParameters { AssemblyResolver = resolver, ReadSymbols = false });
int count = 0;
foreach (var type in asm.MainModule.GetTypes())
foreach (var method in type.Methods.Where(m => m.HasBody))
foreach (var ins in method.Body.Instructions)
{
    if ((ins.OpCode == OpCodes.Call || ins.OpCode == OpCodes.Callvirt) && ins.Operand is MethodReference mr
        && replacements.TryGetValue(mr.FullName, out var repl))
    {
        ins.OpCode = OpCodes.Call;
        ins.Operand = asm.MainModule.ImportReference(repl);
        count++;
        Console.WriteLine($"  {type.FullName}.{method.Name}: {mr.Name} → TLDCompat");
    }
}
// Патчи Harmony к переделанным в 2.58 частям игры: снимаем атрибут, и MelonLoader их не применяет.
foreach (var type in asm.MainModule.GetTypes().Where(t => disablePatches.Contains(t.FullName)))
{
    int removed = 0;
    foreach (var attr in type.CustomAttributes.Where(a => a.AttributeType.Name == "HarmonyPatch").ToList())
    {
        type.CustomAttributes.Remove(attr);
        removed++;
    }
    Console.WriteLine($"  {type.FullName}: отключён патч Harmony ({removed})");
}

Directory.CreateDirectory(outDir);
asm.Write(Path.Combine(outDir, Path.GetFileName(input)));
Console.WriteLine($"{Path.GetFileName(input)}: заменено вызовов {count}");
