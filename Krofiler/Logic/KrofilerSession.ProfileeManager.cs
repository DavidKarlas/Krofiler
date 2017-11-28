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

		TcpClient client;
		StreamWriter writer;
		public async Task TakeHeapShot()
		{
			if (client == null) {
				client = new TcpClient();
				await client.ConnectAsync(IPAddress.Loopback, TcpPort);
				writer = new StreamWriter(client.GetStream());
			}
			await writer.WriteAsync("heapshot\n").ConfigureAwait(false);
		}

		public void KillProfilee()
		{
			runner.Kill();
		}
	}
}

