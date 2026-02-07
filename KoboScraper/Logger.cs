namespace KoboScraper
{
	public static class Logger
	{
		public enum LogLevel
		{
			Debug,
			Info,
			Warning,
			Error
		}

		public static Action<LogLevel, string>? Output;
		public static void Log(LogLevel level, string message)
		{
			string prefix = level switch
			{
				LogLevel.Debug		=> "DEBUG",
				LogLevel.Info		=> "INFO ",
				LogLevel.Warning	=> "WARN ",
				LogLevel.Error		=> "ERROR",
				_ => "UNKWN"
			};

			string line = $"[{DateTime.Now:yyyy/MM/dd HH:mm:ss.fff}][{prefix}] {message}";
			Output?.Invoke(level, line);
		}
	}
}
