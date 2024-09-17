using System;

namespace AnoxAPE
{
    public class InputStream
    {
        private Stream _stream;

        public ISet<long>? LabelTracker { get; set; }
        public long Position { get { return _stream.Position; } }


        public InputStream(Stream stream)
        {
            _stream = stream;
        }

        public byte[] ReadBytes(uint count)
        {
            long readStart = _stream.Position;

            byte[] bytes = new byte[count];
            if (_stream.Read(bytes, 0, (int)count) != count)
                throw new Exception($"Failed to read {count} bytes at file position {readStart}");

            return bytes;
        }

        public uint ReadUInt32()
        {
            byte[] bytes = ReadBytes(4);

            uint result = bytes[0];
            result |= ((uint)bytes[1] << 8);
            result |= ((uint)bytes[2] << 16);
            result |= ((uint)bytes[3] << 24);

            return result;
        }

        public ushort ReadUInt16()
        {
            byte[] bytes = ReadBytes(2);
            return (ushort)(bytes[0] | (bytes[1] << 8));
        }

        public void ExpectUInt32(uint expectedValue)
        {
            long readStart = _stream.Position;
            uint u32Value = ReadUInt32();

            if (u32Value != expectedValue)
                throw new Exception($"Expected U32 value {expectedValue} at position {readStart} but value was {u32Value}");
        }

        public byte ReadByte()
        {
            long readStart = _stream.Position;

            int b = _stream.ReadByte();
            if (b == -1)
                throw new IOException($"Failed to read 4 bytes at file position {readStart}");

            return (byte)b;
        }

        public ulong ReadUInt64()
        {
            byte[] bytes = ReadBytes(8);

            ulong result = bytes[0];
            result |= ((ulong)bytes[1] << 8);
            result |= ((ulong)bytes[2] << 16);
            result |= ((ulong)bytes[3] << 24);
            result |= ((ulong)bytes[4] << 32);
            result |= ((ulong)bytes[5] << 40);
            result |= ((ulong)bytes[6] << 48);
            result |= ((ulong)bytes[7] << 56);

            return result;
        }

        public float ReadFloat()
        {
            uint u32 = ReadUInt32();
            return BitConverter.UInt32BitsToSingle(u32);
        }

        public bool IsAtEOF()
        {
            return _stream.Position == _stream.Length;
        }

        public void MarkSwitchLabel()
        {
            if (LabelTracker != null)
                LabelTracker.Add(_stream.Position);
        }

        public void ReportError(string err)
        {
            throw new Exception($"{err}, last read ended at {_stream.Position}");
        }
    }
}
