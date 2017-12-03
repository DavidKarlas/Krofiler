using System;
using Eto.Forms;
using System.Linq;
using System.Threading.Tasks;

namespace Krofiler
{
	public class HomePage : StackLayout
	{
		public HomePage()
		{
			Orientation = Orientation.Horizontal;
			var rpa = new ListBoxWithTitle("Recently profiled apps(double click)", Settings.Instance.RecentlyProfiledApps);
			rpa.ActivatedItem += StartProcess;
			Items.Add(new StackLayoutItem(rpa, VerticalAlignment.Stretch, true));
			var rpo = new ListBoxWithTitle("Recently profiling outputs(double click)", Settings.Instance.RecentlyRecordedFiles);
			rpo.ActivatedItem += StartFromFile;
			Items.Add(new StackLayoutItem(rpo, VerticalAlignment.Stretch, true));
			var rfo = new ListBoxWithTitle("Recently opened files(double click)", Settings.Instance.RecentlyOpenedFiles);
			rfo.ActivatedItem += StartFromFile;
			Items.Add(new StackLayoutItem(rfo, VerticalAlignment.Stretch, true));
		}

		protected override void OnLoad(EventArgs e)
		{
			base.OnLoad(e);
			var openFile = new Command {
				MenuText = "Open file"
			};
			openFile.Executed += delegate {
				var openFileDialog = new OpenFileDialog();
				openFileDialog.CheckFileExists = true;
				openFileDialog.MultiSelect = false;
				openFileDialog.Filters.Add(new FileDialogFilter("MLPD", "mlpd"));
				if (DialogResult.Ok == openFileDialog.ShowDialog(this)) {
					StartFromFile(openFileDialog.FileName);
				}
			};

			var profileApp = new Command {
				MenuText = "Profile application"
			};

			profileApp.Executed += delegate {
				var openFileDialog = new OpenFileDialog();
				openFileDialog.CheckFileExists = true;
				openFileDialog.MultiSelect = false;
				openFileDialog.Filters.Add(new FileDialogFilter(".exe application", "exe"));
				if (DialogResult.Ok == openFileDialog.ShowDialog(this)) {
					StartProcess(openFileDialog.FileName);
				}
			};

			var fileMenuItems = this.ParentWindow.Menu.Items.OfType<ButtonMenuItem>().Single(i => i.Text == "&File").Items;

			fileMenuItems.Insert(0, profileApp);
			fileMenuItems.Insert(0, openFile);

			var commandButtonsStack = new StackLayout();
			commandButtonsStack.Items.Add(new Button() {
				Command = openFile,
				Text = "Open File"
			});
			commandButtonsStack.Items.Add(new Button() {
				Command = profileApp,
				Text = "Profile Application"
			});

			Items.Insert(0, commandButtonsStack);
		}


		void StartFromFile(string fileName)
		{
			Settings.Instance.RecentlyOpenedFiles.Remove(fileName);
			Settings.Instance.RecentlyOpenedFiles.Insert(0, fileName);
			Settings.Instance.Save();

			StartProfiling(new StartProfilingFromFileInfo() {
				MlpdFilePath = fileName
			});
		}

		void StartProcess(string exeName)
		{
			Settings.Instance.RecentlyProfiledApps.Remove(exeName);
			Settings.Instance.RecentlyProfiledApps.Insert(0, exeName);
			Settings.Instance.Save();
			StartProfiling(new StartProfilingProcessInfo() {
				ExePath = exeName,
				Args = ""
			});
		}

		void StartProfiling(StartProfilingInfo startProfInfo)
		{
			var profilingInProgressPage = new ProfilingPage();
			var tabPage = new TabPage(profilingInProgressPage) {
				Text = "Profiling"
			};
			((TabControl)Parent.Parent).Pages.Add(tabPage);
			((TabControl)Parent.Parent).SelectedPage = tabPage;
			profilingInProgressPage.StartProfiling(startProfInfo);
		}
	}
}

