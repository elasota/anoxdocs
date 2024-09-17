

namespace AnoxAPE.HLCompiler
{
    internal class PositionTrackingReader
    {
        internal struct RewindPos
        {
            public int FilePos { get; private set; }
            public int FileLine { get; private set; }
            public int FileCol { get; private set; }

            public RewindPos(int filePos, int fileLine, int fileCol)
            {
                FilePos = filePos;
                FileLine = fileLine;
                FileCol = fileCol;
            }
        }

        public ILogger.LocationTag LocationTag
        {
            get
            {
                return new ILogger.LocationTag(_locationTag);
            }
        }

        public bool IsAtEndOfFile
        {
            get
            {
                return _filePos == _fileBytes.Length;
            }
        }

        public int FilePosition
        {
            get
            {
                return _filePos;
            }
        }

        private ILogger.MutableLocationTag _locationTag;
        private int _filePos;
        private byte[] _fileBytes;

        public PositionTrackingReader(byte[] fileBytes, string fileName)
        {
            _locationTag = new ILogger.MutableLocationTag(fileName, 0, 0);
            _filePos = 0;
            _fileBytes = fileBytes;
        }

        public void StepAhead(int numBytes)
        {
            if (numBytes < 0 || numBytes > _fileBytes.Length)
                throw new ArgumentOutOfRangeException(nameof(numBytes));

            int endPos = _filePos + numBytes;
            while (_filePos < endPos)
            {
                byte b = _fileBytes[_filePos++];
                if (b == '\n')
                {
                    _locationTag.FileLine++;
                    _locationTag.FileCol = 0;
                }
                else
                    _locationTag.FileCol++;
            }
        }

        public byte PeekOne()
        {
            return _fileBytes[_filePos];
        }

        public bool Matches(ByteString str)
        {
            return Matches(str.ToSlice());
        }

        public bool Matches(ByteStringSlice slice)
        {
            if (slice.Length == 0)
                return true;

            int availableBytes = _fileBytes.Length - _filePos;
            if (availableBytes < slice.Length)
                return false;

            return slice.Equals(new ByteStringSlice(_fileBytes, _filePos, slice.Length));
        }

        public RewindPos GetRewindPos()
        {
            return new RewindPos(_filePos, _locationTag.FileLine, _locationTag.FileCol);
        }

        public void Rewind(RewindPos pos)
        {
            _filePos = pos.FilePos;
            _locationTag.FileLine = pos.FileLine;
            _locationTag.FileCol = pos.FileCol;
        }

        public ByteStringSlice GetSlice(int start, int length)
        {
            return new ByteStringSlice(_fileBytes, start, length);
        }
    }
}
