using System;
using MelonLoader;

namespace ComplexLogger;

public class BaseLogger<T> where T : MelonBase
{
	public void Write(string message, params object[] parameters)
	{
		Melon<T>.Logger.Msg(message, parameters);
	}

	protected void Write(string message, ConsoleColor color, params object[] parameters)
	{
		Melon<T>.Logger.Msg(color, message, parameters);
	}
}
