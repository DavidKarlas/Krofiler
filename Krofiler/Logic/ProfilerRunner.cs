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
			var profileOptions = $"--profile=log:nodefaults,heapshot-on-shutdown,heapshot=ondemand,gcalloc,gcmove,gcroot,counter,maxframes={options.MaxFrames},output=\"{LogFilePath}\" ";
			if (exePath.EndsWith(".exe", StringComparison.Ordinal)) {
				profileProcess.StartInfo.FileName = "/Library/Frameworks/Mono.framework/Versions/Current/bin/mono64";
				profileProcess.StartInfo.Arguments = profileOptions;
			} else {
				profileProcess.StartInfo.EnvironmentVariables["MONO_ENV_OPTIONS"] = profileOptions;
				profileProcess.StartInfo.FileName = "open";
				profileProcess.StartInfo.Arguments = "-n ";
			}
			profileProcess.StartInfo.Arguments += $"\"{exePath}\" {args}";
			profileProcess.Start();
		}

		internal void Kill()
		{
			if (!profileProcess.HasExited)
				profileProcess.Kill();
		}
	}
}


