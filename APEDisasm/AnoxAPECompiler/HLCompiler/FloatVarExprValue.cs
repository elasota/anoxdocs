using AnoxAPE;
using AnoxAPE.Elements;

namespace AnoxAPECompiler.HLCompiler
{
    internal class FloatVarExprValue : IExprValue
    {
        public ExprType ExprType { get { return ExprType.FloatVar; } }

        public ExprResultType ResultType { get { return ExprResultType.Float; } }

        public ExpressionValue.EOperandType OperandType { get { return ExpressionValue.EOperandType.FloatVar; } }

        public ByteStringSlice VarName { get; private set; }

        public FloatVarExprValue(ByteStringSlice varName)
        {
            VarName = varName;
        }
    }
}
