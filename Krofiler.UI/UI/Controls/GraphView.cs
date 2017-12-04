using System;
using System.Collections.Generic;
using System.Linq;
using Eto.Drawing;
using Eto.Forms;

namespace Krofiler
{
	public class GraphInfo
	{
		public string Title { get; set; }
		public Color Color { get; set; }
	}
	public class GraphView : Drawable
	{
		int NumberOfGraphs { get => graphsInfos.Length; }
		double[] MaxValues;
		double[] MinValues;
		readonly GraphInfo[] graphsInfos;
		readonly TimeSpan timeLimit;

		public GraphView(GraphInfo[] graphsColors, TimeSpan timeLimit)
		{
			this.timeLimit = timeLimit;
			this.graphsInfos = graphsColors;
			MaxValues = Enumerable.Repeat(double.MinValue, NumberOfGraphs).ToArray();
			MinValues = Enumerable.Repeat(double.MaxValue, NumberOfGraphs).ToArray();
		}

		protected override void OnPaint(PaintEventArgs e)
		{
			base.OnPaint(e);
			var rect = e.ClipRectangle;
			var padding = 10f;
			var bottom = rect.Bottom - padding;
			var top = rect.Top + padding;
			var left = rect.Left + padding;
			var right = rect.Right - padding;
			for (int graphIndex = 0; graphIndex < NumberOfGraphs; graphIndex++) {
				var path = new GraphicsPath();
				path.MoveTo(left, bottom);
				float increment = 20;
				if (increment * filteredList.Count > (right - left)) {
					increment = (right - left) / filteredList.Count;
				}
				float x = left;
				path.StartFigure();
				foreach (var p in filteredList) {
					path.LineTo(x, bottom - p.scaled[graphIndex] * (bottom - top));
					x += increment;
				}
				path.LineTo(x - increment, bottom);
				path.CloseFigure();
				e.Graphics.FillPath(graphsInfos[graphIndex].Color, path);
			}
		}

		class Entry
		{
			public TimeSpan time;
			public double[] values;
			public float[] scaled;
		}
		TimeSpan latest = TimeSpan.MinValue;
		List<Entry> list = new List<Entry>();
		List<Entry> filteredList = new List<Entry>();
		public void AddSample(TimeSpan timeSpan, params double[] values)
		{
			if (latest < timeSpan)
				latest = timeSpan;
			if (values.Length != NumberOfGraphs)
				throw new Exception();
			bool needsRecalc = false;
			var entry = new Entry {
				time = timeSpan,
				values = values,
				scaled = new float[NumberOfGraphs]
			};
			list.Add(entry);
			for (int i = 0; i < NumberOfGraphs; i++) {
				if (MinValues[i] > values[i]) {
					MinValues[i] = values[i];
					needsRecalc = true;
				}
				if (MaxValues[i] < values[i]) {
					MaxValues[i] = values[i];
					needsRecalc = true;
				}
			}
			if (needsRecalc) {
				//TODO: Can be optimised to recalc just for 1 graph(the one that changes)
				foreach (var ent in list) {
					CalcScale(ent);
				}
			} else {
				CalcScale(entry);
			}
			var startTime = latest.Add(-timeLimit);
			IEnumerable<Entry> filteredEnum;
			if (timeLimit == TimeSpan.Zero)
				filteredEnum = list;
			else
				filteredEnum = list.Where(i => i.time > startTime);
			filteredList = filteredEnum.OrderBy(e => e.time).ToList();
			this.Invalidate();
		}

		private void CalcScale(Entry entry)
		{
			for (int i = 0; i < NumberOfGraphs; i++) {
				if (MaxValues[i] - MinValues[i] > 0)
					entry.scaled[i] = (float)((entry.values[i] - MinValues[i]) / (MaxValues[i] - MinValues[i]));
				else
					entry.scaled[i] = 0;
			}
		}
	}
}
