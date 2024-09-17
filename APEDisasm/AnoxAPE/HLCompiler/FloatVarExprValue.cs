namespace AnoxAPE.HLCompiler
{
    internal class FloatVarExprValue : IExprValue
    {
        public ExprType ExprType { get { return ExprType.FloatVar; } }

        public ExprResultType ResultType { get { return ExprResultType.Float; } }

        public ByteStringSlice VarName { get; private set; }

        public FloatVarExprValue(ByteStringSlice varName)
        {
            VarName = varName;
        }
    }
}
