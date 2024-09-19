namespace AnoxAPE.Elements
{
    public class StringOperand : IExpressionOperand
    {
        public ByteString Value { get; private set; }

        public StringOperand()
        {
            Value = new ByteString();
        }

        public StringOperand(ByteString value)
        {
            Value = value;
        }

        public void Load(InputStream inStream, int indent, OutputStream? disasmStream)
        {
            if (disasmStream != null)
                disasmStream.WriteLineIndented(indent, "StringOperand");

            Value.Load(inStream, indent + 1, disasmStream);
        }

        public void Write(OutputStream outStream)
        {
            Value.Write(outStream);
        }
    }
}
