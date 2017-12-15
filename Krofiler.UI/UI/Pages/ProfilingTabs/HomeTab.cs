using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using Eto.Drawing;
using Eto.Forms;
using Mono.Profiler.Log;
using System.Threading;

namespace Krofiler
{
	public class HomeTab : StackLayout, IProfilingTab
	{
		public KrofilerSession CurrentSession;
		ProgressBar progressBar;
		GridView countersView = new GridView();
		GraphView allocationsGraph = new GraphView(new GraphInfo[]{
			new GraphInfo{ Title="Objects #",Color= Color.FromArgb(255,0,0,128)},
			new GraphInfo{ Title="Bytes",Color= Color.FromArgb(0,0,255,128)}
		}, TimeSpan.FromSeconds(30));
		GraphView workingMemory = new GraphView(new GraphInfo[]{
			new GraphInfo{ Title="Bytes",Color= Color.FromArgb(255,0,0,255)}
		}, TimeSpan.Zero);
		public HomeTab()
		{
			Orientation = Orientation.Horizontal;
			Items.Add(new StackLayoutItem(randomStuff, HorizontalAlignment.Stretch));
			if (Settings.Instance.ShowPerformanceCounters)
				Items.Add(new StackLayoutItem(countersView, VerticalAlignment.Stretch, true));
			else if (Settings.Instance.ShowGraphs) {
				var graphsStacklayout = new Splitter() { Orientation = Orientation.Vertical };
				graphsStacklayout.Panel1 = allocationsGraph;
				graphsStacklayout.Panel2 = workingMemory;
				graphsStacklayout.Position = 200;
				Items.Add(new StackLayoutItem(graphsStacklayout, VerticalAlignment.Stretch, true));
			}
			countersView.DataStore = new ObservableCollection<CountersRow>();
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
			stackLayout.Items.Add(new Label { Text = "Select to compare" });
			stackLayout.Items.Add(new Label { Text = "or double click." });
			var listBoxsSplitter = new StackLayout { Orientation = Orientation.Horizontal };
			listViewLeft = new ListBox();
			listViewLeft.Width = 50;
			listViewLeft.Height = 300;
			listViewRight = new ListBox();
			listViewRight.Width = 50;
			listViewRight.Height = 300;
			EventHandler<MouseEventArgs> eh = (s, e) => {
				var hs = ((s as ListBox)?.SelectedValue as ListItem)?.Tag as Heapshot;
				if (hs == null)
					return;
				InsertTab(new ObjectListTab(CurrentSession, hs, hs.TypesToObjectsListMap), this);
			};
			listViewLeft.MouseDoubleClick += eh;
			listViewRight.MouseDoubleClick += eh;
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
				CurrentSession = KrofilerSession.CreateFromProcess(((StartProfilingProcessInfo)profilingInfo).ExePath, ((StartProfilingProcessInfo)profilingInfo).Args, options);
				Settings.Instance.RecentlyRecordedFiles.Remove(CurrentSession.MlpdPath);
				Settings.Instance.RecentlyRecordedFiles.Insert(0, CurrentSession.MlpdPath);
				Settings.Instance.Save();
			} else if (profilingInfo is StartProfilingFromFileInfo) {
				CurrentSession = KrofilerSession.CreateFromFile(((StartProfilingFromFileInfo)profilingInfo).MlpdFilePath);
			} else {
				throw new NotSupportedException($"{profilingInfo.GetType().FullName} is not supported.");
			}
			CurrentSession.NewHeapshot += HandleNewHeapshot;
			CurrentSession.AllocationsPerSecond += AllocationsPerSecond;
			CurrentSession.CountersDescriptionsAdded += CountersDescriptionsAdded;
			CurrentSession.CounterSamplesAdded += CounterSamplesAdded;
			CurrentSession.UserError += UserError;
			CurrentSession.StartParsing();
		}

