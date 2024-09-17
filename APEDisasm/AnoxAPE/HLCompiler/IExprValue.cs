namespace AnoxAPE.HLCompiler
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
    }
}
