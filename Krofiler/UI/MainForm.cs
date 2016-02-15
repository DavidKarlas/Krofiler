using System;
using Eto.Forms;
using Eto.Drawing;
using System.Threading.Tasks;
using System.Threading;
using HeapShot.Reader;
using MonoDevelop.Profiler;
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
		KrofilerSession CurrentSession;
		TabControl mainTabControl;

		public MainForm()
		{
			Title = "Krofiler";
			ClientSize = new Size(1500, 1000);

			// create a few commands that can be used for the menu and toolbar
			var openFile = new Command {
				MenuText = "Open file"
			};
			openFile.Executed += (sender, e) => {
				var openFileDialog = new OpenFileDialog();
				openFileDialog.CheckFileExists = true;
				openFileDialog.MultiSelect = false;
				openFileDialog.Filters.Add(new FileDialogFilter("MLPD", "mlpd"));
				openFileDialog.ShowDialog(this);
				var fileName = openFileDialog.FileName;
				StartFromFile(fileName);
			};
			var killProfilee = new Command {
				MenuText = "Kill profilee"
			};
			killProfilee.Executed += (sender, e) => {
				CurrentSession.KillProfilee();
			};

			var takeHeapshot = new Command() {
				MenuText = "Take Heapshot"
			};
			takeHeapshot.Executed += async delegate {
				await CurrentSession.TakeHeapShot();
			};

			var quitCommand = new Command {
				MenuText = "Quit",
				Shortcut = Application.Instance.CommonModifier | Keys.Q
			};
			quitCommand.Executed += (sender, e) => Application.Instance.Quit();

			var aboutCommand = new Command { MenuText = "About..." };
			aboutCommand.Executed += (sender, e) => MessageBox.Show(this, "About my app...");

			// create menu
			Menu = new MenuBar {
				Items = {
					new ButtonMenuItem { Text = "&File", Items = { openFile,takeHeapshot,killProfilee } },
				},
				ApplicationItems = {
					// application (OS X) or file menu (others)
					new ButtonMenuItem { Text = "&Preferences..." }
				},
				QuitItem = quitCommand,
				AboutItem = aboutCommand
			};
			mainTabControl = new TabControl();
			mainTabControl.Pages.Add(new TabPage(new HomePage()) {
				Text = "Home"
			});
			Content = mainTabControl;

#if DEBUG
			//Used when developing/testing useful for quick load of existing files
			if (false) {
				StartProcess("/Users/davidkarlas/Projects/leaker/leaker/bin/Debug/leaker.exe");
				//StartProcess("/Users/davidkarlas/GIT/MD3/monodevelop/main/build/bin/MonoDevelop.exe");
			} else {
				StartFromFile(Directory.GetFiles("/Users/davidkarlas/Desktop/profiles/").OrderBy(p => new FileInfo(p).LastWriteTime).Last());
			}
#endif
		}

		void StartFromFile(string fileName)
		{
			CurrentSession = KrofilerSession.CreateFromFile(fileName);
			mainTabControl.Pages.Add(new TabPage(new HomePage()) {
				Text = "Profiling"
			});
			StartProfiling();
		}

		void StartProcess(string exeName)
		{
			CurrentSession = KrofilerSession.CreateFromProcess(exeName);
			mainTabControl.Pages.Add(new TabPage(new ProfilingInProgressPage(CurrentSession)) {
				Text = "Profiling"
			});
			StartProfiling();
		}

		void StartProfiling()
		{
			CurrentSession.NewHeapshot += HandleNewHeapshot;
			CurrentSession.StartParsing();
		}
		int n;
		void HandleNewHeapshot(KrofilerSession session, Heapshot heapshot)
		{
			Application.Instance.Invoke(() => {
				n++;
				mainTabControl.Pages.Add(new TabPage(new HeapshotPage(session, heapshot)) {
					Text = "Heap " + n
				});
			});
		}

	}
}
