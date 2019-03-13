using System;
using Eto.Forms;
using System.Linq;
using System.Collections.Generic;

namespace Krofiler
{
	public class ObjectDetailsPanel : StackLayout
	{
		readonly KrofilerSession session;
		readonly Heapshot heapshot;
		ListBox stacktraceView;
		ListBox referencesList;
		TabControl retentionPaths;
		public event InsertTabDelegate InsertTab;
		Label detailsLabel = new Label();

		ObjectInfo objectInfo;
		public ObjectInfo Object {
			get {
				return objectInfo;
			}
			set {
				if (objectInfo?.ObjAddr == value.ObjAddr)
					return;
				objectInfo = value;
				OnObjectIdChanged();
			}
		}

		void AddSingleEntry(string text)
		{
			var listBox = new ListBox();
			listBox.Items.Add(text);
			retentionPaths.Pages.Add(new TabPage(listBox));
		}

		void OnObjectIdChanged()
		{
			var allocationTimestamp = TimeSpan.FromTicks((long)(objectInfo.AllocationTimestamp(session) / 100));
			var heapShotFirstSeen = session.Heapshots.FirstOrDefault(h => h.Time > allocationTimestamp);
			detailsLabel.Text = "Allocation time:" + allocationTimestamp + " first seen in Heapshot: " + heapShotFirstSeen?.Name;
			stacktraceView.Items.Clear();
			var sf = objectInfo.Backtrace(session).Reverse().Select(b => session.GetMethodName(b));
			foreach (var f in sf) {
				stacktraceView.Items.Add(f);
			}

			retentionPaths.Pages.Clear();
			var pathsToRoot = heapshot.GetTop5PathsToRoots(objectInfo.ObjAddr);
			int i = 0;
			foreach (var path in pathsToRoot) {
				i++;
				var listBox = new ListBox();
				listBox.MouseDoubleClick += (s, e) => {
					if (listBox.SelectedValue is RetentionItem ri) {
						var newTab = new ObjectListTab(session, heapshot, CreateObjectList(ri.obj));
						newTab.InsertTab += InsertTab;
						InsertTab(newTab, null);
					}
				};
				var page = new TabPage(listBox) {
					Text = $"Path {i}"
				};
				foreach (var edge in path) {
					var objInfo = heapshot.GetObjectInfo(edge);
					var typeName = session.GetTypeName(objInfo.TypeId);
					listBox.Items.Add(new RetentionItem(typeName, objInfo));//TODO: Add field to name
				}
				listBox.Items.Add(new RetentionItem("Root:" + heapshot.Roots[path.Last()].GetName(session.processor), heapshot.GetObjectInfo(path.Last())));
				retentionPaths.Pages.Add(page);
			}
			if (!pathsToRoot.Any())
				if (heapshot.Roots.TryGetValue(objectInfo.ObjAddr, out var root))
					AddSingleEntry("Object is root itself:" + root.GetName(session.processor));
				else
					AddSingleEntry("This is weird... Couldn't find path to root");

			referencesList.Items.Clear();
			foreach (var r in heapshot.GetReferencedTo(objectInfo.ObjAddr)) {
				var obj = heapshot.GetObjectInfo(r);
				referencesList.Items.Add(new RetentionItem(session.GetTypeName(obj.TypeId) + ": " + r, obj));
			}
		}

		private static Dictionary<long, LazyObjectsList> CreateObjectList(ObjectInfo obj)
		{
			return new Dictionary<long, LazyObjectsList>() {
				[obj.TypeId] = new SingleLazyObjectList(obj)
			};
		}

		class RetentionItem : IListItem
		{
			internal readonly ObjectInfo obj;
			readonly string typeName;

			public RetentionItem(string typeName, ObjectInfo obj)
			{
				this.typeName = typeName;
				this.obj = obj;
			}
			public string Text { get => typeName; set => throw new NotImplementedException(); }

			public string Key => obj.ObjAddr.ToString();

		}

		public ObjectDetailsPanel(KrofilerSession session, Heapshot heapshot)
		{
			Items.Add(detailsLabel);
			var tabControl = new TabControl();
			this.heapshot = heapshot;
			this.session = session;
			retentionPaths = new TabControl();
			tabControl.Pages.Add(new TabPage(retentionPaths) {
				Text = "Retension Paths"
			});
			stacktraceView = new ListBox();
			tabControl.Pages.Add(new TabPage(stacktraceView) {
				Text = "Creation Stacktrace"
			});
			referencesList = new ListBox();
			tabControl.Pages.Add(new TabPage(referencesList) {
				Text = "References"
			});
			referencesList.MouseDoubleClick += (s, e) => {
				if (referencesList.SelectedValue is RetentionItem ri) {
					var newTab = new ObjectListTab(session, heapshot, new Dictionary<long,LazyObjectsList>() {
						[ri.obj.TypeId] = new SingleLazyObjectList(ri.obj)
					});
					newTab.InsertTab += InsertTab;
					InsertTab(newTab, null);
				}
			};
			Items.Add(new StackLayoutItem(tabControl, HorizontalAlignment.Stretch, true));
		}
	}
}

