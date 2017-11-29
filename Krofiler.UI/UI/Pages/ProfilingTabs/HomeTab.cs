using System;
using System.Collections.Generic;
using System.IO;
using Eto.Forms;
namespace Krofiler
{
	public class HomeTab : StackLayout, IProfilingTab
	{
		public KrofilerSession CurrentSession;
		ProgressBar progressBar;
		public HomeTab()
		{
			Orientation = Orientation.Horizontal;
			Items.Add(new StackLayoutItem(randomStuff, HorizontalAlignment.Stretch, true));
			FillRandomStuff();
		}

		ListBox listViewLeft;
		ListBox listViewRight;

		void FillRandomStuff()
		{
			progressBar = new ProgressBar();
			progressBar.MinValue = 0;
			progressBar.MaxValue = 1000;
			timer = new UITimer() {
				Interval = 0.5
			};
			timer.Elapsed += UpdateParsingProgress;
			timer.Start();
			randomStuff.Items.Add(progressBar);

			var stackLayout = new StackLayout();
			stackLayout.Items.Add(new Label { Text = "Select heapshots for compare:" });
			var listBoxsSplitter = new StackLayout { Orientation = Orientation.Horizontal };
			listViewLeft = new ListBox();
			listViewLeft.Width = 150;
			listViewLeft.Height = 300;
			listViewRight = new ListBox();
			listViewRight.Width = 150;
			listViewRight.Height = 300;
			listBoxsSplitter.Items.Add(listViewLeft);
			listBoxsSplitter.Items.Add(listViewRight);
			stackLayout.Items.Add(new StackLayoutItem(listBoxsSplitter, VerticalAlignment.Stretch, true));
			var compareButton = new Button { Text = "Compare" };
			compareButton.Click += delegate {
				var left = (listViewLeft.SelectedValue as ListItem)?.Tag as Heapshot;
				var right = (listViewRight.SelectedValue as ListItem)?.Tag as Heapshot;
				if (left == null && right == null) {
					MessageBox.Show("Select heapshots on both sides.");
					return;
				}
				if (left == null) {
					MessageBox.Show("Select heapshot on left side.");
					return;
				}
				if (right == null) {
					MessageBox.Show("Select heapshot on right side.");
					return;
				}
				if (left == right) {
					MessageBox.Show("You have same heapshot selected on both sides.");
					return;
				}
				InsertTab(new CompareHeapshotsTab(CurrentSession, (Heapshot)left, (Heapshot)right), this);
			};
			stackLayout.Items.Add(compareButton);
			randomStuff.Items.Add(stackLayout);
		}

		StackLayout randomStuff = new StackLayout();
		UITimer timer;

		public string Title {
			get {
				return "Home";
			}
		}

		public string Details {
			get {
				return "";
			}
		}

		public Control TabContent {
			get {
				return this;
			}
		}

		public event InsertTabDelegate InsertTab;

		ProfileAppOptions OpenOptionsDialog()
		{
			var options = new ProfileAppOptions();
			options.MaxFrames = Settings.Instance.MaxFrames;
			options.OutputDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "Desktop");
			return options;
		}

		public void StartProfiling(StartProfilingInfo profilingInfo)
		{
			if (profilingInfo is StartProfilingProcessInfo) {
				var options = OpenOptionsDialog();
				CurrentSession = KrofilerSession.CreateFromProcess(((StartProfilingProcessInfo)profilingInfo).ExePath, options);
			} else if (profilingInfo is StartProfilingFromFileInfo) {
				CurrentSession = KrofilerSession.CreateFromFile(((StartProfilingFromFileInfo)profilingInfo).MlpdFilePath);
			} else {
				throw new NotSupportedException($"{profilingInfo.GetType().FullName} is not supported.");
			}
			CurrentSession.NewHeapshot += HandleNewHeapshot;
			CurrentSession.UserError += UserError;
			CurrentSession.StartParsing();
		}

		private void UserError(KrofilerSession session, string message, string details)
		{
			Application.Instance.Invoke(delegate {
				Eto.Forms.MessageBox.Show(details, message, type: MessageBoxType.Error);
			});
		}

		void HandleNewHeapshot(KrofilerSession session, Heapshot hs)
		{
			Application.Instance.Invoke(delegate {
				var hsButton = new Button {
					Text = hs.Name
				};
				listViewLeft.Items.Add(new ListItem() {
					Text = hs.Name,
					Tag = hs
				});
				listViewRight.Items.Add(new ListItem() {
					Text = hs.Name,
					Tag = hs
				});
				hsButton.Click += delegate {
					InsertTab(new ObjectListTab(session, hs, hs.TypesToObjectsListMap), this);
				};
				commandButtonsStack.Items.Add(hsButton);
			});
		}

		StackLayout commandButtonsStack;

		void UpdateParsingProgress(object sender, EventArgs e)
		{
			progressBar.Value = (int)(CurrentSession.ParsingProgress * 1000);
		}
		protected override void OnLoad(EventArgs e)
		{
			base.OnLoad(e);
			if (commandButtonsStack != null)
				return;
			var takeHeapshot = new Command() {
				MenuText = "Take Heapshot"
			};
			takeHeapshot.Executed += async delegate {
				await CurrentSession.TakeHeapShot();
			};

			var killProfilee = new Command {
				MenuText = "Kill profilee"
			};

			killProfilee.Executed += delegate {
				CurrentSession.KillProfilee();
			};

			var profilingMenu = new ButtonMenuItem() {
				Text = "&Profiling"
			};
			this.ParentWindow.Menu.Items.Add(profilingMenu);
			var profMenuItems = profilingMenu.Items;

			profMenuItems.Insert(0, takeHeapshot);
			profMenuItems.Insert(0, killProfilee);

			commandButtonsStack = new StackLayout();
			commandButtonsStack.Items.Add(new Button() {
				Command = takeHeapshot,
				Text = "Take Heapshot"
			});
			commandButtonsStack.Items.Add(new Button() {
				Command = killProfilee,
				Text = "Kill profilee"
			});

			Items.Insert(0, commandButtonsStack);
		}
	}
}

