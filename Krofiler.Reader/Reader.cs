using System;
using System.IO;
using System.Text;
using System.Diagnostics;
using System.Collections.Generic;

namespace Krofiler.Reader
{
	public class Reader
	{
		public double Progress {
			get {
				return (double)stream.Position / stream.Length;
			}
		}

		Stream stream;
		MyBinaryReader reader;
		public Reader(Stream stream)
		{
			this.stream = stream;
			reader = new MyBinaryReader(stream);
		}

		public byte PointerSize {
			get {
				return reader.pointerSize;
			}
		}

		public object ReadNext()
		{
			if (stream.Length == stream.Position)
				return null;
			var type = reader.ReadByte();
			switch (type) {
				case 1:
					return new Root(reader);
				case 2:
					return new HeapObject(reader);
				case 3:
					var addr = reader.ReadPointer();
					var stack = new List<string>();
					if ((reader.Flags & CaptureFlags.AllocsStackTrace) == CaptureFlags.AllocsStackTrace) {
						do {
							var method = reader.ReadString();
							if (method == "") {
								break;
							}
							stack.Add(method);
						} while (true);
					}
					return new HeapAlloc() {
						Address = addr,
						AllocStack = stack.ToArray()
					};
				case 4:
					var len = reader.ReadByte();
					var moves = new HeapMoves() {
						Moves = new long[len]
					};
					for (int i = 0; i < len; i++) {
						moves.Moves[i] = reader.ReadPointer();
					}
					return moves;
				case 5:
					return new MoreReferences(reader);
				case 6:
					return new ClassInfo(reader);
				case 7:
					return new HeapStart();
				case 8:
					return new HeapEnd();
				default:
					throw new InvalidDataException($"Type:{type}");
			}
		}
	}
}
