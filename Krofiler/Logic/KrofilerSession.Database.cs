using System;
using System.Collections.Generic;
using System.IO;
using SQLitePCL;

namespace Krofiler
{
	partial class KrofilerSession
	{
		private static void check_ok(sqlite3 db, int rc)
		{
			if (raw.SQLITE_OK != rc)
				throw new Exception(raw.sqlite3_errstr(rc) + ": " + raw.sqlite3_errmsg(db));
		}
	}
}
