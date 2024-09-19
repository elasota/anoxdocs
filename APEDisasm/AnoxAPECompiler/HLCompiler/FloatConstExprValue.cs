using AnoxAPE.Elements;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
