using System;

namespace ComplexLogger.Utilities;

public class ConversionUtilities
{
	public class Miles
	{
		public static double ToMetersPerSecond(double input)
		{
			return input * 0.44704;
		}

		public static double ToKilometersPerHour(double input)
		{
			return input * 1.609344;
		}
	}

	public class Kilometers
	{
		public static double ToMetersPerSecond(double input)
		{
			return input / 3.6;
		}

		public static double ToMilesPerHour(double input)
		{
			return input / 1.609344;
		}
	}

	public class Meters
	{
	}

	public static int GetNormalizedSpeed(double input)
	{
		return (int)Math.Round(input, 0, MidpointRounding.ToEven);
	}
}
