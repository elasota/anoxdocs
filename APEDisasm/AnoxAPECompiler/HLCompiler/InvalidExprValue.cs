using AnoxAPE.Elements;

namespace AnoxAPECompiler.HLCompiler
{
    internal class InvalidExprValue : IExprValue
    {
        public ExprType ExprType { get { return ExprType.Invalid; } }

        public ExprResultType ResultType { get { return ExprResultType.Invalid; } }

        public ExpressionValue.EOperandType OperandType { get { throw new Exception("Can't resolve operand type of invalid expression"); } }
    }
}
