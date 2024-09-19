namespace AnoxAPECompiler
{
    public interface ILogger
    {
        public enum Severity
        {
            Info,
            Warning,
            Error,
        }

        public struct LocationTag
        {
            public string FileName { get; private set; }
            public int FileLine { get; private set; }   // Starts at 0
            public int FileCol { get; private set; }   // Starts at 0

            internal LocationTag(MutableLocationTag locationTag)
            {
                FileName = locationTag.FileName;
                FileLine = locationTag.FileLine;
                FileCol = locationTag.FileCol;
            }
        };

        internal struct MutableLocationTag
        {
            public string FileName;
            public int FileLine;   // Starts at 0
            public int FileCol;    // Starts at 0

            public MutableLocationTag(string fileName, int line, int col)
            {
                FileName = fileName;
                FileLine = line;
                FileCol = col;
            }
        };

        public struct MessageProperties
        {
            public Severity Severity { get; private set; }
            public LocationTag LocationTag { get; private set; }

            public MessageProperties(Severity severity, LocationTag locationTag)
            {
                LocationTag = locationTag;
                Severity = severity;
            }
        }

        public void WriteLine(MessageProperties msgProps, string message);
    }
}