		private void AllocationsPerSecond(KrofilerSession session, TimeSpan time, double objects, double bytes)
		{
			if (Settings.Instance.ShowGraphs) {
				Application.Instance.AsyncInvoke(delegate {
					allocationsGraph.AddSample(time, objects, bytes);
				});
			}
		}
		double currentWorkingSet = 0;
		private void CounterSamplesAdded(SuperEvent obj)
		{
			//Application.Instance.AsyncInvoke(delegate {
			//	if (Settings.Instance.ShowPerformanceCounters) {
			//		var row = new CountersRow();
			//		row.time = obj.Time;
			//		foreach (var item in obj.Samples) {
			//			row.Counters[item.Index] = item.Value;
			//		}
			//			((ObservableCollection<CountersRow>)countersView.DataStore).Add(row);
			//	} else if (Settings.Instance.ShowGraphs) {
			//		var workingSetValue = obj.Samples.FirstOrDefault(s => s.Index == 4).Value;
			//		if (workingSetValue != null)
			//			currentWorkingSet += (double)(long)workingSetValue;
			//		workingMemory.AddSample(obj.Time, currentWorkingSet);
			//	}
			//});
		}

		class CountersRow
		{
			public TimeSpan time;
			public Dictionary<long, object> Counters = new Dictionary<long, object>();
		}

		private void CountersDescriptionsAdded()
		{
			Application.Instance.AsyncInvoke(delegate {
				if (Settings.Instance.ShowPerformanceCounters) {
					countersView.Columns.Add(new GridColumn() {
						Resizable = true,
						AutoSize = true,
						Editable = false,
						HeaderText = "Time sice start",
						DataCell = new TextBoxCell { Binding = Binding.Delegate<CountersRow, string>(r => r.time.ToString()) },
					});
					foreach (var item in CurrentSession.Descriptions) {
						countersView.Columns.Add(new GridColumn() {
							Resizable = true,
							AutoSize = false,
							Editable = false,
							Sortable = true,
							HeaderText = item.GetCounterName(CurrentSession.processor) + $"({(LogCounterUnit)(item.CounterDescriptionsEvent_SectionTypeUnitVariance & (0xF << 24))}) {item.GetSectionName(CurrentSession.processor)}",
							DataCell = new TextBoxCell { Binding = Binding.Delegate<CountersRow, string>(r => r.Counters.ContainsKey(item.CounterDescriptionsEvent_Index) ? r.Counters[item.CounterDescriptionsEvent_Index].ToString() : "") },
						});
					}
				}
			});
		}

		private void UserError(KrofilerSession session, string message, string details)
		{
			Application.Instance.Invoke(delegate {
				Eto.Forms.MessageBox.Show(details, message, type: MessageBoxType.Error);
			});
		}

		void HandleNewHeapshot(KrofilerSession session, Heapshot hs)
		{
			Application.Instance.AsyncInvoke(delegate {
				listViewLeft.Items.Add(new ListItem() {
					Text = hs.Name,
					Tag = hs
				});
				listViewRight.Items.Add(new ListItem() {
					Text = hs.Name,
					Tag = hs
				});
			});
		}

		StackLayout commandButtonsStack;

		void UpdateParsingProgress(object sender, EventArgs e)
		{
			progressBar.Value = (int)(CurrentSession.ParsingProgress * 1000);
		}

		CancellationTokenSource cpuSamplingCancelationSource;

		protected override void OnLoad(EventArgs e)
		{
			base.OnLoad(e);
			if (commandButtonsStack != null)
				return;
			var sampleCpu = new Command() {
				MenuText = "Start CPU Sample"
			};
			var cpuSampleButton = new Button() {
				Command = sampleCpu,
				Text = "Start CPU Sample"
			};
			sampleCpu.Executed += async delegate {
				if (cpuSamplingCancelationSource != null) {
					cpuSampleButton.Text = sampleCpu.MenuText = "Start CPU Sampling";
					cpuSamplingCancelationSource.Cancel();
					cpuSamplingCancelationSource = null;
				} else {
					cpuSampleButton.Text = sampleCpu.MenuText = "Stop CPU Sampling";
					cpuSamplingCancelationSource = new CancellationTokenSource();
					var samplingResult = await CpuSampling.SampleWrapper.Instance.StartSampling(CurrentSession, cpuSamplingCancelationSource.Token);
					InsertTab(new CpuSamplingTab(CurrentSession, samplingResult), this);
				}
			};
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

			//profMenuItems.Insert(0, sampleCpu);
			profMenuItems.Insert(0, takeHeapshot);
			profMenuItems.Insert(0, killProfilee);

			commandButtonsStack = new StackLayout();
			commandButtonsStack.Items.Add(cpuSampleButton);
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

