using System.Collections.Generic;
namespace Krofiler.Reader
{
	public class FieldInfo
	{
		public int Offset;
		public string Name;
		public int Flags;
		public long PointingTo;

		public const int FIELD_ATTRIBUTE_FIELD_ACCESS_MASK = 0x0007;
		public const int FIELD_ATTRIBUTE_COMPILER_CONTROLLED = 0x0000;
		public const int FIELD_ATTRIBUTE_PRIVATE = 0x0001;
		public const int FIELD_ATTRIBUTE_FAM_AND_ASSEM = 0x0002;
		public const int FIELD_ATTRIBUTE_ASSEMBLY = 0x0003;
		public const int FIELD_ATTRIBUTE_FAMILY = 0x0004;
		public const int FIELD_ATTRIBUTE_FAM_OR_ASSEM = 0x0005;
		public const int FIELD_ATTRIBUTE_PUBLIC = 0x0006;

		public const int FIELD_ATTRIBUTE_STATIC = 0x0010;
		public const int FIELD_ATTRIBUTE_INIT_ONLY = 0x0020;
		public const int FIELD_ATTRIBUTE_LITERAL = 0x0040;
		public const int FIELD_ATTRIBUTE_NOT_SERIALIZED = 0x0080;
		public const int FIELD_ATTRIBUTE_SPECIAL_NAME = 0x0200;
		public const int FIELD_ATTRIBUTE_PINVOKE_IMPL = 0x2000;

		/* For runtime use only */
		public const int FIELD_ATTRIBUTE_RESERVED_MASK = 0x9500;
		public const int FIELD_ATTRIBUTE_RT_SPECIAL_NAME = 0x0400;
		public const int FIELD_ATTRIBUTE_HAS_FIELD_MARSHAL = 0x1000;
		public const int FIELD_ATTRIBUTE_HAS_DEFAULT = 0x8000;
		public const int FIELD_ATTRIBUTE_HAS_FIELD_RVA = 0x0100;
	}

	public class ClassInfo
	{
		public readonly ushort Id;
		public readonly long Address;
		public readonly ushort ParentId;
		public readonly string Name;
		public readonly FieldInfo[] Fields;

		internal ClassInfo(MyBinaryReader reader)
		{
			Id = reader.ReadUInt16();
			Address = reader.ReadPointer();
			ParentId = reader.ReadUInt16();
			Name = reader.ReadString();
			var fieldsCount = reader.ReadUInt16();
			Fields = new FieldInfo[fieldsCount];
			for (int i = 0; i < fieldsCount; i++) {
				Fields[i] = new FieldInfo() {
					Name = reader.ReadString(),
					Offset = reader.ReadUInt16(),
					Flags = reader.ReadUInt16(),
					PointingTo = reader.ReadPointer()
				};
			}
		}
	}
}