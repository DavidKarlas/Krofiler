﻿using System;
using Eto.Forms;

namespace Krofiler.XamMac2
{
	public class Program
	{
		[STAThread]
		public static void Main (string[] args)
		{
			new Application (Eto.Platforms.XamMac2).Run (new MainForm ());
		}
	}
}
