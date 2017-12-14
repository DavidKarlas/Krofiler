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
		void Preferences(object sender, EventArgs e)
		{
			var window = new Form() {
				Title = "Preferences"
			};
			var numericMaxFrames = new NumericUpDown { Value = Settings.Instance.MaxFrames, MaxValue = 32, MinValue = 0, MaximumDecimalPlaces = 0, Increment = 1 };
			var enableCounters = new CheckBox { Checked = Settings.Instance.ShowPerformanceCounters };
			var enableGraphs = new CheckBox { Checked = Settings.Instance.ShowGraphs };
			window.Content = new TableLayout {
				Spacing = new Size(5, 5), // space between each cell
				Padding = new Padding(10, 10, 10, 10), // space around the table's sides
				Rows =
				{
					new TableRow(new Label { Text = "Max frames" },numericMaxFrames),
					new TableRow(new Label { Text = "Show Performance Counters" },enableCounters),
					new TableRow(new Label { Text = "Enable graphs" },enableGraphs),
					new TableRow(
						new Button { Text = "Cancel", Command=new Command(delegate {
							window.Close();
						}) },
						 new Button { Text = "Save", Command=new Command(delegate {
							Settings.Instance.MaxFrames=(int)numericMaxFrames.Value;
							Settings.Instance.ShowPerformanceCounters=enableCounters.Checked??false;
							Settings.Instance.ShowGraphs=enableGraphs.Checked??false;
							Settings.Instance.Save();
							window.Close();
						}) }
						),
						// by default, the last row & column will get scaled. This adds a row at the end to take the extra space of the form.
						// otherwise, the above row will get scaled and stretch the TextBox/ComboBox/CheckBox to fill the remaining height.
						new TableRow { ScaleHeight = true }
					}
			};
			window.Show();
		}

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
					new ButtonMenuItem {
						Text = "&Preferences...",
						Command= new Command(Preferences)
					}
				},
				QuitItem = quitCommand,
				AboutItem = aboutCommand,
				HelpItems = {
					new ButtonMenuItem{ Text="Profile for 5 seconds", Command= new Command(delegate {
						UIThreadMonitor.Profile (5);
					})}
				}
			};

			var mainTabControl = new TabControl();
			mainTabControl.Pages.Add(new TabPage(new HomePage()) {
				Text = "Home"
			});
			Content = mainTabControl;
		}

	}
}
