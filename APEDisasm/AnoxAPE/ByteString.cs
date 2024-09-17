using System.Collections;
using System.Text;

namespace AnoxAPE
{
    internal class ByteStringSliceEnumerator : IEnumerator<byte>
    {
        private byte[] _bytes;
        private int _index;
        private int _endIndex;
        private int _initialIndex;

        public ByteStringSliceEnumerator(byte[] bytes, int offset, int length)
        {
            _initialIndex = offset - 1;
            _bytes = bytes;
            _index = _initialIndex;
            _endIndex = offset + length;
        }

        public byte Current
        {
            get
            {
                return _bytes[_index];
            }
        }

        object IEnumerator.Current
        {
            get
            {
                return _bytes[_index];
            }
        }

        public void Dispose()
        {
        }

        public bool MoveNext()
        {
            _index++;
            return _index < _endIndex;
        }

        public void Reset()
        {
            _index = _initialIndex;
        }
    }

    public struct ByteStringSlice : IEquatable<ByteStringSlice>, IEquatable<ByteString>, IEnumerable<byte>
    {
        private byte[] _bytes;
        private int _offset;
        private int _length;
        
        private string DebugString
        {
            get
            {
                return DebugEncoding.GetString(_bytes, _offset, _length);
            }
        }

        private static Encoding DebugEncoding = Encoding.GetEncoding("us-ascii", new EncoderExceptionFallback(), new DecoderReplacementFallback("?"));

        public int Length
        {
            get
            {
                return _length;
            }
        }

        public ByteStringSlice(byte[] bytes, int offset, int length)
        {
            _bytes = bytes;
            _offset = offset;
            _length = length;

            if (offset < 0 || offset > _bytes.Length)
                throw new ArgumentOutOfRangeException(nameof(offset));
                
            if (_bytes.Length - offset < length)
                throw new ArgumentOutOfRangeException(nameof(length));
        }

        public byte this[int index]
        {
            get
            {
                if (index < 0)
                    throw new ArgumentOutOfRangeException("index");
                if (index >= _length)
                    throw new ArgumentOutOfRangeException("index");
                return _bytes[_offset + index];
            }
        }

        public bool Equals(ByteString? other)
        {
            if (other == null)
                return false;

            return Equals(other.ToSlice());
        }

        public bool Equals(ByteStringSlice other)
        {
            if (_length != other._length)
                return false;

            for (int i = 0; i < _length; i++)
            {
                if (_bytes[i + _offset] != other._bytes[i + other._offset])
                    return false;
            }

            return true;
        }

        public override bool Equals(object? other)
        {
            if (other == null)
                return false;

            if (other is ByteStringSlice)
                return Equals((ByteStringSlice)other);

            return false;
        }

        public override readonly int GetHashCode()
        {
            return 0;
        }

        public ByteString ToByteString()
        {
            byte[] bytes = new byte[_length];
            for (int i = 0; i < _length; i++)
                bytes[i] = _bytes[_offset + i];

            return new ByteString(bytes);
        }

        public IEnumerator<byte> GetEnumerator()
        {
            return new ByteStringSliceEnumerator(_bytes, _offset, _length);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return new ByteStringSliceEnumerator(_bytes, _offset, _length);
        }

        public string ToString(System.Text.Encoding encoding)
        {
            return encoding.GetString(_bytes, _offset, _length);
        }
    }

    public class ByteString : IEquatable<ByteString>, IEquatable<ByteStringSlice>, IEnumerable<byte>
    {
        public byte[] Bytes { get; private set; }

        private string DebugString
        {
            get
            {
                return DebugEncoding.GetString(Bytes);
            }
        }

        private static Encoding DebugEncoding = Encoding.GetEncoding("us-ascii", new EncoderExceptionFallback(), new DecoderReplacementFallback("?"));

        public int Length { get { return Bytes.Length; } }

        public ByteString()
        {
            Bytes = new byte[0];
        }

        public ByteString(byte[] bytes)
        {
            Bytes = bytes;
        }

        public void LoadWithLength(uint length, InputStream inStream, int indent, OutputStream? disasmStream)
        {
            if (length == 0 || length > 32768)
                inStream.ReportError("Invalid length of string");

            Bytes = inStream.ReadBytes(length - 1);
            if (inStream.ReadByte() != 0)
                inStream.ReportError("String was not null-terminated");

            if (disasmStream != null)
            {
                disasmStream.WriteIndent(indent);
                disasmStream.WriteString("String '");
                disasmStream.WriteBytes(Bytes);
                disasmStream.WriteString("'\n");
            }
        }

        public void Load(InputStream inStream, int indent, OutputStream? disasmStream)
        {
            LoadWithLength(inStream.ReadUInt32(), inStream, indent, disasmStream);
        }

        public void Write(OutputStream outStream)
        {
            outStream.WriteUInt32((uint)Bytes.Length + 1);
            outStream.WriteBytes(Bytes);
            outStream.WriteByte(0);
        }

        public static ByteString FromAsciiString(string str)
        {
            return new ByteString(System.Text.Encoding.UTF8.GetBytes(str));
        }

        public override bool Equals(object? other)
        {
            ByteString? otherByteStr = other as ByteString;
            if (otherByteStr == null)
                return false;

            return this.Equals(otherByteStr);
        }

        public bool Equals(ByteStringSlice other)
        {
            return ToSlice().Equals(other);
        }

        public bool Equals(ByteString? other)
        {
            if (other == null)
                return false;

            return ToSlice().Equals(other.ToSlice());
        }

        public override int GetHashCode()
        {
            return Bytes.GetHashCode();
        }

        public ByteStringSlice ToSlice()
        {
            return new ByteStringSlice(Bytes, 0, Bytes.Length);
        }

        public IEnumerator<byte> GetEnumerator()
        {
            return ToSlice().GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return ToSlice().GetEnumerator();
        }
    }
}
