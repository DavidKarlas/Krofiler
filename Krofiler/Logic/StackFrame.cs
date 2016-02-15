using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Krofiler
{
	[DebuggerDisplay("{MethodName}")]
	public class StackFrame
	{
		public string MethodName {
			get {
				if (methodId == -1)
					return "[root]";
				return session.GetMethodName(methodId);
			}
		}

		public Dictionary<long, StackFrame> Children = new Dictionary<long, StackFrame>();
		public StackFrame Parent;
		public long methodId;
		readonly KrofilerSession session;

		public StackFrame(KrofilerSession session, long methodId)
		{
			this.session = session;
			this.methodId = methodId;
		}

		internal StackFrame GetStackFrame(KrofilerSession session, long[] frame, int index)
		{
			if (frame.Length == index)
				return this;
			if (!Children.ContainsKey(frame[index])) {
				Children[frame[index]] = new StackFrame(session, frame[index]) {
					Parent = this
				};
			}
			return Children[frame[index]].GetStackFrame(session, frame, index + 1);
		}
	}
}

