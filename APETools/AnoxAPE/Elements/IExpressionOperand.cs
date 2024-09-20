// (c) 2024 Eric Lasota / Gale Force Games
// SPDX-License-Identifier: MIT
namespace AnoxAPE.Elements
{
    public interface IExpressionOperand
    {
        public void Load(InputStream inStream, int indent, OutputStream? disasmStream);
        public void Write(OutputStream outStream);
    }
}
