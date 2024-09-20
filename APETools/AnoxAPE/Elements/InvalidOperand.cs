// (c) 2024 Eric Lasota / Gale Force Games
// SPDX-License-Identifier: MIT
namespace AnoxAPE.Elements
{
    public class InvalidOperand : IExpressionOperand
    {
        public void Load(InputStream inStream, int indent, OutputStream? disasmStream)
        {
            throw new Exception("Operand was invalid");
        }

        public void Write(OutputStream outStream)
        {
            throw new Exception("Operand was invalid");
        }
    }
}
