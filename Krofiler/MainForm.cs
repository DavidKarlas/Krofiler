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
		ProfilerRunner runner;
		public MainForm()
		{
			//Just for testing:
			if (false) {
				runner = new ProfilerRunner();
				//runner.Start("/Users/davidkarlas/Projects/testProfiler/testProfiler/bin/Debug/testProfiler.exe");
				runner.Start("/Users/davidkarlas/GIT/MD3/monodevelop/main/build/bin/MonoDevelop.exe");
				ProcessFile(runner.LogFilePath);

				//Task.Run(async () => {
				//	await Task.Delay(5000);
				//	TakeHeapShot();
				//	await Task.Delay(10000);
				//	TakeHeapShot();
				//});
			} else {
				ProcessFile(Directory.GetFiles("/Users/davidkarlas/Desktop/profiles/").OrderBy(p => new FileInfo(p).LastWriteTime).Last());
			}

			Title = "My Eto Form";
			ClientSize = new Size(400, 350);

			// scrollable region as the main content
			Content = new Scrollable {
				// table with three rows
				Content = new TableLayout(
					null,
					// row with three columns
					new TableRow(null, new Label { Text = "Hello World!" }, null),
					null
				)
			};

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
				ProcessFile(fileName);
			};
			var killProfilee = new Command {
				MenuText = "Kill profilee"
			};
			killProfilee.Executed += (sender, e) => {
				runner.Kill();
			};

			var takeHeapshot = new Command {
				MenuText = "Take Heapshot"
			};
			takeHeapshot.Executed += (sender, e) => {
				TakeHeapShot();
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
		}

		Thread processingThread;
		CancellationTokenSource cts = new CancellationTokenSource();
		void ProcessFile(string fileName)
		{
			var token = cts.Token;
			processingThread = new Thread(() => {
				int retries = 0;
			retryOpeningLogfile:
				retries++;
				if (retries > 1) {
					if (retries == 4) {
						Application.Instance.AsyncInvoke(() => {
							if (!File.Exists(fileName)) {
								MessageBox.Show("File doesn't exist.");
							} else if (new FileInfo(fileName).Length == 0) {
								MessageBox.Show("File is empty.");
							} else {
								MessageBox.Show("Failed to read file.");
							}
						});
					}
					Thread.Sleep(500);
				}
				LogFileReader reader;
				try {
					reader = new LogFileReader(fileName);
				} catch {
					goto retryOpeningLogfile;
				}
				var header = Header.Read(reader);
				if (header == null) {
					goto retryOpeningLogfile;
				}
				Port = header.Port;
				reader.Header = header;

				while (!token.IsCancellationRequested) {
					var buffer = BufferHeader.Read(reader);
					if (buffer == null) {
						if (runner != null) {
							if (runner.HasExited)
								break;
							Thread.Sleep(100);
							continue;
						} else {
							break;
						}
					}
					methodBaseCC = buffer.MethodBase;
					reader.BufferHeader = buffer;
					while (!reader.IsBufferEmpty) {
						ProcessEvent(buffer, Event.Read(reader));
					}
				}
				foreach (var v in rootStackFrame.Children.OrderBy(f => f.Value.MethodName)) {
					var tuple123123123 = MX(v.Value);
					Console.WriteLine(v.Key + " " + v.Value.MethodName + " MaxDepth:" + tuple123123123.Item1);
					var cur123 = tuple123123123.Item2;
					while (cur123.Parent != null) {
						Console.WriteLine(cur123.MethodName);
						cur123 = cur123.Parent;
					}
				}

				foreach (var hs in heapshots) {
					Console.WriteLine("New HS:");
					foreach (var p in hs.TypesToObjectsListMap)
						PrintTypeReport(p);
				}
				Console.WriteLine("New objects:");
				foreach (var typeReport in HeapShot.NewObjects(heapshots[0], heapshots[1])) {
					PrintTypeReport(typeReport);
				}
				Console.WriteLine("Deleted objects:");
				foreach (var typeReport in HeapShot.DeletedObjects(heapshots[0], heapshots[1])) {
					PrintTypeReport(typeReport);
				}
				Application.Instance.AsyncInvoke(() => {
					MessageBox.Show("Finished processing file.");
				});
			});
			processingThread.Start();
		}

		Tuple<int, StackFrame> MX(StackFrame value)
		{
			var max = 0; Tuple<int, StackFrame> tuple = new Tuple<int, StackFrame>(0, value);
			foreach (var v in value.Children) {
				var t = MX(v.Value);
				if (t.Item1 > max) {
					max = t.Item1;
					tuple = t;
				}
			}

			return new Tuple<int, StackFrame>(max + 1, tuple.Item2);
		}

		void PrintTypeReport(KeyValuePair<long, List<long>> p)
		{
			Console.WriteLine(GetClassName(p.Key) + " " + p.Value.Count + ": " + string.Join(",", p.Value.Select(i => i.ToString())));
		}


		int Port;
		async void TakeHeapShot()
		{
			using (var client = new TcpClient()) {
				await client.ConnectAsync(IPAddress.Loopback, Port);
				using (var writer = new StreamWriter(client.GetStream())) {
					writer.Write("heapshot\n");
				}
			}
		}

		string GetClassName(long id)
		{
			if (classIdToName.ContainsKey(id)) {
				return classIdToName[id];
			} else {
				return "<no name>";
			}
		}

		Dictionary<long, string> classIdToName = new Dictionary<long, string>();
		LargeList<AllocStruct> allocs = new LargeList<AllocStruct>();
		void ProcessEvent(BufferHeader buffer, Event ev)
		{
			var allocEvent = ev as AllocEvent;
			if (allocEvent != null) {
				allocs.Add(new AllocStruct() {
					StackFrame = GetStackFrame(buffer, allocEvent.Backtrace),
					Object = buffer.ObjBase + allocEvent.Obj
				});
			}
			var methodEvent = ev as MethodEvent;
			if (methodEvent != null) {
				methodBaseCC += methodEvent.Method;
				if (methodEvent.Type == MethodEvent.MethodType.Jit) {
					methodName.Add(methodBaseCC, methodEvent.Name);
				}
			}
			var typeEvent = ev as MetadataEvent;
			if (typeEvent != null) {
				switch (typeEvent.MType) {
					case MetadataEvent.MetaDataType.Class:
						classIdToName[buffer.PtrBase + typeEvent.Pointer] = typeEvent.Name;
						break;
					case MetadataEvent.MetaDataType.Image:
						break;
					case MetadataEvent.MetaDataType.Assembly:
						break;
					case MetadataEvent.MetaDataType.Domain:
						break;
					case MetadataEvent.MetaDataType.Thread:
						break;
					case MetadataEvent.MetaDataType.Context:
						break;
					default:
						break;
				}
			}
			var gcEvent = ev as MoveGcEvent;
			if (gcEvent != null) {
				if (heapshots.Count == 1) {
					for (var i = 0; i < gcEvent.ObjAddr.Length; i += 2) {
						long oldAddress = gcEvent.ObjAddr[i] + buffer.ObjBase;
						long newAddress = gcEvent.ObjAddr[i + 1] + buffer.ObjBase;
						heapshots[0].ChangeAddress(oldAddress, newAddress);
					}
				}
			}
			var heapEvent = ev as HeapEvent;
			if (heapEvent != null) {
				switch (heapEvent.Type) {
					case HeapEvent.EventType.Start:
						currentHeapshot = new HeapShot();
						Console.WriteLine("Heap started");
						break;
					case HeapEvent.EventType.End:
						heapshots.Add(currentHeapshot);
						currentHeapshot = null;
						Console.WriteLine("Heap ended");
						break;
					case HeapEvent.EventType.Root:
						//for (int i = 0; i < heapEvent.RootRefs.Length; i++) {
						//	rootsTracker.ProcessEvent(heapEvent.RootRefs[i], heapEvent.RootRefTypes[i], heapEvent.RootRefExtraInfos[i]);
						//}
						break;
					case HeapEvent.EventType.Object:
						currentHeapshot.AddObject(buffer, heapEvent);
						break;
					default:
						throw new ArgumentOutOfRangeException();
				}
			}
		}
		RootsTracker rootsTracker = new RootsTracker();
		class RootsTracker
		{

			internal void ProcessEvent(long v1, HeapEvent.RootType rootType, ulong v2)
			{
				Console.WriteLine(v1 + " " + rootType);
			}
		}

		HeapShot currentHeapshot;
		List<HeapShot> heapshots = new List<HeapShot>();

		class HeapShot
		{
			ulong totalSize = 0;
			long totalObjects = 0;

			struct ObjectStruct
			{
				public long Id;
				public long TypeId;
			}

			public Dictionary<long, List<long>> TypesToObjectsListMap = new Dictionary<long, List<long>>();
			public Dictionary<long, long> ObjectsToTypeMap = new Dictionary<long, long>();

			public void AddObject(BufferHeader buffer, HeapEvent ev)
			{
				var v = buffer.PtrBase + ev.Class;
				if (ObjectsToTypeMap.ContainsKey(ev.Object + buffer.ObjBase)) {
					if (ObjectsToTypeMap[ev.Object + buffer.ObjBase] != v)
						return;
					return;
				}
				totalSize += ev.Size;
				totalObjects++;
				if (!TypesToObjectsListMap.ContainsKey(v))
					TypesToObjectsListMap[v] = new List<long>();
				TypesToObjectsListMap[v].Add(ev.Object + buffer.ObjBase);
				ObjectsToTypeMap.Add(ev.Object + buffer.ObjBase, v);
			}

			internal static IEnumerable<KeyValuePair<long, List<long>>> NewObjects(HeapShot heapShot1, HeapShot heapShot2)
			{
				foreach (var t in heapShot2.TypesToObjectsListMap) {
					if (heapShot1.TypesToObjectsListMap.ContainsKey(t.Key)) {
						var list = t.Value.Except(heapShot1.TypesToObjectsListMap[t.Key]).ToList();
						if (list.Count == 0) {
							continue;
						} else {
							yield return new KeyValuePair<long, List<long>>(t.Key, list);
						}
					} else {
						yield return t;
					}
				}
			}

			internal static IEnumerable<KeyValuePair<long, List<long>>> DeletedObjects(HeapShot heapShot1, HeapShot heapShot2)
			{
				return NewObjects(heapShot2, heapShot1);
			}

			internal void ChangeAddress(long oldAddress, long newAddress)
			{
				if (!ObjectsToTypeMap.ContainsKey(oldAddress))
					return;//I guess this allocation happened after our HS
				var ttt = ObjectsToTypeMap[oldAddress];
				var typeMap = TypesToObjectsListMap[ttt];
				typeMap.Remove(oldAddress);
				typeMap.Add(newAddress);
				ObjectsToTypeMap.Remove(oldAddress);
				ObjectsToTypeMap.Add(newAddress, ttt);
			}
		}

		long methodBaseCC;

		public static Dictionary<long, string> methodName = new Dictionary<long, string>();
		int maxDepth = 0;
		StackFrame GetStackFrame(BufferHeader buffer, long[] backtrace)
		{
			maxDepth = Math.Max(maxDepth, backtrace.Length);
			//if (backtrace.Length > 100) {
			//	Console.WriteLine("Bt:");
			//	foreach (var v in backtrace)
			//		Console.WriteLine(v + "(" + GetMethod(v) + "),");
			//}
			return rootStackFrame.GetStackFrame(buffer, backtrace, 0);
		}

		StackFrame rootStackFrame = new StackFrame(-1);

		public static string GetMethod(long methodId)
		{
			if (methodId == -1)
				return "[root]";
			if (!Krofiler.MainForm.methodName.ContainsKey(methodId))
				return "Not existing(" + methodId + ").";
			return Krofiler.MainForm.methodName[methodId];
		}
		[DebuggerDisplay("{MethodName}")]
		class StackFrame
		{
			public string MethodName {
				get {
					return GetMethod(methodId);
				}
			}


			public Dictionary<long, StackFrame> Children = new Dictionary<long, StackFrame>();
			public StackFrame Parent;
			public long methodId;

			public StackFrame(long methodId)
			{
				this.methodId = methodId;
			}

			internal StackFrame GetStackFrame(BufferHeader buffer, long[] frame, int index)
			{
				if (frame.Length == index)
					return this;
				if (!Children.ContainsKey(frame[index])) {
					Children[frame[index]] = new StackFrame(frame[index]) {
						Parent = this
					};
				}
				return Children[frame[index]].GetStackFrame(buffer, frame, index + 1);
			}
		}

		class AllocStruct
		{
			public StackFrame StackFrame;
			public long Object;
		}
	}
}
