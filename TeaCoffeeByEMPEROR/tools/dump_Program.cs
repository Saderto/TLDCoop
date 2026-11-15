using System; using System.IO; using System.Linq; using System.Reflection;
var refs = args[0];
var paths = Directory.GetFiles(refs, "*.dll").Concat(Directory.GetFiles(Path.GetDirectoryName(typeof(object).Assembly.Location), "*.dll"));
var mlc = new MetadataLoadContext(new PathAssemblyResolver(paths));
var asm = mlc.LoadFromAssemblyPath(Path.Combine(refs, "Assembly-CSharp.dll"));
foreach (var name in args.Skip(1)) {
  if (name.StartsWith("?")) { var q = name.Substring(1); foreach (var tt in asm.GetTypes()) { try { foreach (var mm in tt.GetMembers(BindingFlags.Public|BindingFlags.Instance|BindingFlags.Static|BindingFlags.DeclaredOnly)) if (mm.Name.Contains(q, StringComparison.OrdinalIgnoreCase) && !mm.Name.StartsWith("Native") && !mm.Name.StartsWith("get_") && !mm.Name.StartsWith("set_")) Console.WriteLine(tt.FullName + " :: " + mm); } catch {} } continue; }
  var t = asm.GetTypes().FirstOrDefault(x => x.FullName == name || x.Name == name);
  if (t == null) { Console.WriteLine("?? " + name); continue; }
  Console.WriteLine("### " + t.FullName + " : " + t.BaseType?.Name);
  if (t.IsEnum) { Console.WriteLine("  enum " + string.Join(", ", Enum.GetNames(t))); continue; }
  foreach (var m in t.GetMethods(BindingFlags.Public|BindingFlags.Instance|BindingFlags.Static|BindingFlags.DeclaredOnly).Where(m => !m.IsSpecialName))
    Console.WriteLine("  " + (m.IsStatic ? "static " : "") + m.ReturnType.Name + " " + m.Name + "(" + string.Join(", ", m.GetParameters().Select(p => p.ParameterType.Name + " " + p.Name)) + ")");
  Console.WriteLine("  props: " + string.Join(", ", t.GetProperties(BindingFlags.Public|BindingFlags.Instance|BindingFlags.Static|BindingFlags.DeclaredOnly).Where(p => !p.Name.StartsWith("Native")).Select(p => p.PropertyType.Name + " " + p.Name)));
}
