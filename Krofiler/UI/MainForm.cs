using System;
using Eto.Forms;
using Eto.Drawing;
using System.Threading.Tasks;
using System.Threading;
using System.IO;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Sockets;
using System.Net;

namespace Krofiler
{
	/// <summary>
	/// Your application's main form
	/// </summary>
	public class MainForm : Form
	{
		public MainForm()
		{
			Title = "Krofiler";
			//TODO: Store in xml location/size and load here
			ClientSize = new Size(1500, 1000);
			var quitCommand = new Command {
				MenuText = "Quit",
				Shortcut = Application.Instance.CommonModifier | Keys.Q
			};
			quitCommand.Executed += delegate { Application.Instance.Quit(); };

			var aboutCommand = new Command { MenuText = "About..." };
			aboutCommand.Executed += delegate { MessageBox.Show(this, "Created by David Karlaš"); };

			// create menu
			Menu = new MenuBar {
				Items = {
					new ButtonMenuItem { Text = "&File" },
				},
				ApplicationItems = {
					// application (OS X) or file menu (others)
					new ButtonMenuItem { Text = "&Preferences..." }
				},
				QuitItem = quitCommand,
				AboutItem = aboutCommand
			};

			var mainTabControl = new TabControl();
			mainTabControl.Pages.Add(new TabPage(new HomePage()) {
				Text = "Home"
			});
			Content = mainTabControl;
		}

	}
}
