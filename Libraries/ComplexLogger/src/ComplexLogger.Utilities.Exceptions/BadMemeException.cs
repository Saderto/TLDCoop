using System;

namespace ComplexLogger.Utilities.Exceptions;

[Serializable]
public class BadMemeException : Exception
{
	public BadMemeException()
	{
	}

	public BadMemeException(string message)
		: base(message)
	{
	}

	public BadMemeException(string message, Exception inner)
		: base(message, inner)
	{
	}
}
