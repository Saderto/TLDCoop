using System;

namespace ComplexLogger.Utilities.Exceptions;

[Serializable]
public class ComplexLoggerException : Exception
{
	public ComplexLoggerException()
	{
	}

	public ComplexLoggerException(string? message)
		: base(message)
	{
	}

	public ComplexLoggerException(string? paramName, string? message)
		: base(message)
	{
	}

	public ComplexLoggerException(string? message, Exception innerException)
		: base(message, innerException)
	{
	}
}
