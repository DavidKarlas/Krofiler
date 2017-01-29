using System;
using Eto.Forms;
namespace Krofiler
{
	public class ObjectDetailsPanel : TabControl
	{
		readonly Heapshot heapshot;
		RetentionGraph retensionsGraph;
		ListBox stacktraceView;
		TextArea stringValue;
		TabPage stringValuePage;
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
			var sf = heapshot[ObjectId].allocStack;
			if (sf != null)
				foreach (var m in sf)
					stacktraceView.Items.Add(m);
			retensionsGraph.ObjectId = ObjectId;
			if (heapshot[ObjectId].ClassId == 2) {
				stringValue.Text = heapshot[ObjectId].StringValue;
				stringValuePage.Visible = true;
			} else {
				stringValuePage.Visible = false;
			}
			Invalidate();
		}

		public ObjectDetailsPanel(KrofilerSession session, Heapshot heapshot)
		{
			this.heapshot = heapshot;
			retensionsGraph = new RetentionGraph(session, heapshot);
			var scrollable = new Scrollable() { Content = retensionsGraph };
			scrollable.ExpandContentHeight = true;
			scrollable.ExpandContentWidth = true;
			Pages.Add(new TabPage(scrollable) {
				Text = "Retension Path"
			});
			stacktraceView = new ListBox();
			Pages.Add(new TabPage(stacktraceView) {
				Text = "Creation Stacktrace"
			});
			stringValue = new TextArea();
			stringValuePage = new TabPage(stringValue) {
				Text = "String Value"
			};
			Pages.Add(stringValuePage);
		}
	}
}

