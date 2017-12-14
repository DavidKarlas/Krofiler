using System;
using Eto.Forms;
using Krofiler.CpuSampling;

namespace Krofiler
{
	public class CpuSamplingTab : Splitter, IProfilingTab
	{
		public string Title => "CPU Sampling";

		public string Details => "";

		public Control TabContent => control;

		public event InsertTabDelegate InsertTab;

		CpuSampleView control;

		public CpuSamplingTab(KrofilerSession currentSession, SamplingResult samplingResult){
			control = new CpuSampleView(samplingResult);
		}
	}
}
