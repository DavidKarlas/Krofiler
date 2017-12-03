using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace Krofiler
{
	public partial class KrofilerSession
	{
		public string MlpdPath { get => fileToProcess; }
        public event Action<KrofilerSession, Heapshot> NewHeapshot;
		public event Action<KrofilerSession> Finished;
		public event Action<KrofilerSession, string, string> UserError;
	}
}

