using System;
namespace Krofiler
{
	public partial class KrofilerSession
	{
		string fileToProcess;

		public static KrofilerSession CreateFromFile(string fileName)
		{
			var session = new KrofilerSession();
			session.fileToProcess = fileName;
			return session;
		}

		public static KrofilerSession CreateFromProcess(string executableName)
		{
			var session = new KrofilerSession();
			session.runner = new ProfilerRunner();
			session.runner.Start(executableName);
			session.fileToProcess = session.runner.LogFilePath;
			return session;
		}
	}
}

