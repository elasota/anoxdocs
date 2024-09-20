// (c) 2024 Eric Lasota / Gale Force Games
// SPDX-License-Identifier: MIT
namespace AnoxAPE.Elements
{
    public class ExpressionValueOperand : IExpressionOperand
    {
        public ExpressionValue Expression { get; private set; }

        public ExpressionValueOperand()
        {
            Expression = new ExpressionValue();
        }

        public ExpressionValueOperand(ExpressionValue expression)
        {
            Expression = expression;
        }

        public void Load(InputStream inStream, int indent, OutputStream? disasmStream)
        {
            Expression.Load(inStream, indent, disasmStream);
        }

        public void Write(OutputStream outStream)
        {
            Expression.Write(outStream);
        }
    }
}
