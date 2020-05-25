using System;
using Eto.Forms;

namespace Krofiler.Wpf
{
	public class Program
	{
		[STAThread]
		public static void Main (string[] args)
		{
			var app = new Application(Eto.Platforms.Wpf);
			app.Run (new MainForm ());
		}
	}
}
