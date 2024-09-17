using System.Globalization;

namespace AnoxAPE.Elements
{
    public class FloatOperand : IExpressionOperand
    {
        public float Value { get; private set; }

        public FloatOperand()
        {
        }

        public void Load(InputStream inStream, int indent, OutputStream? disasmStream)
        {
            Value = inStream.ReadFloat();

            if (disasmStream != null)
            {
                string floatStr = Value.ToString("G9", CultureInfo.InvariantCulture);
                disasmStream.WriteLineIndented(indent, $"FloatOperand({floatStr})");
            }
        }

        public void Write(OutputStream outStream)
        {
            outStream.WriteFloat32(Value);
        }
    }
}
