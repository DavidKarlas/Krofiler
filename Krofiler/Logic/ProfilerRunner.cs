using System;
using System.Diagnostics;
using System.IO;

namespace Krofiler
{
	class ProfilerRunner
	{
		internal string LogFilePath;
		Process profileProcess;

		public bool HasExited { get { return profileProcess.HasExited; } }

		internal void Start(string exePath, ProfileAppOptions options)
		{
			LogFilePath =  Path.Combine(options.OutputDir, Path.GetRandomFileName() + ".mlpd");
			profileProcess = new Process();
			profileProcess.StartInfo.FileName = "/Library/Frameworks/Mono.framework/Versions/Current/bin/mono64";
			profileProcess.StartInfo.Arguments = $"--profile=log:heapshot=ondemand,alloc,nocalls,maxframes={options.MaxFrames},output=\"{LogFilePath}\" \"{exePath}\"";
			profileProcess.Start();
		}

		internal void Kill()
		{
			if (!profileProcess.HasExited)
				profileProcess.Kill();
		}
	}
}


