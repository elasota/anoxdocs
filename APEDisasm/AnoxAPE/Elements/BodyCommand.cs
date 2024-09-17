namespace AnoxAPE.Elements
{
    public class BodyCommand : IWindowCommand
    {
        public OptionalExpression Condition { get; private set; }
        public ByteString BodyStr { get; private set; }
        public FormattingValue FormattingValue { get; private set; }

        public WindowCommandType WindowCommandType { get { return WindowCommandType.Body; } }

        public BodyCommand()
        {
            Condition = new OptionalExpression();
            BodyStr = new ByteString();
            FormattingValue = new FormattingValue();
        }

        public void Load(InputStream inStream, int indent, OutputStream? disasmStream)
        {
            if (disasmStream != null)
                disasmStream.WriteLineIndented(indent, "BodyCommand");

            Condition.Load(inStream, indent + 1, disasmStream);
            BodyStr.Load(inStream, indent + 1, disasmStream);
            FormattingValue.Load(inStream, indent + 1, disasmStream);
        }

        public void WriteWithID(OutputStream outStream)
        {
            outStream.WriteByte(66);
            Condition.Write(outStream);
            BodyStr.Write(outStream);
            FormattingValue.Write(outStream);
        }
    }
}
