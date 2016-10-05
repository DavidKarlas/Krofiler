using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using System.Threading;

namespace Krofiler
{
	public partial class KrofilerSession
	{
		/// <summary>
		/// True means we are connected, data is being generated, we can request heapshots
		/// False means we are loading from old file
		/// </summary>
		public bool Live { get; private set; }

		/// <summary>
		/// Read from .mldp file and used to connect to runtime to control profiler(invoke heapshot etc.)
		/// </summary>
		int TcpPort;

		public async Task TakeHeapShot()
		{
			using (var client = new TcpClient()) {
				await client.ConnectAsync(IPAddress.Loopback, TcpPort);
				using (var writer = new StreamWriter(client.GetStream())) {
					writer.Write("heapshot\n");
					writer.Flush();
					Thread.Sleep(1000);
				}
			}
		}

		public void KillProfilee()
		{
			runner.Kill();
		}
	}
}

