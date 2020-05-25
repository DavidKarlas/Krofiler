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
				throw new Exception(raw.sqlite3_errstr(rc).utf8_to_string() + ": " + raw.sqlite3_errmsg(db).utf8_to_string());
		}
	}
}
