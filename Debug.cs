using System;
using System.IO;

namespace Lily
{
	public static class Debug
	{
		static FileStream file = File.Open("debug.log", FileMode.Append);
		static StreamWriter FileWriter = new StreamWriter(file);
		public static void WriteLine(object o)
		{
			var time = DateTime.Now;
			Console.Error.WriteLine($"[Debug]: {time.ToString("T")}: {o}");
		}

		public static void FileWriteLine(object o)
		{
			var time = DateTime.Now;
			Console.Error.WriteLine($"[Debug]: {time.ToString("T")}: {o}");
			FileWriter.WriteLine($"{time.ToString("T")}: {o}");
			FileWriter.Flush();
		}
	}
}