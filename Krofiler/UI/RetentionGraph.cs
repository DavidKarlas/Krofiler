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
		}

		public long ObjectId { get; set; }

		const int RowHeight = 30;
		const int BubbleWidth = 100;
		const int SpacingX = 20;
		const int SpacingY = 20;
		int currentX;
		int currentY;
		List<long> visited = new List<long>();

		protected override void OnPaint(PaintEventArgs e)
		{
			currentX = SpacingX;
			currentY = SpacingY;
			visited.Clear();
			DrawRow(e, ObjectId);
			base.OnPaint(e);
		}


		void DrawRow(PaintEventArgs e, long objId)
		{
			if (objId == 0 || visited.Contains(objId))
				return;
			visited.Add(objId);
			var objInfo = heapshot.ObjectsInfoMap[objId];
			e.Graphics.FillRectangle(Brushes.AntiqueWhite, currentX, currentY, BubbleWidth, RowHeight);
			e.Graphics.DrawText(Fonts.Sans(12), Colors.Black, currentX + 5, currentY + 5, session.GetTypeName(objInfo.TypeId));
			currentY += RowHeight + SpacingY;
			foreach (var b in objInfo.GetReferencesFrom(heapshot)) {
				var retObjInfo = heapshot.ObjectsInfoMap[b];
				for (int i = 0; i < retObjInfo.ReferencesTo.Length; i++) {
					if (retObjInfo.ReferencesTo[i] == objId) {
						e.Graphics.DrawText(Fonts.Sans(11), SolidBrush.Instantiator(Colors.Red), currentX, currentY - RowHeight + (SpacingY / 2), session.GetFieldName(retObjInfo.TypeId, retObjInfo.ReferencesAt[i]));
					}
				}
				DrawRow(e, b);
				currentX += BubbleWidth + SpacingX;
			}
		}
	}
}

