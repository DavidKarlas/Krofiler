using System;
using Eto.Forms;
using Krofiler.CpuSampling;
using Eto.Drawing;

namespace Krofiler
{
	public class CpuSampleView : Drawable
	{
		private SamplingResult samplingResult;
		SampleFrame currentFrame;
		float frameHeight = 16;
		Font font;

		public CpuSampleView(SamplingResult samplingResult)
		{
			this.samplingResult = samplingResult;
			currentFrame = samplingResult.RootFrame;
			font = new Font("Menlo", frameHeight);
		}

		protected override void OnPaint(PaintEventArgs e)
		{
			e.Graphics.Clear(Eto.Drawing.Colors.White);
			RecursivlyDrawChildren(e.Graphics, currentFrame, 0, 0, e.ClipRectangle.Width);
		}

		void RecursivlyDrawChildren(Graphics g, SampleFrame frame, float x, float y, float width)
		{
			for (int i = 0; i < frame.Children.Count; i++) {
				var child = frame.Children[i];
				var childWidth = width * child.Value / frame.Value;
				g.DrawRectangle(Colors.Orange, x, y, childWidth, frameHeight);
				g.SetClip(new RectangleF(x, y, childWidth, frameHeight));
				g.DrawText(font, Colors.Black, x, y, child.Name);
				g.ResetClip();
				Console.WriteLine($"i{i} X:{x} Y:{y} Width:{childWidth} Height{frameHeight}");
				RecursivlyDrawChildren(g, child, x, y + frameHeight, childWidth);
				x += childWidth;
			}
		}
	}
}
