using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using Coordinates2D = System.Tuple<float, float>;
using Coordinates3D = System.Tuple<float, float, float>;
using System.IO;

namespace EsfLibrary {
    public abstract class DelegatingDecoderNode<T> : CodecNode<T> {
        protected ValueReader<T> Read;
        protected ValueWriter<T> Write;
        public DelegatingDecoderNode(Converter<T> conv, ValueReader<T> reader, ValueWriter<T> writer)
            : base(conv) {
            Read = reader;
            Write = writer;
        }
        protected override T ReadValue(BinaryReader reader, EsfType readAs) { return Read(reader); }
        public override void WriteValue(BinaryWriter writer) {
#if DEBUG
//            if (Modified) {
//                Console.WriteLine("Writing {0}", Value);
//            }
#endif
            Write(writer, Value);
        }
    }

    #region Primitive Value Nodes
    public class Int32ShortNode : CodecNode<int> {
        public Int32ShortNode(int value) : this() {
            Value = value;
        }
        public Int32ShortNode()
            : base(int.Parse) {
            TypeCode = EsfType.INT32_SHORT;
        }

        public override EsfNode CreateCopy() {
            return new IntNode {
                Value = this.Value
            };
        }

        protected override int ReadValue(BinaryReader reader, EsfType readAs) {
            return reader.ReadInt16();
        }

        public override void WriteValue(BinaryWriter writer) {
            writer.Write((short)Value);
        }
    }
    public class UInt32ByteNode : CodecNode<uint> {
        public UInt32ByteNode(uint value) : this() {
            Value = value;
        }
        public UInt32ByteNode() : base(uint.Parse) {
            TypeCode = EsfType.UINT32_BYTE;
        }
        public override EsfNode CreateCopy() {
            return new UIntNode {
                Value = this.Value,
                Modified = false
            };
        }

        protected override uint ReadValue(BinaryReader reader, EsfType readAs) {
            return reader.ReadByte();
        }

        public override void WriteValue(BinaryWriter writer) {
            writer.Write((byte)Value);
        }
    }

    public class UInt32ShortNode : CodecNode<uint> {
        public UInt32ShortNode(uint value) : this() {
            Value = value;
        }
        public UInt32ShortNode() : base(uint.Parse) {
            TypeCode = EsfType.UINT32_SHORT;
        }
        public override EsfNode CreateCopy() {
            return new UIntNode {
                Value = this.Value,
                Modified = false
            };
        }

        protected override uint ReadValue(BinaryReader reader, EsfType readAs) {
            return reader.ReadUInt16();
        }

        public override void WriteValue(BinaryWriter writer) {
            writer.Write((ushort)Value);
        }
    }
    public class Int32ZeroNode : IntNode {
        public Int32ZeroNode() : base(0) {
            TypeCode = EsfType.INT32_ZERO;
        }

        protected override int ReadValue(BinaryReader reader, EsfType readAs) {
            return 0;
        }

        public override void WriteValue(BinaryWriter writer) {

        }
    }
    public class IntNode : DelegatingDecoderNode<int> {
        public IntNode(int value) : this() {
            Value = value;
        }
        public IntNode()
            : base(int.Parse,
                EsfCodec.ReadInt,
                delegate(BinaryWriter writer, int v) { writer.Write(v); }) {
            TypeCode = EsfType.INT32;
        }

        public override EsfNode CreateCopy() {
            return new IntNode {
                Value = this.Value
            };
        }
    }
    public class UIntNode : DelegatingDecoderNode<uint> {
        public UIntNode(uint value) : this() {
            Value = value;
        }
        public UIntNode()
            : base(uint.Parse,
                EsfCodec.ReadUInt,
                delegate(BinaryWriter writer, uint u) { writer.Write(u); }) {
                    TypeCode = EsfType.UINT32;
        }
        public override EsfNode CreateCopy() {
            return new UIntNode {
                Value = this.Value,
                Modified = false
            };
        }
    }
    public class BoolNode : DelegatingDecoderNode<bool> {
        public BoolNode(bool value) : this() {
            Value = value;
        }
        public BoolNode()
            : base(bool.Parse,
                EsfCodec.ReadBool,
                delegate(BinaryWriter writer, bool b) { writer.Write(b); }) {
                    TypeCode = EsfType.BOOL;
        }
        public override EsfNode CreateCopy() {
            return new BoolNode {
                Value = this.Value
            };
        }
    }
    public class BoolTrueNode : CodecNode<bool> {
        public BoolTrueNode() : base(bool.Parse) {
            TypeCode = EsfType.BOOL_TRUE;
        }

