using System;
namespace Krofiler
{
	public partial class KrofilerSession
	{
		void ReportUserError(string message, string details = null)
		{
			UserError?.Invoke(this, message, details);
		}
	}
}

