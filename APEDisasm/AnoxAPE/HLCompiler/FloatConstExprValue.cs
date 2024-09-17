using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AnoxAPE.HLCompiler
{
    internal class FloatConstExprValue : IExprValue
    {
        public ExprType ExprType { get { return ExprType.FloatConst; } }
        public ExprResultType ResultType { get { return ExprResultType.Float; } }

        public float Value { get; private set; }

        public FloatConstExprValue(float value)
        {
            Value = value;
        }
    }
}
