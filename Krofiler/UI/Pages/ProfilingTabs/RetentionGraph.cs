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

		int spaceNeeded(long objId)
		{
			if (objId == 0 || spvisited.Contains(objId))
				return 0;
			spvisited.Add(objId);
			var objInfo = heapshot.ObjectsInfoMap[objId];
			int total = 0;
			foreach (var b in objInfo.GetReferencesFrom(heapshot)) {
				total += spaceNeeded(b);
			}
			return Math.Max(1, total);
		}


		void DrawRow(PaintEventArgs e, long objId, int depth, int currentX)
		{
			if (objId == 0 || visited.Contains(objId))
				return;
			visited.Add(objId);
			var objInfo = heapshot.ObjectsInfoMap[objId];
			int currentY = SpacingY + (RowHeight + SpacingY) * depth++;
			e.Graphics.FillRectangle(Brushes.AntiqueWhite, currentX, currentY, BubbleWidth, RowHeight);
			e.Graphics.DrawText(Fonts.Sans(12), Colors.Black, currentX + 5, currentY + 5, session.GetTypeName(objInfo.TypeId));
			e.Graphics.DrawText(Fonts.Sans(12), Colors.Black, currentX + 5, currentY + 5 + 13, objInfo.ObjAddr.ToString());
			foreach (var b in objInfo.GetReferencesFrom(heapshot)) {
				var retObjInfo = heapshot.ObjectsInfoMap[b];
				for (int i = 0; i < retObjInfo.ReferencesTo.Length; i++) {
					if (retObjInfo.ReferencesTo[i] == objId) {
						e.Graphics.DrawText(Fonts.Sans(11), SolidBrush.Instantiator(Colors.Red), currentX, currentY + RowHeight + 7, session.GetFieldName(retObjInfo.TypeId, retObjInfo.ReferencesAt[i]));
					}
				}
				DrawRow(e, b, depth, currentX);
				spvisited.Clear();
				//currentX += spaceNeeded(b) * (BubbleWidth + SpacingX);
				//currentX -= BubbleWidth + SpacingX;
			}
		}
	}
}

