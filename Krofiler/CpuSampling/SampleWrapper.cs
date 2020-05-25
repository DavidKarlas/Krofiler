#if MAC
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;

namespace Krofiler.CpuSampling
{
	public class SamplingResult
	{
		public SampleFrame RootFrame;
	}


	public class SampleFrame
	{
		public int Depth;
		public string Name { get; set; }
		public int Value { get; set; }
		public List<SampleFrame> Children { get; } = new List<SampleFrame>();
	}

	public class SampleWrapper
	{
		public readonly static SampleWrapper Instance = new SampleWrapper();
		public async Task<SamplingResult> StartSampling(KrofilerSession session, CancellationToken token)
		{
			var outputFilePath = Path.GetTempFileName();
			var startInfo = new ProcessStartInfo("sample");
			startInfo.UseShellExecute = false;
			startInfo.Arguments = $"{session.ProcessId} {10000} -file {outputFilePath}";
			var sampleProcess = Process.Start(startInfo);
			var waitForCancelTaskSource = new TaskCompletionSource<bool>();
			token.Register(() => waitForCancelTaskSource.SetResult(true));
			await waitForCancelTaskSource.Task;
			Mono.Unix.Native.Syscall.kill(sampleProcess.Id, Mono.Unix.Native.Signum.SIGINT);
			sampleProcess.WaitForExit();
			return new SamplingResult()
			{
				RootFrame = ParseSampleOutput(outputFilePath)
			};
		}

		SampleFrame ParseSampleOutput(string fileName)
		{
			using (var sr = new StreamReader(fileName))
			{
				string line;
				SampleFrame currentFrame = new SampleFrame();
				var rootFrame = currentFrame;
				var rx2 = new Regex(@"^([ +!:|]+)([0-9]+) (.*)", RegexOptions.Compiled);
				var stack = new Stack<SampleFrame>();
				while ((line = sr.ReadLine()) != null)
				{
					var match = rx2.Match(line);
					if (!match.Success)
						continue;
					var depth = match.Groups[1].Length;
					var count = int.Parse(match.Groups[2].Value);
					var txt = match.Groups[3].Value;
					if (txt.StartsWith(" ", StringComparison.Ordinal))
						continue;
					while (depth <= currentFrame.Depth)
					{
						currentFrame = stack.Pop();
					}
					if (stack.Count != ((depth - 4) / 2))
						throw new Exception();
					stack.Push(currentFrame);
					if (currentFrame.Value < 0 && currentFrame.Name != null)
						throw new Exception();
					currentFrame.Children.Add(currentFrame = new SampleFrame()
					{
						Depth = depth,
						Value = count,
						Name = txt
					});
				}
				rootFrame.Value = rootFrame.Children.Sum(c => c.Value);
				return rootFrame;
			}
		}

		void ConvertJITAddressesToMethodNames(string fileName)
		{
			var unmanagedFrame = new Regex(@"\?\?\?  \(in <unknown binary>\)  \[0x([0-9a-f]+)\]", RegexOptions.Compiled);
			var managedFrame = new Regex(@"\?\?\?  \(in <unknown binary>\)  \[0x([0-9a-f]+)\]", RegexOptions.Compiled);
			var threadName = new Regex(@"\?\?\?  \(in <unknown binary>\)  \[0x([0-9a-f]+)\]", RegexOptions.Compiled);
			if (File.Exists(fileName) && new FileInfo(fileName).Length > 0)
			{
				using (var sr = new StreamReader(fileName))
				{
					string line;
					while ((line = sr.ReadLine()) != null)
					{
						var unmanagedMatch = unmanagedFrame.Match(line);
						if (unmanagedMatch.Success)
						{

						}
						else
						{
							var managedMatch = managedFrame.Match(line);
							if (managedMatch.Success)
							{
								var offset = long.Parse(managedMatch.Groups[1].Value, NumberStyles.HexNumber);
							}
							else
							{
								var threadMatch = threadName.Match(line);
								if (threadMatch.Success)
								{

								}
							}
						}
					}
				}
			}
		}
	}
} 
#endif