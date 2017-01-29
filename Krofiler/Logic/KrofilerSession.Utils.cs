using System;
using System.Linq;
using Krofiler.Reader;

namespace Krofiler
{
	public partial class KrofilerSession
	{
		ushort PointerSize;
		void ReportUserError(string message, string details = null)
		{
			UserError?.Invoke(this, message, details);
		}

		public string GetReferenceFieldName(Heapshot hs, long source, long target)
		{
			var s = hs[source];
			var t = hs[target];

			for (int i = 0; i < s.Refs.Length; i++) {
				if (s.Refs[i] == target) {
					var klass = hs.Types[s.ClassId];
					if (klass.Name.EndsWith("[]", StringComparison.Ordinal)) {
						return $"[{s.Offsets[i] / PointerSize}]";
					}
					FieldInfo field = null;
					while (true) {
						field = klass.Fields.FirstOrDefault(f => (f.Flags & FieldInfo.FIELD_ATTRIBUTE_STATIC) == 0 && f.Offset == s.Offsets[i]);
						if (field != null)
							break;
						klass = hs.Types[klass.ParentId];
					}
					return field.Name;
				}
			}
			throw new InvalidProgramException();
		}
	}
}