        public override EsfNode CreateCopy() {
            return new BoolTrueNode();
        }

        protected override bool ReadValue(BinaryReader reader, EsfType readAs) {
            return true;
        }

        public override void WriteValue(BinaryWriter writer) {

        }
    }
    public class BoolFalseNode : CodecNode<bool> {
        public BoolFalseNode() : base(bool.Parse) {
            TypeCode = EsfType.BOOL_FALSE;
        }

        public override EsfNode CreateCopy() {
            return new BoolFalseNode();
        }

        protected override bool ReadValue(BinaryReader reader, EsfType readAs) {
            return false;
        }

        public override void WriteValue(BinaryWriter writer) {

        }
    }
    public class FloatNode : DelegatingDecoderNode<float> {
        // Node text is written with an invariant decimal point, so it has to be read back the same
        // way rather than through the current culture.
        static float ParseSingle(string value) => float.Parse(value, CultureInfo.InvariantCulture);

        public FloatNode(float value) : this() {
            Value = value;
        }
        public FloatNode()
            : base(ParseSingle,
                EsfCodec.ReadFloat,
                delegate(BinaryWriter writer, float f) { writer.Write(f); }) {
                    TypeCode = EsfType.SINGLE;
        }
        public override EsfNode CreateCopy() {
            return new FloatNode {
                Value = this.Value
            };
        }
    }
    public class ByteNode : DelegatingDecoderNode<byte> {
        public ByteNode(byte value) : this() {
            Value = value;
        }
        public ByteNode()
            : base(byte.Parse,
                EsfCodec.ReadByte,
                delegate(BinaryWriter writer, byte b) { writer.Write(b); }) {
            TypeCode = EsfType.UINT8;
        }
        public override EsfNode CreateCopy() {
            return new ByteNode {
                Value = this.Value
            };
        }
    }
    public class SByteNode : DelegatingDecoderNode<sbyte> {
        public SByteNode(sbyte value) : this() {
            Value = value;
        }
        public SByteNode() : base(sbyte.Parse,
                EsfCodec.ReadSbyte,
                delegate(BinaryWriter writer, sbyte b) { writer.Write(b); }) {
                    TypeCode = EsfType.INT8;
        }
        public override EsfNode CreateCopy() {
            return new SByteNode {
                Value = this.Value
            };
        }
    }
    public class ShortNode : DelegatingDecoderNode<short> {
        public ShortNode(short value) : this() {
            Value = value;
        }
        public ShortNode()
            : base(short.Parse,
             EsfCodec.ReadShort,
                delegate(BinaryWriter writer, short b) { writer.Write(b); }) {
                    TypeCode  = EsfType.INT16;
        }
        public override EsfNode CreateCopy() {
            return new ShortNode {
                Value = this.Value
            };
        }
    }
    public class UShortNode : DelegatingDecoderNode<ushort> {
        public UShortNode(ushort value) : this() {
            Value = value;
        }
        public UShortNode()
            : base(ushort.Parse,
                EsfCodec.ReadUshort,
                delegate(BinaryWriter writer, ushort b) { writer.Write(b); }) {
                    TypeCode = EsfType.UINT16;
        }
        public override EsfNode CreateCopy() {
            return new UShortNode {
                Value = this.Value
            };
        }
    }
    public class LongNode : DelegatingDecoderNode<long> {
        public LongNode(long value) : this() {
            Value = value;
        }
        public LongNode()
            : base(long.Parse,
                EsfCodec.ReadLong,
                delegate(BinaryWriter writer, long b) { writer.Write(b); }) {
                    TypeCode = EsfType.INT64;
        }
        public override EsfNode CreateCopy() {
            return new LongNode {
                Value = this.Value
            };
        }
    }
    public class ULongNode : DelegatingDecoderNode<ulong> {
        public ULongNode(ulong value) : this() {
            Value = value;
        }
        public ULongNode()
            : base(ulong.Parse,
                EsfCodec.ReadUlong,
                delegate(BinaryWriter writer, ulong b) { writer.Write(b); }) {
                    TypeCode = EsfType.UINT64;
        }
        public override EsfNode CreateCopy() {
            return new ULongNode {
                Value = this.Value
            };
        }
    }
    public class DoubleNode : DelegatingDecoderNode<double> {
        /// <inheritdoc cref="FloatNode.ParseSingle"/>
        static double ParseDouble(string value) => double.Parse(value, CultureInfo.InvariantCulture);

