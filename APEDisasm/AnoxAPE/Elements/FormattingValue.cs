namespace AnoxAPE.Elements
{
    public class FormattingValue
    {
        public IReadOnlyList<TypedFormattingValue> Values { get; private set; }

        private List<TypedFormattingValue> _values;

        public FormattingValue()
        {
            _values = new List<TypedFormattingValue>();
            Values = _values;
        }

        public FormattingValue(IEnumerable<TypedFormattingValue> values)
        {
            _values = new List<TypedFormattingValue>(values);
            Values = _values;
        }

        public void Load(InputStream inStream, int indent, OutputStream? disasmStream)
        {
            byte doneByte = inStream.ReadByte();

            if (disasmStream != null)
                disasmStream.WriteLineIndented(indent, "FormattingValue");

            while (doneByte == 0)
            {
                TypedFormattingValue value = new TypedFormattingValue();
                value.Load(inStream, indent + 1, disasmStream);
                _values.Add(value);

                doneByte = inStream.ReadByte();
            }

            if (doneByte != 255)
                inStream.ReportError("Invalid FormattingValue done sequence");

            byte afterDoneByte = inStream.ReadByte();

            if (afterDoneByte != 255)
                inStream.ReportError("Invalid FormattingValue done sequence");
        }

        public void Write(OutputStream outStream)
        {
            foreach (TypedFormattingValue value in Values)
            {
                outStream.WriteByte(0);
                value.Write(outStream);
            }

            outStream.WriteByte(255);
            outStream.WriteByte(255);
        }
    }
}
