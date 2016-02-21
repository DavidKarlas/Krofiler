using System;
using Eto.Forms;
namespace Krofiler
{
	public class ObjectDetailsPanel : TabControl
	{
		readonly KrofilerSession session;
		readonly Heapshot heapshot;
		RetentionGraph retensionsGraph;
		ListBox stacktraceView;

		long objAddr;
		public long ObjectId {
			get {
				return objAddr;
			}
			set {
				if (objAddr == value)
					return;
				objAddr = value;
				OnObjectIdChanged();
			}
		}

		void OnObjectIdChanged()
		{
			stacktraceView.Items.Clear();
			var sf = heapshot.ObjectsInfoMap[objAddr].StackFrame;
			while (sf != null) {
				stacktraceView.Items.Add(sf.MethodName);
				sf = sf.Parent;
			}
			retensionsGraph.ObjectId = objAddr;
			Invalidate();
		}

		public ObjectDetailsPanel(KrofilerSession session, Heapshot heapshot)
		{
			this.heapshot = heapshot;
			this.session = session;
			retensionsGraph = new RetentionGraph(session, heapshot);
			var scrollable = new Scrollable() { Content = retensionsGraph };
			scrollable.ExpandContentHeight = false;
			scrollable.ExpandContentWidth = false;
			Pages.Add(new TabPage(scrollable) {
				Text = "Retension Path"
			});
			stacktraceView = new ListBox();
			Pages.Add(new TabPage(stacktraceView) {
				Text = "Creation Stacktrace"
			});
		}
	}
}

