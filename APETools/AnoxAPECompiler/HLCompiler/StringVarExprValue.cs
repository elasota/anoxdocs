// (c) 2024 Eric Lasota / Gale Force Games
// SPDX-License-Identifier: MIT
using AnoxAPE;
using AnoxAPE.Elements;

namespace AnoxAPECompiler.HLCompiler
{
    internal class StringVarExprValue : IExprValue
    {
        public ExprType ExprType { get { return ExprType.StringVar; } }

        public ExprResultType ResultType { get { return ExprResultType.String; } }

        public ByteStringSlice VarName { get; private set; }

        public ExpressionValue.EOperandType OperandType { get { return ExpressionValue.EOperandType.StringVar; } }

        public StringVarExprValue(ByteStringSlice varName)
        {
            VarName = varName;
        }
    }
}
