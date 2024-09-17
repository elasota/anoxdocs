namespace AnoxAPE.HLCompiler
{
    internal class StringConstExprValue : IExprValue
    {
        public ExprType ExprType { get { return ExprType.StringConst; } }

        public ExprResultType ResultType { get { return ExprResultType.String; } }

        public ByteStringSlice UnescapedValue { get; private set; }

        public StringConstExprValue(ByteStringSlice value)
        {
            UnescapedValue = value;
        }
    }
}
