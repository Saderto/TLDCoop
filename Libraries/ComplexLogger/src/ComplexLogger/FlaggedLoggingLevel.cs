using System;

namespace ComplexLogger;

[Flags]
public enum FlaggedLoggingLevel
{
	[Obsolete("Use Always instead")]
	None = 0,
	Trace = 1,
	Debug = 2,
	Verbose = 4,
	Warning = 8,
	Error = 0x10,
	Critical = 0x20,
	Exception = 0x40,
	Always = 0x80
}
