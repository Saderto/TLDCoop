using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using ComplexLogger.Utilities.Exceptions;
using Il2Cpp;
using MelonLoader;
using MelonLoader.Pastel;
using UnityEngine;

namespace ComplexLogger;

public class ComplexLogger<T> : BaseLogger<T> where T : MelonBase
{
	public void Log(FlaggedLoggingLevel level, LoggingSubType LogSubType, [CallerMemberName] string memberName = "")
	{
		Log(string.Empty, level, (LoggingSubType?)LogSubType, (Exception?)null, memberName);
	}

	public void Log(string message, FlaggedLoggingLevel level, LoggingSubType LogSubType, [CallerMemberName] string memberName = "")
	{
		Log(message, level, (LoggingSubType?)LogSubType, (Exception?)null, memberName);
	}

	public void Log(string message, FlaggedLoggingLevel level, [CallerMemberName] string memberName = "")
	{
		Log(message, level, (LoggingSubType?)LoggingSubType.Normal, (Exception?)null, memberName);
	}

	public void Log(string message, FlaggedLoggingLevel level, Exception exception, [CallerMemberName] string memberName = "")
	{
		Log(message, level, (LoggingSubType?)LoggingSubType.Normal, exception, memberName);
	}

	public void Log(string Title, string[] Lines, System.Drawing.Color Color, FlaggedLoggingLevel Level)
	{
		if (Main.CurrentLevel.HasFlag(Level))
		{
			WriteLogBlock(Title, Lines, Color);
		}
	}

	public void Log(string Title, string[] Lines, List<System.Drawing.Color> Color, FlaggedLoggingLevel Level)
	{
		if (Main.CurrentLevel.HasFlag(Level))
		{
			WriteLogBlock(Title, Lines, Color);
		}
	}

	public void Log(string message, FlaggedLoggingLevel level, LoggingSubType? LogSubType, Exception? exception, [CallerMemberName] string memberName = "")
	{
		if (level == FlaggedLoggingLevel.None)
		{
			Write("Using the 'FlaggedLoggingLevel.None' level is not going to be supported anymore. Please use 'FlaggedLoggingLevel.Always' instead");
		}
		if (LogSubType.HasValue && LogSubType != LoggingSubType.Normal)
		{
			if (LogSubType.GetValueOrDefault() == LoggingSubType.Separator)
			{
				WriteSeperator(level);
				return;
			}
			if (LogSubType.GetValueOrDefault() == LoggingSubType.IntraSeparator)
			{
				WriteIntraSeparator(level, message);
				return;
			}
			if (LogSubType.GetValueOrDefault() == LoggingSubType.uConsole)
			{
				uConsole.Log(message);
				if (exception != null)
				{
					uConsole.Log(exception.Message);
				}
				return;
			}
			if (LogSubType.GetValueOrDefault() == LoggingSubType.Block)
			{
				throw new ComplexLoggerException("This LogSubType is not supported with the given arguements. Please use Log(string, string[], System.Drawing.Color, FlaggedLoggingLevel) or Log(string, string[], List<System.Drawing.Color>, FlaggedLoggingLevel)");
			}
		}
		List<object> list = new List<object>();
		List<object> list2 = new List<object>();
		list.Add(UnityEngine.Color.red);
		list2.Add(UnityEngine.Color.red);
		list2.Add(FontStyle.Bold);
		if (Main.CurrentLevel.HasFlag(level))
		{
			switch (level)
			{
			case FlaggedLoggingLevel.Always:
				Write(memberName + "::" + message, ConsoleColor.Green);
				break;
			case FlaggedLoggingLevel.Trace:
				Write("[TRACE] " + memberName + "::" + message);
				break;
			case FlaggedLoggingLevel.Debug:
				Write("[DEBUG] " + memberName + "::" + message);
				break;
			case FlaggedLoggingLevel.Verbose:
				Write("[INFO] " + memberName + "::" + message);
				break;
			case FlaggedLoggingLevel.Warning:
				Write("[WARNING] " + memberName + "::" + message, list);
				break;
			case FlaggedLoggingLevel.Error:
				Write("[ERROR] " + memberName + "::" + message, list);
				break;
			case FlaggedLoggingLevel.Critical:
				Write("[CRITICAL] " + memberName + "::" + message, list2);
				break;
			case FlaggedLoggingLevel.Exception:
				WriteException(message, exception);
				break;
			}
		}
	}

