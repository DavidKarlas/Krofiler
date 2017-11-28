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
					string[] stack = null;
					long[] stackAddresses = null;
					if ((reader.Flags & CaptureFlags.AllocsStackTrace) == CaptureFlags.AllocsStackTrace) {
						int size = reader.ReadByte();
						stack = new string[size];
						stackAddresses = new long[size];
						for (int i = 0; i < size; i++) {
							stackAddresses[i] = reader.ReadPointer();
							stack[i] = reader.ReadString();
						}
					}
					return new HeapAlloc() {
						Address = addr,
						AllocStack = stack,
						AllocStackAddresses = stackAddresses
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
				case 9:
					return new RootRegister(reader);
				case 10:
					return new RootUnregister(reader);
				case 11:
					return new MethodJit(reader);
				default:
					throw new InvalidDataException($"Type:{type}");
			}
		}
	}
}
