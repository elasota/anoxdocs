namespace AnoxAPE.Elements
{
    public class TitleCommand : IWindowCommand
    {
        public OptionalExpression Condition { get; private set; }
        public ByteString Text { get; private set; }
        public FormattingValue FormattingValue { get; private set; }

        public WindowCommandType WindowCommandType { get { return WindowCommandType.Title; } }

        public TitleCommand()
        {
            Condition = new OptionalExpression();
            Text = new ByteString();
            FormattingValue = new FormattingValue();
        }

        public void Load(InputStream inStream, int indent, OutputStream? disasmStream)
        {
            if (disasmStream != null)
                disasmStream.WriteLineIndented(indent, "TitleCommand");

            Condition.Load(inStream, indent + 1, disasmStream);
            Text.Load(inStream, indent + 1, disasmStream);
            FormattingValue.Load(inStream, indent + 1, disasmStream);
        }

        public void WriteWithID(OutputStream outStream)
        {
            outStream.WriteByte(84);
            Condition.Write(outStream);
            Text.Write(outStream);
            FormattingValue.Write(outStream);
        }
    }
}
