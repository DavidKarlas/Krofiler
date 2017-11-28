using System;
using Eto.Forms;
using System.Linq;
using System.Collections.Generic;

namespace Krofiler
{
	public class ObjectDetailsPanel : TabControl
	{
		readonly KrofilerSession session;
		readonly Heapshot heapshot;
		ListBox stacktraceView;
		ListBox retentionList;
		public event InsertTabDelegate InsertTab;

		ObjectInfo objectInfo;
		public ObjectInfo Object {
			get {
				return objectInfo;
			}
			set {
				if (objectInfo == value)
					return;
				objectInfo = value;
				OnObjectIdChanged();
			}
		}

		void OnObjectIdChanged()
		{
			stacktraceView.Items.Clear();
			var sf = objectInfo.Allocation.Backtrace.Reverse ().Select(b => session.GetMethodName(b));
			foreach (var f in sf) {
				stacktraceView.Items.Add(f);
			}

			retentionList.Items.Clear();
			if (heapshot.Roots.TryGetValue(objectInfo.ObjAddr, out var root)) {
				retentionList.Items.Add("Object is root itself:" + root);
			} else {
				var shortestPath = heapshot.GetShortestPathToRoot(objectInfo.ObjAddr);
				foreach (var edge in shortestPath) {
					var objInfo = heapshot.ObjectsInfoMap[edge.Source];
					var typeName = session.GetTypeName(objInfo.TypeId);
					retentionList.Items.Add(new RetentionItem(typeName, objInfo));//TODO: Add field to name
				}
				if (shortestPath.Any()) {
					if (heapshot.Roots.TryGetValue(shortestPath.Last().Source, out root))
						retentionList.Items.Add("Root info:" + root);
				} else
					retentionList.Items.Add("This is weird... Couldn't find path to root");
			}
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
			this.heapshot = heapshot;
			this.session = session;
			retentionList = new ListBox();
			Pages.Add(new TabPage(retentionList) {
				Text = "Retension Path"
			});

			retentionList.MouseDoubleClick += (s, e) => {
				if (retentionList.SelectedValue is RetentionItem ri) {
					var newTab = new ObjectListTab(session, heapshot, new Dictionary<long, List<ObjectInfo>>() {
						[ri.obj.TypeId] = new List<ObjectInfo>() { ri.obj }
					});
					newTab.InsertTab += InsertTab;
					InsertTab(newTab, null);
				}
			};

			stacktraceView = new ListBox();
			Pages.Add(new TabPage(stacktraceView) {
				Text = "Creation Stacktrace"
			});
		}
	}
}

