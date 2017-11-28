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
		TabControl retentionPaths;
		public event InsertTabDelegate InsertTab;

		ObjectInfo objectInfo;
		public ObjectInfo Object
		{
			get
			{
				return objectInfo;
			}
			set
			{
				if (objectInfo == value)
					return;
				objectInfo = value;
				OnObjectIdChanged ();
			}
		}

		void AddSingleEntry (string text)
		{
			var listBox = new ListBox ();
			listBox.Items.Add (text);
			retentionPaths.Pages.Add (new TabPage (listBox));
		}

		void OnObjectIdChanged ()
		{
			stacktraceView.Items.Clear ();
			var sf = objectInfo.Allocation.Backtrace.Reverse ().Select (b => session.GetMethodName (b));
			foreach (var f in sf)
			{
				stacktraceView.Items.Add (f);
			}

			retentionPaths.Pages.Clear ();
			if (heapshot.Roots.TryGetValue (objectInfo.ObjAddr, out var root))
			{
				AddSingleEntry ("Object is root itself:" + root);
			}
			else
			{
				var pathsToRoot = heapshot.GetTop5PathsToRoots (objectInfo.ObjAddr);
				int i = 0;
				foreach (var path in pathsToRoot.OrderBy(p=>p.Count()))
				{
					i++;
					var listBox = new ListBox ();
					listBox.MouseDoubleClick += (s, e) =>
					 {
						 if (listBox.SelectedValue is RetentionItem ri)
						 {
							 var newTab = new ObjectListTab (session, heapshot, new Dictionary<long, List<ObjectInfo>> ()
							 {
								 [ri.obj.TypeId] = new List<ObjectInfo> () { ri.obj }
							 });
							 newTab.InsertTab += InsertTab;
							 InsertTab (newTab, null);
						 }
					 };
					var page = new TabPage (listBox)
					{
						Text = $"Path {i}"
					};
					retentionPaths.Pages.Add (page);
					foreach (var edge in path)
					{
						var objInfo = heapshot.ObjectsInfoMap [edge.Target];
						var typeName = session.GetTypeName (objInfo.TypeId);
						listBox.Items.Add (new RetentionItem (typeName, objInfo));//TODO: Add field to name
					}
				}
				if (!pathsToRoot.Any ())
					AddSingleEntry ("This is weird... Couldn't find path to root");
			}
		}

		class RetentionItem : IListItem
		{
			internal readonly ObjectInfo obj;
			readonly string typeName;

			public RetentionItem (string typeName, ObjectInfo obj)
			{
				this.typeName = typeName;
				this.obj = obj;
			}
			public string Text { get => typeName; set => throw new NotImplementedException (); }

			public string Key => obj.ObjAddr.ToString ();

		}

		public ObjectDetailsPanel (KrofilerSession session, Heapshot heapshot)
		{
			this.heapshot = heapshot;
			this.session = session;
			retentionPaths = new TabControl ();
			Pages.Add (new TabPage (retentionPaths)
			{
				Text = "Retension Paths"
			});

			stacktraceView = new ListBox ();
			Pages.Add (new TabPage (stacktraceView)
			{
				Text = "Creation Stacktrace"
			});
		}
	}
}

