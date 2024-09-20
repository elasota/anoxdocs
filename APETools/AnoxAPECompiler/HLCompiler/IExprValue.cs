// (c) 2024 Eric Lasota / Gale Force Games
// SPDX-License-Identifier: MIT
using AnoxAPE.Elements;

namespace AnoxAPECompiler.HLCompiler
{
    internal enum ExprType
    {
        FloatConst,
        FloatVar,
        StringConst,
        StringVar,
        Expr,
        Invalid,
    }

    internal enum ExprResultType
    {
        Float,
        String,
        Invalid,
    }

    internal interface IExprValue
    {
        ExprType ExprType { get; }
        ExprResultType ResultType { get; }
        ExpressionValue.EOperandType OperandType { get; }
    }
}
