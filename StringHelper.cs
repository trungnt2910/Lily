using System;

namespace Lily
{
	public static class StringHelper
	{
		public static string TrimPunctuation(this string input)
		{
			if (string.IsNullOrEmpty(input))
			{
				return input;
			}
			if ("!?.".Contains(input[input.Length - 1]))
			{
				return input.Substring(0, input.Length - 1);
			}
			return input;
		}

		public static string Sort(this string str)
		{	
			var arr = str.ToCharArray();
			Array.Sort(arr);
			return new string(arr);
		}
	}
}