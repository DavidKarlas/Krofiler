using System;
using Eto.Forms;
using Eto.Drawing;
using System.Collections.Generic;
using System.Linq;

namespace Krofiler
{
	public class RetentionGraph : Drawable
	{
		readonly KrofilerSession session;
		readonly Heapshot heapshot;

		public RetentionGraph(KrofilerSession session, Heapshot heapshot)
		{
			this.heapshot = heapshot;
			this.session = session;
			Width = 1000;
			Height = 2000;
		}

		public long? ObjectId { get; set; }

		const int RowHeight = 30;
		const int BubbleWidth = 100;
		const int SpacingX = 20;
		const int SpacingY = 20;

		protected override void OnPaint(PaintEventArgs e)
		{
			//int depth = 0;
			//if (ObjectId.HasValue) {
			//	bool first = true;
			//	var path = heapshot.GetTop5PathsToRoots(ObjectId.Value).ToArray();
			//	long rootId;
			//	if (path.Length == 0) {
			//		DrawRow(e, ObjectId.Value, 0);
			//		rootId = ObjectId.Value;
			//		depth++;
			//	} else {
			//		foreach (var item in path) {
			//			if (first) {
			//				DrawRow(e, item.Source, depth++);
			//				first = false;
			//			}
			//			DrawConnection(e, session.GetFieldName(item.Source, 0), depth);
			//			DrawRow(e, item.Target, depth++);
			//		}
			//		rootId = path.Last().Target;
			//	}
			//	if (heapshot.Roots.TryGetValue(rootId, out var root) && !string.IsNullOrEmpty(root)) {
			//		DrawConnection(e, root, depth);
			//	}
			//}
			//base.OnPaint(e);
		}

		void DrawConnection(PaintEventArgs e, string fieldName, int depth)
		{
			e.Graphics.DrawText(Fonts.Sans(12), Colors.Black, 5, (RowHeight + SpacingY) * depth, fieldName);
		}

		void DrawRow(PaintEventArgs e, long objId, int depth)
		{
			if (objId == 0)
				return;
			var objInfo = heapshot.ObjectsInfoMap[objId];
			int currentY = SpacingY + (RowHeight + SpacingY) * depth;
			var size = e.Graphics.MeasureString(Fonts.Sans(12), session.GetTypeName(objInfo.TypeId));
			e.Graphics.FillRectangle(heapshot.Roots.ContainsKey(objId) ? Brushes.Red : Brushes.Yellow, 0, currentY, Math.Max(BubbleWidth, (int)size.Width) + 10, RowHeight);
			e.Graphics.DrawText(Fonts.Sans(12), Colors.Black, 5, currentY + 5, session.GetTypeName(objInfo.TypeId));
			e.Graphics.DrawText(Fonts.Sans(12), Colors.Black, 5, currentY + 5 + 13, objInfo.ObjAddr.ToString());
		}
	}
}

