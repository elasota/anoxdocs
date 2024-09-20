// (c) 2024 Eric Lasota / Gale Force Games
// SPDX-License-Identifier: MIT
using System.Globalization;

namespace AnoxAPE.Elements
{
    public class FloatOperand : IExpressionOperand
    {
        public float Value { get; private set; }

        public FloatOperand()
        {
            Value = 0.0f;
        }

        public FloatOperand(float value)
        {
            Value = value;
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
