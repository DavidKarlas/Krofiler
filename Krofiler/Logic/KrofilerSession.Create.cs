using System;
namespace Krofiler
{

	public class ProfileAppOptions
	{
		public int MaxFrames { get; set; }
		public string OutputDir { get; set; }
	}

	public partial class KrofilerSession
	{
		string fileToProcess;

		public static KrofilerSession CreateFromFile(string fileName)
		{
			var session = new KrofilerSession();
			session.fileToProcess = fileName;
			return session;
		}

		public static KrofilerSession CreateFromProcess(string executableName, string args, ProfileAppOptions options)
		{
			var session = new KrofilerSession();
			session.runner = new ProfilerRunner();
			session.runner.Start(executableName, args, options);
			session.fileToProcess = session.runner.LogFilePath;
			return session;
		}
	}
}