        public DoubleNode(double value) : this() {
            Value = value;
        }
        public DoubleNode()
            : base(ParseDouble,
               EsfCodec.ReadDouble,
                delegate(BinaryWriter writer, double b) { writer.Write(b); }) {
                    TypeCode = EsfType.DOUBLE;
        }
        public override EsfNode CreateCopy() {
            return new DoubleNode {
                Value = this.Value
            };
        }
    }
    #endregion

    public class StringNode : DelegatingDecoderNode<string> {
        public StringNode(ValueReader<string> reader, ValueWriter<string> writer)
            : base(delegate(string v) { return v; },
                reader,
                writer) {
        }
        public override EsfNode CreateCopy() {
            return new StringNode(Read, Write) {
                TypeCode = this.TypeCode,
                Value = this.Value
            };
        }
    }
    public class AsciiStringNode : StringNode {
        public AsciiStringNode(EsfCodec codec, string value) : this(codec) {
            Value = value;
        }
        // Workaround: we can't pass an instanced method so we pass `null` instead and set the value manually in constructor
        public AsciiStringNode(EsfCodec codec) : base(null, null) {
            TypeCode = EsfType.ASCII;
            Codec = codec;
            if (Codec is AbcfFileCodec) {
                Read = Codec.ReadAsciiString;
                Write = (Codec as AbcfFileCodec).WriteAsciiReference;
            }
        }
    }
    public class Utf16StringNode : StringNode {
        public Utf16StringNode(EsfCodec codec, string value) : this(codec) {
            Value = value;
        }
        // Workaround: we can't pass an instanced method so we pass `null` instead and set the value manually in constructor
        public Utf16StringNode(EsfCodec codec) : base(null, null) {
            TypeCode = EsfType.UTF16;
            Codec = codec;
            if (Codec is AbcfFileCodec) {
                Read = Codec.ReadUtf16String;
                Write = (Codec as AbcfFileCodec).WriteUtf16Reference;
            }
        }
    }

    public class Coordinate2DNode : CodecNode<Coordinates2D> {
        static Coordinates2D Parse(string value) {
            string removedBrackets = value.Substring(1, value.Length - 2);
            string[] coords = removedBrackets.Split(',');
            // Invariant: the text form uses '.' as the decimal point and ',' as the separator, so
            // a comma-decimal locale would otherwise misread every coordinate.
            Coordinates2D result = new Coordinates2D(
                float.Parse(coords[0].Trim(), CultureInfo.InvariantCulture),
                float.Parse(coords[1].Trim(), CultureInfo.InvariantCulture)
            );
            return result;
        }
        public Coordinate2DNode(float x, float y) : base(Parse) {
            TypeCode = EsfType.COORD2D;
            Value = Tuple.Create(x, y);
        }
        public Coordinate2DNode() : this(0.0f, 0.0f) {

        }
        protected override Coordinates2D ReadValue(BinaryReader reader, EsfType readAs) {
            Coordinates2D result = new Coordinates2D(reader.ReadSingle(), reader.ReadSingle());
            return result;
        }
        public override void WriteValue(BinaryWriter writer) {
            writer.Write(Value.Item1);
            writer.Write(Value.Item2);
        }
        public override EsfNode CreateCopy() {
            return new Coordinate2DNode {
                Value = this.Value
            };
        }
    }
    public class Coordinates3DNode : CodecNode<Coordinates3D> {
        static Coordinates3D Parse(string value) {
            string removedBrackets = value.Substring(1, value.Length - 2);
            string[] coords = removedBrackets.Split(',');
            Coordinates3D result = new Coordinates3D(
                float.Parse(coords[0].Trim(), CultureInfo.InvariantCulture),
                float.Parse(coords[1].Trim(), CultureInfo.InvariantCulture),
                float.Parse(coords[2].Trim(), CultureInfo.InvariantCulture)
            );
            return result;
        }
        public Coordinates3DNode(float x, float y, float z) : base(Parse) {
            TypeCode = EsfType.COORD3D;
            Value = Tuple.Create(x, y, z);
        }
        public Coordinates3DNode() : this(0.0f, 0.0f, 0.0f) {

        }
        protected override Coordinates3D ReadValue(BinaryReader reader, EsfType readAs) {
            Coordinates3D result = new Coordinates3D(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
            return result;
        }
        public override void WriteValue(BinaryWriter writer) {
            writer.Write(Value.Item1);
            writer.Write(Value.Item2);
            writer.Write(Value.Item3);
        }
        public override EsfNode CreateCopy() {
            return new Coordinates3DNode {
                Value = this.Value
            };
        }
    }
}