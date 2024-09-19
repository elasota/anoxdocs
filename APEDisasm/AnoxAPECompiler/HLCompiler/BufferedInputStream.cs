namespace AnoxAPECompiler.HLCompiler
{
    internal class BufferedInputStream
    {
        private Stack<byte> _byteStack;
        private Stream _stream;

        public BufferedInputStream(Stream stream)
        {
            _byteStack = new Stack<byte>();
            _stream = stream;
        }

        public int Read(byte[] buffer, int offset, int count)
        {
            if (offset < 0 || offset > buffer.Length)
                throw new ArgumentOutOfRangeException("offset");

            if ((buffer.Length - offset) < count)
                throw new ArgumentOutOfRangeException("count");

            int initialOffset = offset;
            int endOffset = offset + count;

            while (offset < endOffset)
            {
                if (_byteStack.Count == 0)
                    break;

                buffer[offset] = _byteStack.Pop();
                offset++;
            }

            if (endOffset != offset)
                offset += _stream.Read(buffer, offset, endOffset - offset);

            return offset - initialOffset;
        }

        public void ReturnBytes(byte[] buffer, int offset, int count)
        {
            if (offset < 0 || offset > buffer.Length)
                throw new ArgumentOutOfRangeException("offset");

            if ((buffer.Length - offset) < count)
                throw new ArgumentOutOfRangeException("count");

            int endOffset = offset + count;
            while (endOffset > offset)
            {
                endOffset--;
                _byteStack.Push(buffer[endOffset]);
            }
        }
    }
}
