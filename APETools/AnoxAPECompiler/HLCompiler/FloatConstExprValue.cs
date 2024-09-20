// (c) 2024 Eric Lasota / Gale Force Games
// SPDX-License-Identifier: MIT
using AnoxAPE.Elements;

namespace AnoxAPECompiler.HLCompiler
{
    internal class FloatConstExprValue : IExprValue
    {
        public ExprType ExprType { get { return ExprType.FloatConst; } }
        public ExprResultType ResultType { get { return ExprResultType.Float; } }
        public ExpressionValue.EOperandType OperandType { get { return ExpressionValue.EOperandType.FloatConst; } }

        public float Value { get; private set; }

        public FloatConstExprValue(float value)
        {
            Value = value;
        }
    }
}
