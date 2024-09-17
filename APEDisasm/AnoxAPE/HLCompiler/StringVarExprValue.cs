namespace AnoxAPE.HLCompiler
{
    internal class StringVarExprValue : IExprValue
    {
        public ExprType ExprType { get { return ExprType.StringVar; } }

        public ExprResultType ResultType { get { return ExprResultType.String; } }

        public ByteStringSlice VarName { get; private set; }

        public StringVarExprValue(ByteStringSlice varName)
        {
            VarName = varName;
        }
    }
}
