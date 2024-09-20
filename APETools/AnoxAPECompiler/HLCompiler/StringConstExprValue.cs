// (c) 2024 Eric Lasota / Gale Force Games
// SPDX-License-Identifier: MIT
using AnoxAPE;
using AnoxAPE.Elements;

namespace AnoxAPECompiler.HLCompiler
{
    internal class StringConstExprValue : IExprValue
    {
        public ExprType ExprType { get { return ExprType.StringConst; } }

        public ExprResultType ResultType { get { return ExprResultType.String; } }

        public ByteStringSlice UnescapedValue { get; private set; }

        public ExpressionValue.EOperandType OperandType { get { return ExpressionValue.EOperandType.StringConst; } }

        public StringConstExprValue(ByteStringSlice value)
        {
            UnescapedValue = value;
        }
    }
}
