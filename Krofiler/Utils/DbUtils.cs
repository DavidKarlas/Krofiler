using System;
using SQLitePCL;

namespace Krofiler
{
	public static class DbUtils
	{
		public static void check_ok(sqlite3 db, int rc)
		{
			if (raw.SQLITE_OK != rc)
				throw new Exception(raw.sqlite3_errstr(rc) + ": " + raw.sqlite3_errmsg(db));
		}
	}
}
