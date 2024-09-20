// (c) 2024 Eric Lasota / Gale Force Games
// SPDX-License-Identifier: MIT
using AnoxAPE.Elements;

namespace AnoxAPECompiler.HLCompiler
{
    internal class ExpressionExprValue : IExprValue
    {

        public ExpressionValue.EOperator Operator { get; private set; }

        public IExprValue Left { get; private set; }
        public IExprValue Right { get; private set; }

        public ExprType ExprType { get { return ExprType.Expr; } }

        public ExprResultType ResultType { get { return _resultType; } }

        public ExpressionValue.EOperandType OperandType { get { return ExpressionValue.EOperandType.Expression; } }

        private ExprResultType _resultType;

        public ExpressionExprValue(IExprValue left, IExprValue right, ExpressionValue.EOperator op)
        {
            Left = left;
            Right = right;
            Operator = op;

            _resultType = Utils.ResolveResultType(left, right, op);
        }
    }
}