	[Obsolete]
	public void Log(FlaggedLoggingLevel level, LoggingSubType LogSubType, params object[] parameters)
	{
	}

	[Obsolete]
	public void Log(string message, FlaggedLoggingLevel level, LoggingSubType LogSubType, params object[] parameters)
	{
	}

	[Obsolete]
	public void Log(string message, FlaggedLoggingLevel level, params object[] parameters)
	{
	}

	[Obsolete]
	public void Log(string message, FlaggedLoggingLevel level, Exception exception, params object[] parameters)
	{
	}

	[Obsolete]
	public void Log(string message, FlaggedLoggingLevel level, LoggingSubType? LogSubType, Exception? exception, params object[] parameters)
	{
	}

	[Obsolete]
	private void WriteSeperator(params object[] parameters)
	{
	}

	[Obsolete]
	private void WriteSeperator(FlaggedLoggingLevel level, params object[] parameters)
	{
	}

	[Obsolete]
	private void WriteIntraSeparator(string message, params object[] parameters)
	{
	}

	[Obsolete]
	private void WriteIntraSeparator(FlaggedLoggingLevel level, string message, params object[] parameters)
	{
	}

	private void WriteSeperator()
	{
		Write("==============================================================================");
	}

	private void WriteSeperator(FlaggedLoggingLevel level)
	{
		if (Main.CurrentLevel.HasFlag(level))
		{
			WriteSeperator();
		}
	}

	private void WriteIntraSeparator(string message)
	{
		Write("=========================   " + message + "   =========================");
	}

	private void WriteIntraSeparator(FlaggedLoggingLevel level, string message)
	{
		if (Main.CurrentLevel.HasFlag(level))
		{
			WriteIntraSeparator(message);
		}
	}

	private void WriteException(string message, Exception? exception)
	{
		StringBuilder stringBuilder = new StringBuilder();
		stringBuilder.Append("[EXCEPTION] ");
		stringBuilder.Append(message);
		if (exception != null)
		{
			stringBuilder.AppendLine(exception.Message);
		}
		else
		{
			stringBuilder.AppendLine("Exception was null");
		}
		Write(stringBuilder.ToString());
	}

	private void WriteLogBlock(string FormatedMessage)
	{
		Write(FormatedMessage);
	}

	private void WriteLogBlock(string[] lines)
	{
		StringBuilder stringBuilder = new StringBuilder();
		foreach (string value in lines)
		{
			stringBuilder.AppendLine(value);
		}
		WriteLogBlock(stringBuilder.ToString());
	}

	private void WriteLogBlock(string Title, string[] Lines, System.Drawing.Color color)
	{
		List<System.Drawing.Color> lineColors = Lines.Select((string s) => color).ToList();
		WriteLogBlock(Title, Lines, lineColors);
	}

	private void WriteLogBlock(string Title, string[] lines, List<System.Drawing.Color>? lineColors)
	{
		if (lineColors != null && lines.Length != lineColors?.Count)
		{
			throw new ComplexLoggerException($"WriteLogBlock({Title}, {lines.Length}, {lineColors?.Count})::Attempting to build a LogBlock requires equal length arrays for both parameters 'lines' and 'lineColors'");
		}
		StringBuilder stringBuilder = new StringBuilder();
		stringBuilder.AppendLine(Title);
		if (lineColors != null)
		{
			for (int i = 0; i < lines.Length; i++)
			{
				stringBuilder.AppendLine(ConsoleExtensions.Pastel(lines[i], MelonLoader.Logging.ColorARGB.FromArgb(lineColors[i].A, lineColors[i].R, lineColors[i].G, lineColors[i].B)));
			}
		}
		else
		{
			for (int j = 0; j < lines.Length; j++)
			{
				stringBuilder.AppendLine(lines[j]);
			}
		}
		WriteLogBlock(stringBuilder.ToString());
	}
}
