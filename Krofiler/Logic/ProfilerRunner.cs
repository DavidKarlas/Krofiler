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

		internal void Start(string exePath)
		{
			LogFilePath = Path.Combine("/Users/davidkarlas/Desktop/profiles/", Path.GetRandomFileName() + ".krof");
			profileProcess = new Process();
			profileProcess.StartInfo.FileName = "/Library/Frameworks/Mono.framework/Versions/Current/bin/mono64";
			//profileProcess.StartInfo.FileName = "/opt9/mono/bin/mono";
			profileProcess.StartInfo.Arguments = $"--gc=sgen --profile=log:heapshot=ondemand,noalloc,nocalls,maxframes=999,output=\"{LogFilePath}\" \"{exePath}\"";
			//profileProcess.StartInfo.UseShellExecute = false;
			//profileProcess.StartInfo.RedirectStandardOutput = true;
			//profileProcess.StartInfo.RedirectStandardError = true;
			profileProcess.Start();
		}

		internal void Kill()
		{
			if (!profileProcess.HasExited)
				profileProcess.Kill();
		}
	}
}


