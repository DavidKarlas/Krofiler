using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace Krofiler
{
	public partial class KrofilerSession
	{
		public event Action<KrofilerSession, Heapshot> NewHeapshot;
		public event Action<KrofilerSession> Finished;

		public event Action<KrofilerSession, string, string> UserError;
	}
}

