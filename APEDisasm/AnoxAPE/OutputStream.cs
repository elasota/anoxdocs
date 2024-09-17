namespace AnoxAPE
{
    public class OutputStream
    {
        private Stream _stream;
        private byte[] _buffer;

        public OutputStream(Stream stream)
        {
            _stream = stream;
            _buffer = new byte[8];
        }

        public void WriteByte(byte b)
        {
            _stream.WriteByte(b);
        }

        public void WriteBytes(byte[] bytes)
        {
            _stream.Write(bytes, 0, bytes.Length);
        }

        public void WriteFloat32(float value)
        {
            WriteUInt32(BitConverter.SingleToUInt32Bits(value));
        }

        public void WriteUInt16(ushort value)
        {
            for (int i = 0; i < 2; i++)
            {
                _buffer[i] = (byte)(value & 0xff);
                value = (ushort)(value >> 8);
            }

            _stream.Write(_buffer, 0, 2);
        }

        public void WriteUInt32(uint value)
        {
            for (int i = 0; i < 4; i++)
            {
                _buffer[i] = (byte)(value & 0xff);
                value = value >> 8;
            }

            _stream.Write(_buffer, 0, 4);
        }

        public void WriteUInt64(ulong value)
        {
            for (int i = 0; i < 8; i++)
            {
                _buffer[i] = (byte)(value & 0xff);
                value = value >> 8;
            }

            _stream.Write(_buffer, 0, 8);
        }

        public void WriteIndent(int indentLevel)
        {
            if (indentLevel > 0)
            {
                _buffer[0] = 32;
                _buffer[1] = 32;
                _buffer[2] = 32;
                _buffer[3] = 32;

                for (int i = 0; i < indentLevel; i++)
                    _stream.Write(_buffer, 0, 4);
            }
        }

        public void WriteLine(string value)
        {
            byte[] encoded = System.Text.Encoding.UTF8.GetBytes(value);
            WriteBytes(encoded);
            _stream.WriteByte(10);
        }

        public void WriteLineIndented(int indentLevel, string value)
        {
            WriteIndent(indentLevel);
            WriteLine(value);
        }

        public void WriteString(string value)
        {
            byte[] encoded = System.Text.Encoding.UTF8.GetBytes(value);
            WriteBytes(encoded);
        }

        public void Flush()
        {
            _stream.Flush();
        }
    }
}
