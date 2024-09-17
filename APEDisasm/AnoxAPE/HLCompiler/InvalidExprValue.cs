namespace AnoxAPE.HLCompiler
{
    internal class InvalidExprValue : IExprValue
    {
        public ExprType ExprType { get { return ExprType.Invalid; } }

        public ExprResultType ResultType { get { return ExprResultType.Invalid; } }
    }
}
