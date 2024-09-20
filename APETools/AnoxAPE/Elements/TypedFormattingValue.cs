// (c) 2024 Eric Lasota / Gale Force Games
// SPDX-License-Identifier: MIT
namespace AnoxAPE.Elements
{
    public class TypedFormattingValue
    {
        public enum EFormattingValueType
        {
            Float = 4,
            VariableName = 5,
            String = 16,
            StringVariableName = 17,
        }

        public EFormattingValueType Type { get; private set; }
        public IExpressionOperand Value { get; private set; }

        public TypedFormattingValue()
        {
            Value = new InvalidOperand();
        }

        public TypedFormattingValue(EFormattingValueType type, IExpressionOperand value)
        {
            Type = type;
            Value = value;
        }

        public void Load(InputStream inStream, int indent, OutputStream? disasmStream)
        {
            byte typeByte = inStream.ReadByte();

            if (typeByte == 4 || typeByte == 5 || typeByte == 16 || typeByte == 17)
            {
                Type = (EFormattingValueType)typeByte;

                if (typeByte == 4)
                    Value = new FloatOperand();
                else if (typeByte == 5 || typeByte == 17)
                    Value = new StringOperand();
                else if (typeByte == 16)
                    Value = new QuotedStringOperand();
                else
                    throw new Exception("Internal error");

                if (disasmStream != null)
                    disasmStream.WriteLineIndented(indent, $"TypedFormattingValue({Type.ToString()})");
            }
            else
                inStream.ReportError("Invalid type byte for TypedFormattingValue");

            Value.Load(inStream, indent + 1, disasmStream);
        }

        public void Write(OutputStream outStream)
        {
            outStream.WriteByte((byte)Type);
            Value.Write(outStream);
        }
    }
}
