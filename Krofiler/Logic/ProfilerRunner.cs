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

		internal void Start(string exePath, string args, ProfileAppOptions options)
		{
			LogFilePath = Path.Combine(options.OutputDir, $"{Path.GetFileName(exePath)}_{DateTime.Now.ToString("yyyy-MM-dd__HH-mm-ss")}.mlpd");
			profileProcess = new Process();
			profileProcess.StartInfo.UseShellExecute = false;
			profileProcess.StartInfo.FileName = "/Library/Frameworks/Mono.framework/Versions/Current/bin/mono64";
			//profileProcess.StartInfo.WorkingDirectory = "/Users/davidkarlas/GIT/mono/mcs/mcs/";
			//profileProcess.StartInfo.EnvironmentVariables["MONO_GC_PARAMS"] = "cementing";
			profileProcess.StartInfo.Arguments = $"--profile=log:heapshot=ondemand,nodefaults,gcalloc,gcmove,gcroot,counter,maxframes={options.MaxFrames},output=\"{LogFilePath}\" \"{exePath}\" {args}";
			profileProcess.Start();
		}

		internal void Kill()
		{
			if (!profileProcess.HasExited)
				profileProcess.Kill();
		}
	}
}


