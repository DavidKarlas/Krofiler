using System;
using Eto.Forms;
namespace Krofiler
{
	public class ProfilingInProgressPage : StackLayout
	{
		readonly KrofilerSession session;

		public ProfilingInProgressPage(KrofilerSession session)
		{
			this.session = session;
		}
	}
}

