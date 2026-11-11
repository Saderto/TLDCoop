namespace ContainerLabelsBySeagull;

internal static class Palette
{
	public static readonly (string hex, string ru, string en)[] All = new(string, string, string)[10]
	{
		("ffffff", "белый", "white"),
		("ff3b30", "красный", "red"),
		("ff9500", "оранжевый", "orange"),
		("ffd60a", "жёлтый", "yellow"),
		("34c759", "зелёный", "green"),
		("5ac8fa", "голубой", "light blue"),
		("3a6cff", "синий", "blue"),
		("bf5af2", "фиолетовый", "purple"),
		("a2845e", "коричневый", "brown"),
		("9a9a9a", "серый", "grey")
	};

	public static int Wrap(int i)
	{
		return (i % All.Length + All.Length) % All.Length;
	}

	public static string Paint(string text, int color)
	{
		return "[" + All[Wrap(color)].hex + "]" + text + "[-]";
	}

	public static string Name(int color, bool ru)
	{
		if (!ru)
		{
			return All[Wrap(color)].en;
		}
		return All[Wrap(color)].ru;
	}
}
