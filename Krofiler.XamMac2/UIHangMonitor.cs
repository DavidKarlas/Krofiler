using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading;

namespace Krofiler
{
	class UIThreadMonitor
	{
		public static UIThreadMonitor Instance { get; } = new UIThreadMonitor ();

		UIThreadMonitor () { }

		Socket socket;
		Thread tcpLoopThread;
		Thread dumpsReaderThread;
		Thread pumpErrorThread;
		TcpListener listener;
		Process process;

		void TcpLoop ()
		{
			byte [] buffer = new byte [1];
			ManualResetEvent waitUIThread = new ManualResetEvent (false);
			var sw = Stopwatch.StartNew ();
			while (true) {
				sw.Restart ();
				var readBytes = socket.Receive (buffer, 1, SocketFlags.None);
				if (readBytes != 1)
					return;
				waitUIThread.Reset ();

				waitUIThread.WaitOne ();
				socket.Send (buffer);
			}
		}

		TimeSpan forceProfileTime = TimeSpan.Zero;

		public static void Profile (int seconds)
		{
			var outputFilePath = Path.GetTempFileName ();
			var startInfo = new ProcessStartInfo ("sample");
			startInfo.UseShellExecute = false;
			startInfo.Arguments = $"{Process.GetCurrentProcess ().Id} {seconds} -file {outputFilePath}";
			var sampleProcess = Process.Start (startInfo);
			sampleProcess.EnableRaisingEvents = true;
			sampleProcess.Exited += delegate {
				ConvertJITAddressesToMethodNames (outputFilePath, "Profile");
			};
		}

		public bool IsListening { get; private set; }

		public void Start ()
		{
		}

		[DllImport ("__Internal")]
		extern static string mono_pmip (long offset);
		static Dictionary<long, string> methodsCache = new Dictionary<long, string> ();

		void PumpErrorStream ()
		{
			while (!(process?.HasExited ?? true)) {
				process?.StandardError?.ReadLine ();
			}
		}

		void DumpsReader ()
		{
			while (!(process?.HasExited ?? true)) {
				var fileName = process.StandardOutput.ReadLine ();
				ConvertJITAddressesToMethodNames (fileName, "UIThreadHang");
			}
		}

		public void Stop ()
		{
			if (!IsListening)
				return;
			IsListening = false;
			listener.Stop ();
			listener = null;
			process.Kill ();
			process = null;
		}

		static void ConvertJITAddressesToMethodNames (string fileName, string profilingType)
		{
			var rx = new Regex (@"\?\?\?  \(in <unknown binary>\)  \[0x([0-9a-f]+)\]", RegexOptions.Compiled);
			if (File.Exists (fileName) && new FileInfo (fileName).Length > 0) {
				var outputFilename = Path.Combine ("/Users/davidkarlas/Desktop/", $"Profiler_{profilingType}_{DateTime.Now:yyyy-MM-dd__HH-mm-ss}.txt");
				using (var sr = new StreamReader (fileName))
				using (var sw = new StreamWriter (outputFilename)) {
					string line;
					while ((line = sr.ReadLine ()) != null) {
						if (rx.IsMatch (line)) {
							var match = rx.Match (line);
							var offset = long.Parse (match.Groups [1].Value, NumberStyles.HexNumber);
							string pmipMethodName;
							if (!methodsCache.TryGetValue (offset, out pmipMethodName)) {
								pmipMethodName = mono_pmip (offset)?.TrimStart ();
								methodsCache.Add (offset, pmipMethodName);
							}
							if (pmipMethodName != null) {
								line = line.Remove (match.Index, match.Length);
								line = line.Insert (match.Index, pmipMethodName);
							}
						}
						sw.WriteLine (line);
					}
				}
			}
		}
	}
}
