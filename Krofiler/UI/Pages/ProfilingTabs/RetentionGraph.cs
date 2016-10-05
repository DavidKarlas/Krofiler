using System;
using Eto.Forms;
using Eto.Drawing;
using System.Collections.Generic;
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

		public long ObjectId { get; set; }

		const int RowHeight = 30;
		const int BubbleWidth = 100;
		const int SpacingX = 20;
		const int SpacingY = 20;
		//int currentY;
		List<long> visited = new List<long>();
		List<long> spvisited = new List<long>();

		protected override void OnPaint(PaintEventArgs e)
		{
			//currentY = SpacingY;
			visited.Clear();
			DrawRow(e, ObjectId, 0, 0);
			base.OnPaint(e);
		}

		void DrawRow(PaintEventArgs e, long objId, int depth, int currentX)
		{
			if (objId == 0)
				return;
			var objInfo = heapshot.ObjectsInfoMap[objId];
			int currentY = SpacingY + (RowHeight + SpacingY) * depth++;
			var size = e.Graphics.MeasureString(Fonts.Sans(12), session.GetTypeName(objInfo.TypeId));
			e.Graphics.FillRectangle(objInfo.IsRoot ? Brushes.AliceBlue : Brushes.AntiqueWhite, currentX, currentY, Math.Max(BubbleWidth, (int)size.Width) + 10, RowHeight);
			e.Graphics.DrawText(Fonts.Sans(12), Colors.Black, currentX + 5, currentY + 5, session.GetTypeName(objInfo.TypeId));
			e.Graphics.DrawText(Fonts.Sans(12), Colors.Black, currentX + 5, currentY + 5 + 13, objInfo.ObjAddr.ToString());

			if (visited.Contains(objId))
				return;
			visited.Add(objId);

			foreach (var b in objInfo.ReferencesFrom) {
				var retObjInfo = heapshot.ObjectsInfoMap[b];
				for (int i = 0; i < retObjInfo.ReferencesTo.Length; i++) {
					if (retObjInfo.ReferencesTo[i] == objId) {
						e.Graphics.DrawText(Fonts.Sans(11), SolidBrush.Instantiator(Colors.Red), currentX, currentY + RowHeight + 7, session.GetFieldName(retObjInfo.TypeId, retObjInfo.ReferencesAt[i]));
						break;
					}
				}
				DrawRow(e, b, depth, currentX);
				currentX += Math.Max(BubbleWidth, (int)size.Width) + 20;
			}
		}
	}
}

