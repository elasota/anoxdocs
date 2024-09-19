namespace AnoxAPECompiler
{
    public class CompilerException : Exception
    {
        public ILogger.LocationTag LocationTag { get; private set; }
        public string CompilerExceptionMessage { get; private set; }

        public CompilerException(ILogger.LocationTag locationTag, string message)
        {
            LocationTag = locationTag;
            CompilerExceptionMessage = message;
        }

        internal CompilerException(ILogger.MutableLocationTag locationTag, string message)
        {
            LocationTag = new ILogger.LocationTag(locationTag);
            CompilerExceptionMessage = message;
        }

        public CompilerException(string fileName, int line, int col, string message)
        {
            LocationTag = new ILogger.LocationTag(new ILogger.MutableLocationTag(fileName, line, col));
            CompilerExceptionMessage = message;
        }

        public override string Message
        {
            get
            {
                return $"Error at {LocationTag.FileName}({LocationTag.FileLine + 1},{LocationTag.FileCol + 1}): {CompilerExceptionMessage}";
            }
        }
    }
}
